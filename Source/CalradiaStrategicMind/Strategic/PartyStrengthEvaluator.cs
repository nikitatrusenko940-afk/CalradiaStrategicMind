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
            return EvaluatePartyStrengthReport(party).TotalStrength;
        }

        public PartyStrengthReport EvaluatePartyStrengthReport(MobileParty party)
        {
            return SafeExecutor.Run("Evaluate party strength report", () => EvaluatePartyStrengthReportCore(party), PartyStrengthReport.Empty);
        }

        private static PartyStrengthReport EvaluatePartyStrengthReportCore(MobileParty party)
        {
            if (party == null)
            {
                return PartyStrengthReport.Empty;
            }

            var roster = party.MemberRoster;
            var leaderStrength = GetLeaderStrength(party.LeaderHero);
            if (roster == null)
            {
                return new PartyStrengthReport(leaderStrength, 0f, 0f, leaderStrength, 0, 0);
            }

            var healthyStrength = 0f;
            var woundedStrength = 0f;
            var troopCount = 0;
            var woundedCount = 0;
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

                var woundedNumber = element.WoundedNumber;
                if (woundedNumber < 0)
                {
                    woundedNumber = 0;
                }

                var unitStrength = GetUnitStrength(character.Level);
                healthyStrength += healthyCount * unitStrength;
                woundedStrength += woundedNumber * unitStrength * 0.35f;

                troopCount += element.Number;
                woundedCount += woundedNumber;
            }

            var totalStrength = healthyStrength + woundedStrength + leaderStrength;
            return new PartyStrengthReport(totalStrength, healthyStrength, woundedStrength, leaderStrength, troopCount, woundedCount);
        }

        private static float GetUnitStrength(int level)
        {
            if (level < 1)
            {
                level = 1;
            }

            return 1f + level * 0.08f;
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

            return 5f + level * 0.5f;
        }
    }
}
