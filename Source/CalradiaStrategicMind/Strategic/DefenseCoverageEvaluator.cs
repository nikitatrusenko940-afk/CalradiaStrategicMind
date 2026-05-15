using System.Collections.Generic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCoverageEvaluator
    {
        private const float EnoughCoverageRatio = 1.1f;
        private const float ReinforcementCoverageRatio = 0.75f;
        private const float RegionalPressureWeight = 0.25f;

        private readonly SettlementThreatEvaluator _settlementThreatEvaluator;
        private readonly DefenseCandidateSelector _defenseCandidateSelector;

        public DefenseCoverageEvaluator()
        {
            _settlementThreatEvaluator = new SettlementThreatEvaluator();
            _defenseCandidateSelector = new DefenseCandidateSelector();
        }

        public DefenseCoverageReport EvaluateDefenseCoverage(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate defense coverage", () => EvaluateDefenseCoverageCore(settlement), DefenseCoverageReport.Empty);
        }

        public DefenseCoverageReport EvaluateDefenseCoverage(
            Settlement settlement,
            SettlementThreatReport threatReport,
            List<DefenseCandidateReport> candidates)
        {
            return SafeExecutor.Run(
                "Evaluate defense coverage from reports",
                () => EvaluateDefenseCoverageCore(settlement, threatReport, candidates),
                DefenseCoverageReport.Empty);
        }

        private DefenseCoverageReport EvaluateDefenseCoverageCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return DefenseCoverageReport.Empty;
            }

            var threatReport = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            var candidates = _defenseCandidateSelector.FindDefenseCandidates(settlement, 0);
            return EvaluateDefenseCoverageCore(settlement, threatReport, candidates);
        }

        private static DefenseCoverageReport EvaluateDefenseCoverageCore(
            Settlement settlement,
            SettlementThreatReport threatReport,
            List<DefenseCandidateReport> candidates)
        {
            if (settlement == null)
            {
                return DefenseCoverageReport.Empty;
            }

            var suitableCandidateCount = 0;
            var suitableCandidateStrength = 0f;
            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (!candidate.IsSuitable)
                    {
                        continue;
                    }

                    suitableCandidateCount++;
                    suitableCandidateStrength += candidate.CandidateStrength;
                }
            }

            var totalAvailableDefenseStrength = threatReport.GarrisonStrength
                + threatReport.NearbyFriendlyStrength
                + suitableCandidateStrength;
            var relevantThreat = GetRelevantThreat(threatReport);
            var defenseCoverageRatio = relevantThreat > 0f
                ? totalAvailableDefenseStrength / relevantThreat
                : 999f;
            var hasDirectSiegeThreat = threatReport.HasActiveSiege || threatReport.SiegeThreatScore > 0f;
            var hasArmyPresence = threatReport.ArmySiegeThreat > 0f;
            var hasRegionalPressure = threatReport.RegionalEnemyPressure > 0f;
            var isDefenseEnough = !hasDirectSiegeThreat || defenseCoverageRatio >= EnoughCoverageRatio;
            var needsReinforcement = hasDirectSiegeThreat && defenseCoverageRatio < ReinforcementCoverageRatio;

            return new DefenseCoverageReport(
                threatReport.SettlementName,
                threatReport.OwnerKingdomName,
                threatReport.SettlementType,
                threatReport.GarrisonStrength,
                threatReport.NearbyFriendlyStrength,
                suitableCandidateCount,
                suitableCandidateStrength,
                totalAvailableDefenseStrength,
                threatReport.ThreatScore,
                threatReport.SiegeThreatScore,
                threatReport.ArmySiegeThreat,
                threatReport.RegionalEnemyPressure,
                defenseCoverageRatio,
                hasDirectSiegeThreat,
                hasArmyPresence,
                hasRegionalPressure,
                isDefenseEnough,
                needsReinforcement,
                GetReason(threatReport, hasDirectSiegeThreat, hasArmyPresence, hasRegionalPressure, defenseCoverageRatio, suitableCandidateCount));
        }

        private static string GetReason(
            SettlementThreatReport threatReport,
            bool hasDirectSiegeThreat,
            bool hasArmyPresence,
            bool hasRegionalPressure,
            float defenseCoverageRatio,
            int suitableCandidateCount)
        {
            if (!hasDirectSiegeThreat)
            {
                if (hasArmyPresence)
                {
                    return "Army presence covered by local defense";
                }

                if (hasRegionalPressure)
                {
                    return "Regional pressure covered by available defense";
                }

                return suitableCandidateCount > 0
                    ? "No direct siege threat; candidates available"
                    : "No direct siege threat";
            }

            if (defenseCoverageRatio < ReinforcementCoverageRatio)
            {
                return threatReport.HasActiveSiege
                    ? "Coverage low against active siege"
                    : "Coverage low against army siege threat";
            }

            return threatReport.HasActiveSiege
                ? "Coverage sufficient against active siege"
                : "Coverage sufficient against army siege threat";
        }

        private static float GetRelevantThreat(SettlementThreatReport threatReport)
        {
            var directThreat = threatReport.SiegeThreatScore > 0f
                ? threatReport.SiegeThreatScore
                : threatReport.ArmySiegeThreat;
            return directThreat + threatReport.RegionalEnemyPressure * RegionalPressureWeight;
        }
    }
}
