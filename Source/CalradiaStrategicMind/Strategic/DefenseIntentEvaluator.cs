using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseIntentEvaluator
    {
        public DefenseIntentReport EvaluateDefenseIntent(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null)
            {
                return DefenseIntentReport.NotExplicit("No readable defense intent found");
            }

            if (party.CurrentSettlement == settlement && IsFriendlyToSettlement(party, settlement))
            {
                return DefenseIntentReport.Explicit("Party is inside settlement as friendly defender");
            }

            if (party.DefaultBehavior == AiBehavior.DefendSettlement)
            {
                if (party.TargetSettlement == settlement)
                {
                    return DefenseIntentReport.Explicit("Party default behavior defends this settlement");
                }

                return DefenseIntentReport.NotExplicit("Party defends a different settlement");
            }

            var army = party.Army;
            if (army != null)
            {
                var leaderParty = army.LeaderParty;
                if (leaderParty != null && leaderParty.DefaultBehavior == AiBehavior.DefendSettlement)
                {
                    if (leaderParty.TargetSettlement == settlement)
                    {
                        return DefenseIntentReport.Explicit("Army leader default behavior defends this settlement");
                    }

                    return DefenseIntentReport.NotExplicit("Army leader defends a different settlement");
                }

                if (army.AiBehaviorObject is Settlement armyTargetSettlement)
                {
                    if (armyTargetSettlement == settlement && leaderParty != null && leaderParty.DefaultBehavior == AiBehavior.DefendSettlement)
                    {
                        return DefenseIntentReport.Explicit("Army behavior object defends this settlement");
                    }

                    if (armyTargetSettlement != settlement)
                    {
                        return DefenseIntentReport.NotExplicit("Army targets a different settlement");
                    }
                }
            }

            if (party.TargetSettlement != null && party.TargetSettlement != settlement)
            {
                return DefenseIntentReport.NotExplicit("Party targets a different settlement");
            }

            return DefenseIntentReport.NotExplicit("No readable defense intent found");
        }

        private static bool IsFriendlyToSettlement(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null || party.MapFaction == null || settlement.MapFaction == null)
            {
                return false;
            }

            return party.MapFaction == settlement.MapFaction
                || party.MapFaction.MapFaction == settlement.MapFaction;
        }
    }

    public struct DefenseIntentReport
    {
        private DefenseIntentReport(bool isExplicitDefender, string reason)
        {
            IsExplicitDefender = isExplicitDefender;
            Reason = reason;
        }

        public bool IsExplicitDefender { get; private set; }

        public string Reason { get; private set; }

        public static DefenseIntentReport Explicit(string reason)
        {
            return new DefenseIntentReport(true, reason);
        }

        public static DefenseIntentReport NotExplicit(string reason)
        {
            return new DefenseIntentReport(false, reason);
        }
    }
}
