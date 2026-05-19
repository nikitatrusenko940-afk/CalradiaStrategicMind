using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmAssignmentConflictChecker
    {
        private readonly CsmArmyAssignmentRegistry _armyAssignments;
        private readonly CsmDefenseAssignmentRegistry _defenseAssignments;

        public CsmAssignmentConflictChecker(CsmArmyAssignmentRegistry armyAssignments, CsmDefenseAssignmentRegistry defenseAssignments)
        {
            _armyAssignments = armyAssignments;
            _defenseAssignments = defenseAssignments;
        }

        public CsmAssignmentConflictReport CheckPartyForNewDefenseCommand(MobileParty party, Settlement target)
        {
            return CheckParty(party, target, true);
        }

        public CsmAssignmentConflictReport CheckPartyForNewArmyCommand(MobileParty party, Settlement target)
        {
            return CheckParty(party, target, false);
        }

        private CsmAssignmentConflictReport CheckParty(MobileParty party, Settlement target, bool forDefenseCommand)
        {
            var report = CsmAssignmentConflictReport.Allowed();
            if (party == null)
            {
                return Block(report, "Unknown", "none", "Candidate party is missing");
            }

            report.IsPlayerParty = party.IsMainParty || party == MobileParty.MainParty;
            if (report.IsPlayerParty)
            {
                return Block(report, "Player", "none", "Candidate is player party");
            }

            report.IsInBattle = party.MapEvent != null;
            if (report.IsInBattle)
            {
                return Block(report, "Campaign", "none", "Candidate is in battle");
            }

            var defenseAssignment = _defenseAssignments == null ? null : _defenseAssignments.GetActiveAssignmentForParty(GetPartyId(party), GetPartyName(party));
            report.HasActiveDefenseAssignment = defenseAssignment != null;
            report.IsAssignedDefender = defenseAssignment != null;
            if (forDefenseCommand && defenseAssignment != null)
            {
                return Block(report, "DefenseController", defenseAssignment.SettlementName, "Candidate has active CSM defense assignment targeting " + defenseAssignment.SettlementName);
            }

            if (!forDefenseCommand && defenseAssignment != null)
            {
                return Block(report, "DefenseController", defenseAssignment.SettlementName, "Candidate blocked because it has active CSM defense assignment");
            }

            var armyAssignment = GetArmyAssignment(party);
            report.HasActiveArmyAssignment = armyAssignment != null;
            report.IsArmyLeaderWithActiveMission = armyAssignment != null && party.Army != null && party.Army.LeaderParty == party;
            if (forDefenseCommand && armyAssignment != null)
            {
                return Block(report, "ArmyDirector", armyAssignment.TargetSettlementName, "Blocked by active CSM army assignment targeting " + armyAssignment.TargetSettlementName);
            }

            if (!forDefenseCommand && armyAssignment != null)
            {
                return Block(report, "ArmyDirector", armyAssignment.TargetSettlementName, "Active CSM army assignment already exists");
            }

            report.IsBesiegingAssignedArmyTarget = armyAssignment != null
                && party.BesiegedSettlement != null
                && IsSameSettlement(party.BesiegedSettlement, armyAssignment.TargetSettlementId, armyAssignment.TargetSettlementName);

            report.IsAlreadyBesiegingDifferentTarget = party.BesiegedSettlement != null && !IsSameSettlement(party.BesiegedSettlement, target);
            if (report.IsAlreadyBesiegingDifferentTarget)
            {
                return Block(report, "Campaign", GetSettlementName(party.BesiegedSettlement), "Candidate is already besieging enemy target");
            }

            report.IsAlreadyDefendingDifferentSettlement = party.TargetSettlement != null
                && party.DefaultBehavior == AiBehavior.DefendSettlement
                && !IsSameSettlement(party.TargetSettlement, target);
            if (report.IsAlreadyDefendingDifferentSettlement)
            {
                return Block(report, "DefenseController", GetSettlementName(party.TargetSettlement), "Candidate is already defending different settlement");
            }

            return report;
        }

        private CsmArmyAssignment GetArmyAssignment(MobileParty party)
        {
            if (_armyAssignments == null || party == null || party.Army == null || party.Army.LeaderParty == null)
            {
                return null;
            }

            return _armyAssignments.GetActiveAssignmentForArmy(GetPartyId(party.Army.LeaderParty));
        }

        private static CsmAssignmentConflictReport Block(CsmAssignmentConflictReport report, string system, string target, string reason)
        {
            report.IsBlocked = true;
            report.BlockingSystem = system;
            report.BlockingAssignmentTarget = string.IsNullOrWhiteSpace(target) ? "none" : target;
            report.Reason = reason;
            return report;
        }

        private static bool IsSameSettlement(Settlement left, Settlement right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(left.StringId) && left.StringId == right.StringId
                || left.Name != null && right.Name != null && NamesEqual(left.Name.ToString(), right.Name.ToString());
        }

        private static bool IsSameSettlement(Settlement settlement, string id, string name)
        {
            if (settlement == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(id) && settlement.StringId == id
                || settlement.Name != null && NamesEqual(settlement.Name.ToString(), name);
        }

        private static string GetPartyId(MobileParty party)
        {
            return party == null || party.StringId == null ? string.Empty : party.StringId;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
