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
        private int _badSiegesDetected;
        private int _badSiegesReleased;
        private int _objectiveMismatches;
        private int _objectiveSyncs;

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
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            CsmArmyLifecycleReport lifecycle,
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
                    ProcessAssignment(snapshot, objective, assignment, registry, defenseRegistry, recentlyReleasedArmies, recentlyFailedTargets, lifecycle, defenseSnapshots, observationTick, reports);
                    continue;
                }

                if (!snapshot.IsValidForCsm)
                {
                    continue;
                }

                var released = recentlyReleasedArmies == null ? null : recentlyReleasedArmies.GetActiveReleaseForArmy(snapshot.ArmyId, observationTick);
                if (released != null && !IsValidEnemySiege(snapshot, objective))
                {
                    if (lifecycle != null)
                    {
                        lifecycle.ReleaseCooldownBlocks++;
                    }

                    if (recentlyReleasedArmies.ShouldLog(released, observationTick))
                    {
                        reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "Skipped", "Army recently released from CSM control; cooldown active"));
                    }
                    else if (lifecycle != null)
                    {
                        lifecycle.DuplicateReleaseReportsSuppressed++;
                    }

                    continue;
                }

                var conflict = new CsmAssignmentConflictChecker(registry, defenseRegistry).CheckPartyForNewArmyCommand(snapshot.LeaderParty, snapshot.CurrentTargetSettlement);
                if (conflict.IsBlocked)
                {
                    reports.Add(CreateReport(observationTick, snapshot, "AttackSettlement", conflict.BlockingAssignmentTarget, false, "Skipped", conflict.Reason));
                    continue;
                }

                if (TryReleaseWeakArmy(snapshot, registry, recentlyReleasedArmies, observationTick, reports))
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

                if (TryRedirectBadSiege(snapshot, objective, defenseSnapshots, registry, defenseRegistry, recentlyReleasedArmies, recentlyFailedTargets, _targetScorer, _badSiegeEvaluator, observationTick, reports))
                {
                    continue;
                }

                TryAssignAttackTarget(snapshot, objective, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, _targetScorer, observationTick, reports);
            }

            return reports;
        }

        public void ResetTargetScoringSummary()
        {
            _targetScorer.ResetSummary();
            _badSiegesDetected = 0;
            _badSiegesReleased = 0;
            _objectiveMismatches = 0;
            _objectiveSyncs = 0;
        }

        public CsmArmyTargetScoringSummary GetTargetScoringSummary()
        {
            return _targetScorer.SnapshotSummary();
        }

        public int BadSiegesDetected
        {
            get { return _badSiegesDetected; }
        }

        public int BadSiegesReleased
        {
            get { return _badSiegesReleased; }
        }

        public int ObjectiveMismatches
        {
            get { return _objectiveMismatches; }
        }

        public int ObjectiveSyncs
        {
            get { return _objectiveSyncs; }
        }

        private void ProcessAssignment(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            CsmArmyLifecycleReport lifecycle,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            int observationTick,
            List<CsmArmyDirectorReport> reports)
        {
            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            var mission = _missionTracker.Evaluate(snapshot, objective, assignment, target, _badSiegeEvaluator, _targetScorer, defenseSnapshots, registry, observationTick);
            if (mission != null && mission.Handled && TryHandleMissionState(snapshot, objective, assignment, registry, defenseRegistry, recentlyReleasedArmies, recentlyFailedTargets, lifecycle, defenseSnapshots, observationTick, reports, mission.State, target))
            {
                return;
            }

            var invalidReason = GetInvalidAssignmentReason(snapshot, objective, assignment, observationTick);
            if (!string.IsNullOrWhiteSpace(invalidReason))
            {
                var status = GetClosedStatus(assignment, observationTick, invalidReason);
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, status, invalidReason, GetReleaseType(status), observationTick);
                _missionTracker.CloseState(assignment, ToMissionStatus(status), invalidReason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, status, invalidReason));
                return;
            }

            if (HasAssignmentObjectiveMismatch(objective, assignment))
            {
                _objectiveMismatches++;
                LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Mismatch", "Army displayed objective differs from CSM assignment target");
                if (IsAttackObjective(assignment.ObjectiveType))
                {
                    var syncTarget = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
                    if (_missionTracker.HasExceededSyncAttempts(assignment))
                    {
                        CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Invalid", "Mission invalid because objective sync attempts were exceeded", "Invalid", observationTick);
                        _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Mission invalid because objective sync attempts were exceeded", observationTick);
                        reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Invalid", "Mission invalid because objective sync attempts were exceeded"));
                        return;
                    }

                    _missionTracker.IncrementSyncAttempt(assignment);
                    if (TrySyncArmyAttackObjective(snapshot, assignment, syncTarget))
                    {
                        registry.MarkReasserted(assignment, observationTick, "Reasserted because army displayed objective mismatched CSM assignment target");
                        _objectiveSyncs++;
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
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Invalid", "Army assignment target not found", "Invalid", observationTick);
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
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            CsmArmyLifecycleReport lifecycle,
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

            if (state.CurrentState == CsmArmyMissionStatus.Completed)
            {
                if (TryHandleCompletedMission(snapshot, objective, assignment, registry, defenseRegistry, recentlyReleasedArmies, recentlyFailedTargets, defenseSnapshots, observationTick, reports, state))
                {
                    return true;
                }

                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Completed", state.Reason, "Completed", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Completed, state.Reason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Completed", state.Reason));
                return true;
            }

            if (state.CurrentState == CsmArmyMissionStatus.Invalid
                || state.CurrentState == CsmArmyMissionStatus.Expired
                || state.CurrentState == CsmArmyMissionStatus.ActiveSiegeRedirectBlocked
                || state.CurrentState == CsmArmyMissionStatus.ReleasedForRecovery)
            {
                var status = state.CurrentState == CsmArmyMissionStatus.ActiveSiegeRedirectBlocked ? "Invalid" : state.CurrentState.ToString();
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, status, state.Reason, GetReleaseType(status), observationTick);
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
                return TryHandleMissionObjectiveMismatch(snapshot, objective, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, lifecycle, observationTick, reports, target);
            }

            if (state.CurrentState == CsmArmyMissionStatus.Stalled)
            {
                return TryHandleStalledMission(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, observationTick, reports, target);
            }

            if (state.CurrentState == CsmArmyMissionStatus.Unsafe)
            {
                return TryHandleUnsafeMission(snapshot, objective, assignment, defenseSnapshots, registry, defenseRegistry, recentlyReleasedArmies, recentlyFailedTargets, observationTick, reports);
            }

            return false;
        }

        private bool TryHandleCompletedMission(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            CsmArmyMissionState state)
        {
            if (!IsWaitingAfterCompletedMission(snapshot, objective, assignment))
            {
                return false;
            }

            const string waitingReason = "Army completed mission but has no active objective";
            _missionTracker.UpdateState(state, CsmArmyMissionStatus.WaitingAfterCompletedMission, waitingReason, observationTick);

            var score = _targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, observationTick);
            if (score == null)
            {
                var rejected = _targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, observationTick);
                LogTargetRejection(observationTick, snapshot, rejected);
                var releaseReason = "Army released because completed mission left it without valid objective";
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", releaseReason, "PostCompletionNoTarget", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, releaseReason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", "none", false, "Released", releaseReason));
                return true;
            }

            LogTargetScore(observationTick, snapshot, score);
            var target = score.Target;
            var targetId = GetSettlementId(target);
            var targetName = GetSettlementName(target);
            if (defenseRegistry != null && defenseRegistry.HasActiveAssignmentForSettlement(targetId, targetName))
            {
                const string blockedReason = "Post-completion target blocked because settlement has active CSM defense assignment";
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", blockedReason, "PostCompletionNoTarget", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, blockedReason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, false, "Released", blockedReason));
                return true;
            }

            var conflict = new CsmAssignmentConflictChecker(registry, defenseRegistry).CheckPartyForNewArmyCommand(snapshot.LeaderParty, target);
            if (conflict.IsBlocked)
            {
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", conflict.Reason, "PostCompletionNoTarget", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, conflict.Reason, observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, false, "Released", conflict.Reason));
                return true;
            }

            var reason = "Army reassigned after completed mission left it without valid objective";
            CloseAssignmentWithoutRelease(assignment, registry, "Completed", waitingReason);

            CsmArmyAssignment newAssignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "AttackSettlement", targetId, targetName, observationTick, reason, "VanillaArmy", out newAssignment))
            {
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Army released because post-completion reassignment could not create assignment", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, false, "Skipped", "Army released because post-completion reassignment could not create assignment"));
                return true;
            }

            if (!CanApplyAttackCommand(snapshot, newAssignment))
            {
                CloseTransientAssignment(newAssignment, registry, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Army released because post-completion attack command was unsafe", observationTick);
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Attack command blocked because party is not a vanilla army leader", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            if (!TrySyncArmyAttackObjective(snapshot, newAssignment, target))
            {
                CloseTransientAssignment(newAssignment, registry, "Invalid", "Post-completion reassignment failed because attack objective sync failed");
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Army released because post-completion attack objective sync failed", observationTick);
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Post-completion reassignment failed because attack objective sync failed", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, false, "Skipped", "Post-completion reassignment failed because attack objective sync failed"));
                return true;
            }

            _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Completed, waitingReason, observationTick);
            reports.Add(CreateReport(observationTick, snapshot, "PostCompletionRecovery", targetName, true, "Reassigned", reason));
            return true;
        }

        private bool TryHandleMissionObjectiveMismatch(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            CsmArmyLifecycleReport lifecycle,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            Settlement target)
        {
            _objectiveMismatches++;
            LogObjectiveSync(observationTick, snapshot, assignment, objective, false, "Mismatch", "Army displayed objective differs from CSM assignment target");
            if (!IsAttackObjective(assignment.ObjectiveType))
            {
                return true;
            }

            if (_missionTracker.GetSyncAttemptCount(assignment) > ArmyDirectorSettings.MaxObjectiveSyncAttemptsBeforeRelease
                || _missionTracker.GetConsecutiveObjectiveMismatchTicks(assignment) > ArmyDirectorSettings.MaxConsecutiveObjectiveMismatchTicks)
            {
                const string releaseReason = "Released because vanilla repeatedly overrode CSM army objective";
                var state = _missionTracker.GetOrCreateState(assignment);
                _missionTracker.UpdateState(state, CsmArmyMissionStatus.RepeatedObjectiveMismatch, "Vanilla repeatedly overrode CSM army objective", observationTick);
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", releaseReason, "ReassertFailed", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, releaseReason, observationTick);
                if (lifecycle != null)
                {
                    lifecycle.ObjectiveMismatchReleases++;
                }

                reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "Completed", releaseReason));
                return true;
            }

            if (_missionTracker.HasExceededSyncAttempts(assignment))
            {
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Invalid", "Mission invalid because objective sync attempts were exceeded", "Invalid", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Mission invalid because objective sync attempts were exceeded", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, "Invalid", "Mission invalid because objective sync attempts were exceeded"));
                return true;
            }

            _missionTracker.IncrementSyncAttempt(assignment);
            if (_missionTracker.GetSyncAttemptCount(assignment) > ArmyDirectorSettings.MaxObjectiveSyncAttemptsBeforeRelease
                || _missionTracker.GetConsecutiveObjectiveMismatchTicks(assignment) > ArmyDirectorSettings.MaxConsecutiveObjectiveMismatchTicks)
            {
                const string releaseReason = "Released because vanilla repeatedly overrode CSM army objective";
                var state = _missionTracker.GetOrCreateState(assignment);
                _missionTracker.UpdateState(state, CsmArmyMissionStatus.RepeatedObjectiveMismatch, "Vanilla repeatedly overrode CSM army objective", observationTick);
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", releaseReason, "ReassertFailed", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, releaseReason, observationTick);
                if (lifecycle != null)
                {
                    lifecycle.ObjectiveMismatchReleases++;
                }

                reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "Completed", releaseReason));
                return true;
            }

            if (TrySyncArmyAttackObjective(snapshot, assignment, target))
            {
                registry.MarkReasserted(assignment, observationTick, "Reasserted because army displayed objective mismatched CSM assignment target");
                _objectiveSyncs++;
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
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            int observationTick,
            List<CsmArmyDirectorReport> reports,
            Settlement target)
        {
            if (_missionTracker.HasExceededRepathAttempts(assignment))
            {
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Expired", "Mission expired because army did not make progress toward assigned target", "Invalid", observationTick);
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
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            int observationTick,
            List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowBadSiegeRedirect)
            {
                return true;
            }

            if (_missionTracker.HasExceededRedirects(assignment))
            {
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", "Army released because repeated redirects failed to stabilize mission", "ReassertFailed", observationTick);
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
            if (badSiege.IsBadSiege)
            {
                _badSiegesDetected++;
            }

            var score = _targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, observationTick);
            if (score == null)
            {
                var rejected = _targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, observationTick);
                LogTargetRejection(observationTick, snapshot, rejected);
                if (badSiege.IsBadSiege)
                {
                    _badSiegesReleased++;
                }

                LogBadSiegeEvaluation(observationTick, snapshot, badSiege, null, "No safe replacement target: " + GetReplacementRejectReason(rejected));
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", "Bad siege had no safe replacement target; released army from CSM control", "BadSiegeNoReplacement", observationTick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.ReleasedForRecovery, "Bad siege had no safe replacement target; released army from CSM control", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "ReleaseForRecovery", "none", false, "ReleasedForRecovery", "Bad siege had no safe replacement target; released army from CSM control"));
                return true;
            }

            LogTargetScore(observationTick, snapshot, score);
            LogBadSiegeEvaluation(observationTick, snapshot, badSiege, score, string.Empty);
            var target = score.Target;
            var reason = "Redirected bad siege to better scored attack target: " + badSiege.Reason;
            var inheritedRedirectCount = _missionTracker.GetRedirectCount(assignment);
            var previousTargetName = assignment.TargetSettlementName;
            CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", reason, "Invalid", observationTick);
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
                CloseTransientAssignment(newAssignment, registry, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Attack command blocked because party is not a vanilla army leader", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            if (!TrySyncArmyAttackObjective(snapshot, newAssignment, target))
            {
                CloseTransientAssignment(newAssignment, registry, "Invalid", "Redirect command failed because attack objective sync failed");
                _missionTracker.CloseState(newAssignment, CsmArmyMissionStatus.Invalid, "Redirect command failed because attack objective sync failed", observationTick);
                reports.Add(CreateReport(observationTick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Redirect command failed because attack objective sync failed"));
                return true;
            }

            reports.Add(CreateReport(observationTick, snapshot, newAssignment.ObjectiveType, GetSettlementName(target), true, newAssignment.Status, reason));
            return true;
        }

        private static bool TryReleaseWeakArmy(CsmArmySnapshot snapshot, CsmArmyAssignmentRegistry registry, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, int tick, List<CsmArmyDirectorReport> reports)
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
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, "ReleasedForRecovery", "Army released for recovery", "WeakOrLowCohesion", tick);
            }
            else
            {
                MarkReleased(snapshot, recentlyReleasedArmies, tick, "Weak or low-cohesion army released from CSM control for recovery", "WeakOrLowCohesion", "none");
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
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
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

            _badSiegesDetected++;
            var score = targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
            if (score == null)
            {
                var rejected = targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
                LogTargetRejection(tick, snapshot, rejected);
                _badSiegesReleased++;
                LogBadSiegeEvaluation(tick, snapshot, badSiege, null, "No safe replacement target: " + GetReplacementRejectReason(rejected));
                var activeAssignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
                if (activeAssignment != null)
                {
                    CloseAssignment(snapshot, activeAssignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "ReleasedForRecovery", "Bad siege released from CSM control", "BadSiegeNoReplacement", tick);
                }
                else
                {
                    MarkReleased(snapshot, recentlyReleasedArmies, tick, "Bad siege had no safe replacement target; released army from CSM control", "BadSiegeNoReplacement", "none");
                }

                reports.Add(CreateReport(tick, snapshot, "ReleaseForRecovery", "none", false, "Completed", "Bad siege had no safe replacement target; released army from CSM control"));
                return true;
            }

            LogTargetScore(tick, snapshot, score);
            LogBadSiegeEvaluation(tick, snapshot, badSiege, score, string.Empty);
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
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Invalid", "Attack command blocked because party is not a vanilla army leader", "Invalid", tick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Attack command blocked because party is not a vanilla army leader", tick);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            if (!TrySyncArmyAttackObjective(snapshot, assignment, target))
            {
                CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, recentlyFailedTargets, "Invalid", "Redirect command failed because attack objective sync failed", "Invalid", tick);
                _missionTracker.CloseState(assignment, CsmArmyMissionStatus.Invalid, "Redirect command failed because attack objective sync failed", tick);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Redirect command failed because attack objective sync failed"));
                return true;
            }

            reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, reason));
            return true;
        }

        private static bool TryAssignAttackTarget(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, CsmDefenseAssignmentRegistry defenseRegistry, CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets, CsmArmyAttackTargetScorer targetScorer, int tick, List<CsmArmyDirectorReport> reports)
        {
            if (snapshot.TotalStrength < ArmyDirectorSettings.MinimumArmyStrengthForAttack)
            {
                return false;
            }

            if (objective.CurrentObjective != "IdleOrUnknown" && objective.CurrentObjective != "MovingToSettlement")
            {
                return false;
            }

            var score = targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
            if (score == null)
            {
                var rejected = targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
                LogTargetRejection(tick, snapshot, rejected);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", "none", false, "Skipped", "No attack target passed Army Target Scoring for existing army"));
                return false;
            }

            LogTargetScore(tick, snapshot, score);
            var target = score.Target;
            if (defenseRegistry != null && defenseRegistry.HasActiveAssignmentForSettlement(GetSettlementId(target), GetSettlementName(target)))
            {
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack target blocked because settlement has active CSM defense assignment"));
                return true;
            }

            CsmArmyAssignment assignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "AttackSettlement", GetSettlementId(target), GetSettlementName(target), tick, "Assigned existing vanilla army to scored attack target", "VanillaArmy", out assignment))
            {
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                return true;
            }

            if (!CanApplyAttackCommand(snapshot, assignment))
            {
                CloseTransientAssignment(assignment, registry, "Invalid", "Attack command blocked because party is not a vanilla army leader");
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

        private static bool IsWaitingAfterCompletedMission(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            if (assignment == null || assignment.AssignmentKind != "VanillaArmy")
            {
                return false;
            }

            if (!snapshot.IsValidForCsm || snapshot.IsPlayerArmy || snapshot.Army == null || snapshot.LeaderParty == null)
            {
                return false;
            }

            if (snapshot.LeaderParty.MapEvent != null || snapshot.LeaderParty.BesiegedSettlement != null || snapshot.LeaderParty.SiegeEvent != null)
            {
                return false;
            }

            if (objective == null || objective.CurrentObjective != "IdleOrUnknown")
            {
                return false;
            }

            return objective.CurrentTargetSettlement == null
                && objective.LeaderTargetSettlement == null
                && objective.ArmyAiBehaviorObjectSettlement == null;
        }

        private static bool IsValidEnemySiege(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective)
        {
            if (snapshot.LeaderParty?.MapFaction == null || objective == null || !objective.IsBesieging || objective.CurrentTargetSettlement == null)
            {
                return false;
            }

            var targetFaction = objective.CurrentTargetSettlement.MapFaction;
            return targetFaction != null && snapshot.LeaderParty.MapFaction.IsAtWarWith(targetFaction);
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

        private static void CloseAssignment(
            CsmArmySnapshot snapshot,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            string status,
            string reason,
            string releaseType,
            int tick)
        {
            CloseAssignment(snapshot, assignment, registry, recentlyReleasedArmies, null, status, reason, releaseType, tick);
        }

        private static void CloseAssignment(
            CsmArmySnapshot snapshot,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            string status,
            string reason,
            string releaseType,
            int tick)
        {
            if (registry != null)
            {
                registry.Close(assignment, status, reason);
            }

            MarkReleased(snapshot, recentlyReleasedArmies, tick, reason, releaseType, assignment == null ? "none" : assignment.TargetSettlementName);
            MarkFailedTarget(snapshot, assignment, recentlyFailedTargets, tick, reason, releaseType);
        }

        private static void CloseAssignmentWithoutRelease(CsmArmyAssignment assignment, CsmArmyAssignmentRegistry registry, string status, string reason)
        {
            CloseTransientAssignment(assignment, registry, status, reason);
        }

        private static void CloseTransientAssignment(CsmArmyAssignment assignment, CsmArmyAssignmentRegistry registry, string status, string reason)
        {
            if (registry != null)
            {
                registry.Close(assignment, status, reason);
            }
        }

        private static void MarkReleased(CsmArmySnapshot snapshot, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, int tick, string reason, string releaseType, string lastTargetName)
        {
            if (recentlyReleasedArmies == null || snapshot.Army == null || snapshot.LeaderParty == null)
            {
                return;
            }

            recentlyReleasedArmies.MarkReleased(
                snapshot.ArmyId,
                snapshot.ArmyName,
                GetPartyId(snapshot.LeaderParty),
                GetPartyName(snapshot.LeaderParty),
                snapshot.KingdomName,
                tick,
                reason,
                lastTargetName,
                releaseType);
        }

        private static void MarkFailedTarget(CsmArmySnapshot snapshot, CsmArmyAssignment assignment, CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets, int tick, string reason, string releaseType)
        {
            if (recentlyFailedTargets == null || assignment == null || !ShouldRememberFailedTarget(releaseType, reason))
            {
                return;
            }

            recentlyFailedTargets.MarkFailed(
                snapshot.ArmyId,
                snapshot.ArmyName,
                snapshot.KingdomName,
                assignment.TargetSettlementId,
                assignment.TargetSettlementName,
                tick,
                reason,
                releaseType);
        }

        private static bool ShouldRememberFailedTarget(string releaseType, string reason)
        {
            if (releaseType == "BadSiegeNoReplacement"
                || releaseType == "ReassertFailed"
                || releaseType == "Invalid")
            {
                return true;
            }

            if (reason == "Released because vanilla repeatedly overrode CSM army objective"
                || reason == "Army released because repeated redirects failed to stabilize mission"
                || reason == "Mission expired because army did not make progress toward assigned target")
            {
                return true;
            }

            return false;
        }

        private static string GetReleaseType(string status)
        {
            if (status == "Completed")
            {
                return "Completed";
            }

            if (status == "Invalid" || status == "Expired")
            {
                return "Invalid";
            }

            return "ReleasedForRecovery";
        }

        private static void LogTargetScore(int tick, CsmArmySnapshot snapshot, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target score: tick={tick}, kingdom='{snapshot.KingdomName}', army='{snapshot.ArmyName}', selectedTarget='{score.TargetName}', score={score.Score:0.00}, strategicValue={score.StrategicValueScore:0.00}, frontlineScore={score.FrontlineScore:0.00}, distanceScore={score.DistanceScore:0.00}, strengthRatioScore={score.StrengthRatioScore:0.00}, friendlySupportScore={score.NearbyFriendlySupportScore:0.00}, enemyThreatPenalty={score.NearbyEnemyPenalty:0.00}, overextensionPenalty={score.OverextensionPenalty:0.00}, recentlyFailedPenalty={score.RecentlyFailedTargetPenalty:0.00}, reason='{score.Reason}'");
        }

        private static void LogTargetRejection(int tick, CsmArmySnapshot snapshot, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target rejection: tick={tick}, kingdom='{snapshot.KingdomName}', army='{snapshot.ArmyName}', topRejectedTarget='{score.TargetName}', score={score.Score:0.00}, hardRejectReason='{score.HardRejectReason}', strategicValue={score.StrategicValueScore:0.00}, distance={score.Distance:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, reason='{score.Reason}'");
        }

        private static void LogBadSiegeEvaluation(int tick, CsmArmySnapshot snapshot, CsmBadSiegeEvaluation evaluation, CsmArmyAttackTargetScore replacementScore, string releaseReason)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || evaluation == null || !evaluation.IsBadSiege)
            {
                return;
            }

            var safeReplacementTarget = replacementScore == null ? "none" : replacementScore.TargetName;
            var safeReplacementScore = replacementScore == null ? 0f : replacementScore.Score;
            var nearbyFriendlySupportStrength = replacementScore == null ? 0f : replacementScore.NearbyFriendlySupportStrength;
            CsmLogger.Info(
                $"Observed CSM bad siege evaluation: tick={tick}, army='{snapshot.ArmyName}', target='{evaluation.CurrentTargetName}', armyStrength={evaluation.ArmyStrength:0.00}, targetDefense={evaluation.TargetDefenseStrength:0.00}, strengthRatio={evaluation.StrengthRatio:0.00}, cohesion={evaluation.Cohesion:0.00}, nearbyEnemyArmyStrength={evaluation.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={nearbyFriendlySupportStrength:0.00}, isBadSiege={evaluation.IsBadSiege}, safeReplacementTarget='{safeReplacementTarget}', safeReplacementScore={safeReplacementScore:0.00}, releaseReason='{releaseReason}', reason='{evaluation.Reason}'");
        }

        private static void LogObjectiveSync(int tick, CsmArmySnapshot snapshot, CsmArmyAssignment assignment, CsmArmyObjectiveSnapshot objective, bool applied, string status, string reason)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || assignment == null || objective == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army objective sync: tick={tick}, army='{snapshot.ArmyName}', kingdom='{snapshot.KingdomName}', assignmentTarget='{assignment.TargetSettlementName}', leaderTarget='{objective.LeaderTargetSettlementName}', leaderBesieged='{objective.LeaderBesiegedSettlementName}', armyAiBehaviorObject='{objective.ArmyAiBehaviorObjectSettlementName}', commandApplied={applied}, status='{status}', reason='{reason}'");
            LogObjectiveMismatchClassification(tick, snapshot, assignment, objective, status == "Synced" || status == "Skipped", reason);
        }

        private static void LogObjectiveMismatchClassification(int tick, CsmArmySnapshot snapshot, CsmArmyAssignment assignment, CsmArmyObjectiveSnapshot objective, bool syncAttempted, string reason)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || assignment == null || objective == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army objective mismatch classification: tick={tick}, army='{snapshot.ArmyName}', kingdom='{snapshot.KingdomName}', assignmentTarget='{assignment.TargetSettlementName}', leaderTarget='{objective.LeaderTargetSettlementName}', leaderBesieged='{objective.LeaderBesiegedSettlementName}', armyAiBehaviorObject='{objective.ArmyAiBehaviorObjectSettlementName}', distanceToAssignment={GetDistanceToAssignment(snapshot, assignment):0.00}, classification='{ClassifyObjectiveMismatch(snapshot, assignment, objective)}', syncAttempted={syncAttempted}, reason='Army objective mismatch diagnostic'");
        }

        private static string ClassifyObjectiveMismatch(CsmArmySnapshot snapshot, CsmArmyAssignment assignment, CsmArmyObjectiveSnapshot objective)
        {
            if (assignment == null || objective == null)
            {
                return "UnknownMismatch";
            }

            if (objective.LeaderBesiegedSettlement != null && !IsSameTarget(objective.LeaderBesiegedSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                return "BesiegingDifferentSettlement";
            }

            if (objective.ArmyAiBehaviorObjectSettlement != null && !IsSameTarget(objective.ArmyAiBehaviorObjectSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                return "DifferentAiBehaviorObject";
            }

            if (objective.LeaderTargetSettlement == null && objective.ArmyAiBehaviorObjectSettlement == null && objective.LeaderBesiegedSettlement == null)
            {
                return "MissingLeaderTarget";
            }

            if (objective.LeaderTargetSettlement != null && !IsSameTarget(objective.LeaderTargetSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                var assignmentTarget = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
                if (assignmentTarget != null && objective.LeaderTargetSettlement.Position.Distance(assignmentTarget.Position) <= ArmyDirectorSettings.MissionWrongTargetToleranceDistance)
                {
                    return "GoingToNearbySettlement";
                }

                if (objective.CurrentObjective == "MovingToSettlement" || objective.CurrentObjective == "MovingToBesiegeSettlement")
                {
                    return "TemporaryVanillaRoute";
                }

                return "TrueMismatch";
            }

            return string.IsNullOrWhiteSpace(objective.ObjectiveMismatchReason)
                ? "UnknownMismatch"
                : "TrueMismatch";
        }

        private static float GetDistanceToAssignment(CsmArmySnapshot snapshot, CsmArmyAssignment assignment)
        {
            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            return snapshot.LeaderParty == null || target == null
                ? 0f
                : snapshot.LeaderParty.Position.Distance(target.Position);
        }

        private static string GetReplacementRejectReason(CsmArmyAttackTargetScore rejected)
        {
            if (rejected == null)
            {
                return "No safe replacement target";
            }

            if (rejected.IsActiveDefenseTarget)
            {
                return "Replacement target conflicts with active defense target";
            }

            if (rejected.Distance > ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance)
            {
                return "Replacement target too far";
            }

            if (rejected.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                return "Replacement target low strength ratio";
            }

            if (!string.IsNullOrWhiteSpace(rejected.HardRejectReason))
            {
                return "All replacement targets rejected by target scoring: " + rejected.HardRejectReason;
            }

            return "All replacement targets rejected by target scoring";
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
