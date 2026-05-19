namespace CalradiaStrategicMind.Settings
{
    public static class DirectDefenseCommandSettings
    {
        public static bool EnableDirectDefenseCommand = true;
        public static bool EnableDirectDefenseCommands = true;
        public static bool RequireSettlementNameFilter = false;
        public static string SettlementNameFilter = "";
        public static bool OnlyUrgentDefense = true;
        public static int MaxDirectCommandsPerDay = 2;
        public static int MaxDirectCommandsPerSettlementPerDay = 1;
        public static bool RequireStableWouldActSignal = true;
        public static bool EnableDirectDefenseCommandLogs = true;
        public static bool AllowLordPartyDefenseCommands = true;
        public static bool AllowArmyPartyDefenseCommands = true;
        public static int DefenseCommandCooldownTicks = 3;
        public static int MaxActiveDefenseAssignmentsPerKingdom = 3;
        public static int MaxActiveDefenseAssignmentsGlobal = 12;
        public static float MaxDefenseCommandDistance = 180f;
        public static float MaxUrgentDefenseCommandDistance = 260f;
    }
}
