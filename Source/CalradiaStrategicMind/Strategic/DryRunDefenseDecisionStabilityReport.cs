namespace CalradiaStrategicMind.Strategic
{
    public struct DryRunDefenseDecisionStabilityReport
    {
        public DryRunDefenseDecisionStabilityReport(
            string settlementName,
            string currentAction,
            string stableAction,
            int consecutiveSameActionCount,
            int recentWouldActCount,
            int recentMonitorCount,
            int recentReinforcementRequestCount,
            int recentUrgentDefenseRequestCount,
            bool isStable,
            bool hasStableWouldActSignal,
            bool hasStableMonitorSignal,
            string reason)
        {
            SettlementName = settlementName;
            CurrentAction = currentAction;
            StableAction = stableAction;
            ConsecutiveSameActionCount = consecutiveSameActionCount;
            RecentWouldActCount = recentWouldActCount;
            RecentMonitorCount = recentMonitorCount;
            RecentReinforcementRequestCount = recentReinforcementRequestCount;
            RecentUrgentDefenseRequestCount = recentUrgentDefenseRequestCount;
            IsStable = isStable;
            HasStableWouldActSignal = hasStableWouldActSignal;
            HasStableMonitorSignal = hasStableMonitorSignal;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string CurrentAction { get; private set; }

        public string StableAction { get; private set; }

        public int ConsecutiveSameActionCount { get; private set; }

        public int RecentWouldActCount { get; private set; }

        public int RecentMonitorCount { get; private set; }

        public int RecentReinforcementRequestCount { get; private set; }

        public int RecentUrgentDefenseRequestCount { get; private set; }

        public bool IsStable { get; private set; }

        public bool HasStableWouldActSignal { get; private set; }

        public bool HasStableMonitorSignal { get; private set; }

        public string Reason { get; private set; }
    }
}
