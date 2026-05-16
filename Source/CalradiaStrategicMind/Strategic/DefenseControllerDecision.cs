namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseControllerDecision
    {
        public DefenseControllerDecision(
            string settlementName,
            string ownerKingdomName,
            bool isEnabled,
            bool wouldExecute,
            string action,
            string dryRunAction,
            string primaryCandidateName,
            PartyObservationCategory primaryCandidateCategory,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            IsEnabled = isEnabled;
            WouldExecute = wouldExecute;
            Action = action;
            DryRunAction = dryRunAction;
            PrimaryCandidateName = primaryCandidateName;
            PrimaryCandidateCategory = primaryCandidateCategory;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public bool IsEnabled { get; private set; }

        public bool WouldExecute { get; private set; }

        public string Action { get; private set; }

        public string DryRunAction { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public PartyObservationCategory PrimaryCandidateCategory { get; private set; }

        public string Reason { get; private set; }
    }
}
