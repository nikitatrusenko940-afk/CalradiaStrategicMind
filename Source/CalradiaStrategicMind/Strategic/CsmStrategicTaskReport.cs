namespace CalradiaStrategicMind.Strategic
{
    public class CsmStrategicTaskReport
    {
        public bool HasStrategicTask { get; set; }
        public string TaskType { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetSettlementName { get; set; }
        public string Reason { get; set; }
        public CsmArmyAssignment ArmyAssignment { get; set; }
        public CsmDefenseAssignment DefenseAssignment { get; set; }

        public static CsmStrategicTaskReport None(string reason)
        {
            return new CsmStrategicTaskReport
            {
                HasStrategicTask = false,
                TaskType = "None",
                TargetSettlementId = string.Empty,
                TargetSettlementName = "none",
                Reason = reason
            };
        }
    }
}
