namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseControllerSafetyReport
    {
        public DefenseControllerSafetyReport(
            string settlementName,
            string ownerKingdomName,
            bool allowed,
            bool realControllerEnabled,
            bool dryRunWouldAct,
            bool hasStableWouldActSignal,
            string action,
            string dryRunAction,
            string primaryCandidateName,
            PartyObservationCategory primaryCandidateCategory,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            Allowed = allowed;
            RealControllerEnabled = realControllerEnabled;
            DryRunWouldAct = dryRunWouldAct;
            HasStableWouldActSignal = hasStableWouldActSignal;
            Action = action;
            DryRunAction = dryRunAction;
            PrimaryCandidateName = primaryCandidateName;
            PrimaryCandidateCategory = primaryCandidateCategory;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public bool Allowed { get; private set; }

        public bool RealControllerEnabled { get; private set; }

        public bool DryRunWouldAct { get; private set; }

        public bool HasStableWouldActSignal { get; private set; }

        public string Action { get; private set; }

        public string DryRunAction { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public PartyObservationCategory PrimaryCandidateCategory { get; private set; }

        public string Reason { get; private set; }
    }
}
