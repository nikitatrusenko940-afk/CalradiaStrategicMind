using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class PartyClassifier
    {
        public PartyObservationCategory GetCategory(MobileParty party)
        {
            return SafeExecutor.Run("Classify party", () => GetCategoryCore(party), PartyObservationCategory.Unknown);
        }

        public bool ShouldObserveForStrategicAi(MobileParty party)
        {
            return SafeExecutor.Run("Check strategic party observation category", () => ShouldObserveForStrategicAiCore(party), false);
        }

        private static PartyObservationCategory GetCategoryCore(MobileParty party)
        {
            if (party == null)
            {
                return PartyObservationCategory.Unknown;
            }

            if (party.Army != null && party.Army.LeaderParty == party)
            {
                return PartyObservationCategory.ArmyParty;
            }

            if (party.IsLordParty)
            {
                return PartyObservationCategory.LordParty;
            }

            if (party.IsGarrison)
            {
                return PartyObservationCategory.Garrison;
            }

            if (party.IsBandit)
            {
                return PartyObservationCategory.BanditParty;
            }

            if (party.IsCaravan)
            {
                return PartyObservationCategory.Caravan;
            }

            if (party.IsVillager)
            {
                return PartyObservationCategory.VillagerParty;
            }

            if (party.IsMilitia)
            {
                return PartyObservationCategory.Militia;
            }

            return PartyObservationCategory.Other;
        }

        private static bool ShouldObserveForStrategicAiCore(MobileParty party)
        {
            var category = GetCategoryCore(party);
            return category == PartyObservationCategory.LordParty
                || category == PartyObservationCategory.ArmyParty;
        }
    }
}
