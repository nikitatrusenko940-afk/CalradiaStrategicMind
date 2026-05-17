using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyObjectiveReader
    {
        public CsmArmyObjectiveSnapshot Read(CsmArmySnapshot snapshot)
        {
            var result = new CsmArmyObjectiveSnapshot
            {
                ArmyId = snapshot.ArmyId,
                ArmyName = snapshot.ArmyName,
                KingdomName = snapshot.KingdomName,
                LeaderParty = snapshot.LeaderParty,
                CurrentObjective = "IdleOrUnknown",
                CurrentTargetSettlementName = "none",
                LeaderTargetSettlementName = "none",
                LeaderBesiegedSettlementName = "none",
                LeaderCurrentSettlementName = "none",
                ArmyAiBehaviorObjectSettlementName = "none",
                ObjectiveMismatchReason = "No objective mismatch detected",
                IsReadable = false,
                Reason = "Army objective not readable"
            };

            var army = snapshot.Army;
            var leaderParty = snapshot.LeaderParty;
            if (army == null || leaderParty == null)
            {
                return result;
            }

            var target = GetCurrentTargetSettlement(army, leaderParty);
            result.LeaderTargetSettlement = leaderParty.TargetSettlement;
            result.LeaderTargetSettlementName = GetSettlementName(result.LeaderTargetSettlement);
            result.LeaderBesiegedSettlement = leaderParty.BesiegedSettlement;
            result.LeaderBesiegedSettlementName = GetSettlementName(result.LeaderBesiegedSettlement);
            result.LeaderCurrentSettlement = leaderParty.CurrentSettlement;
            result.LeaderCurrentSettlementName = GetSettlementName(result.LeaderCurrentSettlement);
            result.ArmyAiBehaviorObjectSettlement = army.AiBehaviorObject as Settlement;
            result.ArmyAiBehaviorObjectSettlementName = GetSettlementName(result.ArmyAiBehaviorObjectSettlement);
            result.CurrentTargetSettlement = target;
            result.CurrentTargetSettlementName = GetSettlementName(target);
            result.IsBesieging = leaderParty.BesiegedSettlement != null
                || leaderParty.SiegeEvent != null
                || leaderParty.DefaultBehavior == AiBehavior.BesiegeSettlement
                || leaderParty.ShortTermBehavior == AiBehavior.BesiegeSettlement;
            result.IsDefending = leaderParty.DefaultBehavior == AiBehavior.DefendSettlement
                || leaderParty.ShortTermBehavior == AiBehavior.DefendSettlement;
            result.IsMovingToTarget = target != null && leaderParty.TargetSettlement == target;
            result.IsReadable = true;
            EvaluateMismatch(snapshot, result);

            if (result.IsBesieging && target != null)
            {
                result.CurrentObjective = leaderParty.BesiegedSettlement != null || leaderParty.SiegeEvent != null
                    ? "BesiegingSettlement"
                    : "MovingToBesiegeSettlement";
                result.Reason = "Army is besieging or moving to besiege settlement";
                return result;
            }

            if (result.IsDefending && target != null)
            {
                result.CurrentObjective = "MovingToDefendSettlement";
                result.Reason = "Army is moving to defend settlement";
                return result;
            }

            if (result.IsMovingToTarget)
            {
                result.CurrentObjective = "MovingToSettlement";
                result.Reason = "Army is moving to target settlement";
                return result;
            }

            result.Reason = "Army objective readable but idle or unknown";
            return result;
        }

        public bool HasAssignmentTargetMismatch(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            if (objective == null || assignment == null)
            {
                return false;
            }

            var targetName = assignment.TargetSettlementName;
            if (objective.ArmyAiBehaviorObjectSettlement != null && !IsSameTarget(objective.ArmyAiBehaviorObjectSettlement, assignment.TargetSettlementId, targetName))
            {
                return true;
            }

            return objective.HasObjectiveTargetMismatch;
        }

        private static void EvaluateMismatch(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot result)
        {
            if (snapshot.LeaderParty == null)
            {
                return;
            }

            var kingdomFaction = snapshot.LeaderParty.MapFaction;
            if (result.ArmyAiBehaviorObjectSettlement != null
                && kingdomFaction != null
                && result.ArmyAiBehaviorObjectSettlement.MapFaction == kingdomFaction
                && (result.IsBesieging || result.CurrentObjective == "MovingToBesiegeSettlement"))
            {
                result.HasObjectiveTargetMismatch = true;
                result.ObjectiveMismatchReason = "Army displayed attack objective points to friendly settlement";
                return;
            }

            if (result.ArmyAiBehaviorObjectSettlement != null
                && result.LeaderTargetSettlement != null
                && result.ArmyAiBehaviorObjectSettlement != result.LeaderTargetSettlement)
            {
                result.HasObjectiveTargetMismatch = true;
                result.ObjectiveMismatchReason = "Army displayed objective differs from leader target settlement";
            }
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

        private static Settlement GetCurrentTargetSettlement(Army army, MobileParty leaderParty)
        {
            if (leaderParty.BesiegedSettlement != null)
            {
                return leaderParty.BesiegedSettlement;
            }

            if (leaderParty.TargetSettlement != null)
            {
                return leaderParty.TargetSettlement;
            }

            if (leaderParty.CurrentSettlement != null && leaderParty.CurrentSettlement.SiegeEvent != null)
            {
                return leaderParty.CurrentSettlement;
            }

            return army.AiBehaviorObject as Settlement;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "none" : settlement.Name.ToString();
        }
    }
}
