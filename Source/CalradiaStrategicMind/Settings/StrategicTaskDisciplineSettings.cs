namespace CalradiaStrategicMind.Settings
{
    public static class StrategicTaskDisciplineSettings
    {
        public static bool EnableStrategicTaskDiscipline = true;
        public static float LowValuePartyThreatRatioToAllowEngage = 0.75f;
        public static float LowValuePartyNearAssignedTargetDistance = 8f;
        public static int DisciplineReassertCooldownTicks = 2;
        public static int MaxDisciplineReassertsPerAssignment = 4;
        public static bool AllowEngageIfAlreadyInDefensiveBattle = true;
        public static bool AllowEngageIfLowValueThreatensAssignedSettlement = true;
    }
}
