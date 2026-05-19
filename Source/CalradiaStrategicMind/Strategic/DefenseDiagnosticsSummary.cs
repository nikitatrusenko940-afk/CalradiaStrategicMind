namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseDiagnosticsSummary
    {
        public DefenseDiagnosticsSummary(
            string settlementName,
            string settlementType,
            string ownerKingdomName,
            string threatType,
            string recommendedAction,
            string stableRecommendedAction,
            bool isStable,
            bool shouldEscalate,
            bool shouldDeescalate,
            float defensePriority,
            float defenseCoverageRatio,
            string coverageStatus,
            string primaryCandidateName,
            PartyObservationCategory primaryCandidateCategory,
            float planConfidence,
            string reason)
        {
            SettlementName = settlementName;
            SettlementType = settlementType;
            OwnerKingdomName = ownerKingdomName;
            ThreatType = threatType;
            RecommendedAction = recommendedAction;
            StableRecommendedAction = stableRecommendedAction;
            IsStable = isStable;
            ShouldEscalate = shouldEscalate;
            ShouldDeescalate = shouldDeescalate;
            DefensePriority = defensePriority;
            DefenseCoverageRatio = defenseCoverageRatio;
            CoverageStatus = coverageStatus;
            PrimaryCandidateName = primaryCandidateName;
            PrimaryCandidateCategory = primaryCandidateCategory;
            PlanConfidence = planConfidence;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string SettlementType { get; private set; }

        public string OwnerKingdomName { get; private set; }


        public string ThreatType { get; private set; }

        public string RecommendedAction { get; private set; }

        public string StableRecommendedAction { get; private set; }

        public bool IsStable { get; private set; }

        public bool ShouldEscalate { get; private set; }

        public bool ShouldDeescalate { get; private set; }

        public float DefensePriority { get; private set; }

        public float DefenseCoverageRatio { get; private set; }

        public string CoverageStatus { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public PartyObservationCategory PrimaryCandidateCategory { get; private set; }

        public float PlanConfidence { get; private set; }

        public string Reason { get; private set; }
    }
}
