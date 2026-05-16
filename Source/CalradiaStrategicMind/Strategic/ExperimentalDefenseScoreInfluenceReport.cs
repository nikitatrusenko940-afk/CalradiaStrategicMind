namespace CalradiaStrategicMind.Strategic
{
    public struct ExperimentalDefenseScoreInfluenceReport
    {
        public ExperimentalDefenseScoreInfluenceReport(
            int observationTick,
            string partyName,
            string settlementName,
            bool isEnabled,
            bool wasAttempted,
            bool wasApplied,
            bool wouldAddBehaviorScore,
            float hypotheticalScore,
            float appliedScoreBoost,
            string reason)
        {
            ObservationTick = observationTick;
            PartyName = partyName;
            SettlementName = settlementName;
            IsEnabled = isEnabled;
            WasAttempted = wasAttempted;
            WasApplied = wasApplied;
            WouldAddBehaviorScore = wouldAddBehaviorScore;
            HypotheticalScore = hypotheticalScore;
            AppliedScoreBoost = appliedScoreBoost;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public string PartyName { get; private set; }

        public string SettlementName { get; private set; }

        public bool IsEnabled { get; private set; }

        public bool WasAttempted { get; private set; }

        public bool WasApplied { get; private set; }

        public bool WouldAddBehaviorScore { get; private set; }

        public float HypotheticalScore { get; private set; }

        public float AppliedScoreBoost { get; private set; }

        public string Reason { get; private set; }
    }
}
