namespace CalradiaStrategicMind.Strategic
{
    public class CsmTaskDistractionReport
    {
        public bool IsDistraction { get; set; }
        public bool ShouldReassertTask { get; set; }
        public bool AllowedEngage { get; set; }
        public bool HasTargetParty { get; set; }
        public bool TargetPartyReadable { get; set; }
        public bool TargetPartyLowValue { get; set; }
        public bool AssignedTargetMissing { get; set; }
        public bool StillOnAssignedTask { get; set; }
        public bool ArmyMemberFollowingLeader { get; set; }
        public string AllowedEngageReasonCode { get; set; }
        public string TaskType { get; set; }
        public string AssignedTargetName { get; set; }
        public string DistractionPartyName { get; set; }
        public string DistractionCategory { get; set; }
        public string Reason { get; set; }
    }
}
