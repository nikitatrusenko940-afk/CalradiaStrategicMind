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
            CsmArmyDirector armyDirector,
            out DefenseCandidateScore topRejected)
        {
            topRejected = null;
            DefenseCandidateScore best = null;
            var evaluatedAny = false;

            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var score = ScoreReport(settlement, candidates[index], defenseAssignments, armyDirector);
                    evaluatedAny = true;
                    TrackScore(score, ref best, ref topRejected);
                }
            }

            if (!evaluatedAny && !string.IsNullOrWhiteSpace(fallbackCandidateName))
            {
                var party = FindPartyByName(fallbackCandidateName);
                var score = ScoreParty(settlement, party, fallbackCandidateName, defenseAssignments, armyDirector);
                TrackScore(score, ref best, ref topRejected);
            }

            return best;
        }

        private static void TrackScore(DefenseCandidateScore score, ref DefenseCandidateScore best, ref DefenseCandidateScore topRejected)
        {
            if (score == null)
            {
                return;
            }

            if (score.IsRejected)
            {
                if (topRejected == null || score.Score > topRejected.Score)
                {
                    topRejected = score;
                }

                return;
            }

            if (best == null || score.Score > best.Score)
            {
                best = score;
            }
        }

        private static DefenseCandidateScore ScoreReport(
            Settlement settlement,
            DefenseCandidateReport candidate,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyDirector armyDirector)
        {
            var party = FindPartyByName(candidate.CandidatePartyName);
            var score = ScoreParty(settlement, party, candidate.CandidatePartyName, defenseAssignments, armyDirector);
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
            CsmArmyDirector armyDirector)
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
                Reason = "Selected best urgent defense candidate"
            };

            var rejectReason = GetRejectReason(settlement, party, defenseAssignments, armyDirector);
            if (!string.IsNullOrWhiteSpace(rejectReason))
            {
                score.IsRejected = true;
                score.Score = -10000f;
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
            CsmArmyDirector armyDirector)
        {
            if (party == null)
            {
                return "Rejected because candidate party was not found";
            }

            if (settlement == null)
            {
                return "Rejected because target settlement was not found";
            }

            if (party.IsMainParty || party == MobileParty.MainParty)
            {
                return "Rejected because candidate is player party";
            }

            if (party.MapFaction != settlement.MapFaction)
            {
                return "Rejected because candidate faction does not match settlement owner faction";
            }

            if (party.MapEvent != null)
            {
                return "Rejected because candidate is in battle";
            }

            if (party.Army != null && party.Army.LeaderParty != party)
            {
                return "Rejected because candidate is army member but not army leader";
            }

            if (armyDirector != null && armyDirector.HasActiveAssignmentForParty(party))
            {
                return "Rejected because candidate has active CSM army assignment";
            }

            if (defenseAssignments != null && defenseAssignments.HasActiveAssignmentForParty(GetPartyId(party), GetPartyName(party)))
            {
                return "Rejected because candidate has active CSM defense assignment";
            }

            if (party.BesiegedSettlement != null && party.BesiegedSettlement.MapFaction != party.MapFaction)
            {
                return "Rejected because candidate is already besieging enemy target";
            }

            if (party.Position.Distance(settlement.Position) > DirectDefenseCommandSettings.MaxUrgentDefenseCommandDistance)
            {
                return "Rejected because candidate is too far for urgent defense command";
            }

            if (party.Party == null || party.Party.EstimatedStrength <= 0f || party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return "Rejected because candidate has no usable strength";
            }

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

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
