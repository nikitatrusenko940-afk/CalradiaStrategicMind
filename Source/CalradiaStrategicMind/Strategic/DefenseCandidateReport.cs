namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseCandidateReport
    {
        public DefenseCandidateReport(
            string settlementName,
            string candidatePartyName,
            string candidateLeaderName,
            PartyObservationCategory candidateCategory,
            float candidateStrength,
            float distanceToSettlement,
            int healthyTroopCount,
            int woundedTroopCount,
            float woundedRatio,
            bool isArmyLeader,
            bool isArmyMember,
            bool isWeak,
            bool isTooFar,
            bool isBusy,
            float availabilityScore,
            bool isSuitable,
            float suitabilityScore,
            string reason)
        {
            SettlementName = settlementName;
            CandidatePartyName = candidatePartyName;
            CandidateLeaderName = candidateLeaderName;
            CandidateCategory = candidateCategory;
            CandidateStrength = candidateStrength;
            DistanceToSettlement = distanceToSettlement;
            HealthyTroopCount = healthyTroopCount;
            WoundedTroopCount = woundedTroopCount;
            WoundedRatio = woundedRatio;
            IsArmyLeader = isArmyLeader;
            IsArmyMember = isArmyMember;
            IsWeak = isWeak;
            IsTooFar = isTooFar;
            IsBusy = isBusy;
            AvailabilityScore = availabilityScore;
            IsSuitable = isSuitable;
            SuitabilityScore = suitabilityScore;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string CandidatePartyName { get; private set; }

        public string CandidateLeaderName { get; private set; }

        public PartyObservationCategory CandidateCategory { get; private set; }

        public float CandidateStrength { get; private set; }

        public float DistanceToSettlement { get; private set; }

        public int HealthyTroopCount { get; private set; }

        public int WoundedTroopCount { get; private set; }

        public float WoundedRatio { get; private set; }

        public bool IsArmyLeader { get; private set; }

        public bool IsArmyMember { get; private set; }

        public bool IsWeak { get; private set; }

        public bool IsTooFar { get; private set; }

        public bool IsBusy { get; private set; }

        public float AvailabilityScore { get; private set; }

        public bool IsSuitable { get; private set; }

        public float SuitabilityScore { get; private set; }

        public string Reason { get; private set; }
    }
}
