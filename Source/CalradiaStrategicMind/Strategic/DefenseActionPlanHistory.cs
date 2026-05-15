using System.Collections.Generic;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseActionPlanHistory
    {
        private const int MaxEntriesPerSettlement = 5;
        private const int StableActionThreshold = 2;

        private readonly Dictionary<string, List<DefenseActionPlanHistoryEntry>> _entriesBySettlement;

        public DefenseActionPlanHistory()
        {
            _entriesBySettlement = new Dictionary<string, List<DefenseActionPlanHistoryEntry>>();
        }

        public void Record(DefenseActionPlan plan, int observationTick)
        {
            SafeExecutor.Run("Record defense action plan history", () => RecordCore(plan, observationTick));
        }

        public DefenseActionPlanStabilityReport EvaluateStability(DefenseActionPlan plan, int observationTick)
        {
            return SafeExecutor.Run(
                "Evaluate defense action plan stability",
                () => EvaluateStabilityCore(plan, observationTick),
                new DefenseActionPlanStabilityReport(
                    "unknown",
                    "None",
                    "None",
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "No history"));
        }

        private void RecordCore(DefenseActionPlan plan, int observationTick)
        {
            var key = GetSettlementKey(plan);
            if (!_entriesBySettlement.ContainsKey(key))
            {
                _entriesBySettlement[key] = new List<DefenseActionPlanHistoryEntry>();
            }

            var entries = _entriesBySettlement[key];
            entries.Add(new DefenseActionPlanHistoryEntry(
                plan.SettlementName,
                plan.OwnerKingdomName,
                plan.RecommendedAction,
                plan.NeedsDefenseAction,
                plan.DefensePriority,
                plan.DefenseCoverageRatio,
                plan.SelectedCandidateCount,
                plan.SelectedCandidateStrength,
                plan.PrimaryCandidateName,
                plan.PlanConfidence,
                observationTick));

            while (entries.Count > MaxEntriesPerSettlement)
            {
                entries.RemoveAt(0);
            }
        }

        private DefenseActionPlanStabilityReport EvaluateStabilityCore(DefenseActionPlan plan, int observationTick)
        {
            var key = GetSettlementKey(plan);
            List<DefenseActionPlanHistoryEntry> entries;
            if (!_entriesBySettlement.TryGetValue(key, out entries) || entries.Count == 0)
            {
                return new DefenseActionPlanStabilityReport(
                    plan.SettlementName,
                    plan.RecommendedAction,
                    "None",
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "No recent history");
            }

            var consecutiveSameActionCount = GetConsecutiveSameActionCount(entries, plan.RecommendedAction);
            var recentNeedsDefenseActionCount = 0;
            var recentUrgentDefenseCount = 0;
            var recentReinforceCount = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.NeedsDefenseAction)
                {
                    recentNeedsDefenseActionCount++;
                }

                if (entry.RecommendedAction == "UrgentDefense")
                {
                    recentUrgentDefenseCount++;
                }
                else if (entry.RecommendedAction == "Reinforce")
                {
                    recentReinforceCount++;
                }
            }

            var isStable = consecutiveSameActionCount >= StableActionThreshold;
            var shouldEscalate = recentUrgentDefenseCount > 0
                || (plan.RecommendedAction == "Reinforce" && consecutiveSameActionCount >= StableActionThreshold);
            var shouldDeescalate = ShouldDeescalate(entries, plan.RecommendedAction, consecutiveSameActionCount);
            var stableRecommendedAction = isStable ? plan.RecommendedAction : "None";

            return new DefenseActionPlanStabilityReport(
                plan.SettlementName,
                plan.RecommendedAction,
                stableRecommendedAction,
                consecutiveSameActionCount,
                recentNeedsDefenseActionCount,
                recentUrgentDefenseCount,
                recentReinforceCount,
                isStable,
                shouldEscalate,
                shouldDeescalate,
                GetReason(isStable, shouldEscalate, shouldDeescalate, plan.RecommendedAction));
        }

        private static int GetConsecutiveSameActionCount(
            List<DefenseActionPlanHistoryEntry> entries,
            string recommendedAction)
        {
            var count = 0;
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].RecommendedAction != recommendedAction)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static bool ShouldDeescalate(
            List<DefenseActionPlanHistoryEntry> entries,
            string currentAction,
            int consecutiveSameActionCount)
        {
            if ((currentAction != "Monitor" && currentAction != "None")
                || consecutiveSameActionCount < StableActionThreshold)
            {
                return false;
            }

            for (var index = 0; index < entries.Count - consecutiveSameActionCount; index++)
            {
                var action = entries[index].RecommendedAction;
                if (action == "Reinforce" || action == "UrgentDefense")
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetReason(
            bool isStable,
            bool shouldEscalate,
            bool shouldDeescalate,
            string currentAction)
        {
            if (shouldEscalate)
            {
                return currentAction == "UrgentDefense"
                    ? "Urgent defense observed in recent history"
                    : "Reinforce action persisted across observations";
            }

            if (shouldDeescalate)
            {
                return "Recent action deescalated to monitor or none";
            }

            if (isStable)
            {
                return "Recommended action is stable";
            }

            return "Recommended action is not stable yet";
        }

        private static string GetSettlementKey(DefenseActionPlan plan)
        {
            return string.IsNullOrEmpty(plan.SettlementName)
                ? "unknown"
                : plan.SettlementName;
        }
    }
}
