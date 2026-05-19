using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseDiagnosticsSummaryBuilder
    {
        public DefenseDiagnosticsSummary Build(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DefenseActionPlanStabilityReport stabilityReport)
        {
            return SafeExecutor.Run(
                "Build defense diagnostics summary",
                () => BuildCore(snapshot, actionPlan, stabilityReport),
                new DefenseDiagnosticsSummary(
                    "unknown",
                    "Unknown",
                    "unknown",
                    "None",
                    "None",
                    "None",
                    false,
                    false,
                    false,
                    0f,
                    999f,
                    "Unknown",
                    "none",
                    PartyObservationCategory.Unknown,
                    0f,
                    "No relevant threat"));
        }

        private static DefenseDiagnosticsSummary BuildCore(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DefenseActionPlanStabilityReport stabilityReport)
        {
            return new DefenseDiagnosticsSummary(
                actionPlan.SettlementName,
                snapshot.ThreatReport.SettlementType,
                actionPlan.OwnerKingdomName,
                GetThreatType(snapshot),
                actionPlan.RecommendedAction,
                stabilityReport.StableRecommendedAction,
                stabilityReport.IsStable,
                stabilityReport.ShouldEscalate,
                stabilityReport.ShouldDeescalate,
                actionPlan.DefensePriority,
                actionPlan.DefenseCoverageRatio,
                GetCoverageStatus(snapshot.CoverageReport),
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                actionPlan.PlanConfidence,
                GetReason(snapshot, actionPlan, stabilityReport));
        }

        private static string GetThreatType(DefenseEvaluationSnapshot snapshot)
        {
            if (snapshot.PriorityReport.HasActiveSiege)
            {
                return "Siege";
            }

            if (snapshot.CoverageReport.HasDirectSiegeThreat)
            {
                return "ImmediateThreat";
            }

            if (snapshot.CoverageReport.HasArmyPresence
                && snapshot.PriorityReport.DefensePriority >= DefenseActionThresholdSettings.MinimumUrgentPriority)
            {
                return "HighArmyPressure";
            }

            if (snapshot.CoverageReport.HasArmyPresence)
            {
                return "ArmyPresence";
            }

            return snapshot.CoverageReport.HasRegionalPressure
                ? "RegionalPressure"
                : "None";
        }

        private static string GetCoverageStatus(DefenseCoverageReport coverageReport)
        {
            if (coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold)
            {
                return "Critical";
            }

            if (coverageReport.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold)
            {
                return "Low";
            }

            return coverageReport.IsDefenseEnough ? "Enough" : "Unknown";
        }

        private static string GetReason(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DefenseActionPlanStabilityReport stabilityReport)
        {
            if (stabilityReport.ShouldEscalate)
            {
                return "Escalation candidate";
            }

            if (stabilityReport.ShouldDeescalate)
            {
                return "Deescalation candidate";
            }

            if (stabilityReport.IsStable)
            {
                return "Stable action detected";
            }

            if (actionPlan.NeedsDefenseAction)
            {
                return "Defense action may be needed";
            }

            if (snapshot.CoverageReport.HasArmyPresence)
            {
                return "Monitoring enemy army presence";
            }

            if (snapshot.CoverageReport.HasRegionalPressure)
            {
                return "Monitoring regional pressure";
            }

            return "No relevant threat";
        }
    }
}
