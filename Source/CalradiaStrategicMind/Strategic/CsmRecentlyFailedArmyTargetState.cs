namespace CalradiaStrategicMind.Strategic
{
    public class CsmRecentlyFailedArmyTargetState
    {
        public string ArmyId { get; set; }
        public string ArmyName { get; set; }
        public string KingdomName { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetSettlementName { get; set; }
        public int FailedTick { get; set; }
        public string FailureReason { get; set; }
        public string FailureType { get; set; }
    }
}
