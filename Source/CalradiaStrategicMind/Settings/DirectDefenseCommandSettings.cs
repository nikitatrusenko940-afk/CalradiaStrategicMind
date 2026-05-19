namespace CalradiaStrategicMind.Settings
{
    public static class DirectDefenseCommandSettings
    {
        public static bool EnableDirectDefenseCommand = true;
        public static bool RequireSettlementNameFilter = false;
        public static string SettlementNameFilter = "";
        public static bool OnlyUrgentDefense = true;
        public static int MaxDirectCommandsPerDay = 2;
        public static int MaxDirectCommandsPerSettlementPerDay = 1;
        public static bool RequireStableWouldActSignal = true;
        public static bool EnableDirectDefenseCommandLogs = true;
    }
}
