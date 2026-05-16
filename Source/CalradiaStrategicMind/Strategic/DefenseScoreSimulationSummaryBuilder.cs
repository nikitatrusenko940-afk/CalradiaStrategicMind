using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseScoreSimulationSummaryBuilder
    {
        private int _observationTick;
        private int _totalScoreSimulations;
        private int _blockedBySafetyCount;
        private int _wouldAddScoreCount;
        private float _hypotheticalScoreTotal;
        private float _maxHypotheticalScore;
        private string _topScoreSettlementName;
        private string _topScoreCandidateName;
        private PartyObservationCategory _topScoreCandidateCategory;
        private string _topScoreRecommendedAction;
        private string _topScoreReason;

        public DefenseScoreSimulationSummaryBuilder()
        {
            Reset(0);
        }

        public void BeginTick(int observationTick)
        {
            SafeExecutor.Run("Begin defense score simulation summary tick", () =>
            {
                if (_observationTick != observationTick)
                {
                    Reset(observationTick);
                }
            });
        }

        public void Record(DefenseScoreSimulationReport report)
        {
            SafeExecutor.Run("Record defense score simulation summary", () =>
            {
                _totalScoreSimulations++;
                _hypotheticalScoreTotal += report.HypotheticalScore;

                if (report.IsBlockedBySafety)
                {
                    _blockedBySafetyCount++;
                }

                if (report.WouldAddScore)
                {
                    _wouldAddScoreCount++;
                }

                if (_totalScoreSimulations == 1 || report.HypotheticalScore > _maxHypotheticalScore)
                {
                    _maxHypotheticalScore = report.HypotheticalScore;
                    _topScoreSettlementName = report.SettlementName;
                    _topScoreCandidateName = report.CandidateName;
                    _topScoreCandidateCategory = report.CandidateCategory;
                    _topScoreRecommendedAction = report.RecommendedAction;
                    _topScoreReason = report.Reason;
                }
            });
        }

        public DefenseScoreSimulationDailySummary BuildSummary()
        {
            return SafeExecutor.Run(
                "Build defense score simulation summary",
                BuildSummaryCore,
                CreateEmptySummary("Score simulation summary failed"));
        }

        private DefenseScoreSimulationDailySummary BuildSummaryCore()
        {
            if (_totalScoreSimulations == 0)
            {
                return CreateEmptySummary("No score simulations");
            }

            var averageHypotheticalScore = _hypotheticalScoreTotal / _totalScoreSimulations;
            return new DefenseScoreSimulationDailySummary(
                _observationTick,
                _totalScoreSimulations,
                _blockedBySafetyCount,
                _wouldAddScoreCount,
                _maxHypotheticalScore,
                averageHypotheticalScore,
                _topScoreSettlementName,
                _topScoreCandidateName,
                _topScoreCandidateCategory,
                _topScoreRecommendedAction,
                _topScoreReason,
                GetReason());
        }

        private string GetReason()
        {
            if (_totalScoreSimulations == 0)
            {
                return "No score simulations";
            }

            if (_wouldAddScoreCount > 0)
            {
                return "Unexpected would-add-score signal detected";
            }

            if (_blockedBySafetyCount == _totalScoreSimulations)
            {
                return "All score simulations blocked by safety";
            }

            return "Score simulations calculated for diagnostics only";
        }

        private DefenseScoreSimulationDailySummary CreateEmptySummary(string reason)
        {
            return new DefenseScoreSimulationDailySummary(
                _observationTick,
                0,
                0,
                0,
                0f,
                0f,
                "none",
                "none",
                PartyObservationCategory.Unknown,
                "None",
                "none",
                reason);
        }

        private void Reset(int observationTick)
        {
            _observationTick = observationTick;
            _totalScoreSimulations = 0;
            _blockedBySafetyCount = 0;
            _wouldAddScoreCount = 0;
            _hypotheticalScoreTotal = 0f;
            _maxHypotheticalScore = 0f;
            _topScoreSettlementName = "none";
            _topScoreCandidateName = "none";
            _topScoreCandidateCategory = PartyObservationCategory.Unknown;
            _topScoreRecommendedAction = "None";
            _topScoreReason = "none";
        }
    }
}
