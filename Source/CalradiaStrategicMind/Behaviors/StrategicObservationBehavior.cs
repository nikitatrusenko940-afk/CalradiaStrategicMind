using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Strategic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Behaviors
{
    public class StrategicObservationBehavior : CampaignBehaviorBase
    {
        private const int MaxPartiesPerDailyObservation = 5;

        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;
        private int _nextPartyIndex;
        private int _observationTick;

        public StrategicObservationBehavior()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_nextPartyIndex", ref _nextPartyIndex);
            dataStore.SyncData("_observationTick", ref _observationTick);
        }

        private void OnDailyTick()
        {
            SafeExecutor.Run("Strategic observation daily tick", ObserveParties);
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
