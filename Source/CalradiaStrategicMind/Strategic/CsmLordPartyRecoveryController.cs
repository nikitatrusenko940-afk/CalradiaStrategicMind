using System;
using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmLordPartyRecoveryController
    {
        private const float StrongEnemyNearRecoveryTargetDistance = 35f;
        private const float StrongEnemyRecoveryThreatStrength = 300f;
        private const float RecoveryArrivalDistance = 2.5f;

        private readonly CsmLordPartyRecoveryRegistry _registry;
        private readonly PartyClassifier _partyClassifier;
        private int _currentTick = -1;
        private int _evaluatedLordParties;
        private int _needsRecovery;
        private int _commandsApplied;
        private int _blockedByArmyAssignment;
        private int _blockedByDefenseAssignment;
        private int _createdRecoveries;
        private int _completedRecoveries;
        private int _expiredRecoveries;
        private int _invalidRecoveries;
        private int _villageTargetsSelected;
        private int _villageCommandsApplied;
        private int _hubCommandsApplied;
        private int _villageVisitsCompleted;
        private int _loopsCompleted;
        private int _routeFailures;
        private int _recoveriesCompletedAt95;
        private int _rejectedVillagesTooFarFromParty;

        public CsmLordPartyRecoveryController()
        {
            _registry = new CsmLordPartyRecoveryRegistry();
            _partyClassifier = new PartyClassifier();
        }

        public CsmLordPartyRecoveryRegistry Registry
        {
            get { return _registry; }
        }

        public void Execute(
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            SafeExecutor.Run(
                "Run CSM lord party recovery controller",
                () => ExecuteCore(defenseAssignments, armyAssignments, observationTick));
        }

        private void ExecuteCore(
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            ResetTick(observationTick);
            if (!LordPartyRecoverySettings.EnableLordPartyRecoveryController)
            {
                LogLifecycleSnapshot(observationTick);
                LogSummary(observationTick);
                LogRouteSummary(observationTick);
                return;
            }

            ProcessActiveRecoveries(defenseAssignments, armyAssignments, observationTick);

            var parties = MobileParty.All;
            if (parties == null)
            {
                LogLifecycleSnapshot(observationTick);
                LogSummary(observationTick);
                LogRouteSummary(observationTick);
                return;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (!ShouldEvaluateLordParty(party))
                {
                    continue;
                }

                _evaluatedLordParties++;
                var evaluation = CsmLordPartyRecoveryEvaluator.Evaluate(party);
                LogEvaluation(observationTick, party, evaluation);
                if (!evaluation.NeedsRecovery)
                {
                    continue;
                }

                _needsRecovery++;
                if (_registry.HasActiveRecoveryForParty(party))
                {
                    continue;
                }

                if (HasActiveDefenseAssignment(party, defenseAssignments))
                {
                    _blockedByDefenseAssignment++;
                    continue;
                }

                if (party.Army != null || HasActiveArmyAssignmentForParty(party, armyAssignments))
                {
                    _blockedByArmyAssignment++;
                    continue;
                }

                if (!CanCommandRecoveryParty(party))
                {
                    LogCommand(observationTick, party, null, "Unknown", "Failed", false, GetCommandBlockedReason(party, null));
                    continue;
                }

                var hub = FindRecoveryHub(party);
                if (hub == null)
                {
                    _routeFailures++;
                    LogCommand(observationTick, party, null, "Unknown", "HubSelected", false, "NoTarget");
                    continue;
                }

                LogHub(observationTick, party, hub);
                var assignment = _registry.CreateOrUpdate(party, hub, observationTick, "Weak lord party assigned to recover");
                if (assignment != null)
                {
                    _createdRecoveries++;
                    AdvanceRecoveryRoute(party, assignment, hub, evaluation, observationTick);
                }
            }

            LogLifecycleSnapshot(observationTick);
            LogSummary(observationTick);
            LogRouteSummary(observationTick);
        }

        private void ProcessActiveRecoveries(
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            var recoveries = _registry.GetActiveRecoveries();
            for (var index = 0; index < recoveries.Count; index++)
            {
                var assignment = recoveries[index];
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                var evaluation = party == null ? new CsmLordPartyRecoveryEvaluation() : CsmLordPartyRecoveryEvaluator.Evaluate(party);
                var closeReason = GetRecoveryInterruptionCloseReason(assignment, party, defenseAssignments, armyAssignments);
                if (!string.IsNullOrWhiteSpace(closeReason))
                {
                    CloseRecovery(assignment, GetRecoveryCloseStatus(closeReason), closeReason, observationTick, party, evaluation);
                    continue;
                }

                if (evaluation.HealthyRatio >= GetTargetHealthyPartyRatioForRecovery())
                {
                    CloseRecovery(assignment, "Completed", "Recovery completed because party reached target healthy ratio", observationTick, party, evaluation);
                    _recoveriesCompletedAt95++;
                    continue;
                }

                var hub = FindRecoveryHubForAssignment(assignment);
                if (hub == null || !IsSafeFriendlyRecoveryHub(party, hub))
                {
                    var replacementHub = FindRecoveryHub(party);
                    if (replacementHub == null)
                    {
                        CloseRecovery(assignment, "Expired", "Recovery hub no longer safe and no replacement hub found", observationTick, party, evaluation);
                        continue;
                    }

                    assignment.SetRecoveryHub(replacementHub);
                    assignment.SetCurrentRecruitingTarget(replacementHub);
                    assignment.RecruitingLoopStatus = "HubSelected";
                    assignment.VillageVisitsInCurrentLoop = 0;
                    assignment.LastArrivedAtHubTick = -1;
                    assignment.LastArrivedAtVillageTick = -1;
                    assignment.ClearRecentlyVisitedVillages();
                    assignment.LastRecruitingLoopReason = "Recovery hub replaced because previous hub was unsafe";
                    LogHub(observationTick, party, replacementHub);
                    hub = replacementHub;
                }

                assignment.LastObservedTick = observationTick;
                AdvanceRecoveryRoute(party, assignment, hub, evaluation, observationTick);
                LogRecoveryLifecycle(observationTick, party, assignment, "Active", evaluation.HealthyRatio, "Recovery assignment remains active");
            }
        }

        private string GetRecoveryInterruptionCloseReason(
            CsmLordPartyRecoveryAssignment assignment,
            MobileParty party,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments)
        {
            if (assignment == null)
            {
                return "Recovery assignment missing";
            }

            if (party == null || !party.IsActive || party.IsDisbanding || party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return "Recovery party invalid";
            }

            if (party.Army != null || HasActiveArmyAssignmentForParty(party, armyAssignments))
            {
                return "Recovery completed because party joined real army or CSM army assignment";
            }

            if (HasActiveDefenseAssignment(party, defenseAssignments))
            {
                return "Recovery completed because party got active defense assignment";
            }

            return null;
        }

        private static string GetRecoveryCloseStatus(string reason)
        {
            if (reason != null && reason.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Invalid";
            }

            if (reason != null && reason.IndexOf("no replacement hub", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Expired";
            }

            return "Completed";
        }

        private void CloseRecovery(
            CsmLordPartyRecoveryAssignment assignment,
            string status,
            string reason,
            int observationTick,
            MobileParty party,
            CsmLordPartyRecoveryEvaluation evaluation)
        {
            _registry.Close(assignment, status, reason);
            RecordClosedRecovery(status);
            LogRecoveryLifecycle(
                observationTick,
                party,
                assignment,
                status,
                evaluation == null ? 0f : evaluation.HealthyRatio,
                reason);
        }

        private void RecordClosedRecovery(string status)
        {
            if (NamesEqual(status, "Completed"))
            {
                _completedRecoveries++;
                return;
            }

            if (NamesEqual(status, "Expired"))
            {
                _expiredRecoveries++;
                return;
            }

            if (NamesEqual(status, "Invalid"))
            {
                _invalidRecoveries++;
            }
        }

        private void AdvanceRecoveryRoute(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            CsmLordPartyRecoveryEvaluation evaluation,
            int observationTick)
        {
            if (party == null || assignment == null || hub == null)
            {
                _routeFailures++;
                LogCommand(observationTick, party, null, "Unknown", GetRouteStatus(assignment), false, "NoTarget");
                return;
            }

            if (!CanCommandRecoveryParty(party))
            {
                LogCommand(observationTick, party, GetCurrentRouteTarget(assignment, hub), GetSettlementType(GetCurrentRouteTarget(assignment, hub)), GetRouteStatus(assignment), false, GetCommandBlockedReason(party, GetCurrentRouteTarget(assignment, hub)));
                return;
            }

            if (!LordPartyRecoverySettings.EnableRecoveryRecruitingLoop)
            {
                assignment.RecruitingLoopStatus = "ReturningToHub";
                CommandReturnToHub(party, assignment, hub, observationTick, "Recovery recruiting loop disabled; returning to hub");
                return;
            }

            var status = GetRouteStatus(assignment);
            if (NamesEqual(status, "MovingToVillage"))
            {
                HandleMovingToVillage(party, assignment, hub, observationTick);
                return;
            }

            if (NamesEqual(status, "VisitingVillage"))
            {
                HandleVisitingVillage(party, assignment, hub, observationTick);
                return;
            }

            if (NamesEqual(status, "ReturningToHub"))
            {
                HandleReturningToHub(party, assignment, hub, observationTick);
                return;
            }

            if (NamesEqual(status, "VisitingHub"))
            {
                HandleVisitingHub(party, assignment, hub, evaluation, observationTick);
                return;
            }

            assignment.RecruitingLoopStatus = "HubSelected";
            CommandNextVillageOrReturnToHub(party, assignment, hub, observationTick, "Recovery hub selected; choosing first village target");
        }

        private void HandleMovingToVillage(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            int observationTick)
        {
            var village = FindCurrentRouteTarget(assignment);
            if (village == null || !IsSafeFriendlyRecoveryVillage(party, village, hub))
            {
                assignment.LastRecruitingLoopReason = "Current village target missing or unsafe";
                CommandNextVillageOrReturnToHub(party, assignment, hub, observationTick, "Current village target missing or unsafe");
                return;
            }

            if (IsPartyAtSettlement(party, village))
            {
                assignment.RecruitingLoopStatus = "VisitingVillage";
                assignment.LastArrivedAtVillageTick = observationTick;
                assignment.LastRecruitingLoopReason = "Party arrived at recovery village";
                return;
            }

            if (party.TargetSettlement != village)
            {
                TryCommandRecovery(party, village, assignment, observationTick, "MovingToVillage", true, "Recommanding party to recovery village target");
            }
        }

        private void HandleVisitingVillage(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            int observationTick)
        {
            var village = FindCurrentRouteTarget(assignment);
            if (village == null || !IsSafeFriendlyRecoveryVillage(party, village, hub))
            {
                assignment.LastRecruitingLoopReason = "Visited village target became unavailable";
                CommandNextVillageOrReturnToHub(party, assignment, hub, observationTick, "Visited village target became unavailable");
                return;
            }

            if (!IsPartyAtSettlement(party, village))
            {
                assignment.RecruitingLoopStatus = "MovingToVillage";
                if (party.TargetSettlement != village)
                {
                    TryCommandRecovery(party, village, assignment, observationTick, "MovingToVillage", true, "Party left village before visit hold completed");
                }

                return;
            }

            if (assignment.LastArrivedAtVillageTick < 0)
            {
                assignment.LastArrivedAtVillageTick = observationTick;
                return;
            }

            if (observationTick - assignment.LastArrivedAtVillageTick < GetRecoveryVillageVisitHoldTicks())
            {
                return;
            }

            assignment.VillageVisitsInCurrentLoop++;
            assignment.TotalVillageVisits++;
            assignment.LastArrivedAtVillageTick = -1;
            assignment.AddRecentlyVisitedVillage(GetSettlementName(village));
            assignment.LastRecruitingLoopReason = "Recovery village visit completed";
            _villageVisitsCompleted++;

            if (assignment.VillageVisitsInCurrentLoop >= GetRecoveryVillagesPerLoop())
            {
                CommandReturnToHub(party, assignment, hub, observationTick, "Recovery village loop quota reached; returning to hub");
                return;
            }

            CommandNextVillageOrReturnToHub(party, assignment, hub, observationTick, "Recovery village visit completed; selecting next village");
        }

        private void HandleReturningToHub(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            int observationTick)
        {
            if (IsPartyAtSettlement(party, hub))
            {
                assignment.RecruitingLoopStatus = "VisitingHub";
                assignment.LastArrivedAtHubTick = observationTick;
                assignment.SetCurrentRecruitingTarget(hub);
                assignment.LastRecruitingLoopReason = "Party arrived at recovery hub";
                return;
            }

            if (party.TargetSettlement != hub)
            {
                CommandReturnToHub(party, assignment, hub, observationTick, "Recommanding party to recovery hub");
            }
        }

        private void HandleVisitingHub(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            CsmLordPartyRecoveryEvaluation evaluation,
            int observationTick)
        {
            if (!IsPartyAtSettlement(party, hub))
            {
                assignment.RecruitingLoopStatus = "ReturningToHub";
                CommandReturnToHub(party, assignment, hub, observationTick, "Party left recovery hub before visit hold completed");
                return;
            }

            if (assignment.LastArrivedAtHubTick < 0)
            {
                assignment.LastArrivedAtHubTick = observationTick;
                return;
            }

            if (observationTick - assignment.LastArrivedAtHubTick < GetRecoveryHubVisitHoldTicks())
            {
                return;
            }

            if (evaluation != null && evaluation.HealthyRatio >= GetTargetHealthyPartyRatioForRecovery())
            {
                return;
            }

            if (assignment.VillageVisitsInCurrentLoop > 0)
            {
                _loopsCompleted++;
            }

            assignment.VillageVisitsInCurrentLoop = 0;
            assignment.LastArrivedAtHubTick = -1;
            assignment.ClearRecentlyVisitedVillages();
            assignment.LastRecruitingLoopReason = "Recovery hub visit completed; starting another village loop";
            CommandNextVillageOrReturnToHub(party, assignment, hub, observationTick, "Recovery hub visit completed; starting another village loop");
        }

        private void CommandNextVillageOrReturnToHub(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            int observationTick,
            string reason)
        {
            var village = FindRecoveryVillageTarget(party, hub, assignment);
            if (village != null)
            {
                LogVillageTarget(observationTick, party, hub, village, assignment);
                TryCommandRecovery(party, village, assignment, observationTick, "MovingToVillage", true, reason);
                return;
            }

            _routeFailures++;
            CommandReturnToHub(
                party,
                assignment,
                hub,
                observationTick,
                "No safe nearby recovery village available; returning to hub",
                "NoNearbyVillageAvailable");
        }

        private void CommandReturnToHub(
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            Settlement hub,
            int observationTick,
            string reason,
            string commandBlockedReasonOverride = null)
        {
            if (hub == null)
            {
                _routeFailures++;
                LogCommand(observationTick, party, null, "Town/Castle", "ReturningToHub", false, commandBlockedReasonOverride ?? "NoTarget");
                return;
            }

            var result = TryCommandRecovery(party, hub, assignment, observationTick, "ReturningToHub", false, reason, commandBlockedReasonOverride);
            if (result.BlockedReason == "AlreadyAtTarget")
            {
                assignment.RecruitingLoopStatus = "VisitingHub";
                assignment.LastArrivedAtHubTick = observationTick;
                assignment.LastRecruitingLoopReason = "Party already at recovery hub";
            }
        }

        private RecoveryCommandResult TryCommandRecovery(
            MobileParty party,
            Settlement target,
            CsmLordPartyRecoveryAssignment assignment,
            int observationTick,
            string routeStatus,
            bool villageCommand,
            string reason,
            string commandBlockedReasonOverride = null)
        {
            if (target == null || assignment == null)
            {
                _routeFailures++;
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? "NoTarget");
                return new RecoveryCommandResult(false, "NoTarget");
            }

            assignment.SetCurrentRecruitingTarget(target);
            assignment.RecruitingLoopStatus = routeStatus;
            assignment.LastRecruitingLoopReason = reason;

            if (!CanCommandRecoveryParty(party))
            {
                var blockedReason = GetCommandBlockedReason(party, target);
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? blockedReason);
                return new RecoveryCommandResult(false, blockedReason);
            }

            var safeTarget = villageCommand
                ? IsSafeFriendlyRecoveryVillage(party, target, FindRecoveryHubForAssignment(assignment))
                : IsSafeFriendlyRecoveryHub(party, target);
            if (!safeTarget)
            {
                _routeFailures++;
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? "UnsafeTarget");
                return new RecoveryCommandResult(false, "UnsafeTarget");
            }

            if (IsPartyAtSettlement(party, target))
            {
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? "AlreadyAtTarget");
                return new RecoveryCommandResult(false, "AlreadyAtTarget");
            }

            if (party.TargetSettlement == target)
            {
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? "AlreadyMovingToTarget");
                return new RecoveryCommandResult(false, "AlreadyMovingToTarget");
            }

            if (IsCommandOnCooldown(assignment, observationTick, villageCommand))
            {
                LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, false, commandBlockedReasonOverride ?? "Cooldown");
                return new RecoveryCommandResult(false, "Cooldown");
            }

            var applied = SafeExecutor.Run(
                "Command weak lord party recovery recruiting route",
                () =>
                {
                    party.SetMoveGoToSettlement(target, party.NavigationCapability, false);
                    return true;
                },
                false);

            if (applied)
            {
                _registry.MarkCommanded(assignment, target, observationTick, routeStatus, reason);
                if (villageCommand)
                {
                    assignment.LastVillageCommandTick = observationTick;
                    _villageCommandsApplied++;
                }
                else
                {
                    assignment.LastHubCommandTick = observationTick;
                    _hubCommandsApplied++;
                }

                _commandsApplied++;
            }
            else
            {
                _routeFailures++;
            }

            LogCommand(observationTick, party, target, GetSettlementType(target), routeStatus, applied, commandBlockedReasonOverride ?? (applied ? "None" : "Unknown"));
            return new RecoveryCommandResult(applied, applied ? "None" : "Unknown");
        }

        private bool IsCommandOnCooldown(CsmLordPartyRecoveryAssignment assignment, int observationTick, bool villageCommand)
        {
            if (assignment == null)
            {
                return false;
            }

            var lastTick = villageCommand ? assignment.LastVillageCommandTick : assignment.LastHubCommandTick;
            var cooldown = villageCommand ? GetRecoveryVillageCommandCooldownTicks() : GetRecoveryHubReturnCooldownTicks();
            if (lastTick < 0)
            {
                return false;
            }

            return observationTick - lastTick < cooldown;
        }

        private Settlement FindRecoveryHub(MobileParty party)
        {
            var settlements = Settlement.All;
            if (party == null || settlements == null)
            {
                return null;
            }

            Settlement bestTown = null;
            var bestTownDistance = float.MaxValue;
            Settlement bestCastle = null;
            var bestCastleDistance = float.MaxValue;
            Settlement fallback = null;
            var fallbackDistance = float.MaxValue;
            var maxDistance = GetMaxRecoveryDistanceToFriendlySettlement();
            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                {
                    continue;
                }

                if (settlement.IsCastle && !LordPartyRecoverySettings.AllowCastleRecoveryHubFallback)
                {
                    continue;
                }

                if (!IsSafeFriendlyRecoveryHub(party, settlement))
                {
                    continue;
                }

                var distance = party.Position.Distance(settlement.Position);
                if (distance < fallbackDistance)
                {
                    fallback = settlement;
                    fallbackDistance = distance;
                }

                if (maxDistance > 0 && distance > maxDistance)
                {
                    continue;
                }

                if (settlement.IsTown && distance < bestTownDistance)
                {
                    bestTown = settlement;
                    bestTownDistance = distance;
                }
                else if (settlement.IsCastle && distance < bestCastleDistance)
                {
                    bestCastle = settlement;
                    bestCastleDistance = distance;
                }
            }

            if (LordPartyRecoverySettings.PreferTownRecoveryHub && bestTown != null)
            {
                return bestTown;
            }

            if (!LordPartyRecoverySettings.PreferTownRecoveryHub)
            {
                if (bestTown != null && bestTownDistance <= bestCastleDistance)
                {
                    return bestTown;
                }

                if (bestCastle != null)
                {
                    return bestCastle;
                }
            }

            if (bestTown != null)
            {
                return bestTown;
            }

            if (bestCastle != null)
            {
                return bestCastle;
            }

            return fallback;
        }

        private Settlement FindRecoveryVillageTarget(
            MobileParty party,
            Settlement hub,
            CsmLordPartyRecoveryAssignment assignment)
        {
            var settlements = Settlement.All;
            if (party == null || hub == null || settlements == null)
            {
                return null;
            }

            Settlement best = null;
            var bestScore = float.MaxValue;
            var maxHubDistance = GetMaxRecoveryVillageSearchDistanceFromHub();
            var maxPartyDistance = GetMaxRecoveryVillageDistanceFromParty();
            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null || !settlement.IsVillage)
                {
                    continue;
                }

                if (!IsSafeFriendlyRecoveryVillage(party, settlement, hub))
                {
                    continue;
                }

                if (LordPartyRecoverySettings.AvoidRecentlyVisitedRecoveryVillages
                    && assignment != null
                    && assignment.HasRecentlyVisitedVillage(GetSettlementName(settlement)))
                {
                    continue;
                }

                var distanceFromHub = hub.Position.Distance(settlement.Position);
                if (maxHubDistance > 0 && distanceFromHub > maxHubDistance)
                {
                    continue;
                }

                var distanceFromParty = party.Position.Distance(settlement.Position);
                if (maxPartyDistance > 0 && distanceFromParty > maxPartyDistance)
                {
                    _rejectedVillagesTooFarFromParty++;
                    continue;
                }

                var score = distanceFromParty + distanceFromHub * 0.25f;
                if (score < bestScore)
                {
                    best = settlement;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                _villageTargetsSelected++;
            }

            return best;
        }

        private static bool IsSafeFriendlyRecoveryHub(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null || !settlement.IsActive)
            {
                return false;
            }

            if (!settlement.IsTown && !settlement.IsCastle)
            {
                return false;
            }

            if (settlement.MapFaction == null || party.MapFaction == null || settlement.MapFaction != party.MapFaction)
            {
                return false;
            }

            if (settlement.SiegeEvent != null)
            {
                return false;
            }

            return !HasStrongEnemyNearSettlement(settlement, party.MapFaction);
        }

        private static bool IsSafeFriendlyRecoveryVillage(MobileParty party, Settlement village, Settlement hub)
        {
            if (party == null || village == null || hub == null || !village.IsActive || !village.IsVillage)
            {
                return false;
            }

            if (village.MapFaction == null || party.MapFaction == null || village.MapFaction != party.MapFaction)
            {
                return false;
            }

            if (village.SiegeEvent != null)
            {
                return false;
            }

            if (HasStrongEnemyNearSettlement(village, party.MapFaction))
            {
                return false;
            }

            var maxHubDistance = GetMaxRecoveryVillageSearchDistanceFromHub();
            return maxHubDistance <= 0 || hub.Position.Distance(village.Position) <= maxHubDistance;
        }

        private static bool HasStrongEnemyNearSettlement(Settlement settlement, IFaction friendlyFaction)
        {
            var parties = MobileParty.All;
            if (settlement == null || friendlyFaction == null || parties == null)
            {
                return false;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null || !party.IsActive || party.IsDisbanding || party.MapEvent != null || party.MapFaction == null)
                {
                    continue;
                }

                if (!friendlyFaction.IsAtWarWith(party.MapFaction))
                {
                    continue;
                }

                if (party.Position.Distance(settlement.Position) > StrongEnemyNearRecoveryTargetDistance)
                {
                    continue;
                }

                var strength = party.Party == null ? 0f : party.Party.EstimatedStrength;
                if (strength >= StrongEnemyRecoveryThreatStrength)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldEvaluateLordParty(MobileParty party)
        {
            if (party == null || !party.IsActive || party.IsDisbanding || party.IsMainParty)
            {
                return false;
            }

            if (party.MapFaction == null || party.LeaderHero == null || party.MemberRoster == null)
            {
                return false;
            }

            return _partyClassifier.GetCategory(party) == PartyObservationCategory.LordParty;
        }

        private static bool CanCommandRecoveryParty(MobileParty party)
        {
            if (party == null || !party.IsActive || party.IsDisbanding || party.IsMainParty)
            {
                return false;
            }

            if (party.Army != null || party.MapEvent != null || party.BesiegedSettlement != null)
            {
                return false;
            }

            return party.LeaderHero != null && party.MemberRoster != null && party.MemberRoster.TotalManCount > 0;
        }

        private static string GetCommandBlockedReason(MobileParty party, Settlement target)
        {
            if (target == null)
            {
                return "NoTarget";
            }

            if (party == null || !party.IsActive || party.IsDisbanding || party.IsMainParty)
            {
                return "Unknown";
            }

            if (party.Army != null)
            {
                return "InArmy";
            }

            if (party.MapEvent != null || party.BesiegedSettlement != null)
            {
                return "InBattle";
            }

            return "Unknown";
        }

        private static bool HasActiveDefenseAssignment(MobileParty party, CsmDefenseAssignmentRegistry defenseAssignments)
        {
            return party != null
                && defenseAssignments != null
                && defenseAssignments.HasActiveAssignmentForParty(GetPartyId(party), GetPartyName(party));
        }

        private static bool HasActiveArmyAssignmentForParty(MobileParty party, CsmArmyAssignmentRegistry armyAssignments)
        {
            if (party == null || armyAssignments == null)
            {
                return false;
            }

            var assignments = armyAssignments.GetActiveAssignments();
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                if (assignment == null)
                {
                    continue;
                }

                if (NamesEqual(assignment.LeaderPartyId, GetPartyId(party))
                    || NamesEqual(assignment.LeaderPartyName, GetPartyName(party)))
                {
                    return true;
                }
            }

            return false;
        }

        private Settlement FindRecoveryHubForAssignment(CsmLordPartyRecoveryAssignment assignment)
        {
            if (assignment == null)
            {
                return null;
            }

            var hub = FindSettlementByIdOrName(assignment.RecoveryHubSettlementId, assignment.RecoveryHubSettlementName);
            if (hub != null)
            {
                assignment.EnsureRecoveryHub(hub);
                return hub;
            }

            var legacyTarget = FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
            if (legacyTarget != null && (legacyTarget.IsTown || legacyTarget.IsCastle))
            {
                assignment.SetRecoveryHub(legacyTarget);
                return legacyTarget;
            }

            return null;
        }

        private Settlement FindCurrentRouteTarget(CsmLordPartyRecoveryAssignment assignment)
        {
            if (assignment == null)
            {
                return null;
            }

            var target = FindSettlementByIdOrName(assignment.CurrentRecruitingTargetId, assignment.CurrentRecruitingTargetName);
            if (target != null)
            {
                return target;
            }

            return FindSettlementByIdOrName(assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private Settlement GetCurrentRouteTarget(CsmLordPartyRecoveryAssignment assignment, Settlement fallback)
        {
            var target = FindCurrentRouteTarget(assignment);
            return target ?? fallback;
        }

        private static bool IsPartyAtSettlement(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null)
            {
                return false;
            }

            if (party.CurrentSettlement == settlement)
            {
                return true;
            }

            return party.Position.Distance(settlement.Position) <= RecoveryArrivalDistance;
        }

        private void LogEvaluation(int observationTick, MobileParty party, CsmLordPartyRecoveryEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed lord party recovery evaluation: tick={observationTick}, party='{GetPartyName(party)}', leader='{GetLeaderName(party)}', kingdom='{GetFactionName(party == null ? null : party.MapFaction)}', healthyMen={evaluation.HealthyManCount}, totalMen={evaluation.TotalManCount}, woundedCount={evaluation.WoundedCount}, partyLimit={evaluation.PartyLimit}, healthyRatio={evaluation.HealthyRatio:0.00}, totalRatio={evaluation.TotalRatio:0.00}, needsRecovery={evaluation.NeedsRecovery}, partyLimitFallbackUsed={evaluation.PartyLimitFallbackUsed}, reason='Lord party recovery evaluation'");
        }

        private static void LogHub(int observationTick, MobileParty party, Settlement hub)
        {
            CsmLogger.Info(
                $"Observed lord party recovery hub: tick={observationTick}, party='{GetPartyName(party)}', hub='{GetSettlementName(hub)}', hubType='{GetSettlementType(hub)}', distance={(party == null || hub == null ? 0f : party.Position.Distance(hub.Position)):0.00}, reason='Selected safe friendly recovery hub'");
        }

        private static void LogVillageTarget(
            int observationTick,
            MobileParty party,
            Settlement hub,
            Settlement village,
            CsmLordPartyRecoveryAssignment assignment)
        {
            CsmLogger.Info(
                $"Observed lord party recovery village target: tick={observationTick}, party='{GetPartyName(party)}', hub='{GetSettlementName(hub)}', selectedVillage='{GetSettlementName(village)}', distanceFromParty={(party == null || village == null ? 0f : party.Position.Distance(village.Position)):0.00}, distanceFromHub={(hub == null || village == null ? 0f : hub.Position.Distance(village.Position)):0.00}, villageVisitsInCurrentLoop={(assignment == null ? 0 : assignment.VillageVisitsInCurrentLoop)}, totalVillageVisits={(assignment == null ? 0 : assignment.TotalVillageVisits)}, reason='Selected safe village recruiting target'");
        }

        private static void LogCommand(
            int observationTick,
            MobileParty party,
            Settlement target,
            string targetType,
            string routeStatus,
            bool commandApplied,
            string commandBlockedReason)
        {
            CsmLogger.Info(
                $"Observed lord party recovery command: tick={observationTick}, party='{GetPartyName(party)}', target='{GetSettlementName(target)}', targetType='{targetType}', routeStatus='{routeStatus}', commandApplied={commandApplied}, commandBlockedReason='{commandBlockedReason}', reason='Weak lord party recovery recruiting loop command'");
        }

        private void LogLifecycleSnapshot(int observationTick)
        {
            CsmLogger.Info(
                $"Observed lord party recovery lifecycle snapshot: tick={observationTick}, activeRecoveries={_registry.CountActiveRecoveries()}, created={_createdRecoveries}, completed={_completedRecoveries}, expired={_expiredRecoveries}, invalid={_invalidRecoveries}, reason='Lord party recovery lifecycle snapshot'");
        }

        private void LogRecoveryLifecycle(
            int observationTick,
            MobileParty party,
            CsmLordPartyRecoveryAssignment assignment,
            string status,
            float healthyRatio,
            string reason)
        {
            CsmLogger.Info(
                $"Observed lord party recovery lifecycle: tick={observationTick}, party='{GetPartyName(party)}', status='{status}', healthyRatio={healthyRatio:0.00}, targetRatio={GetTargetHealthyPartyRatioForRecovery():0.00}, routeStatus='{GetRouteStatus(assignment)}', reason='{reason}'");
        }

        private void LogSummary(int observationTick)
        {
            CsmLogger.Info(
                $"Observed lord party recovery summary: tick={observationTick}, evaluatedLordParties={_evaluatedLordParties}, needsRecovery={_needsRecovery}, activeRecoveries={_registry.CountActiveRecoveries()}, commandsApplied={_commandsApplied}, blockedByArmyAssignment={_blockedByArmyAssignment}, blockedByDefenseAssignment={_blockedByDefenseAssignment}, completedRecoveries={_completedRecoveries}, reason='Lord party recovery controller summary'");
        }

        private void LogRouteSummary(int observationTick)
        {
            var activeRecoveries = _registry.GetActiveRecoveries();
            var activeRecruitingLoops = 0;
            var ratioCount = 0;
            var ratioSum = 0f;
            var lowestRatio = float.MaxValue;
            for (var index = 0; index < activeRecoveries.Count; index++)
            {
                var assignment = activeRecoveries[index];
                if (assignment == null)
                {
                    continue;
                }

                if (!NamesEqual(GetRouteStatus(assignment), "Completed") && !NamesEqual(GetRouteStatus(assignment), "Failed"))
                {
                    activeRecruitingLoops++;
                }

                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                if (party == null)
                {
                    continue;
                }

                var evaluation = CsmLordPartyRecoveryEvaluator.Evaluate(party);
                ratioSum += evaluation.HealthyRatio;
                if (evaluation.HealthyRatio < lowestRatio)
                {
                    lowestRatio = evaluation.HealthyRatio;
                }

                ratioCount++;
            }

            var averageRatio = ratioCount <= 0 ? 0f : ratioSum / ratioCount;
            if (ratioCount <= 0)
            {
                lowestRatio = 0f;
            }

            CsmLogger.Info(
                $"Observed lord party recovery route summary: tick={observationTick}, activeRecoveries={_registry.CountActiveRecoveries()}, activeRecruitingLoops={activeRecruitingLoops}, villageTargetsSelected={_villageTargetsSelected}, villageCommandsApplied={_villageCommandsApplied}, hubCommandsApplied={_hubCommandsApplied}, villageVisitsCompleted={_villageVisitsCompleted}, loopsCompleted={_loopsCompleted}, routeFailures={_routeFailures}, recoveriesCompletedAt95={_recoveriesCompletedAt95}, rejectedVillagesTooFarFromParty={_rejectedVillagesTooFarFromParty}, averageHealthyRatioOfRecoveringParties={averageRatio:0.00}, lowestHealthyRatioOfRecoveringParties={lowestRatio:0.00}, reason='Lord party recovery recruiting route summary'");
        }

        private void ResetTick(int observationTick)
        {
            if (_currentTick == observationTick)
            {
                return;
            }

            _currentTick = observationTick;
            _evaluatedLordParties = 0;
            _needsRecovery = 0;
            _commandsApplied = 0;
            _blockedByArmyAssignment = 0;
            _blockedByDefenseAssignment = 0;
            _createdRecoveries = 0;
            _completedRecoveries = 0;
            _expiredRecoveries = 0;
            _invalidRecoveries = 0;
            _villageTargetsSelected = 0;
            _villageCommandsApplied = 0;
            _hubCommandsApplied = 0;
            _villageVisitsCompleted = 0;
            _loopsCompleted = 0;
            _routeFailures = 0;
            _recoveriesCompletedAt95 = 0;
            _rejectedVillagesTooFarFromParty = 0;
        }

        private static string GetRouteStatus(CsmLordPartyRecoveryAssignment assignment)
        {
            return assignment == null || string.IsNullOrWhiteSpace(assignment.RecruitingLoopStatus)
                ? "HubSelected"
                : assignment.RecruitingLoopStatus;
        }

        private static int GetRecoveryAssignmentCooldownTicks()
        {
            return LordPartyRecoverySettings.RecoveryAssignmentCooldownTicks < 0
                ? 0
                : LordPartyRecoverySettings.RecoveryAssignmentCooldownTicks;
        }

        private static int GetMaxRecoveryDistanceToFriendlySettlement()
        {
            return LordPartyRecoverySettings.MaxRecoveryDistanceToFriendlySettlement < 0
                ? 0
                : LordPartyRecoverySettings.MaxRecoveryDistanceToFriendlySettlement;
        }

        private static int GetMaxRecoveryVillageSearchDistanceFromHub()
        {
            return LordPartyRecoverySettings.MaxRecoveryVillageSearchDistanceFromHub < 0
                ? 0
                : LordPartyRecoverySettings.MaxRecoveryVillageSearchDistanceFromHub;
        }

        private static int GetMaxRecoveryVillageDistanceFromParty()
        {
            return LordPartyRecoverySettings.MaxRecoveryVillageDistanceFromParty < 0
                ? 0
                : LordPartyRecoverySettings.MaxRecoveryVillageDistanceFromParty;
        }

        private static int GetRecoveryVillageCommandCooldownTicks()
        {
            return LordPartyRecoverySettings.RecoveryVillageCommandCooldownTicks < 0
                ? GetRecoveryAssignmentCooldownTicks()
                : LordPartyRecoverySettings.RecoveryVillageCommandCooldownTicks;
        }

        private static int GetRecoveryHubReturnCooldownTicks()
        {
            return LordPartyRecoverySettings.RecoveryHubReturnCooldownTicks < 0
                ? GetRecoveryAssignmentCooldownTicks()
                : LordPartyRecoverySettings.RecoveryHubReturnCooldownTicks;
        }

        private static int GetRecoveryHubVisitHoldTicks()
        {
            return LordPartyRecoverySettings.RecoveryHubVisitHoldTicks < 0
                ? 0
                : LordPartyRecoverySettings.RecoveryHubVisitHoldTicks;
        }

        private static int GetRecoveryVillageVisitHoldTicks()
        {
            return LordPartyRecoverySettings.RecoveryVillageVisitHoldTicks < 0
                ? 0
                : LordPartyRecoverySettings.RecoveryVillageVisitHoldTicks;
        }

        private static int GetRecoveryVillagesPerLoop()
        {
            var configured = LordPartyRecoverySettings.RecoveryVillagesPerLoop <= 0
                ? 4
                : LordPartyRecoverySettings.RecoveryVillagesPerLoop;
            var minimum = LordPartyRecoverySettings.MinRecoveryVillagesPerLoop <= 0
                ? 0
                : LordPartyRecoverySettings.MinRecoveryVillagesPerLoop;
            return configured < minimum ? minimum : configured;
        }

        private static float GetTargetHealthyPartyRatioForRecovery()
        {
            return LordPartyRecoverySettings.TargetHealthyPartyRatioForRecovery <= 0f
                ? 0.95f
                : LordPartyRecoverySettings.TargetHealthyPartyRatioForRecovery;
        }

        private static MobileParty FindPartyByIdOrName(string partyId, string partyName)
        {
            var parties = MobileParty.All;
            if (parties == null)
            {
                return null;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(partyId) && NamesEqual(GetPartyId(party), partyId))
                {
                    return party;
                }

                if (!string.IsNullOrWhiteSpace(partyName) && NamesEqual(GetPartyName(party), partyName))
                {
                    return party;
                }
            }

            return null;
        }

        private static Settlement FindSettlementByIdOrName(string settlementId, string settlementName)
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                return null;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(settlementId) && NamesEqual(settlement.StringId, settlementId))
                {
                    return settlement;
                }

                if (!string.IsNullOrWhiteSpace(settlementName) && NamesEqual(GetSettlementName(settlement), settlementName))
                {
                    return settlement;
                }
            }

            return null;
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

        private static string GetFactionName(IFaction faction)
        {
            return faction == null || faction.Name == null ? "unknown" : faction.Name.ToString();
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

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private struct RecoveryCommandResult
        {
            public RecoveryCommandResult(bool applied, string blockedReason)
            {
                Applied = applied;
                BlockedReason = blockedReason;
            }

            public bool Applied { get; private set; }
            public string BlockedReason { get; private set; }
        }
    }

    public static class CsmLordPartyRecoveryEvaluator
    {
        public static CsmLordPartyRecoveryEvaluation Evaluate(MobileParty party)
        {
            if (party == null)
            {
                return new CsmLordPartyRecoveryEvaluation();
            }

            var totalMen = party.MemberRoster == null ? 0 : party.MemberRoster.TotalManCount;
            var woundedCount = GetWoundedCount(party);
            var healthyMen = totalMen - woundedCount;
            if (healthyMen < 0)
            {
                healthyMen = 0;
            }

            var fallbackUsed = false;
            var partyLimit = GetPartyLimit(party);
            if (partyLimit <= 0)
            {
                fallbackUsed = true;
                partyLimit = totalMen > 40 ? totalMen : 40;
            }

            var healthyRatio = partyLimit <= 0 ? 0f : (float)healthyMen / partyLimit;
            var totalRatio = partyLimit <= 0 ? 0f : (float)totalMen / partyLimit;
            return new CsmLordPartyRecoveryEvaluation
            {
                HealthyManCount = healthyMen,
                TotalManCount = totalMen,
                WoundedCount = woundedCount,
                PartyLimit = partyLimit,
                HealthyRatio = healthyRatio,
                TotalRatio = totalRatio,
                NeedsRecovery = healthyRatio < GetMinimumHealthyPartyRatioForOperations(),
                PartyLimitFallbackUsed = fallbackUsed
            };
        }

        private static int GetWoundedCount(MobileParty party)
        {
            if (party == null || party.MemberRoster == null)
            {
                return 0;
            }

            var wounded = 0;
            for (var index = 0; index < party.MemberRoster.Count; index++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(index);
                if (element.WoundedNumber > 0)
                {
                    wounded += element.WoundedNumber;
                }
            }

            return wounded;
        }

        private static int GetPartyLimit(MobileParty party)
        {
            if (party == null || party.Party == null)
            {
                return 0;
            }

            return party.Party.PartySizeLimit;
        }

        private static float GetMinimumHealthyPartyRatioForOperations()
        {
            return LordPartyRecoverySettings.MinimumHealthyPartyRatioForOperations <= 0f
                ? 0.65f
                : LordPartyRecoverySettings.MinimumHealthyPartyRatioForOperations;
        }
    }

    public class CsmLordPartyRecoveryEvaluation
    {
        public int HealthyManCount { get; set; }
        public int TotalManCount { get; set; }
        public int WoundedCount { get; set; }
        public int PartyLimit { get; set; }
        public float HealthyRatio { get; set; }
        public float TotalRatio { get; set; }
        public bool NeedsRecovery { get; set; }
        public bool PartyLimitFallbackUsed { get; set; }
    }
}
