using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseScoreSimulator
    {
        public DefenseScoreSimulationReport Simulate(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport,
            DefenseControllerSafetyReport safetyReport)
        {
            return SafeExecutor.Run(
                "Simulate defense score",
                () => SimulateCore(summary, actionPlan, dryRunDecision, dryRunStabilityReport, safetyReport),
                CreateReport(summary, actionPlan, false, true, "Score simulation failed"));
        }

        private static DefenseScoreSimulationReport SimulateCore(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport dryRunStabilityReport,
            DefenseControllerSafetyReport safetyReport)
        {
            if (!safetyReport.Allowed)
            {
                return CreateReport(summary, actionPlan, false, true, "Score simulation blocked by safety guard");
            }

            if (!dryRunDecision.WouldAct)
            {
                return CreateReport(summary, actionPlan, false, true, "Dry-run does not request score action");
            }

            if (!dryRunStabilityReport.HasStableWouldActSignal)
            {
                return CreateReport(summary, actionPlan, false, true, "No stable would-act signal");
            }

            return CreateReport(summary, actionPlan, false, false, "Hypothetical score calculated only; not inserted into PartyThinkParams");
        }

        private static DefenseScoreSimulationReport CreateReport(
            DefenseDiagnosticsSummary summary,
            DefenseActionPlan actionPlan,
            bool wouldAddScore,
            bool isBlockedBySafety,
            string reason)
        {
            return new DefenseScoreSimulationReport(
                summary.SettlementName,
                summary.OwnerKingdomName,
                actionPlan.PrimaryCandidateName,
                actionPlan.PrimaryCandidateCategory,
                summary.ThreatType,
                summary.RecommendedAction,
                summary.CoverageStatus,
                summary.DefensePriority,
                summary.PlanConfidence,
                CalculateHypotheticalScore(summary.DefensePriority, summary.PlanConfidence),
                wouldAddScore,
                isBlockedBySafety,
                reason);
        }

        private static float CalculateHypotheticalScore(float defensePriority, float planConfidence)
        {
            var score = defensePriority * 0.6f + planConfidence * 0.4f;
            if (score < 0f)
            {
                return 0f;
            }

            if (score > 100f)
            {
                return 100f;
            }

            return score;
        }
    }
}
