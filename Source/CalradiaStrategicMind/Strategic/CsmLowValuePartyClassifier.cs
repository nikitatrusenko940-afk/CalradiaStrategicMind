using System;
using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmLowValuePartyClassifier
    {
        public CsmLowValuePartyClassification Classify(MobileParty party)
        {
            var result = new CsmLowValuePartyClassification
            {
                IsLowValue = false,
                Category = "None",
                Reason = "Party is not a low-value distraction",
                EstimatedStrength = GetEstimatedStrength(party)
            };

            if (party == null)
            {
                result.Reason = "Party is missing";
                return result;
            }

            if (party.IsMainParty || party == MobileParty.MainParty)
            {
                result.Category = "Player";
                result.Reason = "Player party is never low-value";
                return result;
            }

            if (party.IsLordParty || party.Army != null)
            {
                result.Category = "LordOrArmy";
                result.Reason = "Lord and army parties are not low-value";
                return result;
            }

            if (party.IsCaravan || party.IsVillager || party.IsMilitia || party.IsGarrison)
            {
                result.Category = "CivilianOrSettlement";
                result.Reason = "Civilian, militia, and settlement parties are not low-value";
                return result;
            }

            var identity = (GetPartyId(party) + " " + GetPartyName(party)).ToLowerInvariant();
            if (ContainsAny(identity, "looter", "looters"))
            {
                return LowValue(result, "Looter", "Party identity matches looters");
            }

            if (ContainsAny(identity, "bandit", "bandits", "brigand", "brigands"))
            {
                return LowValue(result, "Bandit", "Party identity matches bandits");
            }

            if (ContainsAny(identity, "deserter", "deserters"))
            {
                return LowValue(result, "Deserter", "Party identity matches deserters");
            }

            if (ContainsAny(identity, "raider", "raiders", "criminal", "outlaw", "outlaws"))
            {
                return LowValue(result, "Criminal", "Party identity matches raiders or criminal parties");
            }

            if (ContainsAny(identity, "hideout", "hide-out", "hide out"))
            {
                return LowValue(result, "HideoutRoamer", "Party identity matches hideout roaming parties");
            }

            if (party.MapFaction != null)
            {
                return LowValue(result, "LowValue", "Minor non-lord non-army party");
            }

            result.Category = "Unknown";
            result.Reason = "Party type is not readable enough to mark low-value";
            return result;
        }

        private static CsmLowValuePartyClassification LowValue(CsmLowValuePartyClassification result, string category, string reason)
        {
            result.IsLowValue = true;
            result.Category = category;
            result.Reason = reason;
            return result;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (var index = 0; index < needles.Length; index++)
            {
                if (value.IndexOf(needles[index], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetEstimatedStrength(MobileParty party)
        {
            if (party?.Party != null)
            {
                return party.Party.EstimatedStrength;
            }

            if (party?.MemberRoster != null)
            {
                return party.MemberRoster.TotalManCount;
            }

            return 0f;
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? string.Empty;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? string.Empty : party.Name.ToString();
        }
    }
}
