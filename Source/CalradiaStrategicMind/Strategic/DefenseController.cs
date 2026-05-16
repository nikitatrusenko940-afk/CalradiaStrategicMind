using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseController
    {
        public DefenseControllerDecision Evaluate(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport)
        {
            return SafeExecutor.Run(
                "Evaluate defense controller scaffold",
                () => EvaluateCore(summary, actionPlan, dryRunDecision, dryRunStabilityReport),
                CreateDecision(summary, actionPlan, dryRunDecision, false, false, "Disabled", "Real defense controller disabled"));
        }

        private static DefenseControllerDecision EvaluateCore(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport)
        {
            if (!DefenseControllerSettings.EnableRealDefenseController)
            {
                return CreateDecision(summary, actionPlan, dryRunDecision, false, false, "Disabled", "Real defense controller disabled");
            }

            return CreateDecision(summary, actionPlan, dryRunDecision, true, false, "SafetyBlocked", "Real defense controller scaffold only");
        }

        private static DefenseControllerDecision CreateDecision(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            bool isEnabled,
            bool wouldExecute,
            string action,
            string reason)
        {
            return new DefenseControllerDecision(
                summary.SettlementName,
                summary.OwnerKingdomName,
                isEnabled,
                wouldExecute,
                action,
                dryRunDecision.Action,
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                reason);
        }
    }
}
