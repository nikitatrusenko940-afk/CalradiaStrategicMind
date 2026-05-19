using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmStrategicTaskDetector
    {
        public CsmStrategicTaskReport Detect(
            MobileParty party,
            CsmArmyAssignmentRegistry armyAssignments,
            CsmDefenseAssignmentRegistry defenseAssignments)
        {
            if (party == null)
            {
                return CsmStrategicTaskReport.None("Party is missing");
            }

            if (party.IsMainParty || party == MobileParty.MainParty)
            {
                return CsmStrategicTaskReport.None("Player party ignored");
            }

            var defenseAssignment = defenseAssignments == null ? null : defenseAssignments.GetActiveAssignmentForParty(GetPartyId(party), GetPartyName(party));
            if (defenseAssignment != null)
            {
                return new CsmStrategicTaskReport
                {
                    HasStrategicTask = true,
                    TaskType = "DefenseAssignment",
                    TargetSettlementId = defenseAssignment.SettlementId,
                    TargetSettlementName = defenseAssignment.SettlementName,
                    Reason = "Party has active CSM defense assignment",
                    DefenseAssignment = defenseAssignment
                };
            }

            var armyAssignment = GetArmyAssignment(party, armyAssignments);
            if (armyAssignment == null)
            {
                return CsmStrategicTaskReport.None("No active CSM assignment");
            }

            var isLeader = party.Army != null && party.Army.LeaderParty == party;
            return new CsmStrategicTaskReport
            {
                HasStrategicTask = true,
                TaskType = isLeader ? GetArmyTaskType(armyAssignment) : "ArmyMemberFollowingLeader",
                TargetSettlementId = armyAssignment.TargetSettlementId,
                TargetSettlementName = armyAssignment.TargetSettlementName,
                Reason = isLeader ? "Party is leader of active CSM army assignment" : "Party follows leader with active CSM army assignment",
                ArmyAssignment = armyAssignment
            };
        }

        private static CsmArmyAssignment GetArmyAssignment(MobileParty party, CsmArmyAssignmentRegistry armyAssignments)
        {
            if (party == null || armyAssignments == null || party.Army == null || party.Army.LeaderParty == null)
            {
                return null;
            }

            return armyAssignments.GetActiveAssignmentForArmy(GetPartyId(party.Army.LeaderParty));
        }

        private static string GetArmyTaskType(CsmArmyAssignment assignment)
        {
            if (assignment == null)
            {
                return "None";
            }

            return assignment.ObjectiveType == "DefendSettlement" ? "UrgentDefense" : "ArmyAttack";
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? string.Empty;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }
    }
}
