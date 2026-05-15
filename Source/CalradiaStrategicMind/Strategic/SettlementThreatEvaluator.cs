using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class SettlementThreatEvaluator
    {
        private const float NearbyPartySearchRadius = 80f;
        private const float ActiveSiegeBaseThreat = 500f;

        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;

        public SettlementThreatEvaluator()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
        }

        public SettlementThreatReport EvaluateSettlementThreat(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate settlement threat", () => EvaluateSettlementThreatCore(settlement), SettlementThreatReport.Empty);
        }

        private SettlementThreatReport EvaluateSettlementThreatCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return SettlementThreatReport.Empty;
            }

            var ownerFaction = settlement.MapFaction;
            var garrisonStrength = GetGarrisonStrength(settlement);
            var nearbyEnemyStrength = 0f;
            var nearbyFriendlyStrength = 0f;
            var nearbyEnemyPartyCount = 0;
            var nearbyFriendlyPartyCount = 0;
            var armySiegeThreat = 0f;
            var enemyLordPressure = 0f;
            var nearbyEnemyArmyMemberPartyCount = 0;
            var nearbyEnemyArmyLeaderPartyCount = 0;
            var nearbyEnemyLordPartyCount = 0;
            var nearestEnemyPartyName = "none";
            var nearestEnemyDistance = 0f;
            var strongestEnemyPartyName = "none";
            var strongestEnemyStrength = 0f;
            var strongestEnemyArmyName = "none";
            var strongestEnemyArmyStrength = 0f;
            var strongestEnemyLordName = "none";
            var strongestEnemyLordStrength = 0f;
            var strongestFriendlyPartyName = "none";
            var strongestFriendlyStrength = 0f;
            var hasActiveSiege = settlement.IsUnderSiege;
            var activeSiegeThreat = hasActiveSiege ? ActiveSiegeBaseThreat + garrisonStrength * 0.5f : 0f;
            var parties = MobileParty.All;

            if (parties != null)
            {
                var settlementPosition = settlement.GetPosition2D;
                for (var index = 0; index < parties.Count; index++)
                {
                    var party = parties[index];
                    var category = _partyClassifier.GetCategory(party);
                    if (!ShouldCountNearbyParty(party, category))
                    {
                        continue;
                    }

                    var distance = settlementPosition.Distance(party.GetPosition2D);
                    if (distance > NearbyPartySearchRadius)
                    {
                        continue;
                    }

                    var partyFaction = party.MapFaction;
                    var partyStrength = _partyStrengthEvaluator.EvaluatePartyStrength(party);
                    var distanceWeight = GetDistanceWeight(distance);

                    if (IsEnemy(ownerFaction, partyFaction))
                    {
                        var weightedStrength = partyStrength * distanceWeight;
                        var isArmyMember = party.Army != null;
                        var isArmyLeader = isArmyMember && party.Army.LeaderParty == party;
                        nearbyEnemyStrength += weightedStrength;
                        nearbyEnemyPartyCount++;

                        if (nearestEnemyPartyName == "none" || distance < nearestEnemyDistance)
                        {
                            nearestEnemyPartyName = GetPartyName(party);
                            nearestEnemyDistance = distance;
                        }

                        if (partyStrength > strongestEnemyStrength)
                        {
                            strongestEnemyPartyName = GetPartyName(party);
                            strongestEnemyStrength = partyStrength;
                        }

                        if (isArmyMember)
                        {
                            nearbyEnemyArmyMemberPartyCount++;
                        }

                        if (isArmyLeader)
                        {
                            armySiegeThreat += weightedStrength;
                            nearbyEnemyArmyLeaderPartyCount++;

                            if (partyStrength > strongestEnemyArmyStrength)
                            {
                                strongestEnemyArmyName = GetPartyName(party);
                                strongestEnemyArmyStrength = partyStrength;
                            }
                        }
                        else if (!isArmyMember && category == PartyObservationCategory.LordParty)
                        {
                            enemyLordPressure += weightedStrength;
                            nearbyEnemyLordPartyCount++;

                            if (partyStrength > strongestEnemyLordStrength)
                            {
                                strongestEnemyLordName = GetPartyName(party);
                                strongestEnemyLordStrength = partyStrength;
                            }
                        }
                    }
                    else if (IsFriendly(ownerFaction, partyFaction))
                    {
                        nearbyFriendlyStrength += partyStrength * distanceWeight;
                        nearbyFriendlyPartyCount++;

                        if (partyStrength > strongestFriendlyStrength)
                        {
                            strongestFriendlyPartyName = GetPartyName(party);
                            strongestFriendlyStrength = partyStrength;
                        }
                    }
                }
            }

            var localDefenseStrength = garrisonStrength + nearbyFriendlyStrength * 0.45f;
            var siegeThreatScore = hasActiveSiege
                ? activeSiegeThreat + armySiegeThreat - localDefenseStrength * 0.25f
                : armySiegeThreat - localDefenseStrength;
            if (siegeThreatScore < 0f)
            {
                siegeThreatScore = 0f;
            }

            var regionalEnemyPressure = enemyLordPressure + armySiegeThreat * 0.15f;
            var extremeLordPressure = nearbyEnemyLordPartyCount >= 4 && enemyLordPressure > localDefenseStrength * 2.5f;
            var threatScore = siegeThreatScore + regionalEnemyPressure * 0.2f;
            var isThreatened = hasActiveSiege || siegeThreatScore > 0f || extremeLordPressure;

            return new SettlementThreatReport(
                GetSettlementName(settlement),
                GetFactionName(ownerFaction),
                GetSettlementType(settlement),
                garrisonStrength,
                nearbyEnemyStrength,
                nearbyEnemyPartyCount,
                nearbyFriendlyStrength,
                nearbyFriendlyPartyCount,
                nearestEnemyPartyName,
                nearestEnemyDistance,
                strongestEnemyPartyName,
                strongestEnemyStrength,
                strongestFriendlyPartyName,
                strongestFriendlyStrength,
                threatScore,
                siegeThreatScore,
                armySiegeThreat,
                regionalEnemyPressure,
                armySiegeThreat,
                enemyLordPressure,
                activeSiegeThreat,
                hasActiveSiege,
                nearbyEnemyArmyMemberPartyCount,
                nearbyEnemyArmyLeaderPartyCount,
                nearbyEnemyLordPartyCount,
                strongestEnemyArmyName,
                strongestEnemyArmyStrength,
                strongestEnemyLordName,
                strongestEnemyLordStrength,
                isThreatened);
        }

        private float GetGarrisonStrength(Settlement settlement)
        {
            if (settlement == null || settlement.Parties == null)
            {
                return 0f;
            }

            for (var index = 0; index < settlement.Parties.Count; index++)
            {
                var party = settlement.Parties[index];
                if (party == null || !party.IsGarrison)
                {
                    continue;
                }

                return _partyStrengthEvaluator.EvaluatePartyStrength(party);
            }

            return 0f;
        }

        private static bool ShouldCountNearbyParty(MobileParty party, PartyObservationCategory category)
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

        private static bool IsEnemy(IFaction ownerFaction, IFaction partyFaction)
        {
            if (ownerFaction == null || partyFaction == null)
            {
                return false;
            }

            return ownerFaction.IsAtWarWith(partyFaction);
        }

        private static bool IsFriendly(IFaction ownerFaction, IFaction partyFaction)
        {
            if (ownerFaction == null || partyFaction == null)
            {
                return false;
            }

            return ownerFaction == partyFaction || ownerFaction == partyFaction.MapFaction;
        }

        private static float GetDistanceWeight(float distance)
        {
            if (distance < 0f)
            {
                distance = 0f;
            }

            var weight = 1f - distance / NearbyPartySearchRadius;
            return weight < 0.25f ? 0.25f : weight;
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }

        private static string GetSettlementName(Settlement settlement)
        {
            if (settlement == null || settlement.Name == null)
            {
                return "unknown";
            }

            return settlement.Name.ToString();
        }

        private static string GetFactionName(IFaction faction)
        {
            if (faction == null || faction.Name == null)
            {
                return "none";
            }

            return faction.Name.ToString();
        }

        private static string GetSettlementType(Settlement settlement)
        {
            if (settlement == null)
            {
                return "Unknown";
            }

            if (settlement.IsTown)
            {
                return "Town";
            }

            if (settlement.IsCastle)
            {
                return "Castle";
            }

            return "Other";
        }
    }
}
