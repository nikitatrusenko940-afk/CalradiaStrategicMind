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

            var observedCount = 0;
            for (var index = 0; index < parties.Count && observedCount < MaxPartiesPerDailyObservation; index++)
            {
                var party = parties[index];
                if (!ShouldObserveParty(party))
                {
                    continue;
                }

                var strength = _partyStrengthEvaluator.EvaluatePartyStrength(party);
                var regulars = party.MemberRoster == null ? 0 : party.MemberRoster.TotalRegulars;
                var wounded = party.MemberRoster == null ? 0 : party.MemberRoster.TotalWounded;
                var leaderName = party.LeaderHero == null || party.LeaderHero.Name == null
                    ? "none"
                    : party.LeaderHero.Name.ToString();

                CsmLogger.Info(
                    $"Observed party strength: party='{GetPartyName(party)}', leader='{leaderName}', regulars={regulars}, wounded={wounded}, strength={strength:0.00}");

                observedCount++;
            }

            if (observedCount == 0)
            {
                CsmLogger.Info("Strategic observation completed: no active parties selected");
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
