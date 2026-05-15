using System.Collections.Generic;
using CalradiaStrategicMind.Utils;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseActionPlanner
    {
        private const int MaxSelectedCandidates = 3;

        public DefenseActionPlan CreatePlan(DefenseEvaluationSnapshot snapshot)
        {
            return SafeExecutor.Run("Create defense action plan", () => CreatePlanCore(snapshot), DefenseActionPlan.Empty);
        }

        private static DefenseActionPlan CreatePlanCore(DefenseEvaluationSnapshot snapshot)
        {
            var needReport = snapshot.NeedReport;
            if (string.IsNullOrEmpty(needReport.SettlementName) || needReport.SettlementName == "unknown")
            {
                return DefenseActionPlan.Empty;
            }

            if (needReport.RecommendedAction == "None")
            {
                return CreateEmptyPlan(needReport, "No defense action needed");
            }

            var candidates = GetSelectableCandidates(snapshot.CandidateReports);
            var selectedCandidateCount = GetSelectedCandidateCount(candidates);
            var selectedCandidateStrength = GetSelectedCandidateStrength(candidates, selectedCandidateCount);
            var primaryCandidate = selectedCandidateCount > 0
                ? candidates[0]
                : default(DefenseCandidateReport);
            var planConfidence = GetPlanConfidence(needReport, selectedCandidateCount, selectedCandidateStrength, primaryCandidate);

            if (needReport.RecommendedAction == "Monitor")
            {
                return new DefenseActionPlan(
                    needReport.SettlementName,
                    needReport.OwnerKingdomName,
                    "Monitor",
                    false,
                    needReport.DefensePriority,
                    needReport.DefenseCoverageRatio,
                    selectedCandidateCount,
                    selectedCandidateStrength,
                    GetPrimaryCandidateName(primaryCandidate, selectedCandidateCount),
                    GetPrimaryCandidateCategory(primaryCandidate, selectedCandidateCount),
                    GetPrimaryCandidateStrength(primaryCandidate, selectedCandidateCount),
                    GetPrimaryCandidateDistance(primaryCandidate, selectedCandidateCount),
                    planConfidence,
                    selectedCandidateCount > 0
                        ? "Monitoring with suitable candidates available"
                        : "Monitoring without suitable candidates");
            }

            return new DefenseActionPlan(
                needReport.SettlementName,
                needReport.OwnerKingdomName,
                needReport.RecommendedAction,
                needReport.NeedsDefenseAction,
                needReport.DefensePriority,
                needReport.DefenseCoverageRatio,
                selectedCandidateCount,
                selectedCandidateStrength,
                GetPrimaryCandidateName(primaryCandidate, selectedCandidateCount),
                GetPrimaryCandidateCategory(primaryCandidate, selectedCandidateCount),
                GetPrimaryCandidateStrength(primaryCandidate, selectedCandidateCount),
                GetPrimaryCandidateDistance(primaryCandidate, selectedCandidateCount),
                planConfidence,
                GetActionReason(needReport, selectedCandidateCount));
        }

        private static DefenseActionPlan CreateEmptyPlan(DefenseNeedReport needReport, string reason)
        {
            return new DefenseActionPlan(
                needReport.SettlementName,
                needReport.OwnerKingdomName,
                "None",
                false,
                needReport.DefensePriority,
                needReport.DefenseCoverageRatio,
                0,
                0f,
                "none",
                PartyObservationCategory.Unknown,
                0f,
                0f,
                0f,
                reason);
        }

        private static List<DefenseCandidateReport> GetSelectableCandidates(List<DefenseCandidateReport> candidateReports)
        {
            var candidates = new List<DefenseCandidateReport>();
            if (candidateReports == null)
            {
                return candidates;
            }

            for (var index = 0; index < candidateReports.Count; index++)
            {
                var candidate = candidateReports[index];
                if (!candidate.IsSuitable || candidate.IsTooFar || candidate.IsBusy || candidate.IsWeak)
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            candidates.Sort(CompareCandidatesForPlan);
            return candidates;
        }

        private static int CompareCandidatesForPlan(DefenseCandidateReport left, DefenseCandidateReport right)
        {
            return GetCandidatePlanScore(right).CompareTo(GetCandidatePlanScore(left));
        }

        private static float GetCandidatePlanScore(DefenseCandidateReport candidate)
        {
            var score = candidate.SuitabilityScore;
            if (candidate.IsArmyLeader)
            {
                score += 25f;
            }
            else if (candidate.IsArmyMember)
            {
                score -= 10f;
            }

            return score;
        }

        private static int GetSelectedCandidateCount(List<DefenseCandidateReport> candidates)
        {
            return candidates.Count < MaxSelectedCandidates ? candidates.Count : MaxSelectedCandidates;
        }

        private static float GetSelectedCandidateStrength(List<DefenseCandidateReport> candidates, int selectedCandidateCount)
        {
            var strength = 0f;
            for (var index = 0; index < selectedCandidateCount; index++)
            {
                strength += candidates[index].CandidateStrength;
            }

            return strength;
        }

        private static float GetPlanConfidence(
            DefenseNeedReport needReport,
            int selectedCandidateCount,
            float selectedCandidateStrength,
            DefenseCandidateReport primaryCandidate)
        {
            if (needReport.RecommendedAction == "None")
            {
                return 0f;
            }

            if (selectedCandidateCount <= 0)
            {
                return 20f;
            }

            var confidence = selectedCandidateCount == 1 ? 55f : 75f;
            if (selectedCandidateStrength >= 300f)
            {
                confidence += 10f;
            }

            if (primaryCandidate.IsArmyLeader)
            {
                confidence += 10f;
            }

            return confidence > 100f ? 100f : confidence;
        }

        private static string GetActionReason(DefenseNeedReport needReport, int selectedCandidateCount)
        {
            if (selectedCandidateCount <= 0)
            {
                return needReport.NeedsDefenseAction
                    ? "Defense action needed, but no suitable candidates found"
                    : "No suitable candidates selected";
            }

            return needReport.RecommendedAction == "UrgentDefense"
                ? "Urgent defense plan with suitable candidates"
                : "Reinforcement plan with suitable candidates";
        }

        private static string GetPrimaryCandidateName(DefenseCandidateReport candidate, int selectedCandidateCount)
        {
            return selectedCandidateCount > 0 ? candidate.CandidatePartyName : "none";
        }

        private static PartyObservationCategory GetPrimaryCandidateCategory(DefenseCandidateReport candidate, int selectedCandidateCount)
        {
            return selectedCandidateCount > 0 ? candidate.CandidateCategory : PartyObservationCategory.Unknown;
        }

        private static float GetPrimaryCandidateStrength(DefenseCandidateReport candidate, int selectedCandidateCount)
        {
            return selectedCandidateCount > 0 ? candidate.CandidateStrength : 0f;
        }

        private static float GetPrimaryCandidateDistance(DefenseCandidateReport candidate, int selectedCandidateCount)
        {
            return selectedCandidateCount > 0 ? candidate.DistanceToSettlement : 0f;
        }
    }
}
