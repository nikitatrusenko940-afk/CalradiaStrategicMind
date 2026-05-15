using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace CalradiaStrategicMind.Strategic
{
    public class PartyStrengthEvaluator
    {
        public float EvaluatePartyStrength(MobileParty party)
        {
            return SafeExecutor.Run("Evaluate party strength", () => EvaluatePartyStrengthCore(party), 0f);
        }

        private static float EvaluatePartyStrengthCore(MobileParty party)
        {
            if (party == null)
            {
                return 0f;
            }

            var roster = party.MemberRoster;
            if (roster == null)
            {
                return GetLeaderStrength(party.LeaderHero);
            }

            var strength = 0f;
            strength += roster.TotalRegulars;
            strength += roster.TotalHealthyCount * 0.25f;
            strength -= roster.TotalWounded * 0.35f;
            strength += GetRosterLevelStrength(roster);
            strength += GetLeaderStrength(party.LeaderHero);

            return strength < 0f ? 0f : strength;
        }

        private static float GetRosterLevelStrength(TroopRoster roster)
        {
            if (roster == null)
            {
                return 0f;
            }

            var strength = 0f;
            for (var index = 0; index < roster.Count; index++)
            {
                var element = roster.GetElementCopyAtIndex(index);
                var character = element.Character;
                if (character == null)
                {
                    continue;
                }

                var healthyCount = element.Number - element.WoundedNumber;
                if (healthyCount < 0)
                {
                    healthyCount = 0;
                }

                var level = character.Level;
                if (level < 1)
                {
                    level = 1;
                }

                strength += healthyCount * level;
                strength += element.WoundedNumber * level * 0.25f;
            }

            return strength;
        }

        private static float GetLeaderStrength(Hero leader)
        {
            if (leader == null || leader.CharacterObject == null)
            {
                return 0f;
            }

            var level = leader.CharacterObject.Level;
            if (level < 1)
            {
                level = 1;
            }

            return 10f + level * 2f;
        }
    }
}
