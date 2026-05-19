namespace CalradiaStrategicMind.Strategic
{
    public class CsmReleasedArmyState
    {
        public string ArmyId { get; set; }
        public string ArmyName { get; set; }
        public string LeaderPartyId { get; set; }
        public string LeaderPartyName { get; set; }
        public string KingdomName { get; set; }
        public int ReleasedTick { get; set; }
        public int LastLogTick { get; set; }
        public string ReleaseReason { get; set; }
        public string LastTargetName { get; set; }
        public string ReleaseType { get; set; }
    }
}
