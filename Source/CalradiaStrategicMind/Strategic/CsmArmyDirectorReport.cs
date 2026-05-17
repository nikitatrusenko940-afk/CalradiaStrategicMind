namespace CalradiaStrategicMind.Strategic
{
    public struct CsmArmyDirectorReport
    {
        public CsmArmyDirectorReport(
            int observationTick,
            string armyName,
            string kingdomName,
            string objective,
            string targetName,
            bool commandApplied,
            string status,
            string reason)
        {
            ObservationTick = observationTick;
            ArmyName = armyName;
            KingdomName = kingdomName;
            Objective = objective;
            TargetName = targetName;
            CommandApplied = commandApplied;
            Status = status;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }
        public string ArmyName { get; private set; }
        public string KingdomName { get; private set; }
        public string Objective { get; private set; }
        public string TargetName { get; private set; }
        public bool CommandApplied { get; private set; }
        public string Status { get; private set; }
        public string Reason { get; private set; }
    }
}
