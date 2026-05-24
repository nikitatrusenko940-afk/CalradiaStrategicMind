using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyDirector
    {
        private readonly CsmArmySnapshotBuilder _snapshotBuilder;
        private readonly CsmArmyAssignmentRegistry _assignmentRegistry;
        private readonly CsmArmyOperationalDirector _operationalDirector;
        private readonly CsmArmyFormationDirector _formationDirector;
        private readonly CsmRecentlyReleasedArmyRegistry _recentlyReleasedArmies;
        private readonly CsmRecentlyFailedArmyTargetRegistry _recentlyFailedTargets;
        private int _lastCommandTick;

        public CsmArmyDirector()
        {
            _snapshotBuilder = new CsmArmySnapshotBuilder();
            _assignmentRegistry = new CsmArmyAssignmentRegistry();
            _operationalDirector = new CsmArmyOperationalDirector();
            _formationDirector = new CsmArmyFormationDirector();
            _recentlyReleasedArmies = new CsmRecentlyReleasedArmyRegistry();
            _recentlyFailedTargets = new CsmRecentlyFailedArmyTargetRegistry();
            _lastCommandTick = -9999;
        }

        public List<CsmArmyDirectorReport> Evaluate(List<DefenseEvaluationSnapshot> defenseSnapshots, int observationTick)
        {
            return Evaluate(defenseSnapshots, null, null, observationTick);
        }

        public List<CsmArmyDirectorReport> Evaluate(List<DefenseEvaluationSnapshot> defenseSnapshots, CsmDefenseAssignmentRegistry defenseRegistry, int observationTick)
        {
            return Evaluate(defenseSnapshots, defenseRegistry, null, observationTick);
        }

        public List<CsmArmyDirectorReport> Evaluate(
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmLordPartyRecoveryRegistry recoveryRegistry,
            int observationTick)
        {
            var reports = new List<CsmArmyDirectorReport>();
            if (!ArmyDirectorSettings.EnableArmyDirector)
            {
                return reports;
            }

            var snapshots = _snapshotBuilder.Build();
            var isCommandCooldownActive = observationTick - _lastCommandTick < GetCooldownTicks();
            var lifecycle = new CsmArmyLifecycleReport();
            _operationalDirector.ResetTargetScoringSummary();
            _formationDirector.ResetTargetScoringSummary();
            var operationalReports = _operationalDirector.Execute(snapshots, defenseSnapshots, _assignmentRegistry, defenseRegistry, _recentlyReleasedArmies, _recentlyFailedTargets, lifecycle, observationTick, isCommandCooldownActive);
            AddReports(reports, operationalReports);

            if (isCommandCooldownActive)
            {
                reports.Add(new CsmArmyDirectorReport(observationTick, "none", "none", "AttackSettlement", "none", false, "Skipped", "Army command cooldown active"));
            }
            else
            {
                var formationReports = _formationDirector.Execute(snapshots, defenseSnapshots, _assignmentRegistry, defenseRegistry, recoveryRegistry, _recentlyReleasedArmies, _recentlyFailedTargets, lifecycle, observationTick);
                AddReports(reports, formationReports);
            }

            if (HasNewCommandApplied(reports))
            {
                _lastCommandTick = observationTick;
            }

            LogTargetScoringSummary(observationTick);
            LogLifecycle(observationTick, lifecycle);
            LogDirectorQaSummary(observationTick, reports);
            return reports;
        }

        public int CountActiveAssignments()
        {
            return _assignmentRegistry.CountActiveAssignments();
        }

        public CsmArmyAssignmentRegistry AssignmentRegistry
        {
            get { return _assignmentRegistry; }
        }

        public bool HasActiveAssignmentForParty(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            var army = party.Army;
            if (army == null || army.LeaderParty == null)
            {
                return false;
            }

            var armyId = army.LeaderParty.StringId ?? string.Empty;
            return _assignmentRegistry.HasActiveAssignmentForArmy(armyId);
        }

        public bool ReassertAssignment(MobileParty party, CsmArmyAssignment assignment, int observationTick, out string reason)
        {
            var localReason = string.Empty;
            var result = SafeExecutor.Run(
                "Reassert CSM army task discipline",
                () => ReassertAssignmentCore(party, assignment, observationTick, out localReason),
                false);
            reason = localReason;
            return result;
        }

        private bool ReassertAssignmentCore(MobileParty party, CsmArmyAssignment assignment, int observationTick, out string reason)
        {
            reason = "Army assignment could not be reasserted";
            if (party == null || assignment == null)
            {
                reason = "Party or army assignment missing";
                return false;
            }

            if (party.Army == null || party.Army.LeaderParty == null)
            {
                reason = "Party is not attached to a readable army";
                return false;
            }

            var leaderParty = party.Army.LeaderParty;
            if (leaderParty == null || leaderParty.IsMainParty || leaderParty.MapEvent != null)
            {
                reason = "Army leader cannot safely receive task discipline reassert";
                return false;
            }

            var target = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (target == null || leaderParty.MapFaction == null || target.MapFaction == null)
            {
                reason = "Army assignment target is missing or unreadable";
                return false;
            }

            if (assignment.ObjectiveType == "DefendSettlement")
            {
                if (leaderParty.MapFaction != target.MapFaction)
                {
                    reason = "Army defense target is no longer friendly";
                    return false;
                }

                leaderParty.SetMoveDefendSettlement(target, false, leaderParty.NavigationCapability);
                reason = "Strategic task discipline reasserted CSM army defense assignment";
            }
            else
            {
                if (!leaderParty.MapFaction.IsAtWarWith(target.MapFaction))
                {
                    reason = "Army attack target is no longer enemy";
                    return false;
                }

                leaderParty.SetMoveBesiegeSettlement(target, leaderParty.NavigationCapability);
                reason = "Strategic task discipline reasserted CSM army attack assignment";
            }

            _assignmentRegistry.MarkReasserted(assignment, observationTick, reason);
            return true;
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

        private static void AddReports(List<CsmArmyDirectorReport> target, List<CsmArmyDirectorReport> source)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                target.Add(source[index]);
            }
        }

        private static bool HasNewCommandApplied(List<CsmArmyDirectorReport> reports)
        {
            for (var index = 0; index < reports.Count; index++)
            {
                if (reports[index].CommandApplied && reports[index].Status != "Reasserted")
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCooldownTicks()
        {
            return ArmyDirectorSettings.ArmyCommandCooldownTicks < 0 ? 0 : ArmyDirectorSettings.ArmyCommandCooldownTicks;
        }

        private void LogLifecycle(int observationTick, CsmArmyLifecycleReport lifecycle)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || lifecycle == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army lifecycle: tick={observationTick}, recentlyReleasedArmies={_recentlyReleasedArmies.CountActive(observationTick)}, releaseCooldownBlocks={lifecycle.ReleaseCooldownBlocks}, objectiveMismatchReleases={lifecycle.ObjectiveMismatchReleases}, duplicateReleaseReportsSuppressed={lifecycle.DuplicateReleaseReportsSuppressed}, reason='Army lifecycle snapshot'");
        }

        private void LogTargetScoringSummary(int observationTick)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs)
            {
                return;
            }

            var summary = new CsmArmyTargetScoringSummary();
            summary.Add(_operationalDirector.GetTargetScoringSummary());
            summary.Add(_formationDirector.GetTargetScoringSummary());
            CsmLogger.Info(
                $"Observed CSM army target scoring summary: tick={observationTick}, evaluatedTargets={summary.EvaluatedTargets}, selectedTargets={summary.SelectedTargets}, hardRejectedTargets={summary.HardRejectedTargets}, irrelevantRejectedTargets={summary.IrrelevantRejectedTargets}, tacticalRejectedTargets={summary.TacticalRejectedTargets}, rejectedActiveDefenseTargets={summary.RejectedActiveDefenseTargets}, rejectedOverextendedTargets={summary.RejectedOverextendedTargets}, rejectedLowStrengthTargets={summary.RejectedLowStrengthTargets}, recentlyFailedTargetPenalties={summary.RecentlyFailedTargetPenalties}, reason='Army target scoring v2 snapshot'");
        }

        private void LogDirectorQaSummary(int observationTick, List<CsmArmyDirectorReport> reports)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs)
            {
                return;
            }

            var createdArmies = CountReports(reports, true, "Created");
            var reassertedArmies = CountReports(reports, true, "Reasserted");
            var releasedArmies = CountReleasedReports(reports);
            var skippedCommands = CountReports(reports, false, "Skipped");
            CsmLogger.Info(
                $"Observed CSM army director summary: tick={observationTick}, activeAssignments={_assignmentRegistry.CountActiveAssignments()}, createdArmies={createdArmies}, reassertedArmies={reassertedArmies}, releasedArmies={releasedArmies}, skippedCommands={skippedCommands}, badSiegesDetected={_operationalDirector.BadSiegesDetected}, badSiegesReleased={_operationalDirector.BadSiegesReleased}, objectiveMismatches={_operationalDirector.ObjectiveMismatches}, objectiveSyncs={_operationalDirector.ObjectiveSyncs}, formationAttempts={_formationDirector.FormationAttempts}, formationSuccesses={_formationDirector.FormationSuccesses}, formationFailures={_formationDirector.FormationFailures}, reason='Army Director QA Polish v1 snapshot'");
        }

        private static int CountReports(List<CsmArmyDirectorReport> reports, bool commandApplied, string status)
        {
            var count = 0;
            if (reports == null)
            {
                return count;
            }

            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                if (report.CommandApplied == commandApplied && report.Status == status)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountReleasedReports(List<CsmArmyDirectorReport> reports)
        {
            var count = 0;
            if (reports == null)
            {
                return count;
            }

            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                if (report.Status == "Released"
                    || report.Status == "ReleasedForRecovery"
                    || report.Objective == "ReleaseForRecovery"
                    || (report.Reason != null && report.Reason.IndexOf("released", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
