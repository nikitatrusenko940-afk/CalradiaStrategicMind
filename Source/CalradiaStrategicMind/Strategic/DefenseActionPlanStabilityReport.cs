namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseActionPlanStabilityReport
    {
        public DefenseActionPlanStabilityReport(
            string settlementName,
            string currentRecommendedAction,
            string stableRecommendedAction,
            int consecutiveSameActionCount,
            int recentNeedsDefenseActionCount,
            int recentUrgentDefenseCount,
            int recentReinforceCount,
            bool isStable,
            bool shouldEscalate,
            bool shouldDeescalate,
            string reason)
        {
            SettlementName = settlementName;
            CurrentRecommendedAction = currentRecommendedAction;
            StableRecommendedAction = stableRecommendedAction;
            ConsecutiveSameActionCount = consecutiveSameActionCount;
            RecentNeedsDefenseActionCount = recentNeedsDefenseActionCount;
            RecentUrgentDefenseCount = recentUrgentDefenseCount;
            RecentReinforceCount = recentReinforceCount;
            IsStable = isStable;
            ShouldEscalate = shouldEscalate;
            ShouldDeescalate = shouldDeescalate;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string CurrentRecommendedAction { get; private set; }

        public string StableRecommendedAction { get; private set; }

        public int ConsecutiveSameActionCount { get; private set; }

        public int RecentNeedsDefenseActionCount { get; private set; }

        public int RecentUrgentDefenseCount { get; private set; }

        public int RecentReinforceCount { get; private set; }

        public bool IsStable { get; private set; }

        public bool ShouldEscalate { get; private set; }

        public bool ShouldDeescalate { get; private set; }

        public string Reason { get; private set; }
    }
}
