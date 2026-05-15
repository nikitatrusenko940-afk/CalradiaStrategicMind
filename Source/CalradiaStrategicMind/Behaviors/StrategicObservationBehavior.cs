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
        private int _nextPartyIndex;
        private int _observationTick;

        public StrategicObservationBehavior()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
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
            var checkedCount = 0;
            if (_nextPartyIndex < 0 || _nextPartyIndex >= parties.Count)
            {
                _nextPartyIndex = 0;
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

                var report = _partyStrengthEvaluator.EvaluatePartyStrengthReport(party);
                var regulars = report.TroopCount - report.WoundedCount;
                if (regulars < 0)
                {
                    regulars = 0;
                }

                var leaderName = party.LeaderHero == null || party.LeaderHero.Name == null
                    ? "none"
                    : party.LeaderHero.Name.ToString();

                CsmLogger.Info(
                    $"Observed party strength: tick={_observationTick}, party='{GetPartyName(party)}', leader='{leaderName}', regulars={regulars}, wounded={report.WoundedCount}, totalStrength={report.TotalStrength:0.00}, healthyStrength={report.HealthyTroopStrength:0.00}, woundedStrength={report.WoundedTroopStrength:0.00}, leaderStrength={report.LeaderStrength:0.00}");

                observedCount++;
            }

            if (observedCount == 0)
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

            if (party.IsMilitia)
            {
                return false;
            }

            return party.MemberRoster != null && party.MemberRoster.TotalManCount > 0;
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
