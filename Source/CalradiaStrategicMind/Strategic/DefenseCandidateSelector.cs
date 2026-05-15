using System.Collections.Generic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCandidateSelector
    {
        private const float DefenseCandidateSearchRadius = 120f;
        private const float TooFarDistance = 95f;
        private const float MinimumSuitableScore = 20f;
        private const int MinimumHealthyTroops = 40;
        private const float HighWoundedRatio = 0.35f;

        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;

        public DefenseCandidateSelector()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
        }

        public List<DefenseCandidateReport> FindDefenseCandidates(Settlement settlement, int maxCandidates)
        {
            return SafeExecutor.Run(
                "Find defense candidates",
                () => FindDefenseCandidatesCore(settlement, maxCandidates),
                new List<DefenseCandidateReport>());
        }

        private List<DefenseCandidateReport> FindDefenseCandidatesCore(Settlement settlement, int maxCandidates)
        {
            var reports = new List<DefenseCandidateReport>();
            if (settlement == null || maxCandidates <= 0)
            {
                return reports;
            }

            var parties = MobileParty.All;
            if (parties == null)
            {
                return reports;
            }

            var ownerFaction = settlement.MapFaction;
            var settlementPosition = settlement.GetPosition2D;
            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                var category = _partyClassifier.GetCategory(party);
                if (!ShouldConsiderParty(party, category))
                {
                    continue;
                }

                if (!IsFriendly(ownerFaction, party.MapFaction))
                {
                    continue;
                }

                var distance = settlementPosition.Distance(party.GetPosition2D);
                if (distance > DefenseCandidateSearchRadius)
                {
                    continue;
                }

                var isArmyMember = party.Army != null;
                var isArmyLeader = isArmyMember && party.Army.LeaderParty == party;
                var armyLeaderNearby = isArmyMember && !isArmyLeader && IsArmyLeaderNearby(party, settlementPosition);
                var strengthReport = _partyStrengthEvaluator.EvaluatePartyStrengthReport(party);
                var healthyTroops = strengthReport.TroopCount - strengthReport.WoundedCount;
                if (healthyTroops < 0)
                {
                    healthyTroops = 0;
                }

                var woundedRatio = GetWoundedRatio(strengthReport.TroopCount, strengthReport.WoundedCount);
                var isWeak = healthyTroops < MinimumHealthyTroops;
                var isTooFar = distance > TooFarDistance;
                var isBusy = IsBusy(party);
                var availabilityScore = GetAvailabilityScore(healthyTroops, woundedRatio, isTooFar, isBusy, isArmyMember, isArmyLeader);
                var suitabilityScore = GetSuitabilityScore(
                    strengthReport.TotalStrength,
                    distance,
                    category,
                    isArmyLeader,
                    isArmyMember,
                    availabilityScore);
                var isSuitable = suitabilityScore >= MinimumSuitableScore
                    && !armyLeaderNearby
                    && !isTooFar
                    && !isWeak
                    && woundedRatio < HighWoundedRatio
                    && !isBusy;

                reports.Add(new DefenseCandidateReport(
                    GetSettlementName(settlement),
                    GetPartyName(party),
                    GetLeaderName(party),
                    category,
                    strengthReport.TotalStrength,
                    distance,
                    healthyTroops,
                    strengthReport.WoundedCount,
                    woundedRatio,
                    isArmyLeader,
                    isArmyMember,
                    isWeak,
                    isTooFar,
                    isBusy,
                    availabilityScore,
                    isSuitable,
                    suitabilityScore,
                    GetReason(isArmyLeader, isArmyMember, armyLeaderNearby, isSuitable, isTooFar, isWeak, woundedRatio, isBusy)));
            }

            reports.Sort(CompareCandidates);
            if (reports.Count > maxCandidates)
            {
                reports.RemoveRange(maxCandidates, reports.Count - maxCandidates);
            }

            return reports;
        }

        private static bool ShouldConsiderParty(MobileParty party, PartyObservationCategory category)
        {
            if (party == null)
            {
                return false;
            }

            if (!party.IsActive || party.IsDisbanding || party.IsGarrison || party.IsMilitia)
            {
                return false;
            }

            if (party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return false;
            }

            return category == PartyObservationCategory.LordParty
                || category == PartyObservationCategory.ArmyParty;
        }

        private static bool IsArmyLeaderNearby(MobileParty party, TaleWorlds.Library.Vec2 settlementPosition)
        {
            if (party == null || party.Army == null || party.Army.LeaderParty == null)
            {
                return false;
            }

            var leaderParty = party.Army.LeaderParty;
            if (!leaderParty.IsActive || leaderParty.IsDisbanding)
            {
                return false;
            }

            return settlementPosition.Distance(leaderParty.GetPosition2D) <= DefenseCandidateSearchRadius;
        }

        private static bool IsFriendly(IFaction ownerFaction, IFaction partyFaction)
        {
            if (ownerFaction == null || partyFaction == null)
            {
                return false;
            }

            return ownerFaction == partyFaction || ownerFaction == partyFaction.MapFaction;
        }

        private static bool IsBusy(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            return party.SiegeEvent != null
                || party.BesiegedSettlement != null
                || party.BesiegerCamp != null;
        }

        private static float GetWoundedRatio(int troopCount, int woundedCount)
        {
            if (troopCount <= 0 || woundedCount <= 0)
            {
                return 0f;
            }

            return (float)woundedCount / troopCount;
        }

        private static float GetAvailabilityScore(
            int healthyTroops,
            float woundedRatio,
            bool isTooFar,
            bool isBusy,
            bool isArmyMember,
            bool isArmyLeader)
        {
            var score = 100f;
            if (isTooFar)
            {
                score -= 25f;
            }

            if (woundedRatio >= HighWoundedRatio)
            {
                score -= 35f;
            }
            else
            {
                score -= woundedRatio * 40f;
            }

            if (healthyTroops < MinimumHealthyTroops)
            {
                score -= 30f;
            }

            if (isBusy)
            {
                score -= 40f;
            }

            if (isArmyMember && !isArmyLeader)
            {
                score -= 15f;
            }

            return score < 0f ? 0f : score;
        }

        private static float GetSuitabilityScore(
            float strength,
            float distance,
            PartyObservationCategory category,
            bool isArmyLeader,
            bool isArmyMember,
            float availabilityScore)
        {
            var distanceScore = DefenseCandidateSearchRadius - distance;
            if (distanceScore < 0f)
            {
                distanceScore = 0f;
            }

            var score = strength * 0.35f + distanceScore * 0.5f;
            if (isArmyLeader)
            {
                score += 25f;
            }
            else if (category == PartyObservationCategory.LordParty && !isArmyMember)
            {
                score += 10f;
            }
            else if (isArmyMember)
            {
                score -= 15f;
            }

            score *= availabilityScore / 100f;
            return score < 0f ? 0f : score;
        }

        private static int CompareCandidates(DefenseCandidateReport left, DefenseCandidateReport right)
        {
            var suitableCompare = right.IsSuitable.CompareTo(left.IsSuitable);
            if (suitableCompare != 0)
            {
                return suitableCompare;
            }

            return right.SuitabilityScore.CompareTo(left.SuitabilityScore);
        }

        private static string GetReason(
            bool isArmyLeader,
            bool isArmyMember,
            bool armyLeaderNearby,
            bool isSuitable,
            bool isTooFar,
            bool isWeak,
            float woundedRatio,
            bool isBusy)
        {
            if (armyLeaderNearby)
            {
                return "Army member: leader party is a better main candidate";
            }

            if (isTooFar)
            {
                return "Unsuitable: too far";
            }

            if (woundedRatio >= HighWoundedRatio)
            {
                return "Unsuitable: too many wounded";
            }

            if (isWeak)
            {
                return "Unsuitable: weak party";
            }

            if (isBusy)
            {
                return "Unsuitable: already involved in siege";
            }

            if (isArmyLeader)
            {
                return isSuitable
                    ? "Suitable: nearby friendly army leader"
                    : "Nearby friendly army leader, but suitability is low";
            }

            if (isArmyMember)
            {
                return isSuitable
                    ? "Suitable: army member without nearby leader"
                    : "Army member with limited independent suitability";
            }

            return isSuitable
                ? "Suitable: nearby strong friendly lord party"
                : "Nearby friendly lord party, but suitability is low";
        }

        private static string GetSettlementName(Settlement settlement)
        {
            if (settlement == null || settlement.Name == null)
            {
                return "unknown";
            }

            return settlement.Name.ToString();
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }

        private static string GetLeaderName(MobileParty party)
        {
            if (party == null || party.LeaderHero == null || party.LeaderHero.Name == null)
            {
                return "none";
            }

            return party.LeaderHero.Name.ToString();
        }
    }
}
