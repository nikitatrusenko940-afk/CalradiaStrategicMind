using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class SettlementValueEvaluator
    {
        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;

        public SettlementValueEvaluator()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
        }

        public SettlementValueReport EvaluateSettlementValue(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate settlement value", () => EvaluateSettlementValueCore(settlement), SettlementValueReport.Empty);
        }

        private SettlementValueReport EvaluateSettlementValueCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return SettlementValueReport.Empty;
            }

            var isTown = settlement.IsTown;
            var isCastle = settlement.IsCastle;
            var prosperity = GetProsperity(settlement);
            var garrisonStrength = GetGarrisonStrength(settlement);
            var typeValue = isTown ? 120f : isCastle ? 70f : 20f;
            var prosperityValue = isTown ? prosperity * 0.05f : 0f;
            var garrisonValue = garrisonStrength * 0.25f;
            var strategicValue = typeValue + prosperityValue + garrisonValue;
            var reason = GetValueReason(isTown, isCastle, prosperity, garrisonStrength);

            return new SettlementValueReport(
                GetSettlementName(settlement),
                GetFactionName(settlement.MapFaction),
                GetSettlementType(settlement),
                isTown,
                isCastle,
                prosperity,
                garrisonStrength,
                strategicValue,
                reason);
        }

        private static float GetProsperity(Settlement settlement)
        {
            if (settlement == null || !settlement.IsTown || settlement.Town == null)
            {
                return 0f;
            }

            return settlement.Town.Prosperity;
        }

        private float GetGarrisonStrength(Settlement settlement)
        {
            if (settlement == null || settlement.Parties == null)
            {
                return 0f;
            }

            for (var index = 0; index < settlement.Parties.Count; index++)
            {
                var party = settlement.Parties[index];
                if (party == null || !party.IsGarrison)
                {
                    continue;
                }

                return _partyStrengthEvaluator.EvaluatePartyStrength(party);
            }

            return 0f;
        }

        private static string GetValueReason(bool isTown, bool isCastle, float prosperity, float garrisonStrength)
        {
            if (isTown)
            {
                return prosperity > 0f
                    ? "Town base value, prosperity, and garrison strength"
                    : "Town base value and garrison strength";
            }

            if (isCastle)
            {
                return "Castle base value and garrison strength";
            }

            return garrisonStrength > 0f
                ? "Other settlement with garrison strength"
                : "Other settlement fallback value";
        }

        private static string GetSettlementName(Settlement settlement)
        {
            if (settlement == null || settlement.Name == null)
            {
                return "unknown";
            }

            return settlement.Name.ToString();
        }

        private static string GetFactionName(IFaction faction)
        {
            if (faction == null || faction.Name == null)
            {
                return "none";
            }

            return faction.Name.ToString();
        }

        private static string GetSettlementType(Settlement settlement)
        {
            if (settlement == null)
            {
                return "Unknown";
            }

            if (settlement.IsTown)
            {
                return "Town";
            }

            if (settlement.IsCastle)
            {
                return "Castle";
            }

            return "Other";
        }
    }
}
