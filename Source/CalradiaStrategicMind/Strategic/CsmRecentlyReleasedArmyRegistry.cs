using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmRecentlyReleasedArmyRegistry
    {
        private readonly Dictionary<string, CsmReleasedArmyState> _statesByArmy;
        private readonly Dictionary<string, CsmReleasedArmyState> _statesByLeader;

        public CsmRecentlyReleasedArmyRegistry()
        {
            _statesByArmy = new Dictionary<string, CsmReleasedArmyState>();
            _statesByLeader = new Dictionary<string, CsmReleasedArmyState>();
        }

        public void MarkReleased(
            string armyId,
            string armyName,
            string leaderPartyId,
            string leaderPartyName,
            string kingdomName,
            int tick,
            string reason,
            string lastTargetName,
            string releaseType)
        {
            var state = new CsmReleasedArmyState
            {
                ArmyId = armyId,
                ArmyName = string.IsNullOrWhiteSpace(armyName) ? "unknown" : armyName,
                LeaderPartyId = leaderPartyId,
                LeaderPartyName = string.IsNullOrWhiteSpace(leaderPartyName) ? "unknown" : leaderPartyName,
                KingdomName = string.IsNullOrWhiteSpace(kingdomName) ? "unknown" : kingdomName,
                ReleasedTick = tick,
                LastLogTick = -9999,
                ReleaseReason = reason,
                LastTargetName = string.IsNullOrWhiteSpace(lastTargetName) ? "none" : lastTargetName,
                ReleaseType = releaseType
            };

            _statesByArmy[Normalize(armyId)] = state;
            if (!string.IsNullOrWhiteSpace(leaderPartyId))
            {
                _statesByLeader[Normalize(leaderPartyId)] = state;
            }
        }

        public CsmReleasedArmyState GetActiveReleaseForArmy(string armyId, int tick)
        {
            CsmReleasedArmyState state;
            if (!_statesByArmy.TryGetValue(Normalize(armyId), out state))
            {
                return null;
            }

            return IsCooldownActive(state, tick) ? state : null;
        }

        public CsmReleasedArmyState GetActiveReleaseForLeader(MobileParty party, int tick)
        {
            if (party == null)
            {
                return null;
            }

            CsmReleasedArmyState state;
            if (!_statesByLeader.TryGetValue(Normalize(party.StringId), out state))
            {
                return null;
            }

            return IsCooldownActive(state, tick) ? state : null;
        }

        public bool ShouldLog(CsmReleasedArmyState state, int tick)
        {
            if (state == null)
            {
                return false;
            }

            if (tick - state.LastLogTick < GetLogCooldownTicks())
            {
                return false;
            }

            state.LastLogTick = tick;
            return true;
        }

        public int CountActive(int tick)
        {
            var count = 0;
            foreach (var state in _statesByArmy.Values)
            {
                if (IsCooldownActive(state, tick))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsCooldownActive(CsmReleasedArmyState state, int tick)
        {
            return state != null && tick - state.ReleasedTick < GetCooldownTicks();
        }

        private static int GetCooldownTicks()
        {
            return ArmyDirectorSettings.ReleasedArmyCooldownTicks < 0 ? 0 : ArmyDirectorSettings.ReleasedArmyCooldownTicks;
        }

        private static int GetLogCooldownTicks()
        {
            return ArmyDirectorSettings.ReleasedArmyLogCooldownTicks < 1 ? 1 : ArmyDirectorSettings.ReleasedArmyLogCooldownTicks;
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
