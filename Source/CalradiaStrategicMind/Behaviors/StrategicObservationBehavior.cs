using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Strategic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Behaviors
{
    public class StrategicObservationBehavior : CampaignBehaviorBase
    {
        private const int MaxPartiesPerDailyObservation = 5;
        private const int MaxSettlementsPerDailyObservation = 3;

        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;
        private readonly SettlementThreatEvaluator _settlementThreatEvaluator;
        private readonly SettlementValueEvaluator _settlementValueEvaluator;
        private readonly DefensePriorityEvaluator _defensePriorityEvaluator;
        private int _nextPartyIndex;
        private int _nextSettlementIndex;
        private int _observationTick;

        public StrategicObservationBehavior()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
            _settlementThreatEvaluator = new SettlementThreatEvaluator();
            _settlementValueEvaluator = new SettlementValueEvaluator();
            _defensePriorityEvaluator = new DefensePriorityEvaluator();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_nextPartyIndex", ref _nextPartyIndex);
            dataStore.SyncData("_nextSettlementIndex", ref _nextSettlementIndex);
            dataStore.SyncData("_observationTick", ref _observationTick);
        }

        private void OnDailyTick()
        {
            SafeExecutor.Run("Strategic observation daily tick", () =>
            {
                ObserveParties();
                ObserveSettlements();
            });
        }

        private void ObserveParties()
        {
            var parties = MobileParty.All;
            if (parties == null)
            {
                CsmLogger.Warn("Strategic observation skipped: MobileParty.All is null");
                return;
            }

            _observationTick++;
            var observedCount = 0;
            var fallbackObservedCount = 0;
            var fallbackParties = new MobileParty[MaxPartiesPerDailyObservation];
            var fallbackCategories = new PartyObservationCategory[MaxPartiesPerDailyObservation];
            var checkedCount = 0;
            var startPartyIndex = _nextPartyIndex;
            if (_nextPartyIndex < 0 || _nextPartyIndex >= parties.Count)
            {
                _nextPartyIndex = 0;
                startPartyIndex = 0;
            }

            while (checkedCount < parties.Count && observedCount < MaxPartiesPerDailyObservation)
            {
                var party = parties[_nextPartyIndex];
                _nextPartyIndex++;
                if (_nextPartyIndex >= parties.Count)
                {
                    _nextPartyIndex = 0;
                }

                checkedCount++;
                if (!ShouldObserveParty(party))
                {
                    continue;
                }

                var category = _partyClassifier.GetCategory(party);
                if (_partyClassifier.ShouldObserveForStrategicAi(party))
                {
                    LogPartyObservation(party, category);
                    observedCount++;
                    continue;
                }

                if (observedCount == 0
                    && fallbackObservedCount < MaxPartiesPerDailyObservation
                    && ShouldUseFallbackCategory(category))
                {
                    fallbackParties[fallbackObservedCount] = party;
                    fallbackCategories[fallbackObservedCount] = category;
                    fallbackObservedCount++;
                }
            }

            if (checkedCount >= parties.Count)
            {
                _nextPartyIndex = startPartyIndex + 1;
                if (_nextPartyIndex >= parties.Count)
                {
                    _nextPartyIndex = 0;
                }
            }

            if (observedCount > 0)
            {
                return;
            }

            for (var index = 0; index < fallbackObservedCount; index++)
            {
                LogPartyObservation(fallbackParties[index], fallbackCategories[index]);
            }

            if (fallbackObservedCount == 0)
            {
                CsmLogger.Info($"Strategic observation completed: tick={_observationTick}, no active parties selected");
            }
        }

        private static bool ShouldObserveParty(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            if (!party.IsActive || party.IsDisbanding)
            {
                return false;
            }

            return party.MemberRoster != null && party.MemberRoster.TotalManCount > 0;
        }

        private static bool ShouldUseFallbackCategory(PartyObservationCategory category)
        {
            return category != PartyObservationCategory.Unknown
                && category != PartyObservationCategory.Militia;
        }

        private void LogPartyObservation(MobileParty party, PartyObservationCategory category)
        {
            var report = _partyStrengthEvaluator.EvaluatePartyStrengthReport(party);
            var regulars = report.TroopCount - report.WoundedCount;
            if (regulars < 0)
            {
                regulars = 0;
            }

            var leaderName = party == null || party.LeaderHero == null || party.LeaderHero.Name == null
                ? "none"
                : party.LeaderHero.Name.ToString();

            CsmLogger.Info(
                $"Observed party strength: tick={_observationTick}, category={category}, party='{GetPartyName(party)}', leader='{leaderName}', regulars={regulars}, wounded={report.WoundedCount}, totalStrength={report.TotalStrength:0.00}, healthyStrength={report.HealthyTroopStrength:0.00}, woundedStrength={report.WoundedTroopStrength:0.00}, leaderStrength={report.LeaderStrength:0.00}");
        }

        private void ObserveSettlements()
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                CsmLogger.Warn("Settlement threat observation skipped: Settlement.All is null");
                return;
            }

            var observedCount = 0;
            var checkedCount = 0;
            if (_nextSettlementIndex < 0 || _nextSettlementIndex >= settlements.Count)
            {
                _nextSettlementIndex = 0;
            }

            while (checkedCount < settlements.Count && observedCount < MaxSettlementsPerDailyObservation)
            {
                var settlement = settlements[_nextSettlementIndex];
                _nextSettlementIndex++;
                if (_nextSettlementIndex >= settlements.Count)
                {
                    _nextSettlementIndex = 0;
                }

                checkedCount++;
                if (!ShouldObserveSettlement(settlement))
                {
                    continue;
                }

                LogSettlementThreat(settlement);
                LogSettlementValue(settlement);
                LogDefensePriority(settlement);
                observedCount++;
            }
        }

        private static bool ShouldObserveSettlement(Settlement settlement)
        {
            if (settlement == null)
            {
                return false;
            }

            if (!settlement.IsActive)
            {
                return false;
            }

            return settlement.IsTown || settlement.IsCastle;
        }

        private void LogSettlementThreat(Settlement settlement)
        {
            var report = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            CsmLogger.Info(
                $"Observed settlement threat: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, garrisonStrength={report.GarrisonStrength:0.00}, nearbyEnemyStrength={report.NearbyEnemyStrength:0.00}, nearbyEnemyPartyCount={report.NearbyEnemyPartyCount}, nearbyFriendlyStrength={report.NearbyFriendlyStrength:0.00}, nearbyFriendlyPartyCount={report.NearbyFriendlyPartyCount}, strongestEnemyPartyName='{report.StrongestEnemyPartyName}', strongestEnemyStrength={report.StrongestEnemyStrength:0.00}, nearestEnemyPartyName='{report.NearestEnemyPartyName}', nearestEnemyDistance={report.NearestEnemyDistance:0.00}, threatScore={report.ThreatScore:0.00}, isThreatened={report.IsThreatened}");
        }

        private void LogSettlementValue(Settlement settlement)
        {
            var report = _settlementValueEvaluator.EvaluateSettlementValue(settlement);
            CsmLogger.Info(
                $"Observed settlement value: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, prosperity={report.Prosperity:0.00}, garrisonStrength={report.GarrisonStrength:0.00}, strategicValue={report.StrategicValue:0.00}, reason='{report.ValueReason}'");
        }

        private void LogDefensePriority(Settlement settlement)
        {
            var report = _defensePriorityEvaluator.EvaluateDefensePriority(settlement);
            CsmLogger.Info(
                $"Observed defense priority: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, threatScore={report.ThreatScore:0.00}, strategicValue={report.StrategicValue:0.00}, threatComponent={report.ThreatComponent:0.00}, valueComponent={report.ValueComponent:0.00}, threatenedBonus={report.ThreatenedBonus:0.00}, defensePriority={report.DefensePriority:0.00}, isThreatened={report.IsThreatened}, shouldRequestDefense={report.ShouldRequestDefense}, reason='{report.Reason}'");
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }
    }
}
