using System;
using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class ExperimentalDefenseScoreInfluenceRegistry
    {
        private static readonly Dictionary<string, RegistryEntry> ReportsByPartyAndSettlement = new Dictionary<string, RegistryEntry>();
        private static readonly Dictionary<string, RegistryEntry> ReportsByParty = new Dictionary<string, RegistryEntry>();
        private static int _observationTick;

        public void BeginTick(int observationTick)
        {
            SafeExecutor.Run("Begin experimental defense score influence registry tick", () =>
            {
                _observationTick = observationTick;
                RemoveExpiredReports(observationTick);
            });
        }

        public int CurrentObservationTick
        {
            get { return _observationTick; }
        }

        public void Record(DefenseScoreSimulationReport report, int observationTick)
        {
            SafeExecutor.Run("Record experimental defense score influence report", () =>
            {
                if (string.IsNullOrWhiteSpace(report.CandidateName) || string.IsNullOrWhiteSpace(report.SettlementName))
                {
                    return;
                }

                if (IsNone(report.CandidateName) || IsNone(report.SettlementName))
                {
                    return;
                }

                var entry = new RegistryEntry(report, observationTick);
                ReportsByPartyAndSettlement[CreateKey(report.CandidateName, report.SettlementName)] = entry;
                ReportsByParty[Normalize(report.CandidateName)] = entry;
                RemoveExpiredReports(observationTick);
            });
        }

        public bool TryGetRecentReportForPartyAndSettlement(string partyName, string settlementName, int currentTick, out DefenseScoreSimulationReport report)
        {
            report = default(DefenseScoreSimulationReport);
            var foundReport = default(DefenseScoreSimulationReport);
            var result = SafeExecutor.Run(
                "Get recent experimental defense score influence report for party and settlement",
                () =>
                {
                    RegistryEntry entry;
                    if (!ReportsByPartyAndSettlement.TryGetValue(CreateKey(partyName, settlementName), out entry))
                    {
                        return false;
                    }

                    if (IsExpired(entry.ObservationTick, currentTick))
                    {
                        return false;
                    }

                    foundReport = entry.Report;
                    return true;
                },
                false);

            if (result)
            {
                report = foundReport;
            }

            return result;
        }

        public bool TryGetRecentReportForParty(string partyName, int currentTick, out DefenseScoreSimulationReport report)
        {
            report = default(DefenseScoreSimulationReport);
            var foundReport = default(DefenseScoreSimulationReport);
            var result = SafeExecutor.Run(
                "Get recent experimental defense score influence report for party",
                () =>
                {
                    RegistryEntry entry;
                    if (!ReportsByParty.TryGetValue(Normalize(partyName), out entry))
                    {
                        return false;
                    }

                    if (IsExpired(entry.ObservationTick, currentTick))
                    {
                        return false;
                    }

                    foundReport = entry.Report;
                    return true;
                },
                false);

            if (result)
            {
                report = foundReport;
            }

            return result;
        }

        private static void RemoveExpiredReports(int currentTick)
        {
            RemoveExpiredReports(ReportsByPartyAndSettlement, currentTick);
            RemoveExpiredReports(ReportsByParty, currentTick);
        }

        private static void RemoveExpiredReports(Dictionary<string, RegistryEntry> reports, int currentTick)
        {
            var expiredKeys = new List<string>();
            foreach (var report in reports)
            {
                if (IsExpired(report.Value.ObservationTick, currentTick))
                {
                    expiredKeys.Add(report.Key);
                }
            }

            for (var index = 0; index < expiredKeys.Count; index++)
            {
                reports.Remove(expiredKeys[index]);
            }
        }

        private static bool IsExpired(int reportTick, int currentTick)
        {
            var maxAge = ExperimentalDefenseScoreInfluenceSettings.MaxInfluenceAgeTicks;
            if (maxAge < 0)
            {
                maxAge = 0;
            }

            return currentTick - reportTick > maxAge;
        }

        private static string CreateKey(string partyName, string settlementName)
        {
            return Normalize(partyName) + "|" + Normalize(settlementName);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static bool IsNone(string value)
        {
            return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
        }

        private struct RegistryEntry
        {
            public RegistryEntry(DefenseScoreSimulationReport report, int observationTick)
            {
                Report = report;
                ObservationTick = observationTick;
            }

            public DefenseScoreSimulationReport Report { get; private set; }

            public int ObservationTick { get; private set; }
        }
    }
}
