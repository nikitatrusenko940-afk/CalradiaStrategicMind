using System.Collections.Generic;
using CalradiaStrategicMind.Settings;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmRecentlyFailedArmyTargetRegistry
    {
        private readonly Dictionary<string, CsmRecentlyFailedArmyTargetState> _states;

        public CsmRecentlyFailedArmyTargetRegistry()
        {
            _states = new Dictionary<string, CsmRecentlyFailedArmyTargetState>();
        }

        public void MarkFailed(
            string armyId,
            string armyName,
            string kingdomName,
            string targetSettlementId,
            string targetSettlementName,
            int tick,
            string reason,
            string failureType)
        {
            if (string.IsNullOrWhiteSpace(targetSettlementId) && string.IsNullOrWhiteSpace(targetSettlementName))
            {
                return;
            }

            var state = new CsmRecentlyFailedArmyTargetState
            {
                ArmyId = armyId ?? string.Empty,
                ArmyName = string.IsNullOrWhiteSpace(armyName) ? "unknown" : armyName,
                KingdomName = string.IsNullOrWhiteSpace(kingdomName) ? "unknown" : kingdomName,
                TargetSettlementId = targetSettlementId ?? string.Empty,
                TargetSettlementName = string.IsNullOrWhiteSpace(targetSettlementName) ? "unknown" : targetSettlementName,
                FailedTick = tick,
                FailureReason = reason ?? string.Empty,
                FailureType = failureType ?? string.Empty
            };

            _states[CreateKey(kingdomName, targetSettlementId, targetSettlementName)] = state;
            if (!string.IsNullOrWhiteSpace(armyId))
            {
                _states[CreateArmyKey(armyId, targetSettlementId, targetSettlementName)] = state;
            }
        }

        public CsmRecentlyFailedArmyTargetState GetActiveFailure(
            string armyId,
            string kingdomName,
            string targetSettlementId,
            string targetSettlementName,
            int tick)
        {
            CsmRecentlyFailedArmyTargetState state;
            if (!string.IsNullOrWhiteSpace(armyId)
                && _states.TryGetValue(CreateArmyKey(armyId, targetSettlementId, targetSettlementName), out state)
                && IsActive(state, tick))
            {
                return state;
            }

            return _states.TryGetValue(CreateKey(kingdomName, targetSettlementId, targetSettlementName), out state) && IsActive(state, tick)
                ? state
                : null;
        }

        private static bool IsActive(CsmRecentlyFailedArmyTargetState state, int tick)
        {
            return state != null && tick - state.FailedTick < GetCooldownTicks();
        }

        private static int GetCooldownTicks()
        {
            return ArmyDirectorSettings.RecentlyFailedTargetCooldownTicks < 0 ? 0 : ArmyDirectorSettings.RecentlyFailedTargetCooldownTicks;
        }

        private static string CreateKey(string kingdomName, string targetSettlementId, string targetSettlementName)
        {
            return Normalize(kingdomName) + "|" + Normalize(string.IsNullOrWhiteSpace(targetSettlementId) ? targetSettlementName : targetSettlementId);
        }

        private static string CreateArmyKey(string armyId, string targetSettlementId, string targetSettlementName)
        {
            return "army|" + Normalize(armyId) + "|" + Normalize(string.IsNullOrWhiteSpace(targetSettlementId) ? targetSettlementName : targetSettlementId);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
