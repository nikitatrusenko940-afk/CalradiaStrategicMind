using System;
using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCandidateScorer
    {
        public DefenseCandidateScore SelectBest(
            Settlement settlement,
            List<DefenseCandidateReport> candidates,
            string fallbackCandidateName,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            out DefenseCandidateScore topRejected,
            out DefenseCandidateScoringSummary scoringSummary)
        {
            topRejected = null;
            DefenseCandidateScore best = null;
            var evaluatedAny = false;
            var evaluatedCandidates = 0;
            var validCandidates = 0;
            var rejectedCandidates = 0;
            var rejectedByArmyAssignment = 0;
            var rejectedByDefenseAssignment = 0;
            var rejectedTooFar = 0;
            var rejectedTooWeak = 0;

            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var score = ScoreReport(settlement, candidates[index], defenseAssignments, armyAssignments);
                    evaluatedAny = true;
                    TrackScore(
                        score,
                        ref best,
                        ref topRejected,
                        ref evaluatedCandidates,
                        ref validCandidates,
                        ref rejectedCandidates,
                        ref rejectedByArmyAssignment,
                        ref rejectedByDefenseAssignment,
                        ref rejectedTooFar,
                        ref rejectedTooWeak);
                }
            }

            if (!evaluatedAny && !string.IsNullOrWhiteSpace(fallbackCandidateName))
            {
                var party = FindPartyByName(fallbackCandidateName);
                var score = ScoreParty(settlement, party, fallbackCandidateName, defenseAssignments, armyAssignments);
                TrackScore(
                    score,
                    ref best,
                    ref topRejected,
                    ref evaluatedCandidates,
                    ref validCandidates,
                    ref rejectedCandidates,
                    ref rejectedByArmyAssignment,
                    ref rejectedByDefenseAssignment,
                    ref rejectedTooFar,
                    ref rejectedTooWeak);
            }

            scoringSummary = new DefenseCandidateScoringSummary(
                GetSettlementName(settlement),
                evaluatedCandidates,
                validCandidates,
                rejectedCandidates,
                rejectedByArmyAssignment,
                rejectedByDefenseAssignment,
                rejectedTooFar,
                rejectedTooWeak,
                best == null ? "none" : best.CandidateName,
                "Defense candidate scoring snapshot");
            return best;
        }

        private static void TrackScore(
            DefenseCandidateScore score,
            ref DefenseCandidateScore best,
            ref DefenseCandidateScore topRejected,
            ref int evaluatedCandidates,
            ref int validCandidates,
            ref int rejectedCandidates,
            ref int rejectedByArmyAssignment,
            ref int rejectedByDefenseAssignment,
            ref int rejectedTooFar,
            ref int rejectedTooWeak)
        {
            if (score == null)
            {
                return;
            }

            evaluatedCandidates++;
            if (score.IsRejected)
            {
                rejectedCandidates++;
                if (score.RejectionCategory == "ArmyAssignment")
                {
                    rejectedByArmyAssignment++;
                }
                else if (score.RejectionCategory == "DefenseAssignment")
                {
                    rejectedByDefenseAssignment++;
                }
                else if (score.RejectionCategory == "TooFar")
                {
                    rejectedTooFar++;
                }
                else if (score.RejectionCategory == "TooWeak")
                {
                    rejectedTooWeak++;
                }

                if (topRejected == null || score.Score > topRejected.Score)
                {
                    topRejected = score;
                }

                return;
            }

            validCandidates++;
            if (best == null || score.Score > best.Score)
            {
                best = score;
            }
        }

        private static DefenseCandidateScore ScoreReport(
            Settlement settlement,
            DefenseCandidateReport candidate,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments)
        {
            var party = FindPartyByName(candidate.CandidatePartyName);
            var score = ScoreParty(settlement, party, candidate.CandidatePartyName, defenseAssignments, armyAssignments);
            score.CandidateCategory = candidate.CandidateCategory;
            score.Distance = candidate.DistanceToSettlement;
            score.Strength = candidate.CandidateStrength;
            score.HealthyStrength = candidate.CandidateStrength * (1f - candidate.WoundedRatio);
            if (!score.IsRejected)
            {
                score.Score = CalculateScore(score, candidate.IsArmyLeader, candidate.CandidateCategory, candidate.IsWeak, party, settlement);
                score.Reason = "Selected best urgent defense candidate";
            }

            return score;
        }

        private static DefenseCandidateScore ScoreParty(
            Settlement settlement,
            MobileParty party,
            string candidateName,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments)
        {
            var score = new DefenseCandidateScore
            {
                Party = party,
                CandidateName = string.IsNullOrWhiteSpace(candidateName) ? GetPartyName(party) : candidateName,
                CandidateCategory = GetPartyCategory(party),
                Score = 0f,
                Distance = party == null || settlement == null ? 9999f : party.Position.Distance(settlement.Position),
                Strength = party?.Party == null ? 0f : party.Party.EstimatedStrength,
                HealthyStrength = party?.Party == null ? 0f : party.Party.EstimatedStrength,
                RejectionCategory = "None",
                Reason = "Selected best urgent defense candidate"
            };

            string rejectionCategory;
            var rejectReason = GetRejectReason(settlement, party, defenseAssignments, armyAssignments, out rejectionCategory);
            if (!string.IsNullOrWhiteSpace(rejectReason))
            {
                score.IsRejected = true;
                score.Score = -10000f;
                score.RejectionCategory = rejectionCategory;
                score.Reason = rejectReason;
                return score;
            }

            score.Score = CalculateScore(score, party.Army != null && party.Army.LeaderParty == party, score.CandidateCategory, false, party, settlement);
            return score;
        }

        private static string GetRejectReason(
            Settlement settlement,
            MobileParty party,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            out string rejectionCategory)
        {
            rejectionCategory = "Invalid";
            if (party == null)
            {
                return "Candidate invalid";
            }

            if (settlement == null)
            {
                return "Candidate invalid";
            }

            var conflict = new CsmAssignmentConflictChecker(armyAssignments, defenseAssignments).CheckPartyForNewDefenseCommand(party, settlement);
            if (conflict.IsBlocked)
            {
                if (conflict.HasActiveArmyAssignment)
                {
                    rejectionCategory = "ArmyAssignment";
                    return "Candidate has active CSM army assignment";
                }

                if (conflict.HasActiveDefenseAssignment)
                {
                    rejectionCategory = "DefenseAssignment";
                    return "Candidate already has active CSM defense assignment";
                }

                if (conflict.IsAlreadyDefendingDifferentSettlement)
                {
                    rejectionCategory = "AlreadyDefendingDifferentSettlement";
                    return "Candidate is already defending different settlement";
                }

                return "Candidate invalid";
            }

            if (settlement.MapFaction == null || party.MapFaction != settlement.MapFaction)
            {
                rejectionCategory = "WrongFaction";
                return "Candidate belongs to wrong faction";
            }

            if (party.Army != null && party.Army.LeaderParty != party)
            {
                return "Candidate invalid";
            }

            if (party.Position.Distance(settlement.Position) > DirectDefenseCommandSettings.MaxUrgentDefenseCommandDistance)
            {
                rejectionCategory = "TooFar";
                return "Candidate too far";
            }

            if (party.Party == null || party.Party.EstimatedStrength <= 0f || party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                rejectionCategory = "TooWeak";
                return "Candidate too weak";
            }

            rejectionCategory = "None";
            return null;
        }

        private static float CalculateScore(
            DefenseCandidateScore score,
            bool isArmyLeader,
            PartyObservationCategory category,
            bool isWeak,
            MobileParty party,
            Settlement settlement)
        {
            var value = 0f;
            value += score.Strength * 0.20f;
            value += score.HealthyStrength * 0.25f;
            value -= score.Distance * 0.8f;

            if (party != null && settlement != null && (party.CurrentSettlement == settlement || (party.DefaultBehavior == AiBehavior.DefendSettlement && party.TargetSettlement == settlement)))
            {
                value += 120f;
            }

            if (score.Distance <= 40f)
            {
                value += 60f;
            }

            if (isArmyLeader && DirectDefenseCommandSettings.AllowArmyPartyDefenseCommands)
            {
                value += 40f;
            }

            if (category == PartyObservationCategory.LordParty)
            {
                value += 20f;
            }

            if (party != null && settlement != null && party.TargetSettlement != null && party.TargetSettlement != settlement)
            {
                value -= 80f;
            }

            if (isWeak || score.Strength < 100f)
            {
                value -= 100f;
            }

            return value;
        }

        private static MobileParty FindPartyByName(string partyName)
        {
            var parties = MobileParty.All;
            if (parties == null || string.IsNullOrWhiteSpace(partyName))
            {
                return null;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party?.Name != null && NamesEqual(party.Name.ToString(), partyName))
                {
                    return party;
                }
            }

            return null;
        }

        private static PartyObservationCategory GetPartyCategory(MobileParty party)
        {
            if (party == null)
            {
                return PartyObservationCategory.Unknown;
            }

            return party.Army != null && party.Army.LeaderParty == party
                ? PartyObservationCategory.ArmyParty
                : PartyObservationCategory.LordParty;
        }

        private static string GetPartyId(MobileParty party)
        {
            return party == null || party.StringId == null ? string.Empty : party.StringId;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
