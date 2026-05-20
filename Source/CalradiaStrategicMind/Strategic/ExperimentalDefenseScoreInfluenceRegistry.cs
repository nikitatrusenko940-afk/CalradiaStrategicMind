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
        private static readonly HashSet<string> AppliedBoostKeys = new HashSet<string>();
        private static readonly HashSet<string> LoggedReportKeys = new HashSet<string>();
        private static int _observationTick;
        private static int _summaryTick = -1;
        private static int _attemptedThisTick;
        private static int _appliedThisTick;
        private static int _suppressedDuplicateAppliesThisTick;
        private static int _suppressedDuplicateLogsThisTick;
        private static bool _hasCompletedSummary;
        private static ExperimentalDefenseScoreInfluenceSummary _completedSummary;

        public void BeginTick(int observationTick)
        {
            SafeExecutor.Run("Begin experimental defense score influence registry tick", () =>
            {
                EnsureSummaryTick(observationTick);
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

        public void RecordEvaluation(ExperimentalDefenseScoreInfluenceReport report)
        {
            SafeExecutor.Run("Record experimental defense score influence evaluation", () =>
            {
                EnsureSummaryTick(report.ObservationTick);
                if (report.WasAttempted)
                {
                    _attemptedThisTick++;
                }

                if (report.WasApplied)
                {
                    _appliedThisTick++;
                }
            });
        }

        public bool TryRegisterApply(string partyName, string settlementName, int observationTick)
        {
            return SafeExecutor.Run(
                "Throttle experimental defense score influence apply",
                () =>
                {
                    EnsureSummaryTick(observationTick);
                    if (!ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseInfluenceApplyThrottle)
                    {
                        return true;
                    }

                    var key = CreateThrottleKey(partyName, settlementName, observationTick);
                    if (AppliedBoostKeys.Contains(key))
                    {
                        _suppressedDuplicateAppliesThisTick++;
                        _suppressedDuplicateLogsThisTick++;
                        return false;
                    }

                    AppliedBoostKeys.Add(key);
                    return true;
                },
                true);
        }

        public bool ShouldLogReport(ExperimentalDefenseScoreInfluenceReport report)
        {
            return SafeExecutor.Run(
                "Throttle experimental defense score influence log",
                () =>
                {
                    EnsureSummaryTick(report.ObservationTick);
                    var key = CreateLogKey(report.PartyName, report.SettlementName, report.ObservationTick, report.Reason);
                    if (LoggedReportKeys.Contains(key))
                    {
                        _suppressedDuplicateLogsThisTick++;
                        return false;
                    }

                    LoggedReportKeys.Add(key);
                    return true;
                },
                true);
        }

        public bool TryConsumeCompletedSummary(out ExperimentalDefenseScoreInfluenceSummary summary)
        {
            summary = default(ExperimentalDefenseScoreInfluenceSummary);
            var consumedSummary = default(ExperimentalDefenseScoreInfluenceSummary);
            var result = SafeExecutor.Run(
                "Consume experimental defense score influence summary",
                () =>
                {
                    if (!_hasCompletedSummary)
                    {
                        return false;
                    }

                    consumedSummary = _completedSummary;
                    _hasCompletedSummary = false;
                    return true;
                },
                false);

            if (result)
            {
                summary = consumedSummary;
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

        private static void EnsureSummaryTick(int observationTick)
        {
            if (_summaryTick == observationTick)
            {
                return;
            }

            if (_summaryTick >= 0 && !IsEmptyInitialSummary())
            {
                _completedSummary = new ExperimentalDefenseScoreInfluenceSummary(
                    _summaryTick,
                    _attemptedThisTick,
                    _appliedThisTick,
                    _suppressedDuplicateAppliesThisTick,
                    _suppressedDuplicateLogsThisTick,
                    "Experimental defense score influence throttle snapshot");
                _hasCompletedSummary = true;
            }

            _summaryTick = observationTick;
            _attemptedThisTick = 0;
            _appliedThisTick = 0;
            _suppressedDuplicateAppliesThisTick = 0;
            _suppressedDuplicateLogsThisTick = 0;
            AppliedBoostKeys.Clear();
            LoggedReportKeys.Clear();
        }

        private static bool IsEmptyInitialSummary()
        {
            return _summaryTick == 0
                && _attemptedThisTick == 0
                && _appliedThisTick == 0
                && _suppressedDuplicateAppliesThisTick == 0
                && _suppressedDuplicateLogsThisTick == 0;
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

        private static string CreateThrottleKey(string partyName, string settlementName, int observationTick)
        {
            return CreateKey(partyName, settlementName) + "|" + observationTick;
        }

        private static string CreateLogKey(string partyName, string settlementName, int observationTick, string reason)
        {
            return CreateThrottleKey(partyName, settlementName, observationTick) + "|" + Normalize(reason);
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

    public struct ExperimentalDefenseScoreInfluenceSummary
    {
        public ExperimentalDefenseScoreInfluenceSummary(
            int observationTick,
            int attempted,
            int applied,
            int suppressedDuplicateApplies,
            int suppressedDuplicateLogs,
            string reason)
        {
            ObservationTick = observationTick;
            Attempted = attempted;
            Applied = applied;
            SuppressedDuplicateApplies = suppressedDuplicateApplies;
            SuppressedDuplicateLogs = suppressedDuplicateLogs;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public int Attempted { get; private set; }

        public int Applied { get; private set; }

        public int SuppressedDuplicateApplies { get; private set; }

        public int SuppressedDuplicateLogs { get; private set; }

        public string Reason { get; private set; }
    }
}
