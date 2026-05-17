using System;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Strategic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Behaviors
{
    public class ExperimentalDefenseScoreInfluenceBehavior : CampaignBehaviorBase
    {
        private readonly ExperimentalDefenseScoreInfluenceRegistry _registry;
        private int _lastDebugLogTick;
        private int _debugLogsThisTick;

        public ExperimentalDefenseScoreInfluenceBehavior()
        {
            _registry = new ExperimentalDefenseScoreInfluenceRegistry();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnAiHourlyTick(MobileParty party, PartyThinkParams partyThinkParams)
        {
            SafeExecutor.Run("Experimental defense score influence hourly tick", () =>
            {
                var report = EvaluateAndApply(party, partyThinkParams);
                if (ShouldLogReport(report))
                {
                    LogExperimentalDefenseScoreInfluence(report);
                }
            });
        }

        private ExperimentalDefenseScoreInfluenceReport EvaluateAndApply(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var currentTick = _registry.CurrentObservationTick;
            var partyName = GetPartyName(party);

            if (!ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseScoreInfluence)
            {
                return CreateReport(currentTick, partyName, "none", false, false, false, false, 0f, 0f, "Experimental defense score influence disabled");
            }

            if (party == null)
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Party is null");
            }

            if (partyThinkParams == null)
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Party think params is null");
            }

            if (!party.IsActive || party.IsDisbanding)
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Party is inactive or disbanding");
            }

            if (party.LeaderHero == null)
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Party has no leader");
            }

            if (party.Army != null && party.Army.LeaderParty != party)
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Party is army member and not leader");
            }

            var settlementFilter = ExperimentalDefenseScoreInfluenceSettings.SettlementNameFilter;
            if (ExperimentalDefenseScoreInfluenceSettings.RequireSettlementNameFilter && string.IsNullOrWhiteSpace(settlementFilter))
            {
                return CreateReport(currentTick, partyName, "none", true, false, false, false, 0f, 0f, "Settlement name filter required");
            }

            DefenseScoreSimulationReport scoreReport;
            if (!TryGetScoreReport(partyName, settlementFilter, currentTick, out scoreReport))
            {
                return CreateReport(currentTick, partyName, GetSafeSettlementName(settlementFilter), true, false, false, false, 0f, 0f, "No recent score simulation report");
            }

            if (scoreReport.HypotheticalScore < ExperimentalDefenseScoreInfluenceSettings.MinimumHypotheticalScore)
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Hypothetical score below minimum");
            }

            if (!IsExecutableDefenseAction(scoreReport.RecommendedAction))
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Non-executable score simulation action");
            }

            if (!IsLowOrCriticalCoverage(scoreReport.CoverageStatus))
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Coverage status is not low or critical");
            }

            if (!string.IsNullOrWhiteSpace(settlementFilter) && !NamesEqual(scoreReport.SettlementName, settlementFilter))
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Settlement filter mismatch");
            }

            var settlement = FindSettlementByName(scoreReport.SettlementName);
            if (settlement == null)
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Settlement not found");
            }

            var scoreBoost = ClampScoreBoost(scoreReport.HypotheticalScore);
            if (scoreBoost <= 0f)
            {
                return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, false, false, false, scoreReport.HypotheticalScore, 0f, "Score boost is zero");
            }

            var aiBehaviorData = new AIBehaviorData(
                settlement,
                AiBehavior.DefendSettlement,
                party.NavigationCapability,
                false,
                false,
                false);

            partyThinkParams.AddBehaviorScore((aiBehaviorData, scoreBoost));

            return CreateReport(currentTick, partyName, scoreReport.SettlementName, true, true, true, true, scoreReport.HypotheticalScore, scoreBoost, "Experimental defense score influence applied");
        }

        private bool TryGetScoreReport(string partyName, string settlementFilter, int currentTick, out DefenseScoreSimulationReport scoreReport)
        {
            if (!string.IsNullOrWhiteSpace(settlementFilter))
            {
                return _registry.TryGetRecentReportForPartyAndSettlement(partyName, settlementFilter, currentTick, out scoreReport);
            }

            return _registry.TryGetRecentReportForParty(partyName, currentTick, out scoreReport);
        }

        private static float ClampScoreBoost(float hypotheticalScore)
        {
            var maxScoreBoost = ExperimentalDefenseScoreInfluenceSettings.MaxScoreBoost;
            if (maxScoreBoost < 0f)
            {
                maxScoreBoost = 0f;
            }

            var scaledBoost = hypotheticalScore / 10f;
            if (scaledBoost < 0f)
            {
                return 0f;
            }

            return scaledBoost > maxScoreBoost ? maxScoreBoost : scaledBoost;
        }

        private static bool IsExecutableDefenseAction(string action)
        {
            return NamesEqual(action, "RequestReinforcement")
                || NamesEqual(action, "Reinforcement")
                || NamesEqual(action, "Reinforce")
                || NamesEqual(action, "RequestUrgentDefense")
                || NamesEqual(action, "UrgentDefense");
        }

        private static bool IsLowOrCriticalCoverage(string coverageStatus)
        {
            return NamesEqual(coverageStatus, "Low")
                || NamesEqual(coverageStatus, "Critical");
        }

        private static Settlement FindSettlementByName(string settlementName)
        {
            var settlements = Settlement.All;
            if (settlements == null || string.IsNullOrWhiteSpace(settlementName))
            {
                return null;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null || settlement.Name == null)
                {
                    continue;
                }

                if (NamesEqual(settlement.Name.ToString(), settlementName))
                {
                    return settlement;
                }
            }

            return null;
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(left == null ? string.Empty : left.Trim(), right == null ? string.Empty : right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSafeSettlementName(string settlementName)
        {
            return string.IsNullOrWhiteSpace(settlementName) ? "none" : settlementName;
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }

        private bool ShouldLogReport(ExperimentalDefenseScoreInfluenceReport report)
        {
            if (report.WasAttempted || report.WasApplied)
            {
                return true;
            }

            if (!ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseScoreInfluenceDebugLogs)
            {
                return false;
            }

            if (!IsDebugReason(report.Reason))
            {
                return false;
            }

            if (_lastDebugLogTick != report.ObservationTick)
            {
                _lastDebugLogTick = report.ObservationTick;
                _debugLogsThisTick = 0;
            }

            var maxDebugLogs = ExperimentalDefenseScoreInfluenceSettings.MaxExperimentalDebugLogsPerTick;
            if (maxDebugLogs < 0)
            {
                maxDebugLogs = 0;
            }

            if (_debugLogsThisTick >= maxDebugLogs)
            {
                return false;
            }

            _debugLogsThisTick++;
            return true;
        }

        private static bool IsDebugReason(string reason)
        {
            return reason == "Experimental defense score influence disabled"
                || reason == "Settlement name filter required"
                || reason == "Hypothetical score below minimum"
                || reason == "Non-executable score simulation action"
                || reason == "Coverage status is not low or critical"
                || reason == "Settlement filter mismatch"
                || (reason == "No recent score simulation report" && ExperimentalDefenseScoreInfluenceSettings.LogNoRecentScoreSimulationReport)
                || (reason == "Party is army member and not leader" && ExperimentalDefenseScoreInfluenceSettings.LogArmyMemberSkip);
        }

        private static ExperimentalDefenseScoreInfluenceReport CreateReport(
            int observationTick,
            string partyName,
            string settlementName,
            bool isEnabled,
            bool wasAttempted,
            bool wasApplied,
            bool wouldAddBehaviorScore,
            float hypotheticalScore,
            float appliedScoreBoost,
            string reason)
        {
            return new ExperimentalDefenseScoreInfluenceReport(
                observationTick,
                partyName,
                settlementName,
                isEnabled,
                wasAttempted,
                wasApplied,
                wouldAddBehaviorScore,
                hypotheticalScore,
                appliedScoreBoost,
                reason);
        }

        private static void LogExperimentalDefenseScoreInfluence(ExperimentalDefenseScoreInfluenceReport report)
        {
            CsmLogger.Info(
                $"Observed experimental defense score influence: tick={report.ObservationTick}, party='{report.PartyName}', settlement='{report.SettlementName}', isEnabled={report.IsEnabled}, wasAttempted={report.WasAttempted}, wasApplied={report.WasApplied}, wouldAddBehaviorScore={report.WouldAddBehaviorScore}, hypotheticalScore={report.HypotheticalScore:0.00}, appliedScoreBoost={report.AppliedScoreBoost:0.00}, reason='{report.Reason}'");
        }
    }
}
