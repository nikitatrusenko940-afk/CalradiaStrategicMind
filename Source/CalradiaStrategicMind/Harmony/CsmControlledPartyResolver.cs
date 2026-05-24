using System;
using CalradiaStrategicMind.Strategic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Harmony
{
    public static class CsmControlledPartyResolver
    {
        private static CsmDefenseAssignmentRegistry _defenseAssignments;
        private static CsmArmyAssignmentRegistry _armyAssignments;

        public static void Register(CsmDefenseAssignmentRegistry defenseAssignments, CsmArmyAssignmentRegistry armyAssignments)
        {
            _defenseAssignments = defenseAssignments;
            _armyAssignments = armyAssignments;
        }

        public static CsmControlledPartyInfo Resolve(MobileParty party)
        {
            if (party == null)
            {
                return CsmControlledPartyInfo.None;
            }

            var partyId = GetPartyId(party);
            var partyName = GetPartyName(party);
            var defenseAssignment = _defenseAssignments == null
                ? null
                : _defenseAssignments.GetActiveAssignmentForParty(partyId, partyName);
            if (defenseAssignment != null)
            {
                return new CsmControlledPartyInfo(
                    true,
                    "Defense",
                    "DefendSettlement",
                    defenseAssignment.SettlementName,
                    FindSettlementByIdOrName(defenseAssignment.SettlementId, defenseAssignment.SettlementName));
            }

            var armyAssignments = _armyAssignments == null ? null : _armyAssignments.GetActiveAssignments();
            if (armyAssignments != null)
            {
                for (var index = 0; index < armyAssignments.Count; index++)
                {
                    var assignment = armyAssignments[index];
                    if (assignment == null)
                    {
                        continue;
                    }

                    if (NamesEqual(assignment.LeaderPartyId, partyId) || NamesEqual(assignment.LeaderPartyName, partyName))
                    {
                        return new CsmControlledPartyInfo(
                            true,
                            "Army",
                            assignment.ObjectiveType,
                            assignment.TargetSettlementName,
                            FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName));
                    }
                }
            }

            return CsmControlledPartyInfo.None;
        }

        public static void MarkDefenseLockApplied(MobileParty party, int tick)
        {
            if (party == null || _defenseAssignments == null)
            {
                return;
            }

            var assignment = _defenseAssignments.GetActiveAssignmentForParty(GetPartyId(party), GetPartyName(party));
            if (assignment != null)
            {
                assignment.LastHarmonyDefenseLockTick = tick;
            }
        }

        private static Settlement FindSettlementByIdOrName(string settlementId, string settlementName)
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

                if (!string.IsNullOrWhiteSpace(settlementId) && NamesEqual(settlement.StringId, settlementId))
                {
                    return settlement;
                }

                if (settlement.Name != null && NamesEqual(settlement.Name.ToString(), settlementName))
                {
                    return settlement;
                }
            }

            return null;
        }

        private static string GetPartyId(MobileParty party)
        {
            return party == null || string.IsNullOrWhiteSpace(party.StringId) ? string.Empty : party.StringId;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party == null || party.Name == null ? "unknown" : party.Name.ToString();
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public struct CsmControlledPartyInfo
    {
        public static readonly CsmControlledPartyInfo None = new CsmControlledPartyInfo(false, "None", "None", "none", null);

        public CsmControlledPartyInfo(
            bool isControlled,
            string ownerSystem,
            string objectiveType,
            string expectedTargetName,
            Settlement expectedTarget)
        {
            IsControlled = isControlled;
            OwnerSystem = ownerSystem;
            ObjectiveType = objectiveType;
            ExpectedTargetName = expectedTargetName;
            ExpectedTarget = expectedTarget;
        }

        public bool IsControlled { get; private set; }

        public string OwnerSystem { get; private set; }

        public string ObjectiveType { get; private set; }

        public string ExpectedTargetName { get; private set; }

        public Settlement ExpectedTarget { get; private set; }
    }
}
