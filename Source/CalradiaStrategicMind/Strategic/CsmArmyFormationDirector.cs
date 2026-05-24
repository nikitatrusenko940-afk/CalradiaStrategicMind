using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyFormationDirector
    {
        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly CsmArmyAttackTargetScorer _targetScorer;
        private int _formationAttempts;
        private int _formationSuccesses;
        private int _formationFailures;

        public CsmArmyFormationDirector()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _targetScorer = new CsmArmyAttackTargetScorer();
        }

        public List<CsmArmyDirectorReport> Execute(
            List<CsmArmySnapshot> armySnapshots,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry,
            CsmLordPartyRecoveryRegistry recoveryRegistry,
            CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies,
            CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets,
            CsmArmyLifecycleReport lifecycle,
            int observationTick)
        {
            var reports = new List<CsmArmyDirectorReport>();
            if (!ArmyDirectorSettings.AllowOffensiveArmyFormation)
            {
                return reports;
            }

            var kingdoms = Kingdom.All;
            if (kingdoms == null)
            {
                return reports;
            }

            var createdThisTick = 0;
            var createdByKingdom = new Dictionary<string, int>();
            for (var index = 0; index < kingdoms.Count && createdThisTick < ArmyDirectorSettings.MaxNewArmyCommandsPerTick; index++)
            {
                var kingdom = kingdoms[index];
                if (!IsKingdomEligible(kingdom))
                {
                    continue;
                }

                var kingdomName = GetKingdomName(kingdom);
                if (GetCount(createdByKingdom, kingdomName) >= ArmyDirectorSettings.MaxNewArmyCommandsPerKingdomPerTick)
                {
                    LogFormationSummary(CreateFormationDiagnostics(observationTick, kingdomName, "Skipped", "Kingdom army command limit reached"));
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Kingdom army command limit reached"));
                    continue;
                }

                if (HasActiveAttackArmy(armySnapshots, registry, kingdomName))
                {
                    LogFormationSummary(CreateFormationDiagnostics(observationTick, kingdomName, "Skipped", "Active attack army assignment already exists"));
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Active attack army assignment already exists"));
                    continue;
                }

                if (HasSuitableExistingAttackArmy(armySnapshots, registry, kingdomName))
                {
                    LogFormationSummary(CreateFormationDiagnostics(observationTick, kingdomName, "Skipped", "Suitable existing vanilla army available"));
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Suitable existing vanilla army available"));
                    continue;
                }

                OffensiveOpportunity opportunity;
                ArmyFormationDiagnostics diagnostics;
                if (!TryFindOpportunity(kingdom, defenseSnapshots, registry, defenseRegistry, recoveryRegistry, recentlyReleasedArmies, recentlyFailedTargets, lifecycle, observationTick, kingdomName, out opportunity, out diagnostics))
                {
                    LogFormationDiagnostics(diagnostics, false, "Skipped", "No attack target passed Army Target Scoring");
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "No attack target passed Army Target Scoring"));
                    continue;
                }

                if (HasActiveAssignmentForLeaderArmy(opportunity.Leader, registry))
                {
                    LogFormationDiagnostics(diagnostics, false, "Skipped", "Active CSM army assignment already exists");
                    reports.Add(CreateReport(observationTick, GetPartyName(opportunity.Leader), kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", "Active CSM army assignment already exists"));
                    continue;
                }

                var conflict = new CsmAssignmentConflictChecker(registry, defenseRegistry).CheckPartyForNewArmyCommand(opportunity.Leader, opportunity.Target);
                if (conflict.IsBlocked)
                {
                    LogFormationDiagnostics(diagnostics, false, "Skipped", conflict.Reason);
                    reports.Add(CreateReport(observationTick, GetPartyName(opportunity.Leader), kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", conflict.Reason));
                    continue;
                }

                string creationReason;
                var army = CreateTrueArmy(kingdom, opportunity, out creationReason);
                if (army == null)
                {
                    LogFormationDiagnostics(diagnostics, false, "Skipped", creationReason);
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", creationReason));
                    continue;
                }

                CsmArmyAssignment assignment;
                if (!registry.TryCreate(
                    GetArmyId(army),
                    GetArmyName(army),
                    GetPartyId(opportunity.Leader),
                    GetPartyName(opportunity.Leader),
                    kingdomName,
                    "AttackSettlement",
                    GetSettlementId(opportunity.Target),
                    GetSettlementName(opportunity.Target),
                    observationTick,
                    creationReason,
                    "VanillaArmy",
                    out assignment))
                {
                    LogFormationDiagnostics(diagnostics, false, "Skipped", "Active CSM army assignment already exists");
                    reports.Add(CreateReport(observationTick, GetArmyName(army), kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", "Active CSM army assignment already exists"));
                    continue;
                }

                createdThisTick++;
                createdByKingdom[kingdomName] = GetCount(createdByKingdom, kingdomName) + 1;
                LogFormationDiagnostics(diagnostics, true, assignment.Status, creationReason);
                reports.Add(CreateReport(observationTick, assignment.ArmyName, kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), true, assignment.Status, creationReason));
            }

            return reports;
        }

        public void ResetTargetScoringSummary()
        {
            _targetScorer.ResetSummary();
            _formationAttempts = 0;
            _formationSuccesses = 0;
            _formationFailures = 0;
        }

        public CsmArmyTargetScoringSummary GetTargetScoringSummary()
        {
            return _targetScorer.SnapshotSummary();
        }

        public int FormationAttempts
        {
            get { return _formationAttempts; }
        }

        public int FormationSuccesses
        {
            get { return _formationSuccesses; }
        }

        public int FormationFailures
        {
            get { return _formationFailures; }
        }

        private bool TryFindOpportunity(Kingdom kingdom, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, CsmDefenseAssignmentRegistry defenseRegistry, CsmLordPartyRecoveryRegistry recoveryRegistry, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, CsmRecentlyFailedArmyTargetRegistry recentlyFailedTargets, CsmArmyLifecycleReport lifecycle, int tick, string kingdomName, out OffensiveOpportunity opportunity, out ArmyFormationDiagnostics diagnostics)
        {
            opportunity = default(OffensiveOpportunity);
            diagnostics = CreateFormationDiagnostics(tick, kingdomName, "Rejected", "No formation opportunity found");
            var provisionalLeader = FindBestFormationLeader(kingdom, registry, recoveryRegistry, recentlyReleasedArmies, lifecycle, tick, diagnostics);
            if (provisionalLeader == null)
            {
                diagnostics.Reason = "No valid formation leader";
                LogLeaderSelection(diagnostics);
                return false;
            }

            var estimatedStrength = EstimateFormationStrength(kingdom, provisionalLeader, recoveryRegistry);
            var score = _targetScorer.FindBestTarget(kingdom, provisionalLeader, estimatedStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
            if (score == null)
            {
                var rejected = _targetScorer.FindBestRejectedTarget(kingdom, provisionalLeader, estimatedStrength, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
                LogTargetRejection(tick, kingdomName, GetPartyName(provisionalLeader), rejected);
                diagnostics.SelectedLeader = GetPartyName(provisionalLeader);
                diagnostics.SelectedLeaderStrength = _partyStrengthEvaluator.EvaluatePartyStrength(provisionalLeader);
                diagnostics.Reason = GetRejectedTargetReason(rejected);
                LogLeaderSelection(diagnostics);
                return false;
            }

            LogTargetScore(tick, kingdomName, GetPartyName(provisionalLeader), score);
            var target = score.Target;
            diagnostics.SelectedLeader = GetPartyName(provisionalLeader);
            diagnostics.SelectedLeaderStrength = _partyStrengthEvaluator.EvaluatePartyStrength(provisionalLeader);
            diagnostics.Target = GetSettlementName(target);
            diagnostics.TargetDefense = GetSettlementDefenseStrength(target);
            diagnostics.SelectedLeaderDistanceToTarget = GetDistance(provisionalLeader, target);
            if (defenseRegistry != null && defenseRegistry.HasActiveAssignmentForSettlement(GetSettlementId(target), GetSettlementName(target)))
            {
                LogTargetRejection(tick, kingdomName, GetPartyName(provisionalLeader), score);
                diagnostics.Reason = "Target blocked because settlement has active CSM defense assignment";
                LogLeaderSelection(diagnostics);
                return false;
            }

            var partyDiagnostics = new ArmyPartySelectionDiagnostics(tick, kingdomName, GetPartyName(provisionalLeader), GetSettlementName(target));
            var parties = FindFormationParties(kingdom, target, provisionalLeader, registry, defenseRegistry, recoveryRegistry, recentlyReleasedArmies, lifecycle, tick, partyDiagnostics);
            ApplyPartyDiagnostics(diagnostics, partyDiagnostics);
            LogPartySelection(partyDiagnostics);
            if (parties.Count > 0)
            {
                diagnostics.SelectedLeader = GetPartyName(parties[0]);
                diagnostics.SelectedLeaderStrength = _partyStrengthEvaluator.EvaluatePartyStrength(parties[0]);
                diagnostics.SelectedLeaderDistanceToTarget = GetDistance(parties[0], target);
            }

            LogLeaderSelection(diagnostics);
            if (parties.Count < ArmyDirectorSettings.MinOffensiveFormationParties)
            {
                diagnostics.Reason = "Not enough formation parties";
                return false;
            }

            var totalStrength = 0f;
            for (var index = 0; index < parties.Count; index++)
            {
                totalStrength += _partyStrengthEvaluator.EvaluatePartyStrength(parties[index]);
            }

            var targetDefense = GetSettlementDefenseStrength(target);
            if (targetDefense <= 0f)
            {
                targetDefense = 1f;
            }

            diagnostics.ExpectedArmyStrength = totalStrength;
            diagnostics.TargetDefense = targetDefense;
            diagnostics.ExpectedStrengthRatio = totalStrength / targetDefense;
            if (totalStrength / targetDefense < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                diagnostics.Reason = "Formation strength ratio below required attack threshold";
                return false;
            }

            score = _targetScorer.ScoreTarget(kingdom, parties[0], totalStrength, target, defenseSnapshots, registry, defenseRegistry, recentlyFailedTargets, tick);
            if (!_targetScorer.IsPassed(score))
            {
                LogTargetRejection(tick, kingdomName, GetPartyName(parties[0]), score);
                diagnostics.Reason = GetRejectedTargetReason(score);
                return false;
            }

            opportunity = new OffensiveOpportunity(target, parties);
            diagnostics.Reason = "Formation opportunity passed diagnostics";
            return true;
        }

        private MobileParty FindBestFormationLeader(Kingdom kingdom, CsmArmyAssignmentRegistry registry, CsmLordPartyRecoveryRegistry recoveryRegistry, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, CsmArmyLifecycleReport lifecycle, int tick, ArmyFormationDiagnostics diagnostics)
        {
            var parties = kingdom?.WarPartyComponents;
            MobileParty best = null;
            var bestStrength = 0f;
            if (parties == null)
            {
                return null;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index].MobileParty;
                if (diagnostics != null)
                {
                    diagnostics.EvaluatedLeaders++;
                }

                if (!IsFreeLordParty(party))
                {
                    RecordLeaderReject(diagnostics, party, "Invalid party");
                    continue;
                }

                if (ShouldRejectRecoveryPartyForArmySelection(party, recoveryRegistry))
                {
                    RecordLeaderReject(diagnostics, party, "RecoveryAssigned");
                    continue;
                }

                if (IsRecentlyReleasedLeader(party, recentlyReleasedArmies, lifecycle, tick))
                {
                    if (diagnostics != null)
                    {
                        diagnostics.RejectedRecentlyReleased++;
                    }

                    continue;
                }

                if (HasActiveAssignmentForLeaderArmy(party, registry))
                {
                    if (diagnostics != null)
                    {
                        diagnostics.RejectedAlreadyAssigned++;
                    }
                }

                if (diagnostics != null)
                {
                    diagnostics.ValidLeaders++;
                }

                var strength = _partyStrengthEvaluator.EvaluatePartyStrength(party);
                if (strength > bestStrength)
                {
                    best = party;
                    bestStrength = strength;
                }
            }

            return best;
        }

        private float EstimateFormationStrength(Kingdom kingdom, MobileParty anchorParty, CsmLordPartyRecoveryRegistry recoveryRegistry)
        {
            var strength = 0f;
            var parties = kingdom?.WarPartyComponents;
            var count = 0;
            if (parties == null || anchorParty == null)
            {
                return strength;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index].MobileParty;
                if (!IsFreeLordParty(party)
                    || ShouldRejectRecoveryPartyForArmySelection(party, recoveryRegistry)
                    || party.Position.Distance(anchorParty.Position) > ArmyDirectorSettings.MaxAttackTargetDistance)
                {
                    continue;
                }

                strength += _partyStrengthEvaluator.EvaluatePartyStrength(party);
                count++;
                if (count >= ArmyDirectorSettings.MaxOffensiveFormationParties)
                {
                    return strength;
                }
            }

            return strength;
        }

        private static Army CreateTrueArmy(Kingdom kingdom, OffensiveOpportunity opportunity, out string reason)
        {
            reason = "Safe true army creation unavailable";
            if (kingdom == null || opportunity.Leader == null || opportunity.Leader.LeaderHero == null || opportunity.Target == null)
            {
                return null;
            }

            var partiesToCall = CreatePartiesToCallList(opportunity);
            var created = SafeExecutor.Run(
                "Create true vanilla army for attack settlement",
                () =>
                {
                    kingdom.CreateArmy(opportunity.Leader.LeaderHero, opportunity.Target, Army.ArmyTypes.Besieger, partiesToCall);
                    return FindCreatedArmy(kingdom, opportunity.Leader);
                },
                null);

            if (created == null)
            {
                reason = opportunity.Leader.Army == null
                    ? "CreateArmy did not attach leader to readable vanilla army"
                    : "CreateArmy did not produce readable army";
                return null;
            }

            reason = partiesToCall == null
                ? "Created true vanilla army using Kingdom.CreateArmy with null party call list"
                : "Created true vanilla army with selected party call list";
            return created;
        }

        private static MBReadOnlyList<MobileParty> CreatePartiesToCallList(OffensiveOpportunity opportunity)
        {
            if (opportunity.Parties == null || opportunity.Parties.Count <= 1)
            {
                return null;
            }

            var partiesToCall = new MBList<MobileParty>();
            for (var index = 0; index < opportunity.Parties.Count; index++)
            {
                var party = opportunity.Parties[index];
                if (party != null && party != opportunity.Leader)
                {
                    partiesToCall.Add(party);
                }
            }

            return partiesToCall.Count == 0 ? null : partiesToCall;
        }

        private static Army FindCreatedArmy(Kingdom kingdom, MobileParty leaderParty)
        {
            if (leaderParty == null)
            {
                return null;
            }

            if (leaderParty.Army != null && leaderParty.Army.LeaderParty == leaderParty)
            {
                return leaderParty.Army;
            }

            if (kingdom?.Armies == null)
            {
                return null;
            }

            for (var index = 0; index < kingdom.Armies.Count; index++)
            {
                var army = kingdom.Armies[index];
                if (army != null && army.LeaderParty == leaderParty)
                {
                    return army;
                }
            }

            return null;
        }

        private List<MobileParty> FindFormationParties(Kingdom kingdom, Settlement target, MobileParty leader, CsmArmyAssignmentRegistry registry, CsmDefenseAssignmentRegistry defenseRegistry, CsmLordPartyRecoveryRegistry recoveryRegistry, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, CsmArmyLifecycleReport lifecycle, int tick, ArmyPartySelectionDiagnostics diagnostics)
        {
            var parties = new List<MobileParty>();
            if (kingdom == null || kingdom.WarPartyComponents == null || target == null)
            {
                return parties;
            }

            for (var index = 0; index < kingdom.WarPartyComponents.Count; index++)
            {
                var party = kingdom.WarPartyComponents[index].MobileParty;
                if (diagnostics != null)
                {
                    diagnostics.CandidateParties++;
                }

                if (!IsFreeLordParty(party))
                {
                    RecordPartyReject(diagnostics, party, kingdom, "Invalid party");
                    continue;
                }

                if (ShouldRejectRecoveryPartyForArmySelection(party, recoveryRegistry))
                {
                    RecordPartyReject(diagnostics, party, kingdom, "RecoveryAssigned");
                    continue;
                }

                if (IsRecentlyReleasedLeader(party, recentlyReleasedArmies, lifecycle, tick))
                {
                    RecordPartyReject(diagnostics, party, kingdom, "Recently released");
                    continue;
                }

                if (target.Position.Distance(party.Position) > ArmyDirectorSettings.MaxAttackTargetDistance)
                {
                    RecordPartyReject(diagnostics, party, kingdom, "Too far from target");
                    continue;
                }

                var conflict = new CsmAssignmentConflictChecker(registry, defenseRegistry).CheckPartyForNewArmyCommand(party, target);
                if (conflict.IsBlocked)
                {
                    RecordPartyReject(diagnostics, party, kingdom, conflict.Reason);
                    continue;
                }

                parties.Add(party);
                parties.Sort(ComparePartyStrength);
                if (parties.Count > ArmyDirectorSettings.MaxOffensiveFormationParties)
                {
                    parties.RemoveAt(parties.Count - 1);
                }
            }

            if (diagnostics != null)
            {
                diagnostics.SelectedParties = parties.Count;
                diagnostics.SelectedPartyNames = JoinPartyNames(parties);
                diagnostics.ExpectedCalledStrength = GetCalledStrength(parties);
            }

            return parties;
        }

        private static int ComparePartyStrength(MobileParty left, MobileParty right)
        {
            return right.Party.EstimatedStrength.CompareTo(left.Party.EstimatedStrength);
        }

        private static bool IsFreeLordParty(MobileParty party)
        {
            return party != null
                && party.IsActive
                && !party.IsMainParty
                && !party.IsDisbanding
                && party.Army == null
                && party.LeaderHero != null
                && party.MapEvent == null
                && party.BesiegedSettlement == null
                && party.MemberRoster != null
                && party.MemberRoster.TotalManCount > 0
                && party.DefaultBehavior != AiBehavior.DefendSettlement;
        }

        private static bool ShouldRejectRecoveryPartyForArmySelection(MobileParty party, CsmLordPartyRecoveryRegistry recoveryRegistry)
        {
            if (!LordPartyRecoverySettings.EnableLordPartyRecoveryController
                || !LordPartyRecoverySettings.RecoveryBlocksArmyDirectorSelection
                || party == null
                || recoveryRegistry == null
                || !recoveryRegistry.HasActiveRecoveryForParty(party))
            {
                return false;
            }

            var evaluation = CsmLordPartyRecoveryEvaluator.Evaluate(party);
            return evaluation.HealthyRatio < GetMinimumHealthyPartyRatioForOperations();
        }

        private static bool HasActiveAssignmentForLeaderArmy(MobileParty leaderParty, CsmArmyAssignmentRegistry registry)
        {
            if (leaderParty == null || registry == null)
            {
                return true;
            }

            if (registry.HasActiveAssignmentForArmy(GetPartyId(leaderParty)))
            {
                return true;
            }

            return leaderParty.Army != null && registry.HasActiveAssignmentForArmy(GetArmyId(leaderParty.Army));
        }

        private static bool IsRecentlyReleasedLeader(MobileParty party, CsmRecentlyReleasedArmyRegistry recentlyReleasedArmies, CsmArmyLifecycleReport lifecycle, int tick)
        {
            var released = recentlyReleasedArmies == null ? null : recentlyReleasedArmies.GetActiveReleaseForLeader(party, tick);
            if (released == null)
            {
                return false;
            }

            if (lifecycle != null)
            {
                lifecycle.ReleaseCooldownBlocks++;
            }

            return true;
        }

        private static bool HasActiveAttackArmy(List<CsmArmySnapshot> snapshots, CsmArmyAssignmentRegistry registry, string kingdomName)
        {
            if (snapshots == null)
            {
                return false;
            }

            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                if (snapshot.KingdomName != kingdomName)
                {
                    continue;
                }

                var assignment = registry.GetActiveAssignmentForArmy(snapshot.ArmyId);
                if (assignment != null && assignment.ObjectiveType == "AttackSettlement")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSuitableExistingAttackArmy(List<CsmArmySnapshot> snapshots, CsmArmyAssignmentRegistry registry, string kingdomName)
        {
            if (snapshots == null)
            {
                return false;
            }

            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                if (snapshot.KingdomName == kingdomName
                    && snapshot.IsValidForCsm
                    && snapshot.TotalStrength >= ArmyDirectorSettings.MinimumArmyStrengthForAttack
                    && registry.GetActiveAssignmentForArmy(snapshot.ArmyId) == null
                    && (snapshot.CurrentObjectiveGuess == "IdleOrUnknown" || snapshot.CurrentObjectiveGuess == "MovingToSettlement"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKingdomEligible(Kingdom kingdom)
        {
            return kingdom != null
                && !kingdom.IsEliminated
                && kingdom.FactionsAtWarWith != null
                && kingdom.FactionsAtWarWith.Count > 0
                && kingdom != Clan.PlayerClan.Kingdom;
        }

        private static float GetSettlementDefenseStrength(Settlement settlement)
        {
            var strength = 0f;
            if (settlement?.Town?.GarrisonParty != null)
            {
                strength += settlement.Town.GarrisonParty.Party.EstimatedStrength;
            }

            if (settlement?.MilitiaPartyComponent?.MobileParty != null)
            {
                strength += settlement.MilitiaPartyComponent.MobileParty.Party.EstimatedStrength;
            }

            return strength;
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            return counts.TryGetValue(key, out count) ? count : 0;
        }

        private static float GetMinimumHealthyPartyRatioForOperations()
        {
            return LordPartyRecoverySettings.MinimumHealthyPartyRatioForOperations <= 0f
                ? 0.65f
                : LordPartyRecoverySettings.MinimumHealthyPartyRatioForOperations;
        }

        private static ArmyFormationDiagnostics CreateFormationDiagnostics(int tick, string kingdomName, string status, string reason)
        {
            return new ArmyFormationDiagnostics
            {
                Tick = tick,
                KingdomName = kingdomName,
                SelectedLeader = "none",
                Target = "none",
                Status = status,
                Reason = reason
            };
        }

        private void LogFormationDiagnostics(ArmyFormationDiagnostics diagnostics, bool commandApplied, string status, string reason)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.CommandApplied = commandApplied;
            diagnostics.Status = status;
            diagnostics.Reason = reason;
            LogFormationSummary(diagnostics);
        }

        private void LogFormationSummary(ArmyFormationDiagnostics diagnostics)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || diagnostics == null)
            {
                return;
            }

            _formationAttempts++;
            if (diagnostics.CommandApplied)
            {
                _formationSuccesses++;
            }
            else
            {
                _formationFailures++;
            }

            CsmLogger.Info(
                $"Observed CSM army formation summary: tick={diagnostics.Tick}, kingdom='{diagnostics.KingdomName}', evaluatedLeaders={diagnostics.EvaluatedLeaders}, validLeaders={diagnostics.ValidLeaders}, selectedLeader='{diagnostics.SelectedLeader}', target='{diagnostics.Target}', candidateParties={diagnostics.CandidateParties}, selectedParties={diagnostics.SelectedParties}, rejectedParties={diagnostics.RejectedParties}, rejectedTooFar={diagnostics.RejectedTooFar}, rejectedTooWeak={diagnostics.RejectedTooWeak}, rejectedDefenseAssigned={diagnostics.RejectedDefenseAssigned}, rejectedArmyAssigned={diagnostics.RejectedArmyAssigned}, rejectedRecoveryAssigned={diagnostics.RejectedRecoveryAssigned}, rejectedTooWeakRecovering={diagnostics.RejectedTooWeakRecovering}, expectedArmyStrength={diagnostics.ExpectedArmyStrength:0.00}, targetDefense={diagnostics.TargetDefense:0.00}, expectedStrengthRatio={diagnostics.ExpectedStrengthRatio:0.00}, commandApplied={diagnostics.CommandApplied}, status='{diagnostics.Status}', decisionReason='{diagnostics.Reason}', reason='Army formation diagnostic snapshot'");
        }

        private static void LogLeaderSelection(ArmyFormationDiagnostics diagnostics)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || diagnostics == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army leader selection: tick={diagnostics.Tick}, kingdom='{diagnostics.KingdomName}', target='{diagnostics.Target}', evaluatedLeaders={diagnostics.EvaluatedLeaders}, validLeaders={diagnostics.ValidLeaders}, selectedLeader='{diagnostics.SelectedLeader}', selectedLeaderStrength={diagnostics.SelectedLeaderStrength:0.00}, selectedLeaderDistanceToTarget={diagnostics.SelectedLeaderDistanceToTarget:0.00}, rejectedTooWeak={diagnostics.RejectedLeaderTooWeak}, rejectedTooFar={diagnostics.RejectedLeaderTooFar}, rejectedAlreadyAssigned={diagnostics.RejectedAlreadyAssigned}, rejectedRecentlyReleased={diagnostics.RejectedRecentlyReleased}, rejectedRecoveryAssigned={diagnostics.RejectedLeaderRecoveryAssigned}, rejectedTooWeakRecovering={diagnostics.RejectedLeaderTooWeakRecovering}, reason='Army leader selection diagnostic'");
        }

        private static void LogPartySelection(ArmyPartySelectionDiagnostics diagnostics)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || diagnostics == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army formation party selection: tick={diagnostics.Tick}, kingdom='{diagnostics.KingdomName}', leader='{diagnostics.LeaderName}', target='{diagnostics.TargetName}', selectedParties='{diagnostics.SelectedPartyNames}', selectedPartyCount={diagnostics.SelectedParties}, expectedCalledStrength={diagnostics.ExpectedCalledStrength:0.00}, candidateParties={diagnostics.CandidateParties}, rejectedParties={diagnostics.RejectedParties}, rejectedTooFarFromLeader={diagnostics.RejectedTooFarFromLeader}, rejectedTooFarFromTarget={diagnostics.RejectedTooFarFromTarget}, rejectedTooWeak={diagnostics.RejectedTooWeak}, rejectedAlreadyInArmy={diagnostics.RejectedAlreadyInArmy}, rejectedArmyAssigned={diagnostics.RejectedArmyAssigned}, rejectedDefenseAssigned={diagnostics.RejectedDefenseAssigned}, rejectedRecoveryAssigned={diagnostics.RejectedRecoveryAssigned}, rejectedTooWeakRecovering={diagnostics.RejectedTooWeakRecovering}, rejectedRecentlyReleased={diagnostics.RejectedRecentlyReleased}, rejectedInvalid={diagnostics.RejectedInvalid}, rejectedWrongKingdom={diagnostics.RejectedWrongKingdom}, reason='Selected party call list diagnostic'");
        }

        private static void ApplyPartyDiagnostics(ArmyFormationDiagnostics target, ArmyPartySelectionDiagnostics source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.CandidateParties = source.CandidateParties;
            target.SelectedParties = source.SelectedParties;
            target.RejectedParties = source.RejectedParties;
            target.RejectedTooFar = source.RejectedTooFarFromTarget + source.RejectedTooFarFromLeader;
            target.RejectedTooWeak = source.RejectedTooWeak;
            target.RejectedDefenseAssigned = source.RejectedDefenseAssigned;
            target.RejectedArmyAssigned = source.RejectedArmyAssigned;
            target.RejectedRecoveryAssigned = source.RejectedRecoveryAssigned;
            target.RejectedTooWeakRecovering = source.RejectedTooWeakRecovering;
        }

        private static void RecordLeaderReject(ArmyFormationDiagnostics diagnostics, MobileParty party, string reason)
        {
            if (diagnostics == null)
            {
                return;
            }

            if (party == null || party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0 || party.Party == null || party.Party.EstimatedStrength <= 0f)
            {
                diagnostics.RejectedLeaderTooWeak++;
                return;
            }

            if (reason == "RecoveryAssigned")
            {
                diagnostics.RejectedLeaderRecoveryAssigned++;
                diagnostics.RejectedLeaderTooWeakRecovering++;
                return;
            }

            diagnostics.RejectedLeaderInvalid++;
        }

        private static void RecordPartyReject(ArmyPartySelectionDiagnostics diagnostics, MobileParty party, Kingdom kingdom, string reason)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.RejectedParties++;
            if (party != null && kingdom != null && party.MapFaction != kingdom)
            {
                diagnostics.RejectedWrongKingdom++;
                return;
            }

            if (reason == "Too far from leader")
            {
                diagnostics.RejectedTooFarFromLeader++;
                return;
            }

            if (reason == "Too far from target")
            {
                diagnostics.RejectedTooFarFromTarget++;
                return;
            }

            if (reason == "Recently released")
            {
                diagnostics.RejectedRecentlyReleased++;
                return;
            }

            if (reason == "RecoveryAssigned")
            {
                diagnostics.RejectedRecoveryAssigned++;
                diagnostics.RejectedTooWeakRecovering++;
                return;
            }

            if (reason != null && reason.IndexOf("active CSM army assignment", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                diagnostics.RejectedArmyAssigned++;
                return;
            }

            if (reason != null && reason.IndexOf("active CSM defense assignment", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                diagnostics.RejectedDefenseAssigned++;
                return;
            }

            if (party != null && party.Army != null)
            {
                diagnostics.RejectedAlreadyInArmy++;
                return;
            }

            if (party == null || party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0 || party.Party == null || party.Party.EstimatedStrength <= 0f)
            {
                diagnostics.RejectedTooWeak++;
                return;
            }

            diagnostics.RejectedInvalid++;
        }

        private static string GetRejectedTargetReason(CsmArmyAttackTargetScore score)
        {
            if (score == null)
            {
                return "No attack target passed Army Target Scoring";
            }

            if (!string.IsNullOrWhiteSpace(score.HardRejectReason))
            {
                return score.HardRejectReason;
            }

            return string.IsNullOrWhiteSpace(score.Reason)
                ? "Attack target rejected by Army Target Scoring"
                : score.Reason;
        }

        private static float GetDistance(MobileParty party, Settlement target)
        {
            return party == null || target == null ? 0f : party.Position.Distance(target.Position);
        }

        private static float GetCalledStrength(List<MobileParty> parties)
        {
            var strength = 0f;
            if (parties == null)
            {
                return strength;
            }

            for (var index = 1; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party?.Party != null)
                {
                    strength += party.Party.EstimatedStrength;
                }
            }

            return strength;
        }

        private static string JoinPartyNames(List<MobileParty> parties)
        {
            if (parties == null || parties.Count <= 1)
            {
                return "none";
            }

            var names = new List<string>();
            for (var index = 1; index < parties.Count; index++)
            {
                names.Add(GetPartyName(parties[index]));
            }

            return names.Count == 0 ? "none" : string.Join("; ", names.ToArray());
        }

        private static CsmArmyDirectorReport CreateReport(int tick, string army, string kingdom, string objective, string target, bool applied, string status, string reason)
        {
            return new CsmArmyDirectorReport(tick, army, kingdom, objective, target, applied, status, reason);
        }

        private static void LogTargetScore(int tick, string kingdomName, string armyName, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target score: tick={tick}, kingdom='{kingdomName}', army='{armyName}', selectedTarget='{score.TargetName}', score={score.Score:0.00}, strategicValue={score.StrategicValueScore:0.00}, frontlineScore={score.FrontlineScore:0.00}, distanceScore={score.DistanceScore:0.00}, strengthRatioScore={score.StrengthRatioScore:0.00}, friendlySupportScore={score.NearbyFriendlySupportScore:0.00}, enemyThreatPenalty={score.NearbyEnemyPenalty:0.00}, overextensionPenalty={score.OverextensionPenalty:0.00}, recentlyFailedPenalty={score.RecentlyFailedTargetPenalty:0.00}, reason='{score.Reason}'");
        }

        private static void LogTargetRejection(int tick, string kingdomName, string armyName, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target rejection: tick={tick}, kingdom='{kingdomName}', army='{armyName}', topRejectedTarget='{score.TargetName}', score={score.Score:0.00}, hardRejectReason='{score.HardRejectReason}', strategicValue={score.StrategicValueScore:0.00}, distance={score.Distance:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, reason='{score.Reason}'");
        }

        private static string GetKingdomName(Kingdom kingdom)
        {
            return kingdom?.Name == null ? "unknown" : kingdom.Name.ToString();
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? string.Empty;
        }

        private static string GetPartyName(MobileParty party)
        {
            return party?.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetArmyId(Army army)
        {
            if (army == null || army.LeaderParty == null)
            {
                return "unknown";
            }

            return army.LeaderParty.StringId ?? GetArmyName(army);
        }

        private static string GetArmyName(Army army)
        {
            return army?.Name == null ? "unknown" : army.Name.ToString();
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement?.StringId ?? string.Empty;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "unknown" : settlement.Name.ToString();
        }

        private class ArmyFormationDiagnostics
        {
            public int Tick { get; set; }
            public string KingdomName { get; set; }
            public int EvaluatedLeaders { get; set; }
            public int ValidLeaders { get; set; }
            public string SelectedLeader { get; set; }
            public float SelectedLeaderStrength { get; set; }
            public float SelectedLeaderDistanceToTarget { get; set; }
            public int RejectedLeaderTooWeak { get; set; }
            public int RejectedLeaderTooFar { get; set; }
            public int RejectedLeaderInvalid { get; set; }
            public int RejectedAlreadyAssigned { get; set; }
            public int RejectedRecentlyReleased { get; set; }
            public int RejectedLeaderRecoveryAssigned { get; set; }
            public int RejectedLeaderTooWeakRecovering { get; set; }
            public string Target { get; set; }
            public int CandidateParties { get; set; }
            public int SelectedParties { get; set; }
            public int RejectedParties { get; set; }
            public int RejectedTooFar { get; set; }
            public int RejectedTooWeak { get; set; }
            public int RejectedDefenseAssigned { get; set; }
            public int RejectedArmyAssigned { get; set; }
            public int RejectedRecoveryAssigned { get; set; }
            public int RejectedTooWeakRecovering { get; set; }
            public float ExpectedArmyStrength { get; set; }
            public float TargetDefense { get; set; }
            public float ExpectedStrengthRatio { get; set; }
            public bool CommandApplied { get; set; }
            public string Status { get; set; }
            public string Reason { get; set; }
        }

        private class ArmyPartySelectionDiagnostics
        {
            public ArmyPartySelectionDiagnostics(int tick, string kingdomName, string leaderName, string targetName)
            {
                Tick = tick;
                KingdomName = kingdomName;
                LeaderName = leaderName;
                TargetName = targetName;
                SelectedPartyNames = "none";
            }

            public int Tick { get; private set; }
            public string KingdomName { get; private set; }
            public string LeaderName { get; private set; }
            public string TargetName { get; private set; }
            public int CandidateParties { get; set; }
            public int SelectedParties { get; set; }
            public string SelectedPartyNames { get; set; }
            public float ExpectedCalledStrength { get; set; }
            public int RejectedParties { get; set; }
            public int RejectedTooFarFromLeader { get; set; }
            public int RejectedTooFarFromTarget { get; set; }
            public int RejectedTooWeak { get; set; }
            public int RejectedAlreadyInArmy { get; set; }
            public int RejectedArmyAssigned { get; set; }
            public int RejectedDefenseAssigned { get; set; }
            public int RejectedRecoveryAssigned { get; set; }
            public int RejectedTooWeakRecovering { get; set; }
            public int RejectedRecentlyReleased { get; set; }
            public int RejectedInvalid { get; set; }
            public int RejectedWrongKingdom { get; set; }
        }

        private struct OffensiveOpportunity
        {
            public OffensiveOpportunity(Settlement target, List<MobileParty> parties)
            {
                Target = target;
                Parties = parties;
            }

            public Settlement Target { get; private set; }
            public List<MobileParty> Parties { get; private set; }
            public MobileParty Leader => Parties == null || Parties.Count == 0 ? null : Parties[0];
        }
    }
}
