using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefensePriorityEvaluator
    {
        private const float BasePriority = 5f;
        private const float MaxThreatComponent = 45f;
        private const float ThreatScale = 250f;
        private const float MaxValueComponent = 40f;
        private const float ValueScale = 350f;
        private const float ThreatenedPriorityBonus = 35f;
        private const float DefenseRequestThreshold = 70f;

        private readonly SettlementThreatEvaluator _settlementThreatEvaluator;
        private readonly SettlementValueEvaluator _settlementValueEvaluator;

        public DefensePriorityEvaluator()
        {
            _settlementThreatEvaluator = new SettlementThreatEvaluator();
            _settlementValueEvaluator = new SettlementValueEvaluator();
        }

        public DefensePriorityReport EvaluateDefensePriority(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate defense priority", () => EvaluateDefensePriorityCore(settlement), DefensePriorityReport.Empty);
        }

        private DefensePriorityReport EvaluateDefensePriorityCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return DefensePriorityReport.Empty;
            }

            var threatReport = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            var valueReport = _settlementValueEvaluator.EvaluateSettlementValue(settlement);
            var threatComponent = GetScaledComponent(threatReport.ThreatScore, MaxThreatComponent, ThreatScale);
            var valueComponent = GetScaledComponent(valueReport.StrategicValue, MaxValueComponent, ValueScale);
            var threatenedBonus = threatReport.IsThreatened ? ThreatenedPriorityBonus : 0f;
            var defensePriority = Clamp(BasePriority + threatComponent + valueComponent + threatenedBonus, 0f, 100f);

            var shouldRequestDefense = threatReport.IsThreatened && defensePriority >= DefenseRequestThreshold;

            return new DefensePriorityReport(
                threatReport.SettlementName,
                threatReport.OwnerKingdomName,
                threatReport.SettlementType,
                threatReport.ThreatScore,
                valueReport.StrategicValue,
                threatComponent,
                valueComponent,
                threatenedBonus,
                defensePriority,
                threatReport.NearbyEnemyPartyCount,
                threatReport.StrongestEnemyPartyName,
                threatReport.StrongestEnemyStrength,
                threatReport.NearestEnemyPartyName,
                threatReport.NearestEnemyDistance,
                threatReport.IsThreatened,
                shouldRequestDefense,
                GetReason(threatReport, valueComponent, shouldRequestDefense));
        }

        private static float GetScaledComponent(float value, float max, float scale)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return max * value / (value + scale);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string GetReason(SettlementThreatReport threatReport, float valueComponent, bool shouldRequestDefense)
        {
            if (!threatReport.IsThreatened)
            {
                if (valueComponent > 25f)
                {
                    return "Safe but valuable: high prosperity or garrison";
                }

                return threatReport.NearbyEnemyPartyCount <= 0
                    ? "Safe: no nearby strategic enemies"
                    : "Safe: enemy pressure below local defense";
            }

            if (shouldRequestDefense)
            {
                return valueComponent > 25f
                    ? "Defense recommended: high threat and high value"
                    : "Threatened: enemy pressure exceeds local defense";
            }

            return "Threatened but below request threshold";
        }
    }
}
