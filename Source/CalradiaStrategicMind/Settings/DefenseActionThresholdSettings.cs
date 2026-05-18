namespace CalradiaStrategicMind.Settings
{
    public static class DefenseActionThresholdSettings
    {
        public static float ReinforcementCoverageRatioThreshold = 1.25f;
        public static float UrgentDefenseCoverageRatioThreshold = 0.75f;
        public static float MinimumActionPriority = 60f;
        public static float MinimumUrgentPriority = 80f;
        public static float MinimumActionPlanConfidence = 55f;
        public static bool EnableRealDefenseController = true;
        public static bool RequireStableDefenseSignal = true;
        public static int RequiredStableDefenseTicks = 2;
        public static float MinimumDefensePriorityToAct = 70f;
        public static float MinimumDefenseConfidenceToAct = 70f;
        public static float CriticalCoverageRatio = 0.85f;
        public static float LowCoverageRatio = 1.10f;
        public static bool AllowUrgentDefenseCommands = true;
        public static bool AllowReinforcementDefenseCommands = false;
        public static bool RequireTargetSettlementUnderSiegeForUrgentDefense = true;
        public static bool AllowArmyPresenceDefenseWithoutSiege = false;
    }
}
