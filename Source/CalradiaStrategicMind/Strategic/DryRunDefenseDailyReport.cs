namespace CalradiaStrategicMind.Strategic
{
    public struct DryRunDefenseDailyReport
    {
        public DryRunDefenseDailyReport(
            int observationTick,
            int totalEvaluatedSettlements,
            int ignoreCount,
            int monitorCount,
            int waitCount,
            int requestReinforcementCount,
            int requestUrgentDefenseCount,
            int wouldActCount,
            int wouldMonitorCount,
            int stableWouldActSignalCount,
            int stableMonitorSignalCount,
            string topActionSettlementName,
            string topAction,
            float topActionConfidence,
            string reason)
        {
            ObservationTick = observationTick;
            TotalEvaluatedSettlements = totalEvaluatedSettlements;
            IgnoreCount = ignoreCount;
            MonitorCount = monitorCount;
            WaitCount = waitCount;
            RequestReinforcementCount = requestReinforcementCount;
            RequestUrgentDefenseCount = requestUrgentDefenseCount;
            WouldActCount = wouldActCount;
            WouldMonitorCount = wouldMonitorCount;
            StableWouldActSignalCount = stableWouldActSignalCount;
            StableMonitorSignalCount = stableMonitorSignalCount;
            TopActionSettlementName = topActionSettlementName;
            TopAction = topAction;
            TopActionConfidence = topActionConfidence;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public int TotalEvaluatedSettlements { get; private set; }

        public int IgnoreCount { get; private set; }

        public int MonitorCount { get; private set; }

        public int WaitCount { get; private set; }

        public int RequestReinforcementCount { get; private set; }

        public int RequestUrgentDefenseCount { get; private set; }

        public int WouldActCount { get; private set; }

        public int WouldMonitorCount { get; private set; }

        public int StableWouldActSignalCount { get; private set; }

        public int StableMonitorSignalCount { get; private set; }

        public string TopActionSettlementName { get; private set; }

        public string TopAction { get; private set; }

        public float TopActionConfidence { get; private set; }

        public string Reason { get; private set; }
    }
}
