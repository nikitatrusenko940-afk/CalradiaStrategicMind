namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseActionPlanHistoryEntry
    {
        public DefenseActionPlanHistoryEntry(
            string settlementName,
            string ownerKingdomName,
            string recommendedAction,
            bool needsDefenseAction,
            float defensePriority,
            float defenseCoverageRatio,
            int selectedCandidateCount,
            float selectedCandidateStrength,
            string primaryCandidateName,
            float planConfidence,
            int observationTick)
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
            PlanConfidence = planConfidence;
            ObservationTick = observationTick;
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

        public float PlanConfidence { get; private set; }

        public int ObservationTick { get; private set; }
    }
}
