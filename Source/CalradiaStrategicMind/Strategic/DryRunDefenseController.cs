using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DryRunDefenseController
    {
        public DryRunDefenseDecision EvaluateDryRun(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DefenseActionPlanStabilityReport stabilityReport)
        {
            return SafeExecutor.Run(
                "Evaluate dry-run defense decision",
                () => EvaluateDryRunCore(summary, actionPlan, stabilityReport),
                CreateDecision(summary, false, false, false, false, "Ignore", "None", actionPlan, "Dry-run evaluation failed"));
        }

        private static DryRunDefenseDecision EvaluateDryRunCore(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DefenseActionPlanStabilityReport stabilityReport)
        {
            if (summary.ThreatType == "None")
            {
                return CreateDecision(summary, false, false, false, false, "Ignore", stabilityReport.StableRecommendedAction, actionPlan, "No relevant threat");
            }

            if (summary.RecommendedAction == "Monitor")
            {
                return CreateDecision(summary, false, true, false, false, "Monitor", stabilityReport.StableRecommendedAction, actionPlan, "Dry-run monitor only");
            }

            if (!stabilityReport.ShouldEscalate)
            {
                return CreateDecision(summary, false, false, false, false, "Wait", stabilityReport.StableRecommendedAction, actionPlan, "Waiting for stable escalation signal");
            }

            if (stabilityReport.CurrentRecommendedAction == "UrgentDefense"
                || stabilityReport.StableRecommendedAction == "UrgentDefense")
            {
                return CreateDecision(summary, true, false, false, true, "RequestUrgentDefense", stabilityReport.StableRecommendedAction, actionPlan, "Dry-run would request urgent defense");
            }

            if (stabilityReport.StableRecommendedAction == "Reinforce")
            {
                return CreateDecision(summary, true, false, true, false, "RequestReinforcement", stabilityReport.StableRecommendedAction, actionPlan, "Dry-run would request reinforcement");
            }

            return CreateDecision(summary, false, false, false, false, "Wait", stabilityReport.StableRecommendedAction, actionPlan, "Waiting for stable escalation signal");
        }

        private static DryRunDefenseDecision CreateDecision(
            DefenseDiagnosticsSummary summary,
            bool wouldAct,
            bool wouldMonitor,
            bool wouldRequestReinforcement,
            bool wouldRequestUrgentDefense,
            string action,
            string stableAction,
            DefenseActionPlan actionPlan,
            string reason)
        {
            return new DryRunDefenseDecision(
                summary.SettlementName,
                summary.OwnerKingdomName,
                wouldAct,
                wouldMonitor,
                wouldRequestReinforcement,
                wouldRequestUrgentDefense,
                action,
                stableAction,
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                actionPlan.PlanConfidence,
                reason);
        }
    }
}
