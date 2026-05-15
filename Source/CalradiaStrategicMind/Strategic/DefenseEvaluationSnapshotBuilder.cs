using System.Collections.Generic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseEvaluationSnapshotBuilder
    {
        private readonly SettlementThreatEvaluator _settlementThreatEvaluator;
        private readonly SettlementValueEvaluator _settlementValueEvaluator;
        private readonly DefensePriorityEvaluator _defensePriorityEvaluator;
        private readonly DefenseCandidateSelector _defenseCandidateSelector;
        private readonly DefenseCoverageEvaluator _defenseCoverageEvaluator;
        private readonly DefenseNeedEvaluator _defenseNeedEvaluator;

        public DefenseEvaluationSnapshotBuilder()
        {
            _settlementThreatEvaluator = new SettlementThreatEvaluator();
            _settlementValueEvaluator = new SettlementValueEvaluator();
            _defensePriorityEvaluator = new DefensePriorityEvaluator();
            _defenseCandidateSelector = new DefenseCandidateSelector();
            _defenseCoverageEvaluator = new DefenseCoverageEvaluator();
            _defenseNeedEvaluator = new DefenseNeedEvaluator();
        }

        public DefenseEvaluationSnapshot Build(Settlement settlement, int maxLoggedCandidates)
        {
            return SafeExecutor.Run(
                "Build defense evaluation snapshot",
                () => BuildCore(settlement, maxLoggedCandidates),
                new DefenseEvaluationSnapshot(
                    SettlementThreatReport.Empty,
                    SettlementValueReport.Empty,
                    DefensePriorityReport.Empty,
                    new List<DefenseCandidateReport>(),
                    DefenseCoverageReport.Empty,
                    DefenseNeedReport.Empty));
        }

        private DefenseEvaluationSnapshot BuildCore(Settlement settlement, int maxLoggedCandidates)
        {
            if (settlement == null)
            {
                return new DefenseEvaluationSnapshot(
                    SettlementThreatReport.Empty,
                    SettlementValueReport.Empty,
                    DefensePriorityReport.Empty,
                    new List<DefenseCandidateReport>(),
                    DefenseCoverageReport.Empty,
                    DefenseNeedReport.Empty);
            }

            var threatReport = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            var valueReport = _settlementValueEvaluator.EvaluateSettlementValue(settlement);
            var priorityReport = _defensePriorityEvaluator.EvaluateDefensePriority(settlement, threatReport, valueReport);
            var allCandidateReports = _defenseCandidateSelector.FindDefenseCandidates(settlement, 0);
            var coverageReport = _defenseCoverageEvaluator.EvaluateDefenseCoverage(settlement, threatReport, allCandidateReports);
            var needReport = _defenseNeedEvaluator.EvaluateDefenseNeed(settlement, priorityReport, coverageReport);
            var loggedCandidateReports = GetLoggedCandidates(allCandidateReports, maxLoggedCandidates);

            return new DefenseEvaluationSnapshot(
                threatReport,
                valueReport,
                priorityReport,
                loggedCandidateReports,
                coverageReport,
                needReport);
        }

        private static List<DefenseCandidateReport> GetLoggedCandidates(
            List<DefenseCandidateReport> allCandidateReports,
            int maxLoggedCandidates)
        {
            var loggedCandidates = new List<DefenseCandidateReport>();
            if (allCandidateReports == null || maxLoggedCandidates <= 0)
            {
                return loggedCandidates;
            }

            var count = allCandidateReports.Count < maxLoggedCandidates
                ? allCandidateReports.Count
                : maxLoggedCandidates;
            for (var index = 0; index < count; index++)
            {
                loggedCandidates.Add(allCandidateReports[index]);
            }

            return loggedCandidates;
        }
    }
}
