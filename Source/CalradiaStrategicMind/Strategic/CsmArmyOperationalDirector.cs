using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyOperationalDirector
    {
        private readonly CsmArmyAttackTargetScorer _targetScorer;
        private readonly CsmArmyObjectiveReader _objectiveReader;
        private readonly CsmBadSiegeEvaluator _badSiegeEvaluator;
        private readonly CsmArmyMissionTracker _missionTracker;

        public CsmArmyOperationalDirector()
        {
            _targetScorer = new CsmArmyAttackTargetScorer();
            _objectiveReader = new CsmArmyObjectiveReader();
            _badSiegeEvaluator = new CsmBadSiegeEvaluator(_targetScorer);
            _missionTracker = new CsmArmyMissionTracker();
        }

        public List<CsmArmyDirectorReport> Execute(
            List<CsmArmySnapshot> snapshots,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            int observationTick,
            bool isNewCommandCooldownActive)
        {
            var reports = new List<CsmArmyDirectorReport>();
            if (!ArmyDirectorSettings.AllowExistingArmyControl || snapshots == null)
            {
                return reports;
            }

            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                var objective = _objectiveReader.Read(snapshot);
                var assignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
                if (assignment != null)
                {
                    ProcessAssignment(snapshot, objective, assignment, registry, defenseSnapshots, observationTick, reports);
                    continue;
                }

                if (!snapshot.IsValidForCsm)
                {
                    continue;
                }

                if (TryReleaseWeakArmy(snapshot, registry, observationTick, reports))
                {
                    continue;
                }

                if (isNewCommandCooldownActive)
                {
                    continue;
                }

                if (TryRedirectToDefense(snapshot, defenseSnapshots, registry, observationTick, reports))
                {
                    continue;
                }

                if (TryRedirectBadSiege(snapshot, objective, defenseSnapshots, registry, _targetScorer, _badSiegeEvaluator, observationTick, reports))
                {
                    continue;
                }

                TryAssignAttackTarget(snapshot, objective, defenseSnapshots, registry, _targetScorer, observationTick, reports);
            }

            return reports;
        }

        private void ProcessAssignment(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            int observationTick,
            List<CsmArmyDirectorReport> reports)
        {
            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            var mission = _missionTracker.Evaluate(snapshot, objective, assignment, target, _badSiegeEvaluator, _targetScorer, defenseSnapshots, registry, observationTick);
            if (mission != null && mission.Handled && TryHandleMissionState(snapshot, objective, assignment, registry, defenseSnapshots, observationTick, reports, mission.State, target))
            {
                return;
            }

            var invalidReason = GetInvalidAssignmentReason(snapshot, objective, assignment, observationTick);
            if (!string.IsNullOrWhiteSpace(invalidReason))
            {
                var status = GetClosedStatus(assignment, observationTick, invalidReason);
                registry.Close(assignment, status, invalidReason);
                _missionTracker.CloseState(assignment, ToMissionStatus(status), invalidReason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, status, invalidReason));
                return;
            }

            if (HasAssignmentObjectiveMismatch(objective, assignment))
            {
                LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Mismatch", "Army displayed objective differs from CSM assignment target");
                if (IsAttackObjective(assignment.ObjectiveType))
                {
                    var syncTarget = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
                    if (_missionTracker.HasExceededSyncAttempts(assignment))
                    {
                        registry.Close(assignment, "Invalid", "Mission invalid because objective sync attempts were exceeded");
                        _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Mission invalid because objective sync attempts were exceeded", observationTick);
                        reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Invalid", "Mission invalid because objective sync attempts were exceeded"));
                        return;
                    }

                    _missionTracker.IncrementSyncAttempt(assignment);
                    if (TrySyncArmyAttackObjective(snapshot, assignment, syncTarget))
                    {
                        registry.MarkReasserted(assignment, observationTick, "Reasserted because army displayed objective mismatched CSM assignment target");
                        LogObjectiveSync(observationTick, snapshot, assignment, objective, true, "Synced", "Reasserted because army displayed objective mismatched CSM assignment target");
                        reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, true, "Reasserted", "Reasserted because army displayed objective mismatched CSM assignment target"));
                    }
                    else
                    {
                        LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Skipped", "No safe public army-level objective sync API found; leader party command reasserted only");
                        reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "No safe public army-level objective sync API found; leader party command reasserted only"));
                    }
                }

                return;
            }

            if (IsOperatingOnAssignedTarget(objective, assignment, snapshot))
            {
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "Army already operating on assigned target"));
                return;
            }

            if (assignment.ReassertionCount >= GetMaxReassertions() || observationTick - assignment.LastCommandTick < GetReassertEveryTicks())
            {
                return;
            }

            target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null)
            {
                registry.Close(assignment, "Invalid", "Army assignment target not found");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Army assignment target not found", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Invalid", "Army assignment target not found"));
                return;
            }

            if (IsAttackObjective(assignment.ObjectiveType) && !CanApplyAttackCommand(snapshot, assignment))
            {
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return;
            }

            ApplyArmyCommand(snapshot.LeaderParty, assignment.ObjectiveType, target);
            var reason = assignment.AssignmentKind == "VanillaArmy" && assignment.ObjectiveType == "AttackSettlement"
                ? "CSM vanilla army assignment reasserted"
                : "CSM army assignment reasserted";
            registry.MarkReasserted(assignment, observationTick, reason);
            reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, true, "Reasserted", reason));
        }

        private bool TryHandleMissionState(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            CsmArmyMissionState state,
            Settlement target)
        {
            if (state == null)
            {
                return false;
            }

            if (state.CurrentState == CsmArmyMissionStatus.Completed
                || state.CurrentState == CsmArmyMissionStatus.Invalid
                || state.CurrentState == CsmArmyMissionStatus.Expired
                || state.CurrentState == CsmArmyMissionStatus.ActiveSiegeRedirectBlocked
                || state.CurrentState == CsmArmyMissionStatus.ReleasedForRecovery)
            {
                var status = state.CurrentState == CsmArmyMissionStatus.ActiveSiegeRedirectBlocked ? "Invalid" : state.CurrentState.ToString();
                registry.Close(assignment, status, state.Reason);
                _missionTracker.CloseState(assignment, state.CurrentState, state.Reason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, status, state.Reason));
                return true;
            }

            if (state.CurrentState == CsmArmyMissionStatus.BesiegingAssignedTarget
                || state.CurrentState == CsmArmyMissionStatus.AssaultingAssignedTarget
                || state.CurrentState == CsmArmyMissionStatus.OperatingOnAssignedTarget)
            {
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", state.Reason));
                return true;
            }

            if (state.CurrentState == CsmArmyMissionStatus.ObjectiveMismatch)
            {
                return TryHandleMissionObjectiveMismatch(snapshot, objective, assignment, registry, observationTick, reports, target);
            }

            if (state.CurrentState == CsmArmyMissionStatus.Stalled)
            {
                return TryHandleStalledMission(snapshot, assignment, registry, observationTick, reports, target);
            }

            if (state.CurrentState == CsmArmyMissionStatus.Unsafe)
            {
                return TryHandleUnsafeMission(snapshot, objective, assignment, defenseSnapshots, registry, observationTick, reports);
            }

            return false;
        }

        private bool TryHandleMissionObjectiveMismatch(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            Settlement target)
        {
            LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Mismatch", "Army displayed objective differs from CSM assignment target");
            if (!IsAttackObjective(assignment.ObjectiveType))
            {
                return true;
            }

            if (_missionTracker.HasExceededSyncAttempts(assignment))
            {
                registry.Close(assignment, "Invalid", "Mission invalid because objective sync attempts were exceeded");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Mission invalid because objective sync attempts were exceeded", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Invalid", "Mission invalid because objective sync attempts were exceeded"));
                return true;
            }

            _missionTracker.IncrementSyncAttempt(assignment);
            if (TrySyncArmyAttackObjective(snapshot, assignment, target))
            {
                registry.MarkReasserted(assignment, observationTick, "Reasserted because army displayed objective mismatched CSM assignment target");
                LogObjectiveSync(observationTick, snapshot, assignment, objective, true, "Synced", "Reasserted because army displayed objective mismatched CSM assignment target");
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, true, "Reasserted", "Reasserted because army displayed objective mismatched CSM assignment target"));
                return true;
            }

            LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Skipped", "No safe public army-level objective sync API found; leader party command reasserted only");
            reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "No safe public army-level objective sync API found; leader party command reasserted only"));
            return true;
        }

        private bool TryHandleStalledMission(
            CsmArmySnapshot snapshot,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            Settlement target)
        {
            if (_missionTracker.HasExceededRepathAttempts(assignment))
            {
                registry.Close(assignment, "Expired", "Mission expired because army did not make progress toward assigned target");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Expired, "Mission expired because army did not make progress toward assigned target", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Expired", "Mission expired because army did not make progress toward assigned target"));
                return true;
            }

            if (!IsAttackObjective(assignment.ObjectiveType) || !CanApplyAttackCommand(snapshot, assignment) || target == null)
            {
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "Mission stalled but attack command could not be safely reasserted"));
                return true;
            }

            _missionTracker.IncrementRepathAttempt(assignment);
            if (!TrySyncArmyAttackObjective(snapshot, assignment, target))
            {
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Skipped", "Mission stalled but attack objective sync failed"));
                return true;
            }

            registry.MarkReasserted(assignment, observationTick, "Mission stalled; reasserted assigned attack target");
            reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, true, "Reasserted", "Mission stalled; reasserted assigned attack target"));
            return true;
        }

        private bool TryHandleUnsafeMission(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            int observationTick,
            List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowBadSiegeRedirect)
            {
                return true;
            }

            if (_missionTracker.HasExceededRedirects(assignment))
            {
                registry.Close(assignment, "ReleasedForRecovery", "Army released because repeated redirects failed to stabilize mission");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Army released because repeated redirects failed to stabilize mission", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "ReleasedForRecovery", "Army released because repeated redirects failed to stabilize mission"));
                return true;
            }

            if (_missionTracker.IsRedirectCooldownActive(assignment, observationTick))
            {
                var currentTarget = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
                if (TrySyncArmyAttackObjective(snapshot, assignment, currentTarget))
                {
                    registry.MarkReasserted(assignment, observationTick, "Redirect suppressed by cooldown; reasserted current assignment target");
                    reports.Add(CreateReport(observationTick, snapshot, "RedirectFromBadSiege", assignment.TargetSettlementName, true, "Reasserted", "Redirect suppressed by cooldown; reasserted current assignment target"));
                    return true;
                }

                reports.Add(CreateReport(observationTick, snapshot, "RedirectFromBadSiege", assignment.TargetSettlementName, false, "Skipped", "Redirect suppressed by cooldown but current assignment target could not be reasserted"));
                return true;
            }

            var badSiege = _badSiegeEvaluator.Evaluate(snapshot, objective, defenseSnapshots, registry);
            LogBadSiegeEvaluation(observationTick, snapshot, badSiege);
            var score = _targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
            if (score == null)
            {
                var rejected = _targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
                LogTargetRejection(observationTick, snapshot, rejected);
                registry.Close(assignment, "ReleasedForRecovery", "Bad siege had no safe replacement target; released army from CSM control");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Bad siege had no safe replacement target; released army from CSM control", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "ReleasedForRecovery", "Bad siege had no safe replacement target; released army from CSM control"));
                return true;
            }

            LogTargetScore(observationTick, snapshot, score);
            var target = score.Target;
            var reason = "Redirected bad siege to better scored attack target: " + badSiege.Reason;
            var inheritedRedirectCount = _missionTracker.GetRedirectCount(assignment);
            var previousTargetName = assignment.TargetSettlementName;
            registry.Close(assignment, "ReleasedForRecovery", reason);
            _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, reason, observationTick);

            CsmArmyAssignment newAssignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "RedirectFromBadSiege", GetSettlementId(target), GetSettlementName(target), observationTick, reason, "VanillaArmy", out newAssignment))
            {
                reports.Add(CreateReport(observationTick, snapshot, "RedirectFromBadSiege", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                return true;
            }

            _missionTracker.MarkRedirected(newAssignment, observationTick, previousTargetName, inheritedRedirectCount);

            if (!CanApplyAttackCommand(snapshot, newAssignment))
            {
                registry.Close(newAssignment, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Attack command blocked because party is not a vanilla army leader", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            if (!TrySyncArmyAttackObjective(snapshot, newAssignment, target))
            {
                registry.Close(newAssignment, "Invalid", "Redirect command failed because attack objective sync failed");
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Redirect command failed because attack objective sync failed", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Redirect command failed because attack objective sync failed"));
                return true;
            }

            reports.Add(CreateReport(observationTick, snapshot, newAssignment.ObjectiveType, GetSettlementName(target), true, newAssignment.Status, reason));
            return true;
        }

        private static bool TryReleaseWeakArmy(CsmArmySnapshot snapshot, CsmArmyAssignmentRegistry registry, int tick, List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowWeakArmyRelease)
            {
                return false;
            }

            if (snapshot.TotalStrength >= ArmyDirectorSettings.WeakArmyStrengthThreshold && snapshot.Cohesion >= ArmyDirectorSettings.LowCohesionThreshold)
            {
                return false;
            }

            var assignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
            if (assignment != null)
            {
                registry.Close(assignment, "Completed", "Army released for recovery");
            }

            reports.Add(CreateReport(tick, snapshot, "ReleaseForRecovery", "none", false, "Completed", "Weak or low-cohesion army released from CSM control for recovery"));
            return true;
        }

        private static bool TryRedirectToDefense(
            CsmArmySnapshot snapshot,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            int tick,
            List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowExistingArmyDefenseRedirect || snapshot.TotalStrength < ArmyDirectorSettings.MinimumArmyStrengthForDefenseRedirect || defenseSnapshots == null)
            {
                return false;
            }

            for (var index = 0; index < defenseSnapshots.Count; index++)
            {
                var defense = defenseSnapshots[index];
                if (!IsUrgentDefense(defense))
                {
                    continue;
                }

                var target = FindSettlementByName(defense.ThreatReport.SettlementName);
                if (target == null || target.MapFaction != snapshot.LeaderParty.MapFaction)
                {
                    continue;
                }

                if (target.Position.Distance(snapshot.LeaderParty.Position) > ArmyDirectorSettings.MaxDefenseRedirectDistance)
                {
                    continue;
                }

                CsmArmyAssignment assignment;
                if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "DefendSettlement", GetSettlementId(target), GetSettlementName(target), tick, "Existing army redirected to urgent defense", "VanillaArmy", out assignment))
                {
                    reports.Add(CreateReport(tick, snapshot, "DefendSettlement", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                    return true;
                }

                snapshot.LeaderParty.SetMoveDefendSettlement(target, false, snapshot.LeaderParty.NavigationCapability);
                reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, "Existing army redirected to urgent defense"));
                return true;
            }

            return false;
        }

        private bool TryRedirectBadSiege(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmArmyAttackTargetScorer targetScorer,
            CsmBadSiegeEvaluator badSiegeEvaluator,
            int tick,
            List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowBadSiegeRedirect)
            {
                return false;
            }

            var badSiege = badSiegeEvaluator.Evaluate(snapshot, objective, defenseSnapshots, registry);
            if (!badSiege.IsBadSiege)
            {
                return false;
            }

            LogBadSiegeEvaluation(tick, snapshot, badSiege);
            var score = targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
            if (score == null)
            {
                var rejected = targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
                LogTargetRejection(tick, snapshot, rejected);
                var activeAssignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
                if (activeAssignment != null)
                {
                    registry.Close(activeAssignment, "Completed", "Bad siege released from CSM control");
                }

                reports.Add(CreateReport(tick, snapshot, "ReleaseForRecovery", "none", false, "Completed", "Bad siege had no safe replacement target; released army from CSM control"));
                return true;
            }

            LogTargetScore(tick, snapshot, score);
            var target = score.Target;
            CsmArmyAssignment assignment;
            var reason = "Redirected bad siege to better scored attack target: " + badSiege.Reason;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "RedirectFromBadSiege", GetSettlementId(target), GetSettlementName(target), tick, reason, "VanillaArmy", out assignment))
            {
                reports.Add(CreateReport(tick, snapshot, "RedirectFromBadSiege", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                return true;
            }

            _missionTracker.MarkRedirected(assignment, tick, badSiege.CurrentTargetName, 0);

            if (!CanApplyAttackCommand(snapshot, assignment))
            {
                registry.Close(assignment, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Attack command blocked because party is not a vanilla army leader", tick);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            if (!TrySyncArmyAttackObjective(snapshot, assignment, target))
            {
                registry.Close(assignment, "Invalid", "Redirect command failed because attack objective sync failed");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Redirect command failed because attack objective sync failed", tick);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Redirect command failed because attack objective sync failed"));
                return true;
            }

            reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, reason));
            return true;
        }

        private static bool TryAssignAttackTarget(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, CsmArmyAttackTargetScorer targetScorer, int tick, List<CsmArmyDirectorReport> reports)
        {
            if (snapshot.TotalStrength < ArmyDirectorSettings.MinimumArmyStrengthForAttack)
            {
                return false;
            }

            if (objective.CurrentObjective != "IdleOrUnknown" && objective.CurrentObjective != "MovingToSettlement")
            {
                return false;
            }

            var score = targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
            if (score == null)
            {
                var rejected = targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
                LogTargetRejection(tick, snapshot, rejected);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", "none", false, "Skipped", "No attack target passed Army Target Scoring for existing army"));
                return false;
            }

            LogTargetScore(tick, snapshot, score);
            var target = score.Target;
            CsmArmyAssignment assignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "AttackSettlement", GetSettlementId(target), GetSettlementName(target), tick, "Assigned existing vanilla army to scored attack target", "VanillaArmy", out assignment))
            {
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                return true;
            }

            if (!CanApplyAttackCommand(snapshot, assignment))
            {
                registry.Close(assignment, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            snapshot.LeaderParty.SetMoveBesiegeSettlement(target, snapshot.LeaderParty.NavigationCapability);
            reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, "Assigned existing vanilla army to scored attack target"));
            return true;
        }

        private static string GetInvalidAssignmentReason(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment, int tick)
        {
            if (tick - assignment.StartTick > ArmyDirectorSettings.MaxArmyAssignmentAgeTicks && !IsOperatingOnAssignedTarget(objective, assignment, snapshot))
            {
                return "Assignment expired before army reached target";
            }

            if (!snapshot.IsValidForCsm)
            {
                return "Assignment invalid because army is no longer valid";
            }

            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null && assignment.ObjectiveType != "ReleaseForRecovery")
            {
                return "Assignment invalid because target settlement disappeared";
            }

            if (IsAttackObjective(assignment.ObjectiveType) && target != null)
            {
                if (target.MapFaction == null || snapshot.LeaderParty?.MapFaction == null)
                {
                    return "Assignment invalid because target faction is unreadable";
                }

                if (target.MapFaction == snapshot.LeaderParty.MapFaction || !snapshot.LeaderParty.MapFaction.IsAtWarWith(target.MapFaction))
                {
                    return "Assignment completed because target is no longer enemy";
                }
            }

            if (assignment.ObjectiveType == "DefendSettlement" && target != null && target.MapFaction != snapshot.LeaderParty.MapFaction)
            {
                return "Assignment completed because defense target is no longer friendly";
            }

            return null;
        }

        private static string GetClosedStatus(CsmArmyAssignment assignment, int tick, string reason)
        {
            if (reason == "Assignment expired before army reached target")
            {
                return "Expired";
            }

            if (reason == "Assignment completed because target is no longer enemy"
                || reason == "Assignment completed because defense target is no longer friendly")
            {
                return "Completed";
            }

            return "Invalid";
        }

        private static CsmArmyMissionStatus ToMissionStatus(string status)
        {
            if (status == "Completed")
            {
                return CsmArmyMissionStatus.Completed;
            }

            if (status == "Expired")
            {
                return CsmArmyMissionStatus.Expired;
            }

            if (status == "ReleasedForRecovery")
            {
                return CsmArmyMissionStatus.ReleasedForRecovery;
            }

            return CsmArmyMissionStatus.Invalid;
        }

        private static bool HasAssignmentObjectiveMismatch(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            if (objective == null || assignment == null)
            {
                return false;
            }

            if (objective.HasObjectiveTargetMismatch)
            {
                return true;
            }

            return objective.ArmyAiBehaviorObjectSettlement != null
                && !IsSameTarget(objective.ArmyAiBehaviorObjectSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private static bool IsOperatingOnAssignedTarget(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment, CsmArmySnapshot snapshot)
        {
            if (objective == null || assignment == null || objective.CurrentTargetSettlement == null)
            {
                return false;
            }

            if (HasAssignmentObjectiveMismatch(objective, assignment))
            {
                return false;
            }

            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null || snapshot.LeaderParty?.MapFaction == null || target.MapFaction == null || !snapshot.LeaderParty.MapFaction.IsAtWarWith(target.MapFaction))
            {
                return false;
            }

            var targetMatches = (!string.IsNullOrWhiteSpace(assignment.TargetSettlementId) && objective.CurrentTargetSettlement.StringId == assignment.TargetSettlementId)
                || objective.CurrentTargetSettlementName == assignment.TargetSettlementName;
            if (!targetMatches)
            {
                return false;
            }

            if (IsAttackObjective(assignment.ObjectiveType))
            {
                return objective.CurrentObjective == "BesiegingSettlement"
                    || objective.CurrentObjective == "MovingToBesiegeSettlement"
                    || objective.CurrentObjective == "MovingToSettlement";
            }

            if (assignment.ObjectiveType == "DefendSettlement")
            {
                return objective.CurrentObjective == "MovingToDefendSettlement" || objective.CurrentObjective == "MovingToSettlement";
            }

            return false;
        }

        private static bool IsSameTarget(Settlement settlement, string targetSettlementId, string targetSettlementName)
        {
            if (settlement == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(targetSettlementId) && settlement.StringId == targetSettlementId)
            {
                return true;
            }

            return settlement.Name != null && settlement.Name.ToString() == targetSettlementName;
        }

        private static void ApplyArmyCommand(MobileParty leaderParty, string objective, Settlement target)
        {
            if (objective == "DefendSettlement")
            {
                leaderParty.SetMoveDefendSettlement(target, false, leaderParty.NavigationCapability);
                return;
            }

            if (objective == "SupportSiege")
            {
                leaderParty.SetMoveGoToSettlement(target, leaderParty.NavigationCapability, false);
                return;
            }

            leaderParty.SetMoveBesiegeSettlement(target, leaderParty.NavigationCapability);
        }

        private static bool TrySyncArmyAttackObjective(CsmArmySnapshot snapshot, CsmArmyAssignment assignment, Settlement target)
        {
            if (!CanApplyAttackCommand(snapshot, assignment) || target == null || snapshot.LeaderParty?.MapFaction == null)
            {
                return false;
            }

            if (target.MapFaction == null || !snapshot.LeaderParty.MapFaction.IsAtWarWith(target.MapFaction))
            {
                return false;
            }

            snapshot.Army.ArmyType = Army.ArmyTypes.Besieger;
            snapshot.Army.AiBehaviorObject = target;
            snapshot.LeaderParty.SetMoveBesiegeSettlement(target, snapshot.LeaderParty.NavigationCapability);
            return true;
        }

        private static bool IsUrgentDefense(DefenseEvaluationSnapshot snapshot)
        {
            return (snapshot.ThreatReport.HasActiveSiege || snapshot.CoverageReport.HasDirectSiegeThreat || snapshot.CoverageReport.HasArmyPresence)
                && snapshot.CoverageReport.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold;
        }

        private static bool CanApplyAttackCommand(CsmArmySnapshot snapshot, CsmArmyAssignment assignment)
        {
            return snapshot.IsValidForCsm
                && snapshot.Army != null
                && snapshot.LeaderParty != null
                && snapshot.Army.LeaderParty == snapshot.LeaderParty
                && assignment != null
                && assignment.AssignmentKind == "VanillaArmy"
                && !string.IsNullOrWhiteSpace(assignment.ArmyId)
                && assignment.ArmyId == snapshot.ArmyId;
        }

        private static bool IsAttackObjective(string objective)
        {
            return objective == "AttackSettlement" || objective == "RedirectFromBadSiege";
        }

        private static float GetSettlementDefenseStrength(Settlement settlement)
        {
            var strength = 0f;
            if (settlement?.Town?.GarrisonParty != null)
            {
                strength += settlement.Town.GarrisonParty.Party.EstimatedStrength;
            }

            if (settlement?.MilitiaPartyComponent?.MobileParty != null)
            {
                strength += settlement.MilitiaPartyComponent.MobileParty.Party.EstimatedStrength;
            }

            return strength;
        }

        private static Settlement FindSettlementByName(string name)
        {
            return FindSettlementByIdOrName(string.Empty, name);
        }

        private static Settlement FindSettlementByIdOrName(string id, string name)
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                return null;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(id) && settlement.StringId == id)
                {
                    return settlement;
                }

                if (settlement.Name != null && settlement.Name.ToString() == name)
                {
                    return settlement;
                }
            }

            return null;
        }

        private static CsmArmyDirectorReport CreateReport(int tick, CsmArmySnapshot snapshot, string objective, string target, bool applied, string status, string reason)
        {
            return new CsmArmyDirectorReport(tick, snapshot.ArmyName, snapshot.KingdomName, objective, target, applied, status, reason);
        }

        private static void LogTargetScore(int tick, CsmArmySnapshot snapshot, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target score: tick={tick}, kingdom='{snapshot.KingdomName}', army='{snapshot.ArmyName}', selectedTarget='{score.TargetName}', score={score.Score:0.00}, distance={score.Distance:0.00}, distanceLimit={score.DistanceLimit:0.00}, targetDefense={score.TargetDefenseStrength:0.00}, estimatedAttackStrength={score.EstimatedAttackStrength:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, isFrontline={score.IsFrontlineCandidate}, extendedFrontline={score.UsesExtendedFrontlineRule}, reason='{score.Reason}'");
        }

        private static void LogTargetRejection(int tick, CsmArmySnapshot snapshot, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target rejection: tick={tick}, kingdom='{snapshot.KingdomName}', army='{snapshot.ArmyName}', topRejectedTarget='{score.TargetName}', score={score.Score:0.00}, distance={score.Distance:0.00}, distanceLimit={score.DistanceLimit:0.00}, targetDefense={score.TargetDefenseStrength:0.00}, estimatedAttackStrength={score.EstimatedAttackStrength:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, isFrontline={score.IsFrontlineCandidate}, extendedFrontline={score.UsesExtendedFrontlineRule}, reason='{score.Reason}'");
        }

        private static void LogBadSiegeEvaluation(int tick, CsmArmySnapshot snapshot, CsmBadSiegeEvaluation evaluation)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || evaluation == null || !evaluation.IsBadSiege)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM bad siege evaluation: tick={tick}, army='{snapshot.ArmyName}', target='{evaluation.CurrentTargetName}', armyStrength={evaluation.ArmyStrength:0.00}, targetDefense={evaluation.TargetDefenseStrength:0.00}, strengthRatio={evaluation.StrengthRatio:0.00}, cohesion={evaluation.Cohesion:0.00}, nearbyEnemyArmyStrength={evaluation.NearbyEnemyArmyStrength:0.00}, isBadSiege={evaluation.IsBadSiege}, reason='{evaluation.Reason}'");
        }

        private static void LogObjectiveSync(int tick, CsmArmySnapshot snapshot, CsmArmyAssignment assignment, CsmArmyObjectiveSnapshot objective, bool applied, string status, string reason)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || assignment == null || objective == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army objective sync: tick={tick}, army='{snapshot.ArmyName}', kingdom='{snapshot.KingdomName}', assignmentTarget='{assignment.TargetSettlementName}', leaderTarget='{objective.LeaderTargetSettlementName}', leaderBesieged='{objective.LeaderBesiegedSettlementName}', armyAiBehaviorObject='{objective.ArmyAiBehaviorObjectSettlementName}', commandApplied={applied}, status='{status}', reason='{reason}'");
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? string.Empty;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement?.StringId ?? string.Empty;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private static int GetReassertEveryTicks()
        {
            return ArmyDirectorSettings.ReassertArmyCommandEveryTicks < 1 ? 1 : ArmyDirectorSettings.ReassertArmyCommandEveryTicks;
        }

        private static int GetMaxReassertions()
        {
            return ArmyDirectorSettings.MaxArmyReassertionsPerAssignment < 0 ? 0 : ArmyDirectorSettings.MaxArmyReassertionsPerAssignment;
        }
    }
}
