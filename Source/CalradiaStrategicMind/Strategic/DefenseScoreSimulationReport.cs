namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseScoreSimulationReport
    {
        public DefenseScoreSimulationReport(
            string settlementName,
            string ownerKingdomName,
            string candidateName,
            PartyObservationCategory candidateCategory,
            string threatType,
            string recommendedAction,
            string coverageStatus,
            float defensePriority,
            float planConfidence,
            float hypotheticalScore,
            bool wouldAddScore,
            bool isBlockedBySafety,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            CandidateName = candidateName;
            CandidateCategory = candidateCategory;
            ThreatType = threatType;
            RecommendedAction = recommendedAction;
            CoverageStatus = coverageStatus;
            DefensePriority = defensePriority;
            PlanConfidence = planConfidence;
            HypotheticalScore = hypotheticalScore;
            WouldAddScore = wouldAddScore;
            IsBlockedBySafety = isBlockedBySafety;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string CandidateName { get; private set; }

        public PartyObservationCategory CandidateCategory { get; private set; }

        public string ThreatType { get; private set; }

        public string RecommendedAction { get; private set; }

        public string CoverageStatus { get; private set; }

        public float DefensePriority { get; private set; }

        public float PlanConfidence { get; private set; }

        public float HypotheticalScore { get; private set; }

        public bool WouldAddScore { get; private set; }

        public bool IsBlockedBySafety { get; private set; }

        public string Reason { get; private set; }
    }
}
