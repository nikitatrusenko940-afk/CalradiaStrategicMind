using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmLordPartyRecoveryRegistry
    {
        private readonly Dictionary<string, CsmLordPartyRecoveryAssignment> _activeRecoveries;

        public CsmLordPartyRecoveryRegistry()
        {
            _activeRecoveries = new Dictionary<string, CsmLordPartyRecoveryAssignment>();
        }

        public CsmLordPartyRecoveryAssignment CreateOrUpdate(
            MobileParty party,
            Settlement hub,
            int observationTick,
            string reason)
        {
            if (party == null || hub == null)
            {
                return null;
            }

            var partyId = GetPartyId(party);
            var partyName = GetPartyName(party);
            var key = CreatePartyKey(partyId, partyName);
            CsmLordPartyRecoveryAssignment assignment;
            if (_activeRecoveries.TryGetValue(key, out assignment))
            {
                assignment.EnsureRecoveryHub(hub);
                if (string.IsNullOrWhiteSpace(assignment.TargetSettlementId))
                {
                    assignment.SetCurrentRecruitingTarget(hub);
                }

                assignment.LastObservedTick = observationTick;
                assignment.Status = "Active";
                assignment.Reason = reason;
                return assignment;
            }

            assignment = new CsmLordPartyRecoveryAssignment(
                partyId,
                partyName,
                GetLeaderName(party),
                GetFactionName(party.MapFaction),
                GetSettlementId(hub),
                GetSettlementName(hub),
                GetSettlementType(hub),
                observationTick,
                observationTick,
                -1,
                "Active",
                reason);
            _activeRecoveries[key] = assignment;
            return assignment;
        }

        public CsmLordPartyRecoveryAssignment GetActiveRecoveryForParty(MobileParty party)
        {
            return party == null ? null : GetActiveRecoveryForParty(GetPartyId(party), GetPartyName(party));
        }

        public CsmLordPartyRecoveryAssignment GetActiveRecoveryForParty(string partyId, string partyName)
        {
            CsmLordPartyRecoveryAssignment assignment;
            return _activeRecoveries.TryGetValue(CreatePartyKey(partyId, partyName), out assignment)
                ? assignment
                : null;
        }

        public bool HasActiveRecoveryForParty(MobileParty party)
        {
            return GetActiveRecoveryForParty(party) != null;
        }

        public List<CsmLordPartyRecoveryAssignment> GetActiveRecoveries()
        {
            return new List<CsmLordPartyRecoveryAssignment>(_activeRecoveries.Values);
        }

        public int CountActiveRecoveries()
        {
            return _activeRecoveries.Count;
        }

        public void MarkCommanded(CsmLordPartyRecoveryAssignment assignment, Settlement target, int observationTick, string routeStatus, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.SetCurrentRecruitingTarget(target);
            assignment.LastCommandTick = observationTick;
            assignment.LastObservedTick = observationTick;
            assignment.RecruitingLoopStatus = string.IsNullOrWhiteSpace(routeStatus) ? assignment.RecruitingLoopStatus : routeStatus;
            assignment.Status = "Active";
            assignment.Reason = reason;
            assignment.LastRecruitingLoopReason = reason;
        }

        public void MarkCommanded(CsmLordPartyRecoveryAssignment assignment, int observationTick, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.LastCommandTick = observationTick;
            assignment.LastObservedTick = observationTick;
            assignment.Status = "Active";
            assignment.Reason = reason;
            assignment.LastRecruitingLoopReason = reason;
        }

        public void Close(CsmLordPartyRecoveryAssignment assignment, string status, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.Status = status;
            assignment.Reason = reason;
            _activeRecoveries.Remove(CreatePartyKey(assignment.PartyId, assignment.PartyName));
        }

        private static string CreatePartyKey(string partyId, string partyName)
        {
            return Normalize(string.IsNullOrWhiteSpace(partyId) ? partyName : partyId);
        }

        private static string GetPartyId(MobileParty party)
        {
            return party == null || string.IsNullOrWhiteSpace(party.StringId) ? string.Empty : party.StringId;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party == null || party.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetLeaderName(MobileParty party)
        {
            return party == null || party.LeaderHero == null || party.LeaderHero.Name == null
                ? "none"
                : party.LeaderHero.Name.ToString();
        }

        private static string GetFactionName(TaleWorlds.CampaignSystem.IFaction faction)
        {
            return faction == null || faction.Name == null ? "unknown" : faction.Name.ToString();
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement == null || string.IsNullOrWhiteSpace(settlement.StringId) ? string.Empty : settlement.StringId;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement == null || settlement.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private static string GetSettlementType(Settlement settlement)
        {
            if (settlement == null)
            {
                return "Unknown";
            }

            if (settlement.IsTown)
            {
                return "Town";
            }

            if (settlement.IsCastle)
            {
                return "Castle";
            }

            return settlement.IsVillage ? "Village" : "Unknown";
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    public class CsmLordPartyRecoveryAssignment
    {
        public CsmLordPartyRecoveryAssignment(
            string partyId,
            string partyName,
            string leaderName,
            string kingdomName,
            string targetSettlementId,
            string targetSettlementName,
            string targetSettlementType,
            int assignedTick,
            int lastObservedTick,
            int lastCommandTick,
            string status,
            string reason)
        {
            PartyId = partyId;
            PartyName = partyName;
            LeaderName = leaderName;
            KingdomName = kingdomName;
            TargetSettlementId = targetSettlementId;
            TargetSettlementName = targetSettlementName;
            TargetSettlementType = targetSettlementType;
            RecoveryHubSettlementId = targetSettlementId;
            RecoveryHubSettlementName = targetSettlementName;
            RecoveryHubSettlementType = targetSettlementType;
            CurrentRecruitingTargetId = targetSettlementId;
            CurrentRecruitingTargetName = targetSettlementName;
            CurrentRecruitingTargetType = targetSettlementType;
            RecruitingLoopStatus = "HubSelected";
            AssignedTick = assignedTick;
            LastObservedTick = lastObservedTick;
            LastCommandTick = lastCommandTick;
            LastVillageCommandTick = -1;
            LastHubCommandTick = -1;
            LastArrivedAtVillageTick = -1;
            LastArrivedAtHubTick = -1;
            RecentlyVisitedVillageNames = new List<string>();
            Status = status;
            Reason = reason;
            LastRecruitingLoopReason = reason;
        }

        public string PartyId { get; private set; }
        public string PartyName { get; private set; }
        public string LeaderName { get; set; }
        public string KingdomName { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetSettlementName { get; set; }
        public string TargetSettlementType { get; set; }
        public string RecoveryHubSettlementId { get; set; }
        public string RecoveryHubSettlementName { get; set; }
        public string RecoveryHubSettlementType { get; set; }
        public string CurrentRecruitingTargetId { get; set; }
        public string CurrentRecruitingTargetName { get; set; }
        public string CurrentRecruitingTargetType { get; set; }
        public string RecruitingLoopStatus { get; set; }
        public int VillageVisitsInCurrentLoop { get; set; }
        public int TotalVillageVisits { get; set; }
        public int LastVillageCommandTick { get; set; }
        public int LastHubCommandTick { get; set; }
        public int LastArrivedAtVillageTick { get; set; }
        public int LastArrivedAtHubTick { get; set; }
        public List<string> RecentlyVisitedVillageNames { get; private set; }
        public string LastRecruitingLoopReason { get; set; }
        public int AssignedTick { get; private set; }
        public int LastObservedTick { get; set; }
        public int LastCommandTick { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }

        public void EnsureRecoveryHub(Settlement hub)
        {
            if (!string.IsNullOrWhiteSpace(RecoveryHubSettlementId)
                || !string.IsNullOrWhiteSpace(RecoveryHubSettlementName))
            {
                return;
            }

            SetRecoveryHub(hub);
        }

        public void SetRecoveryHub(Settlement hub)
        {
            RecoveryHubSettlementId = GetSettlementId(hub);
            RecoveryHubSettlementName = GetSettlementName(hub);
            RecoveryHubSettlementType = GetSettlementType(hub);
            if (string.IsNullOrWhiteSpace(TargetSettlementId)
                && string.IsNullOrWhiteSpace(TargetSettlementName))
            {
                SetCurrentRecruitingTarget(hub);
            }
        }

        public void SetCurrentRecruitingTarget(Settlement target)
        {
            TargetSettlementId = GetSettlementId(target);
            TargetSettlementName = GetSettlementName(target);
            TargetSettlementType = GetSettlementType(target);
            CurrentRecruitingTargetId = TargetSettlementId;
            CurrentRecruitingTargetName = TargetSettlementName;
            CurrentRecruitingTargetType = TargetSettlementType;
        }

        public void AddRecentlyVisitedVillage(string villageName)
        {
            if (string.IsNullOrWhiteSpace(villageName))
            {
                return;
            }

            if (RecentlyVisitedVillageNames == null)
            {
                RecentlyVisitedVillageNames = new List<string>();
            }

            for (var index = 0; index < RecentlyVisitedVillageNames.Count; index++)
            {
                if (string.Equals(RecentlyVisitedVillageNames[index], villageName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            RecentlyVisitedVillageNames.Add(villageName);
        }

        public bool HasRecentlyVisitedVillage(string villageName)
        {
            if (string.IsNullOrWhiteSpace(villageName) || RecentlyVisitedVillageNames == null)
            {
                return false;
            }

            for (var index = 0; index < RecentlyVisitedVillageNames.Count; index++)
            {
                if (string.Equals(RecentlyVisitedVillageNames[index], villageName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearRecentlyVisitedVillages()
        {
            if (RecentlyVisitedVillageNames == null)
            {
                RecentlyVisitedVillageNames = new List<string>();
                return;
            }

            RecentlyVisitedVillageNames.Clear();
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement == null || string.IsNullOrWhiteSpace(settlement.StringId) ? string.Empty : settlement.StringId;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement == null || settlement.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private static string GetSettlementType(Settlement settlement)
        {
            if (settlement == null)
            {
                return "Unknown";
            }

            if (settlement.IsTown)
            {
                return "Town";
            }

            if (settlement.IsCastle)
            {
                return "Castle";
            }

            return settlement.IsVillage ? "Village" : "Unknown";
        }
    }
}
