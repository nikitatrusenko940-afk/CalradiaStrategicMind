using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class SettlementThreatEvaluator
    {
        private const float NearbyPartySearchRadius = 80f;

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
            var nearestEnemyPartyName = "none";
            var nearestEnemyDistance = 0f;
            var strongestEnemyPartyName = "none";
            var strongestEnemyStrength = 0f;
            var strongestFriendlyPartyName = "none";
            var strongestFriendlyStrength = 0f;
            var parties = MobileParty.All;

            if (parties != null)
            {
                var settlementPosition = settlement.GetPosition2D;
                for (var index = 0; index < parties.Count; index++)
                {
                    var party = parties[index];
                    if (!ShouldCountNearbyParty(party))
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
                        nearbyEnemyStrength += partyStrength * distanceWeight;
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

            var strongEnemyPressure = 0f;
            var strongEnemyThreshold = garrisonStrength * 0.7f;
            if (strongestEnemyStrength > strongEnemyThreshold)
            {
                strongEnemyPressure = (strongestEnemyStrength - strongEnemyThreshold) * 0.5f;
            }

            var threatScore = nearbyEnemyStrength + strongEnemyPressure - garrisonStrength - nearbyFriendlyStrength * 0.35f;
            var isThreatened = threatScore > 0f || nearbyEnemyStrength > garrisonStrength;

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

        private bool ShouldCountNearbyParty(MobileParty party)
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

            var category = _partyClassifier.GetCategory(party);
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
