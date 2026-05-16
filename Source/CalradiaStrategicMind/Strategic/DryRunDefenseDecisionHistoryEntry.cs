namespace CalradiaStrategicMind.Strategic
{
    public struct DryRunDefenseDecisionHistoryEntry
    {
        public DryRunDefenseDecisionHistoryEntry(
            string settlementName,
            string ownerKingdomName,
            string action,
            string stableAction,
            bool wouldAct,
            bool wouldMonitor,
            bool wouldRequestReinforcement,
            bool wouldRequestUrgentDefense,
            string primaryCandidateName,
            float planConfidence,
            int observationTick)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            Action = action;
            StableAction = stableAction;
            WouldAct = wouldAct;
            WouldMonitor = wouldMonitor;
            WouldRequestReinforcement = wouldRequestReinforcement;
            WouldRequestUrgentDefense = wouldRequestUrgentDefense;
            PrimaryCandidateName = primaryCandidateName;
            PlanConfidence = planConfidence;
            ObservationTick = observationTick;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string Action { get; private set; }

        public string StableAction { get; private set; }

        public bool WouldAct { get; private set; }

        public bool WouldMonitor { get; private set; }

        public bool WouldRequestReinforcement { get; private set; }

        public bool WouldRequestUrgentDefense { get; private set; }

        public string PrimaryCandidateName { get; private set; }

        public float PlanConfidence { get; private set; }

        public int ObservationTick { get; private set; }
    }
}
