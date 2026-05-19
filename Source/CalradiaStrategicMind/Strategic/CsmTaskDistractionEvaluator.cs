using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmTaskDistractionEvaluator
    {
        private readonly CsmLowValuePartyClassifier _lowValueClassifier;

        public CsmTaskDistractionEvaluator()
        {
            _lowValueClassifier = new CsmLowValuePartyClassifier();
        }

        public CsmTaskDistractionReport Evaluate(MobileParty party, CsmStrategicTaskReport task)
        {
            var report = CreateReport(task, "No distraction detected");
            if (party == null || task == null || !task.HasStrategicTask)
            {
                report.Reason = "Party has no active CSM task";
                return report;
            }

            var assignedTarget = FindSettlementByIdOrName(task.TargetSettlementId, task.TargetSettlementName);
            if (assignedTarget == null)
            {
                report.AssignedTargetMissing = true;
                report.AllowedEngageReasonCode = "AssignedTargetMissing";
                report.Reason = "CSM target missing or invalid";
                return report;
            }

            var targetParty = party.TargetParty;
            if (targetParty == null)
            {
                report.HasTargetParty = false;
                report.TargetPartyReadable = false;
                report.AllowedEngageReasonCode = "TargetMissing";
                report.Reason = "Party has no target party";
                return report;
            }

            report.HasTargetParty = true;
            report.TargetPartyReadable = true;
            report.DistractionPartyName = GetPartyName(targetParty);
            if (IsArmyMemberFollowingLeader(party, task, targetParty))
            {
                report.StillOnAssignedTask = true;
                report.ArmyMemberFollowingLeader = true;
                report.DistractionCategory = "ArmyLeader";
                report.Reason = "Army member is following assigned army leader";
                return report;
            }

            var classification = _lowValueClassifier.Classify(targetParty);
            report.DistractionCategory = classification.Category;
            report.TargetPartyLowValue = classification.IsLowValue;
            if (!classification.IsLowValue)
            {
                report.AllowedEngageReasonCode = "TargetNotLowValue";
                report.Reason = "Target party is not low-value";
                return report;
            }

            if (IsOnAssignedTask(party, assignedTarget))
            {
                report.StillOnAssignedTask = true;
                report.Reason = "Party remains on assigned CSM target";
                return report;
            }

            if (IsAllowedEngage(party, targetParty, assignedTarget, classification, report))
            {
                return report;
            }

            report.IsDistraction = true;
            report.ShouldReassertTask = true;
            report.Reason = "Party with active CSM task ignored low-value distraction";
            return report;
        }

        private static bool IsAllowedEngage(
            MobileParty party,
            MobileParty targetParty,
            Settlement assignedTarget,
            CsmLowValuePartyClassification classification,
            CsmTaskDistractionReport report)
        {
            if (StrategicTaskDisciplineSettings.AllowEngageIfAlreadyInDefensiveBattle && party.MapEvent != null)
            {
                report.AllowedEngage = true;
                report.AllowedEngageReasonCode = "DefensiveBattle";
                report.Reason = "Allowed engage because party is already in defensive battle";
                return true;
            }

            var partyStrength = GetEstimatedStrength(party);
            if (partyStrength > 0f && classification.EstimatedStrength >= StrategicTaskDisciplineSettings.LowValuePartyThreatRatioToAllowEngage * partyStrength)
            {
                report.AllowedEngage = true;
                report.AllowedEngageReasonCode = "ThreatRatio";
                report.Reason = "Allowed engage because low-value party is a significant threat";
                return true;
            }

            if (StrategicTaskDisciplineSettings.AllowEngageIfLowValueThreatensAssignedSettlement
                && assignedTarget != null
                && targetParty != null
                && targetParty.Position.Distance(assignedTarget.Position) <= StrategicTaskDisciplineSettings.LowValuePartyNearAssignedTargetDistance)
            {
                report.AllowedEngage = true;
                report.AllowedEngageReasonCode = "NearAssignedSettlement";
                report.Reason = "Allowed engage because low-value party is near assigned settlement";
                return true;
            }

            return false;
        }

        private static bool IsOnAssignedTask(MobileParty party, Settlement assignedTarget)
        {
            if (party == null || assignedTarget == null)
            {
                return false;
            }

            if (party.TargetSettlement == assignedTarget || party.BesiegedSettlement == assignedTarget)
            {
                return true;
            }

            if ((party.DefaultBehavior == AiBehavior.DefendSettlement || party.ShortTermBehavior == AiBehavior.DefendSettlement)
                && party.TargetSettlement == assignedTarget)
            {
                return true;
            }

            if ((party.DefaultBehavior == AiBehavior.BesiegeSettlement || party.ShortTermBehavior == AiBehavior.BesiegeSettlement)
                && (party.TargetSettlement == assignedTarget || party.BesiegedSettlement == assignedTarget))
            {
                return true;
            }

            return false;
        }

        private static bool IsArmyMemberFollowingLeader(MobileParty party, CsmStrategicTaskReport task, MobileParty targetParty)
        {
            return party != null
                && task != null
                && task.TaskType == "ArmyMemberFollowingLeader"
                && targetParty != null
                && party.Army != null
                && party.Army.LeaderParty != null
                && targetParty == party.Army.LeaderParty;
        }

        private static CsmTaskDistractionReport CreateReport(CsmStrategicTaskReport task, string reason)
        {
            return new CsmTaskDistractionReport
            {
                IsDistraction = false,
                ShouldReassertTask = false,
                AllowedEngage = false,
                HasTargetParty = false,
                TargetPartyReadable = false,
                TargetPartyLowValue = false,
                AssignedTargetMissing = false,
                StillOnAssignedTask = false,
                ArmyMemberFollowingLeader = false,
                AllowedEngageReasonCode = "None",
                TaskType = task == null ? "None" : task.TaskType,
                AssignedTargetName = task == null ? "none" : task.TargetSettlementName,
                DistractionPartyName = "none",
                DistractionCategory = "None",
                Reason = reason
            };
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

        private static float GetEstimatedStrength(MobileParty party)
        {
            if (party?.Party != null)
            {
                return party.Party.EstimatedStrength;
            }

            return party?.MemberRoster == null ? 0f : party.MemberRoster.TotalManCount;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }
    }
}
