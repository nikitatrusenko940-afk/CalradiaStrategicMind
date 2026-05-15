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
            bool isArmyLeader,
            bool isArmyMember,
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
            IsArmyLeader = isArmyLeader;
            IsArmyMember = isArmyMember;
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

        public bool IsArmyLeader { get; private set; }

        public bool IsArmyMember { get; private set; }

        public bool IsSuitable { get; private set; }

        public float SuitabilityScore { get; private set; }

        public string Reason { get; private set; }
    }
}
