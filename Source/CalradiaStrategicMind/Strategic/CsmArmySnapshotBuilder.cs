using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmySnapshotBuilder
    {
        public List<CsmArmySnapshot> Build()
        {
            var snapshots = new List<CsmArmySnapshot>();
            var kingdoms = Kingdom.All;
            if (kingdoms == null)
            {
                return snapshots;
            }

            for (var kingdomIndex = 0; kingdomIndex < kingdoms.Count; kingdomIndex++)
            {
                var kingdom = kingdoms[kingdomIndex];
                if (kingdom == null || kingdom.IsEliminated || kingdom.Armies == null)
                {
                    continue;
                }

                for (var armyIndex = 0; armyIndex < kingdom.Armies.Count; armyIndex++)
                {
                    snapshots.Add(CreateSnapshot(kingdom.Armies[armyIndex], kingdom));
                }
            }

            return snapshots;
        }

        private static CsmArmySnapshot CreateSnapshot(Army army, Kingdom kingdom)
        {
            var leaderParty = army == null ? null : army.LeaderParty;
            var target = GetTargetSettlement(army, leaderParty);
            var isPlayerArmy = leaderParty != null && leaderParty.IsMainParty;
            var invalidReason = GetInvalidReason(army, kingdom, leaderParty, isPlayerArmy);
            return new CsmArmySnapshot(
                army,
                GetArmyId(army),
                GetArmyName(army),
                GetKingdomName(kingdom),
                leaderParty,
                army == null ? 0f : army.EstimatedStrength,
                army == null || army.Parties == null ? 0 : army.Parties.Count,
                army == null ? 0f : army.Cohesion,
                target,
                GetObjectiveGuess(leaderParty, target),
                isPlayerArmy,
                string.IsNullOrWhiteSpace(invalidReason),
                string.IsNullOrWhiteSpace(invalidReason) ? "Valid" : invalidReason);
        }

        private static string GetInvalidReason(Army army, Kingdom kingdom, MobileParty leaderParty, bool isPlayerArmy)
        {
            if (army == null)
            {
                return "Army is null";
            }

            if (kingdom == null || kingdom.IsEliminated)
            {
                return "Invalid kingdom";
            }

            if (leaderParty == null)
            {
                return "Army has no leader party";
            }

            if (isPlayerArmy || leaderParty == MobileParty.MainParty)
            {
                return "Player army";
            }

            if (leaderParty.MapEvent != null)
            {
                return "Army leader is in battle";
            }

            if (leaderParty.IsDisbanding)
            {
                return "Army leader is disbanding";
            }

            if (army.EstimatedStrength <= 0f || army.Parties == null || army.Parties.Count <= 0)
            {
                return "Army has no troops";
            }

            if (leaderParty.MapFaction == null || !leaderParty.MapFaction.IsKingdomFaction)
            {
                return "Army faction is not a kingdom";
            }

            return null;
        }

        private static Settlement GetTargetSettlement(Army army, MobileParty leaderParty)
        {
            if (leaderParty != null)
            {
                if (leaderParty.BesiegedSettlement != null)
                {
                    return leaderParty.BesiegedSettlement;
                }

                if (leaderParty.TargetSettlement != null)
                {
                    return leaderParty.TargetSettlement;
                }
            }

            return army != null && army.AiBehaviorObject is Settlement ? (Settlement)army.AiBehaviorObject : null;
        }

        private static string GetObjectiveGuess(MobileParty leaderParty, Settlement target)
        {
            if (leaderParty == null)
            {
                return "IdleOrUnknown";
            }

            if (leaderParty.BesiegedSettlement != null || leaderParty.DefaultBehavior == AiBehavior.BesiegeSettlement)
            {
                return "BesiegingSettlement";
            }

            if (leaderParty.DefaultBehavior == AiBehavior.DefendSettlement)
            {
                return "DefendingSettlement";
            }

            if (target != null && leaderParty.TargetSettlement == target)
            {
                return "MovingToSettlement";
            }

            return "IdleOrUnknown";
        }

        private static string GetArmyId(Army army)
        {
            if (army == null || army.LeaderParty == null)
            {
                return "unknown";
            }

            return army.LeaderParty.StringId ?? GetArmyName(army);
        }

        private static string GetArmyName(Army army)
        {
            return army == null || army.Name == null ? "unknown" : army.Name.ToString();
        }

        private static string GetKingdomName(Kingdom kingdom)
        {
            return kingdom == null || kingdom.Name == null ? "unknown" : kingdom.Name.ToString();
        }
    }
}
