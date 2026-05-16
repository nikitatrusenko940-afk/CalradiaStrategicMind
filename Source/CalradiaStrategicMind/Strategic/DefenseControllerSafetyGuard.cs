using System;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseControllerSafetyGuard
    {
        public DefenseControllerSafetyReport Evaluate(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport,
            DefenseControllerDecision controllerDecision)
        {
            return SafeExecutor.Run(
                "Evaluate defense controller safety guard",
                () => EvaluateCore(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision),
                CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "Real defense controller disabled"));
        }

        private static DefenseControllerSafetyReport EvaluateCore(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport,
            DefenseControllerDecision controllerDecision)
        {
            if (!DefenseControllerSettings.EnableRealDefenseController)
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "Real defense controller disabled");
            }

            if (!controllerDecision.WouldExecute)
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "Controller execution blocked");
            }

            if (!dryRunDecision.WouldAct)
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "Dry-run does not request action");
            }

            if (!dryRunStabilityReport.HasStableWouldActSignal)
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "No stable dry-run would-act signal");
            }

            if (IsNonExecutableDryRunAction(dryRunDecision.Action))
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "Non-executable dry-run action");
            }

            if (string.IsNullOrWhiteSpace(actionPlan.PrimaryCandidateName)
                || actionPlan.PrimaryCandidateName.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, false, "No primary candidate");
            }

            return CreateReport(summary, actionPlan, dryRunDecision, dryRunStabilityReport, controllerDecision, true, "Safety guard would allow future execution");
        }

        private static bool IsNonExecutableDryRunAction(string action)
        {
            return action == "Monitor"
                || action == "Ignore"
                || action == "Wait";
        }

        private static DefenseControllerSafetyReport CreateReport(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport,
            DefenseControllerDecision controllerDecision,
            bool allowed,
            string reason)
        {
            return new DefenseControllerSafetyReport(
                summary.SettlementName,
                summary.OwnerKingdomName,
                allowed,
                DefenseControllerSettings.EnableRealDefenseController,
                dryRunDecision.WouldAct,
                dryRunStabilityReport.HasStableWouldActSignal,
                controllerDecision.Action,
                dryRunDecision.Action,
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                reason);
        }
    }
}
