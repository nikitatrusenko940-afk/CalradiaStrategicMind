namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseCommandReport
    {
        public DefenseCommandReport(
            string settlementName,
            string ownerKingdomName,
            string commandType,
            string candidateName,
            PartyObservationCategory candidateCategory,
            bool isAllowed,
            bool wasExecuted,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            CommandType = commandType;
            CandidateName = candidateName;
            CandidateCategory = candidateCategory;
            IsAllowed = isAllowed;
            WasExecuted = wasExecuted;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string CommandType { get; private set; }

        public string CandidateName { get; private set; }

        public PartyObservationCategory CandidateCategory { get; private set; }

        public bool IsAllowed { get; private set; }

        public bool WasExecuted { get; private set; }

        public string Reason { get; private set; }
    }
}
