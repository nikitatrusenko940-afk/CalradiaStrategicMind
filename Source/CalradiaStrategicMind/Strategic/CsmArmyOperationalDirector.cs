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

        public CsmArmyOperationalDirector()
        {
            _targetScorer = new CsmArmyAttackTargetScorer();
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
                if (!snapshot.IsValidForCsm)
                {
                    continue;
                }

                var assignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
                if (assignment != null)
                {
                    ProcessAssignment(snapshot, assignment, registry, observationTick, reports);
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

                if (TryRedirectBadSiege(snapshot, defenseSnapshots, registry, _targetScorer, observationTick, reports))
                {
                    continue;
                }

                TryAssignAttackTarget(snapshot, defenseSnapshots, registry, _targetScorer, observationTick, reports);
            }

            return reports;
        }

        private static void ProcessAssignment(
            CsmArmySnapshot snapshot,
            CsmArmyAssignment assignment,
            CsmArmyAssignmentRegistry registry,
            int observationTick,
            List<CsmArmyDirectorReport> reports)
        {
            var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, observationTick);
            if (!string.IsNullOrWhiteSpace(invalidReason))
            {
                var status = GetClosedStatus(assignment, observationTick, invalidReason);
                registry.Close(assignment, status, invalidReason);
                reports.Add(CreateReport(observationTick, snapshot, assignment.ObjectiveType, assignment.TargetSettlementName, false, status, invalidReason));
                return;
            }

            if (assignment.ReassertionCount >= GetMaxReassertions() || observationTick - assignment.LastCommandTick < GetReassertEveryTicks())
            {
                return;
            }

            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null)
            {
                registry.Close(assignment, "Invalid", "Army assignment target not found");
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
                registry.Close(assignment, "Completed", "Weak army released from CSM control for recovery");
            }

            reports.Add(CreateReport(tick, snapshot, "ReleaseForRecovery", "none", false, "Completed", "Weak army released from CSM control for recovery"));
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

        private static bool TryRedirectBadSiege(CsmArmySnapshot snapshot, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, CsmArmyAttackTargetScorer targetScorer, int tick, List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.AllowBadSiegeRedirect || snapshot.CurrentTargetSettlement == null || snapshot.CurrentObjectiveGuess != "BesiegingSettlement")
            {
                return false;
            }

            var defense = GetSettlementDefenseStrength(snapshot.CurrentTargetSettlement);
            if (defense <= 0f || snapshot.TotalStrength / defense >= ArmyDirectorSettings.BadSiegeRequiredStrengthRatio)
            {
                return false;
            }

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

                reports.Add(CreateReport(tick, snapshot, "ReleaseForRecovery", "none", false, "Completed", "Bad siege released from CSM control"));
                return true;
            }

            LogTargetScore(tick, snapshot, score);
            var target = score.Target;
            CsmArmyAssignment assignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "RedirectFromBadSiege", GetSettlementId(target), GetSettlementName(target), tick, "Bad siege redirected to better attack target", "VanillaArmy", out assignment))
            {
                reports.Add(CreateReport(tick, snapshot, "RedirectFromBadSiege", GetSettlementName(target), false, "Skipped", "Active CSM army assignment already exists"));
                return true;
            }

            if (!CanApplyAttackCommand(snapshot, assignment))
            {
                registry.Close(assignment, "Invalid", "Attack command blocked because party is not a vanilla army leader");
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", GetSettlementName(target), false, "Skipped", "Attack command blocked because party is not a vanilla army leader"));
                return true;
            }

            snapshot.LeaderParty.SetMoveBesiegeSettlement(target, snapshot.LeaderParty.NavigationCapability);
            reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, "CSM command applied through vanilla army leader only"));
            return true;
        }

        private static bool TryAssignAttackTarget(CsmArmySnapshot snapshot, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, CsmArmyAttackTargetScorer targetScorer, int tick, List<CsmArmyDirectorReport> reports)
        {
            if (snapshot.TotalStrength < ArmyDirectorSettings.MinimumArmyStrengthForAttack)
            {
                return false;
            }

            if (snapshot.CurrentObjectiveGuess != "IdleOrUnknown" && snapshot.CurrentObjectiveGuess != "MovingToSettlement")
            {
                return false;
            }

            var score = targetScorer.FindBestTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
            if (score == null)
            {
                var rejected = targetScorer.FindBestRejectedTarget(snapshot.LeaderParty.MapFaction as Kingdom, snapshot.LeaderParty, snapshot.TotalStrength, defenseSnapshots, registry);
                LogTargetRejection(tick, snapshot, rejected);
                reports.Add(CreateReport(tick, snapshot, "AttackSettlement", "none", false, "Skipped", "No attack target passed Army Target Scoring"));
                return false;
            }

            LogTargetScore(tick, snapshot, score);
            var target = score.Target;
            CsmArmyAssignment assignment;
            if (!registry.TryCreate(snapshot.ArmyId, snapshot.ArmyName, GetPartyId(snapshot.LeaderParty), GetPartyName(snapshot.LeaderParty), snapshot.KingdomName, "AttackSettlement", GetSettlementId(target), GetSettlementName(target), tick, "Existing army assigned attack settlement", "VanillaArmy", out assignment))
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
            reports.Add(CreateReport(tick, snapshot, assignment.ObjectiveType, GetSettlementName(target), true, assignment.Status, "CSM command applied through vanilla army leader only"));
            return true;
        }

        private static string GetInvalidAssignmentReason(CsmArmySnapshot snapshot, CsmArmyAssignment assignment, int tick)
        {
            if (tick - assignment.StartTick > ArmyDirectorSettings.MaxArmyAssignmentAgeTicks)
            {
                return "Army assignment age exceeded";
            }

            if (!snapshot.IsValidForCsm)
            {
                return snapshot.InvalidReason;
            }

            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null && assignment.ObjectiveType != "ReleaseForRecovery")
            {
                return "Target settlement not found";
            }

            if (assignment.ObjectiveType == "AttackSettlement" && target != null && target.MapFaction == snapshot.LeaderParty.MapFaction)
            {
                return "Attack target captured";
            }

            if (assignment.ObjectiveType == "DefendSettlement" && target != null && target.MapFaction != snapshot.LeaderParty.MapFaction)
            {
                return "Defense target no longer friendly";
            }

            return null;
        }

        private static string GetClosedStatus(CsmArmyAssignment assignment, int tick, string reason)
        {
            if (tick - assignment.StartTick > ArmyDirectorSettings.MaxArmyAssignmentAgeTicks)
            {
                return "Expired";
            }

            if (reason == "Attack target captured" || reason == "Defense target no longer friendly")
            {
                return "Completed";
            }

            return "Invalid";
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
