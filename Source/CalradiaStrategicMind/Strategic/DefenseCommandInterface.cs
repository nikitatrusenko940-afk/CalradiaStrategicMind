using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCommandInterface
    {
        private const string RequestReinforcementCommandType = "RequestReinforcement";

        public DefenseCommandReport RequestReinforcement(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DefenseControllerSafetyReport safetyReport)
        {
            return SafeExecutor.Run(
                "Evaluate request reinforcement defense command",
                () => RequestReinforcementCore(summary, actionPlan, dryRunDecision, safetyReport),
                CreateReport(summary, actionPlan, false, false, "Command blocked by safety guard"));
        }

        private static DefenseCommandReport RequestReinforcementCore(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DefenseControllerSafetyReport safetyReport)
        {
            if (!safetyReport.Allowed)
            {
                return CreateReport(summary, actionPlan, false, false, "Command blocked by safety guard");
            }

            return CreateReport(summary, actionPlan, true, false, "Command allowed but execution not implemented");
        }

        private static DefenseCommandReport CreateReport(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            bool isAllowed,
            bool wasExecuted,
            string reason)
        {
            return new DefenseCommandReport(
                summary.SettlementName,
                summary.OwnerKingdomName,
                RequestReinforcementCommandType,
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                isAllowed,
                wasExecuted,
                reason);
        }
    }
}
