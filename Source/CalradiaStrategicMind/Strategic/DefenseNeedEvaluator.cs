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
            if (priorityReport.HasActiveSiege && coverageReport.NeedsReinforcement)
            {
                return "UrgentDefense";
            }

            if (coverageReport.HasDirectSiegeThreat && coverageReport.NeedsReinforcement)
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
                return "Active siege with low defense coverage";
            }

            if (recommendedAction == "Reinforce")
            {
                return "Direct siege threat with low defense coverage";
            }

            if (recommendedAction == "Monitor")
            {
                return coverageReport.HasArmyPresence
                    ? "Army presence covered by current defense"
                    : "Regional pressure only";
            }

            return "No defense action needed";
        }
    }
}
