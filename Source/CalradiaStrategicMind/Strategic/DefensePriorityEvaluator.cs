using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefensePriorityEvaluator
    {
        private const float BasePriority = 5f;
        private const float MaxSiegeThreatComponent = 45f;
        private const float ThreatScale = 250f;
        private const float MaxAreaPressureComponent = 12f;
        private const float AreaPressureScale = 600f;
        private const float MaxValueComponent = 40f;
        private const float ValueScale = 350f;
        private const float ActiveSiegeBonus = 45f;
        private const float EnemyArmyBonus = 25f;
        private const float ExtremePressureBonus = 10f;
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

        public DefensePriorityReport EvaluateDefensePriority(
            Settlement settlement,
            SettlementThreatReport threatReport,
            SettlementValueReport valueReport)
        {
            return SafeExecutor.Run(
                "Evaluate defense priority from reports",
                () => EvaluateDefensePriorityCore(settlement, threatReport, valueReport),
                DefensePriorityReport.Empty);
        }

        private DefensePriorityReport EvaluateDefensePriorityCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return DefensePriorityReport.Empty;
            }

            var threatReport = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            var valueReport = _settlementValueEvaluator.EvaluateSettlementValue(settlement);
            return EvaluateDefensePriorityCore(settlement, threatReport, valueReport);
        }

        private static DefensePriorityReport EvaluateDefensePriorityCore(
            Settlement settlement,
            SettlementThreatReport threatReport,
            SettlementValueReport valueReport)
        {
            if (settlement == null)
            {
                return DefensePriorityReport.Empty;
            }

            var siegeThreatComponent = GetScaledComponent(threatReport.SiegeThreatScore, MaxSiegeThreatComponent, ThreatScale);
            var areaPressureComponent = GetScaledComponent(threatReport.RegionalEnemyPressure, MaxAreaPressureComponent, AreaPressureScale);
            var threatComponent = siegeThreatComponent + areaPressureComponent;
            var valueComponent = GetScaledComponent(valueReport.StrategicValue, MaxValueComponent, ValueScale);
            var threatenedBonus = GetThreatenedBonus(threatReport);
            var defensePriority = Clamp(BasePriority + threatComponent + valueComponent + threatenedBonus, 0f, 100f);

            var shouldRequestDefense = ShouldRequestDefense(threatReport, defensePriority);

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
                threatReport.SiegeThreatScore,
                threatReport.ArmySiegeThreat,
                threatReport.RegionalEnemyPressure,
                threatReport.HasActiveSiege,
                threatReport.NearbyEnemyPartyCount,
                threatReport.NearbyEnemyArmyMemberPartyCount,
                threatReport.NearbyEnemyArmyLeaderPartyCount,
                threatReport.NearbyEnemyLordPartyCount,
                threatReport.StrongestEnemyPartyName,
                threatReport.StrongestEnemyStrength,
                threatReport.NearestEnemyPartyName,
                threatReport.NearestEnemyDistance,
                threatReport.IsThreatened,
                shouldRequestDefense,
                GetReason(threatReport, shouldRequestDefense));
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

        private static float GetThreatenedBonus(SettlementThreatReport threatReport)
        {
            if (threatReport.HasActiveSiege)
            {
                return ActiveSiegeBonus;
            }

            if (threatReport.NearbyEnemyArmyLeaderPartyCount > 0 && threatReport.SiegeThreatScore > 0f)
            {
                return EnemyArmyBonus;
            }

            return threatReport.IsThreatened ? ExtremePressureBonus : 0f;
        }

        private static bool ShouldRequestDefense(SettlementThreatReport threatReport, float defensePriority)
        {
            if (threatReport.HasActiveSiege)
            {
                return true;
            }

            if (threatReport.NearbyEnemyArmyLeaderPartyCount > 0 && threatReport.SiegeThreatScore > 0f)
            {
                return defensePriority >= DefenseRequestThreshold;
            }

            return false;
        }

        private static string GetReason(SettlementThreatReport threatReport, bool shouldRequestDefense)
        {
            if (threatReport.HasActiveSiege)
            {
                return "Critical: active siege";
            }

            if (shouldRequestDefense)
            {
                return "Defense recommended: active siege or army siege threat";
            }

            if (threatReport.NearbyEnemyArmyLeaderPartyCount > 0)
            {
                return threatReport.SiegeThreatScore > 0f
                    ? "Army siege threat: enemy army pressure exceeds local defense"
                    : "Army presence: enemy army nearby, local defense holds";
            }

            if (threatReport.NearbyEnemyLordPartyCount > 0)
            {
                return "Regional pressure: enemy lords nearby";
            }

            return "Safe: no siege threat";
        }
    }
}
