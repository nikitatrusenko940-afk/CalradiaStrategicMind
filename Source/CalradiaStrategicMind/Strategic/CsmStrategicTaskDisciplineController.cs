using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmStrategicTaskDisciplineController
    {
        private readonly CsmStrategicTaskDetector _taskDetector;
        private readonly CsmTaskDistractionEvaluator _distractionEvaluator;
        private readonly Dictionary<string, int> _lastReassertTickByTask;
        private readonly Dictionary<string, int> _reassertCountByTask;

        public CsmStrategicTaskDisciplineController()
        {
            _taskDetector = new CsmStrategicTaskDetector();
            _distractionEvaluator = new CsmTaskDistractionEvaluator();
            _lastReassertTickByTask = new Dictionary<string, int>();
            _reassertCountByTask = new Dictionary<string, int>();
        }

        public void Execute(
            CsmArmyDirector armyDirector,
            DirectDefenseCommandController defenseController,
            int observationTick)
        {
            SafeExecutor.Run("Strategic task discipline", () => ExecuteCore(armyDirector, defenseController, observationTick));
        }

        private void ExecuteCore(
            CsmArmyDirector armyDirector,
            DirectDefenseCommandController defenseController,
            int observationTick)
        {
            if (!StrategicTaskDisciplineSettings.EnableStrategicTaskDiscipline)
            {
                return;
            }

            var parties = MobileParty.All;
            var armyAssignments = armyDirector == null ? null : armyDirector.AssignmentRegistry;
            var defenseAssignments = defenseController == null ? null : defenseController.AssignmentRegistry;
            if (parties == null || (armyAssignments == null && defenseAssignments == null))
            {
                LogSummary(observationTick, new DisciplineDiagnostics());
                return;
            }

            var diagnostics = new DisciplineDiagnostics();
            var detailLogsThisTick = 0;

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null || party.IsMainParty || party == MobileParty.MainParty)
                {
                    continue;
                }

                var task = _taskDetector.Detect(party, armyAssignments, defenseAssignments);
                if (task == null || !task.HasStrategicTask)
                {
                    continue;
                }

                diagnostics.CheckedTasks++;
                var distraction = _distractionEvaluator.Evaluate(party, task);
                if (distraction == null)
                {
                    continue;
                }

                RecordDistractionDiagnostics(diagnostics, distraction);
                if (ShouldLogDiagnosticDetail(distraction) && detailLogsThisTick < 3)
                {
                    LogDiagnostic(observationTick, party, distraction);
                    detailLogsThisTick++;
                }

                if (distraction.AllowedEngage)
                {
                    diagnostics.AllowedEngages++;
                    LogDiscipline(observationTick, party, distraction, "AllowedEngage");
                    continue;
                }

                if (!distraction.IsDistraction)
                {
                    continue;
                }

                diagnostics.DistractionsDetected++;
                var taskKey = GetTaskKey(task);
                var blockReason = GetReassertBlockReason(taskKey, observationTick);
                if (blockReason != "None")
                {
                    RecordReassertBlock(diagnostics, blockReason);
                    diagnostics.Ignored++;
                    LogDiscipline(observationTick, party, distraction, "Ignored");
                    continue;
                }

                string reassertReason;
                diagnostics.ReassertAttempted++;
                if (TryReassertTask(party, task, armyDirector, defenseController, observationTick, out reassertReason))
                {
                    RecordReassert(taskKey, observationTick);
                    diagnostics.TasksReasserted++;
                    distraction.Reason = reassertReason;
                    LogDiscipline(observationTick, party, distraction, "ReassertedTask");
                    continue;
                }

                diagnostics.ReassertFailed++;
                diagnostics.Ignored++;
                distraction.Reason = reassertReason;
                LogDiscipline(observationTick, party, distraction, "Ignored");
            }

            LogSummary(observationTick, diagnostics);
        }

        private bool TryReassertTask(
            MobileParty party,
            CsmStrategicTaskReport task,
            CsmArmyDirector armyDirector,
            DirectDefenseCommandController defenseController,
            int observationTick,
            out string reason)
        {
            reason = "Strategic task discipline could not reassert task";
            if (task == null)
            {
                reason = "Strategic task missing";
                return false;
            }

            if (task.ArmyAssignment != null && armyDirector != null)
            {
                return armyDirector.ReassertAssignment(party, task.ArmyAssignment, observationTick, out reason);
            }

            if (task.DefenseAssignment != null && defenseController != null)
            {
                return defenseController.ReassertDefenseAssignment(party, task.DefenseAssignment, observationTick, out reason);
            }

            reason = "No reassert path available for strategic task";
            return false;
        }

        private string GetReassertBlockReason(string taskKey, int observationTick)
        {
            if (string.IsNullOrWhiteSpace(taskKey))
            {
                return "MissingTaskKey";
            }

            int count;
            if (_reassertCountByTask.TryGetValue(taskKey, out count)
                && count >= GetMaxReasserts())
            {
                return "MaxReasserts";
            }

            int lastTick;
            if (_lastReassertTickByTask.TryGetValue(taskKey, out lastTick)
                && observationTick - lastTick < GetReassertCooldownTicks())
            {
                return "Cooldown";
            }

            return "None";
        }

        private static void RecordDistractionDiagnostics(DisciplineDiagnostics diagnostics, CsmTaskDistractionReport report)
        {
            if (diagnostics == null || report == null)
            {
                return;
            }

            if (report.HasTargetParty)
            {
                diagnostics.TargetPartyReadable++;
            }
            else
            {
                diagnostics.TargetPartyMissing++;
            }

            if (report.TargetPartyLowValue)
            {
                diagnostics.TargetPartyLowValue++;
            }
            else if (report.HasTargetParty && !report.ArmyMemberFollowingLeader)
            {
                diagnostics.TargetPartyNotLowValue++;
            }

            if (report.AssignedTargetMissing)
            {
                diagnostics.AssignedTargetMissing++;
            }

            if (report.StillOnAssignedTask)
            {
                diagnostics.StillOnAssignedTask++;
            }

            if (report.ArmyMemberFollowingLeader)
            {
                diagnostics.ArmyMembersFollowingLeader++;
            }

            if (report.AllowedEngageReasonCode == "DefensiveBattle")
            {
                diagnostics.AllowedByDefensiveBattle++;
            }
            else if (report.AllowedEngageReasonCode == "ThreatRatio")
            {
                diagnostics.AllowedByThreatRatio++;
            }
            else if (report.AllowedEngageReasonCode == "NearAssignedSettlement")
            {
                diagnostics.AllowedByNearAssignedSettlement++;
            }
        }

        private static void RecordReassertBlock(DisciplineDiagnostics diagnostics, string blockReason)
        {
            if (diagnostics == null)
            {
                return;
            }

            if (blockReason == "Cooldown")
            {
                diagnostics.ReassertCooldownBlocked++;
                return;
            }

            if (blockReason == "MaxReasserts")
            {
                diagnostics.ReassertMaxBlocked++;
            }
        }

        private static bool ShouldLogDiagnosticDetail(CsmTaskDistractionReport report)
        {
            if (report == null)
            {
                return false;
            }

            return report.TargetPartyLowValue
                || report.IsDistraction
                || report.AllowedEngage
                || report.TargetPartyReadable && report.TaskType != "ArmyMemberFollowingLeader";
        }

        private void RecordReassert(string taskKey, int observationTick)
        {
            _lastReassertTickByTask[taskKey] = observationTick;
            int count;
            _reassertCountByTask.TryGetValue(taskKey, out count);
            _reassertCountByTask[taskKey] = count + 1;
        }

        private static string GetTaskKey(CsmStrategicTaskReport task)
        {
            if (task == null)
            {
                return string.Empty;
            }

            if (task.ArmyAssignment != null)
            {
                return "army|" + Normalize(task.ArmyAssignment.ArmyId);
            }

            if (task.DefenseAssignment != null)
            {
                return "defense|" + Normalize(task.DefenseAssignment.SettlementId) + "|" + Normalize(task.DefenseAssignment.PartyId);
            }

            return string.Empty;
        }

        private static void LogDiscipline(int tick, MobileParty party, CsmTaskDistractionReport report, string action)
        {
            if (report == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM task discipline: tick={tick}, party='{GetPartyName(party)}', taskType='{report.TaskType}', target='{report.AssignedTargetName}', distraction='{report.DistractionPartyName}', distractionType='{report.DistractionCategory}', action='{action}', reason='{report.Reason}'");
        }

        private static void LogDiagnostic(int tick, MobileParty party, CsmTaskDistractionReport report)
        {
            CsmLogger.Info(
                $"Observed CSM task discipline diagnostic: tick={tick}, party='{GetPartyName(party)}', taskType='{report.TaskType}', assignedTarget='{report.AssignedTargetName}', targetParty='{report.DistractionPartyName}', targetPartyLowValue={report.TargetPartyLowValue}, targetPartyCategory='{report.DistractionCategory}', isDistraction={report.IsDistraction}, allowedEngage={report.AllowedEngage}, reason='{report.Reason}'");
        }

        private static void LogSummary(int tick, DisciplineDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                diagnostics = new DisciplineDiagnostics();
            }

            CsmLogger.Info(
                $"Observed CSM task discipline summary: tick={tick}, checkedTasks={diagnostics.CheckedTasks}, targetPartyReadable={diagnostics.TargetPartyReadable}, targetPartyMissing={diagnostics.TargetPartyMissing}, targetPartyLowValue={diagnostics.TargetPartyLowValue}, targetPartyNotLowValue={diagnostics.TargetPartyNotLowValue}, assignedTargetMissing={diagnostics.AssignedTargetMissing}, stillOnAssignedTask={diagnostics.StillOnAssignedTask}, armyMembersFollowingLeader={diagnostics.ArmyMembersFollowingLeader}, allowedByDefensiveBattle={diagnostics.AllowedByDefensiveBattle}, allowedByThreatRatio={diagnostics.AllowedByThreatRatio}, allowedByNearAssignedSettlement={diagnostics.AllowedByNearAssignedSettlement}, distractionsDetected={diagnostics.DistractionsDetected}, reassertCooldownBlocked={diagnostics.ReassertCooldownBlocked}, reassertMaxBlocked={diagnostics.ReassertMaxBlocked}, reassertAttempted={diagnostics.ReassertAttempted}, tasksReasserted={diagnostics.TasksReasserted}, reassertFailed={diagnostics.ReassertFailed}, allowedEngages={diagnostics.AllowedEngages}, ignored={diagnostics.Ignored}, reason='Strategic task discipline diagnostic snapshot'");
        }

        private static int GetReassertCooldownTicks()
        {
            return StrategicTaskDisciplineSettings.DisciplineReassertCooldownTicks < 0
                ? 0
                : StrategicTaskDisciplineSettings.DisciplineReassertCooldownTicks;
        }

        private static int GetMaxReasserts()
        {
            return StrategicTaskDisciplineSettings.MaxDisciplineReassertsPerAssignment < 0
                ? 0
                : StrategicTaskDisciplineSettings.MaxDisciplineReassertsPerAssignment;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private class DisciplineDiagnostics
        {
            public int CheckedTasks;
            public int TargetPartyReadable;
            public int TargetPartyMissing;
            public int TargetPartyLowValue;
            public int TargetPartyNotLowValue;
            public int AssignedTargetMissing;
            public int StillOnAssignedTask;
            public int ArmyMembersFollowingLeader;
            public int AllowedByDefensiveBattle;
            public int AllowedByThreatRatio;
            public int AllowedByNearAssignedSettlement;
            public int DistractionsDetected;
            public int ReassertCooldownBlocked;
            public int ReassertMaxBlocked;
            public int ReassertAttempted;
            public int TasksReasserted;
            public int ReassertFailed;
            public int AllowedEngages;
            public int Ignored;
        }
    }
}
