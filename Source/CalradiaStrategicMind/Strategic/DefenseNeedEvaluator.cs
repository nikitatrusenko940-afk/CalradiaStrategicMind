using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseNeedEvaluator
    {
        private readonly DefensePriorityEvaluator _defensePriorityEvaluator;
        private readonly DefenseCoverageEvaluator _defenseCoverageEvaluator;

        public DefenseNeedEvaluator()
        {
            _defensePriorityEvaluator = new DefensePriorityEvaluator();
            _defenseCoverageEvaluator = new DefenseCoverageEvaluator();
        }

        public DefenseNeedReport EvaluateDefenseNeed(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate defense need", () => EvaluateDefenseNeedCore(settlement), DefenseNeedReport.Empty);
        }

        public DefenseNeedReport EvaluateDefenseNeed(
            Settlement settlement,
            DefensePriorityReport priorityReport,
            DefenseCoverageReport coverageReport)
        {
            return SafeExecutor.Run(
                "Evaluate defense need from reports",
                () => EvaluateDefenseNeedCore(settlement, priorityReport, coverageReport),
                DefenseNeedReport.Empty);
        }

        private DefenseNeedReport EvaluateDefenseNeedCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return DefenseNeedReport.Empty;
            }

            var priorityReport = _defensePriorityEvaluator.EvaluateDefensePriority(settlement);
            var coverageReport = _defenseCoverageEvaluator.EvaluateDefenseCoverage(settlement);
            return EvaluateDefenseNeedCore(settlement, priorityReport, coverageReport);
        }

        private static DefenseNeedReport EvaluateDefenseNeedCore(
            Settlement settlement,
            DefensePriorityReport priorityReport,
            DefenseCoverageReport coverageReport)
        {
            if (settlement == null)
            {
                return DefenseNeedReport.Empty;
            }

            var recommendedAction = GetRecommendedAction(priorityReport, coverageReport);
            var needsDefenseAction = recommendedAction == "UrgentDefense" || recommendedAction == "Reinforce";

            return new DefenseNeedReport(
                priorityReport.SettlementName,
                priorityReport.OwnerKingdomName,
                priorityReport.SettlementType,
                priorityReport.DefensePriority,
                coverageReport.DefenseCoverageRatio,
                priorityReport.IsThreatened,
                priorityReport.HasActiveSiege,
                coverageReport.HasDirectSiegeThreat,
                coverageReport.HasArmyPresence,
                coverageReport.NeedsReinforcement,
                coverageReport.SuitableCandidateCount,
                needsDefenseAction,
                recommendedAction,
                GetReason(recommendedAction, coverageReport));
        }

        private static string GetRecommendedAction(DefensePriorityReport priorityReport, DefenseCoverageReport coverageReport)
        {
            if (priorityReport.DefensePriority < DefenseActionThresholdSettings.MinimumActionPriority)
            {
                return coverageReport.HasArmyPresence || coverageReport.HasRegionalPressure || coverageReport.HasDirectSiegeThreat
                    ? "Monitor"
                    : "None";
            }

            var isCriticalCoverage = coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold;
            var isLowCoverage = coverageReport.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold;
            var isUrgentThreat = priorityReport.HasActiveSiege
                || (coverageReport.HasDirectSiegeThreat && isCriticalCoverage)
                || (coverageReport.HasArmyPresence && priorityReport.DefensePriority >= DefenseActionThresholdSettings.MinimumUrgentPriority);

            if (isUrgentThreat && isLowCoverage && priorityReport.DefensePriority >= DefenseActionThresholdSettings.MinimumUrgentPriority)
            {
                return "UrgentDefense";
            }

            if ((coverageReport.HasDirectSiegeThreat || coverageReport.HasArmyPresence) && isLowCoverage)
            {
                return "Reinforce";
            }

            if (coverageReport.HasArmyPresence)
            {
                return "Monitor";
            }

            if (coverageReport.HasRegionalPressure)
            {
                return "Monitor";
            }

            return "None";
        }

        private static string GetReason(string recommendedAction, DefenseCoverageReport coverageReport)
        {
            if (recommendedAction == "UrgentDefense")
            {
                return coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold
                    ? "Urgent defense: critical coverage against immediate threat"
                    : "Urgent defense: high army pressure and high priority";
            }

            if (recommendedAction == "Reinforce")
            {
                return coverageReport.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold
                    ? "Request reinforcement: low coverage against relevant threat"
                    : "Request reinforcement: relevant threat needs support";
            }

            if (recommendedAction == "Monitor")
            {
                if (coverageReport.HasArmyPresence && coverageReport.IsDefenseEnough)
                {
                    return "Monitor: army presence covered by current defense";
                }

                if (coverageReport.HasDirectSiegeThreat)
                {
                    return "Monitor: threat below action priority threshold";
                }

                return coverageReport.HasArmyPresence
                    ? "Monitor: army presence without low coverage"
                    : "Monitor: regional pressure only";
            }

            return "No defense action needed";
        }
    }
}
