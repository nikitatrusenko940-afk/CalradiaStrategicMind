using System.Collections.Generic;

namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseEvaluationSnapshot
    {
        public DefenseEvaluationSnapshot(
            SettlementThreatReport threatReport,
            SettlementValueReport valueReport,
            DefensePriorityReport priorityReport,
            List<DefenseCandidateReport> candidateReports,
            DefenseCoverageReport coverageReport,
            DefenseNeedReport needReport)
        {
            ThreatReport = threatReport;
            ValueReport = valueReport;
            PriorityReport = priorityReport;
            CandidateReports = candidateReports;
            CoverageReport = coverageReport;
            NeedReport = needReport;
        }

        public SettlementThreatReport ThreatReport { get; private set; }

        public SettlementValueReport ValueReport { get; private set; }

        public DefensePriorityReport PriorityReport { get; private set; }

        public List<DefenseCandidateReport> CandidateReports { get; private set; }

        public DefenseCoverageReport CoverageReport { get; private set; }

        public DefenseNeedReport NeedReport { get; private set; }
    }
}
