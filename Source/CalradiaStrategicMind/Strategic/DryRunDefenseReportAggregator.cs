using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DryRunDefenseReportAggregator
    {
        private int _observationTick;
        private int _totalEvaluatedSettlements;
        private int _ignoreCount;
        private int _monitorCount;
        private int _waitCount;
        private int _requestReinforcementCount;
        private int _requestUrgentDefenseCount;
        private int _wouldActCount;
        private int _wouldMonitorCount;
        private int _stableWouldActSignalCount;
        private int _stableMonitorSignalCount;
        private string _topActionSettlementName;
        private string _topAction;
        private float _topActionConfidence;

        public DryRunDefenseReportAggregator()
        {
            Reset(0);
        }

        public void BeginTick(int observationTick)
        {
            SafeExecutor.Run("Begin dry-run defense daily report tick", () =>
            {
                if (_observationTick != observationTick)
                {
                    Reset(observationTick);
                }
            });
        }

        public void Record(
            DryRunDefenseDecision decision,
            DryRunDefenseDecisionStabilityReport stabilityReport)
        {
            SafeExecutor.Run("Record dry-run defense daily report item", () => RecordCore(decision, stabilityReport));
        }

        public DryRunDefenseDailyReport BuildReport()
        {
            return SafeExecutor.Run(
                "Build dry-run defense daily report",
                BuildReportCore,
                new DryRunDefenseDailyReport(
                    _observationTick,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    "none",
                    "None",
                    0f,
                    "No dry-run action"));
        }

        private void RecordCore(
            DryRunDefenseDecision decision,
            DryRunDefenseDecisionStabilityReport stabilityReport)
        {
            _totalEvaluatedSettlements++;

            if (decision.Action == "Ignore")
            {
                _ignoreCount++;
            }
            else if (decision.Action == "Monitor")
            {
                _monitorCount++;
            }
            else if (decision.Action == "Wait")
            {
                _waitCount++;
            }
            else if (decision.Action == "RequestReinforcement")
            {
                _requestReinforcementCount++;
            }
            else if (decision.Action == "RequestUrgentDefense")
            {
                _requestUrgentDefenseCount++;
            }

            if (decision.WouldAct)
            {
                _wouldActCount++;
                if (decision.PlanConfidence > _topActionConfidence)
                {
                    _topActionSettlementName = string.IsNullOrEmpty(decision.SettlementName)
                        ? "unknown"
                        : decision.SettlementName;
                    _topAction = string.IsNullOrEmpty(decision.Action)
                        ? "None"
                        : decision.Action;
                    _topActionConfidence = decision.PlanConfidence;
                }
            }

            if (decision.WouldMonitor)
            {
                _wouldMonitorCount++;
            }

            if (stabilityReport.HasStableWouldActSignal)
            {
                _stableWouldActSignalCount++;
            }

            if (stabilityReport.HasStableMonitorSignal)
            {
                _stableMonitorSignalCount++;
            }
        }

        private DryRunDefenseDailyReport BuildReportCore()
        {
            var topActionSettlementName = _wouldActCount > 0 ? _topActionSettlementName : "none";
            var topAction = _wouldActCount > 0 ? _topAction : "None";
            var topActionConfidence = _wouldActCount > 0 ? _topActionConfidence : 0f;

            return new DryRunDefenseDailyReport(
                _observationTick,
                _totalEvaluatedSettlements,
                _ignoreCount,
                _monitorCount,
                _waitCount,
                _requestReinforcementCount,
                _requestUrgentDefenseCount,
                _wouldActCount,
                _wouldMonitorCount,
                _stableWouldActSignalCount,
                _stableMonitorSignalCount,
                topActionSettlementName,
                topAction,
                topActionConfidence,
                GetReason());
        }

        private string GetReason()
        {
            if (_requestUrgentDefenseCount > 0)
            {
                return "Urgent dry-run actions detected";
            }

            if (_requestReinforcementCount > 0)
            {
                return "Reinforcement dry-run actions detected";
            }

            if (_monitorCount > 0)
            {
                return "Monitoring only";
            }

            return "No dry-run action";
        }

        private void Reset(int observationTick)
        {
            _observationTick = observationTick;
            _totalEvaluatedSettlements = 0;
            _ignoreCount = 0;
            _monitorCount = 0;
            _waitCount = 0;
            _requestReinforcementCount = 0;
            _requestUrgentDefenseCount = 0;
            _wouldActCount = 0;
            _wouldMonitorCount = 0;
            _stableWouldActSignalCount = 0;
            _stableMonitorSignalCount = 0;
            _topActionSettlementName = "none";
            _topAction = "None";
            _topActionConfidence = 0f;
        }
    }
}
