namespace CalradiaStrategicMind.Settings
{
    public static class DefenseAssignmentSettings
    {
        public static bool EnableDefenseAssignments = true;
        public static int MaxAssignmentAgeTicks = 12;
        public static int ReassertCommandEveryTicks = 2;
        public static int MaxReassertionsPerAssignment = 4;
        public static bool EnableAssignmentLogs = true;
        public static int MaxDefenseAssignmentAgeTicks = 12;
        public static int DefenseAssignmentProgressCheckEveryTicks = 2;
        public static float DefenseAssignmentProgressMinimumDistanceDelta = 3f;
        public static float DefenseAssignmentArrivedDistance = 10f;
        public static int DefenseAssignmentGraceTicks = 4;
        public static int MinimumTicksBeforeDefenseProgressExpiry = 6;
    }
}
