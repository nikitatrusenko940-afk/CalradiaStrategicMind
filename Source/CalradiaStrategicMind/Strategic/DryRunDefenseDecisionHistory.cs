using System.Collections.Generic;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DryRunDefenseDecisionHistory
    {
        private const int MaxEntriesPerSettlement = 5;
        private const int StableActionThreshold = 2;

        private readonly Dictionary<string, List<DryRunDefenseDecisionHistoryEntry>> _entriesBySettlement;

        public DryRunDefenseDecisionHistory()
        {
            _entriesBySettlement = new Dictionary<string, List<DryRunDefenseDecisionHistoryEntry>>();
        }

        public void Record(DryRunDefenseDecision decision, int observationTick)
        {
            SafeExecutor.Run("Record dry-run defense decision history", () => RecordCore(decision, observationTick));
        }

        public DryRunDefenseDecisionStabilityReport EvaluateStability(
            DryRunDefenseDecision decision,
            int observationTick)
        {
            return SafeExecutor.Run(
                "Evaluate dry-run defense decision stability",
                () => EvaluateStabilityCore(decision, observationTick),
                new DryRunDefenseDecisionStabilityReport(
                    "unknown",
                    "Ignore",
                    "None",
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "No dry-run history"));
        }

        private void RecordCore(DryRunDefenseDecision decision, int observationTick)
        {
            var key = GetSettlementKey(decision);
            if (!_entriesBySettlement.ContainsKey(key))
            {
                _entriesBySettlement[key] = new List<DryRunDefenseDecisionHistoryEntry>();
            }

            var entries = _entriesBySettlement[key];
            entries.Add(new DryRunDefenseDecisionHistoryEntry(
                decision.SettlementName,
                decision.OwnerKingdomName,
                decision.Action,
                decision.StableAction,
                decision.WouldAct,
                decision.WouldMonitor,
                decision.WouldRequestReinforcement,
                decision.WouldRequestUrgentDefense,
                decision.PrimaryCandidateName,
                decision.PlanConfidence,
                observationTick));

            while (entries.Count > MaxEntriesPerSettlement)
            {
                entries.RemoveAt(0);
            }
        }

        private DryRunDefenseDecisionStabilityReport EvaluateStabilityCore(
            DryRunDefenseDecision decision,
            int observationTick)
        {
            var key = GetSettlementKey(decision);
            List<DryRunDefenseDecisionHistoryEntry> entries;
            if (!_entriesBySettlement.TryGetValue(key, out entries) || entries.Count == 0)
            {
                return new DryRunDefenseDecisionStabilityReport(
                    decision.SettlementName,
                    decision.Action,
                    "None",
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "No recent dry-run history");
            }

            var consecutiveSameActionCount = GetConsecutiveSameActionCount(entries, decision.Action);
            var recentWouldActCount = 0;
            var recentMonitorCount = 0;
            var recentReinforcementRequestCount = 0;
            var recentUrgentDefenseRequestCount = 0;

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.WouldAct)
                {
                    recentWouldActCount++;
                }

                if (entry.Action == "Monitor")
                {
                    recentMonitorCount++;
                }

                if (entry.WouldRequestReinforcement)
                {
                    recentReinforcementRequestCount++;
                }

                if (entry.WouldRequestUrgentDefense)
                {
                    recentUrgentDefenseRequestCount++;
                }
            }

            var isStable = consecutiveSameActionCount >= StableActionThreshold;
            var hasStableMonitorSignal = decision.Action == "Monitor"
                && consecutiveSameActionCount >= StableActionThreshold;
            var hasStableWouldActSignal = recentUrgentDefenseRequestCount > 0
                || recentWouldActCount >= StableActionThreshold;
            var stableAction = isStable ? decision.Action : "None";

            return new DryRunDefenseDecisionStabilityReport(
                decision.SettlementName,
                decision.Action,
                stableAction,
                consecutiveSameActionCount,
                recentWouldActCount,
                recentMonitorCount,
                recentReinforcementRequestCount,
                recentUrgentDefenseRequestCount,
                isStable,
                hasStableWouldActSignal,
                hasStableMonitorSignal,
                GetReason(isStable, hasStableWouldActSignal, hasStableMonitorSignal, decision.Action));
        }

        private static int GetConsecutiveSameActionCount(
            List<DryRunDefenseDecisionHistoryEntry> entries,
            string action)
        {
            var count = 0;
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].Action != action)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static string GetReason(
            bool isStable,
            bool hasStableWouldActSignal,
            bool hasStableMonitorSignal,
            string currentAction)
        {
            if (hasStableWouldActSignal)
            {
                return currentAction == "RequestUrgentDefense"
                    ? "Urgent dry-run request observed"
                    : "Dry-run action signal is stable";
            }

            if (hasStableMonitorSignal)
            {
                return "Dry-run monitor signal is stable";
            }

            if (isStable)
            {
                return "Dry-run decision is stable";
            }

            return "Dry-run decision is not stable yet";
        }

        private static string GetSettlementKey(DryRunDefenseDecision decision)
        {
            return string.IsNullOrEmpty(decision.SettlementName)
                ? "unknown"
                : decision.SettlementName;
        }
    }
}
