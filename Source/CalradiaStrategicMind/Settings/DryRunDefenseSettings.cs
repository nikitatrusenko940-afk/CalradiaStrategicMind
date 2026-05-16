namespace CalradiaStrategicMind.Settings
{
    public static class DryRunDefenseSettings
    {
        public static bool EnableDefenseDiagnostics
        {
            get { return DefenseDiagnosticsSettings.EnableDefenseDiagnostics; }
            set { DefenseDiagnosticsSettings.EnableDefenseDiagnostics = value; }
        }

        public static bool EnableVerboseDefenseLogs
        {
            get { return DefenseDiagnosticsSettings.EnableVerboseDefenseLogs; }
            set { DefenseDiagnosticsSettings.EnableVerboseDefenseLogs = value; }
        }

        public static bool EnableDefenseCandidateLogs
        {
            get { return DefenseDiagnosticsSettings.EnableDefenseCandidateLogs; }
            set { DefenseDiagnosticsSettings.EnableDefenseCandidateLogs = value; }
        }

        public static bool EnableDefenseSummaryLogs
        {
            get { return DefenseDiagnosticsSettings.EnableDefenseSummaryLogs; }
            set { DefenseDiagnosticsSettings.EnableDefenseSummaryLogs = value; }
        }

        public static bool EnableDryRunDefenseController
        {
            get { return DefenseDryRunSettings.EnableDryRunDefenseController; }
            set { DefenseDryRunSettings.EnableDryRunDefenseController = value; }
        }

        public static bool EnableRealDefenseController
        {
            get { return DefenseControllerSettings.EnableRealDefenseController; }
            set { DefenseControllerSettings.EnableRealDefenseController = value; }
        }

        public static bool EnableDefenseActionHistory
        {
            get { return DefenseDryRunSettings.EnableDefenseActionHistory; }
            set { DefenseDryRunSettings.EnableDefenseActionHistory = value; }
        }

        public static bool EnableDryRunDecisionHistory
        {
            get { return DefenseDryRunSettings.EnableDryRunDecisionHistory; }
            set { DefenseDryRunSettings.EnableDryRunDecisionHistory = value; }
        }

        public static bool EnableDryRunDailyReport
        {
            get { return DefenseDryRunSettings.EnableDryRunDailyReport; }
            set { DefenseDryRunSettings.EnableDryRunDailyReport = value; }
        }

        public static bool EnableDefenseScoreSimulation
        {
            get { return DefenseScoreSimulationSettings.EnableDefenseScoreSimulation; }
            set { DefenseScoreSimulationSettings.EnableDefenseScoreSimulation = value; }
        }

        public static bool EnableDefenseScoreSimulationSummary
        {
            get { return DefenseScoreSimulationSettings.EnableDefenseScoreSimulationSummary; }
            set { DefenseScoreSimulationSettings.EnableDefenseScoreSimulationSummary = value; }
        }
    }
}
