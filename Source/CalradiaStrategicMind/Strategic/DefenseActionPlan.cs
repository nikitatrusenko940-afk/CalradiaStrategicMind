namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseActionPlan
    {
        public static readonly DefenseActionPlan Empty = new DefenseActionPlan(
            "unknown",
            "unknown",
            "None",
            false,
            0f,
            999f,
            0,
            0f,
            "none",
            PartyObservationCategory.Unknown,
            0f,
            0f,
            0f,
            "No defense action needed");

        public DefenseActionPlan(
            string settlementName,
            string ownerKingdomName,
            string recommendedAction,
            bool needsDefenseAction,
            float defensePriority,
            float defenseCoverageRatio,
            int selectedCandidateCount,
            float selectedCandidateStrength,
            string primaryCandidateName,
            PartyObservationCategory primaryCandidateCategory,
            float primaryCandidateStrength,
            float primaryCandidateDistance,
            float planConfidence,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            RecommendedAction = recommendedAction;
            NeedsDefenseAction = needsDefenseAction;
            DefensePriority = defensePriority;
            DefenseCoverageRatio = defenseCoverageRatio;
            SelectedCandidateCount = selectedCandidateCount;
            SelectedCandidateStrength = selectedCandidateStrength;
            PrimaryCandidateName = primaryCandidateName;
            PrimaryCandidateCategory = primaryCandidateCategory;
            PrimaryCandidateStrength = primaryCandidateStrength;
            PrimaryCandidateDistance = primaryCandidateDistance;
            PlanConfidence = planConfidence;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string RecommendedAction { get; private set; }

        public bool NeedsDefenseAction { get; private set; }

        public float DefensePriority { get; private set; }

        public float DefenseCoverageRatio { get; private set; }

        public int SelectedCandidateCount { get; private set; }

        public float SelectedCandidateStrength { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public PartyObservationCategory PrimaryCandidateCategory { get; private set; }

        public float PrimaryCandidateStrength { get; private set; }

        public float PrimaryCandidateDistance { get; private set; }

        public float PlanConfidence { get; private set; }

        public string Reason { get; private set; }
    }
}
