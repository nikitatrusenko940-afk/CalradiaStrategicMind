namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseScoreSimulationDailySummary
    {
        public DefenseScoreSimulationDailySummary(
            int observationTick,
            int totalScoreSimulations,
            int blockedBySafetyCount,
            int wouldAddScoreCount,
            float maxHypotheticalScore,
            float averageHypotheticalScore,
            string topScoreSettlementName,
            string topScoreCandidateName,
            PartyObservationCategory topScoreCandidateCategory,
            string topScoreRecommendedAction,
            string topScoreReason,
            string reason)
        {
            ObservationTick = observationTick;
            TotalScoreSimulations = totalScoreSimulations;
            BlockedBySafetyCount = blockedBySafetyCount;
            WouldAddScoreCount = wouldAddScoreCount;
            MaxHypotheticalScore = maxHypotheticalScore;
            AverageHypotheticalScore = averageHypotheticalScore;
            TopScoreSettlementName = topScoreSettlementName;
            TopScoreCandidateName = topScoreCandidateName;
            TopScoreCandidateCategory = topScoreCandidateCategory;
            TopScoreRecommendedAction = topScoreRecommendedAction;
            TopScoreReason = topScoreReason;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public int TotalScoreSimulations { get; private set; }

        public int BlockedBySafetyCount { get; private set; }

        public int WouldAddScoreCount { get; private set; }

        public float MaxHypotheticalScore { get; private set; }

        public float AverageHypotheticalScore { get; private set; }

        public string TopScoreSettlementName { get; private set; }

        public string TopScoreCandidateName { get; private set; }

        public PartyObservationCategory TopScoreCandidateCategory { get; private set; }

        public string TopScoreRecommendedAction { get; private set; }

        public string TopScoreReason { get; private set; }

        public string Reason { get; private set; }
    }
}
