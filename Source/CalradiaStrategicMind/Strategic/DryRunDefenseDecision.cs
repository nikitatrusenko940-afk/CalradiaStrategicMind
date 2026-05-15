namespace CalradiaStrategicMind.Strategic
{
    public struct DryRunDefenseDecision
    {
        public DryRunDefenseDecision(
            string settlementName,
            string ownerKingdomName,
            bool wouldAct,
            bool wouldMonitor,
            bool wouldRequestReinforcement,
            bool wouldRequestUrgentDefense,
            string action,
            string stableAction,
            string primaryCandidateName,
            PartyObservationCategory primaryCandidateCategory,
            float planConfidence,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            WouldAct = wouldAct;
            WouldMonitor = wouldMonitor;
            WouldRequestReinforcement = wouldRequestReinforcement;
            WouldRequestUrgentDefense = wouldRequestUrgentDefense;
            Action = action;
            StableAction = stableAction;
            PrimaryCandidateName = primaryCandidateName;
            PrimaryCandidateCategory = primaryCandidateCategory;
            PlanConfidence = planConfidence;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public bool WouldAct { get; private set; }

        public bool WouldMonitor { get; private set; }

        public bool WouldRequestReinforcement { get; private set; }

        public bool WouldRequestUrgentDefense { get; private set; }

        public string Action { get; private set; }

        public string StableAction { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public PartyObservationCategory PrimaryCandidateCategory { get; private set; }

        public float PlanConfidence { get; private set; }

        public string Reason { get; private set; }
    }
}
