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

        public CsmArmyFormationDirector()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _targetScorer = new CsmArmyAttackTargetScorer();
        }

        public List<CsmArmyDirectorReport> Execute(
            List<CsmArmySnapshot> armySnapshots,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
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
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Kingdom army command limit reached"));
                    continue;
                }

                if (HasActiveAttackArmy(armySnapshots, registry, kingdomName))
                {
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Active attack army assignment already exists"));
                    continue;
                }

                if (HasSuitableExistingAttackArmy(armySnapshots, kingdomName))
                {
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "Suitable existing vanilla army available"));
                    continue;
                }

                OffensiveOpportunity opportunity;
                if (!TryFindOpportunity(kingdom, defenseSnapshots, registry, observationTick, kingdomName, out opportunity))
                {
                    reports.Add(CreateReport(observationTick, "none", kingdomName, "AttackSettlement", "none", false, "Skipped", "No attack target passed Army Target Scoring"));
                    continue;
                }

                if (HasActiveAssignmentForLeaderArmy(opportunity.Leader, registry))
                {
                    reports.Add(CreateReport(observationTick, GetPartyName(opportunity.Leader), kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", "Active CSM army assignment already exists"));
                    continue;
                }

                string creationReason;
                var army = CreateTrueArmy(kingdom, opportunity, out creationReason);
                if (army == null)
                {
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
                    reports.Add(CreateReport(observationTick, GetArmyName(army), kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), false, "Skipped", "Active CSM army assignment already exists"));
                    continue;
                }

                createdThisTick++;
                createdByKingdom[kingdomName] = GetCount(createdByKingdom, kingdomName) + 1;
                reports.Add(CreateReport(observationTick, assignment.ArmyName, kingdomName, "AttackSettlement", GetSettlementName(opportunity.Target), true, assignment.Status, creationReason));
            }

            return reports;
        }

        private bool TryFindOpportunity(Kingdom kingdom, List<DefenseEvaluationSnapshot> defenseSnapshots, CsmArmyAssignmentRegistry registry, int tick, string kingdomName, out OffensiveOpportunity opportunity)
        {
            opportunity = default(OffensiveOpportunity);
            var provisionalLeader = FindBestFormationLeader(kingdom);
            if (provisionalLeader == null)
            {
                return false;
            }

            var estimatedStrength = EstimateFormationStrength(kingdom, provisionalLeader);
            var score = _targetScorer.FindBestTarget(kingdom, provisionalLeader, estimatedStrength, defenseSnapshots, registry);
            if (score == null)
            {
                var rejected = _targetScorer.FindBestRejectedTarget(kingdom, provisionalLeader, estimatedStrength, defenseSnapshots, registry);
                LogTargetRejection(tick, kingdomName, GetPartyName(provisionalLeader), rejected);
                return false;
            }

            LogTargetScore(tick, kingdomName, GetPartyName(provisionalLeader), score);
            var target = score.Target;
            var parties = FindFormationParties(kingdom, target);
            if (parties.Count < ArmyDirectorSettings.MinOffensiveFormationParties)
            {
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

            if (totalStrength / targetDefense < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                return false;
            }

            score = _targetScorer.ScoreTarget(kingdom, parties[0], totalStrength, target, defenseSnapshots, registry);
            if (!_targetScorer.IsPassed(score))
            {
                LogTargetRejection(tick, kingdomName, GetPartyName(parties[0]), score);
                return false;
            }

            opportunity = new OffensiveOpportunity(target, parties);
            return true;
        }

        private MobileParty FindBestFormationLeader(Kingdom kingdom)
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
                if (!IsFreeLordParty(party))
                {
                    continue;
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

        private float EstimateFormationStrength(Kingdom kingdom, MobileParty anchorParty)
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
                if (!IsFreeLordParty(party) || party.Position.Distance(anchorParty.Position) > ArmyDirectorSettings.MaxAttackTargetDistance)
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

        private List<MobileParty> FindFormationParties(Kingdom kingdom, Settlement target)
        {
            var parties = new List<MobileParty>();
            if (kingdom == null || kingdom.WarPartyComponents == null || target == null)
            {
                return parties;
            }

            for (var index = 0; index < kingdom.WarPartyComponents.Count; index++)
            {
                var party = kingdom.WarPartyComponents[index].MobileParty;
                if (!IsFreeLordParty(party))
                {
                    continue;
                }

                if (target.Position.Distance(party.Position) > ArmyDirectorSettings.MaxAttackTargetDistance)
                {
                    continue;
                }

                parties.Add(party);
                parties.Sort(ComparePartyStrength);
                if (parties.Count > ArmyDirectorSettings.MaxOffensiveFormationParties)
                {
                    parties.RemoveAt(parties.Count - 1);
                }
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

        private static bool HasSuitableExistingAttackArmy(List<CsmArmySnapshot> snapshots, string kingdomName)
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
                    && snapshot.TotalStrength >= ArmyDirectorSettings.MinimumArmyStrengthForAttack)
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
                $"Observed CSM army target score: tick={tick}, kingdom='{kingdomName}', army='{armyName}', selectedTarget='{score.TargetName}', score={score.Score:0.00}, distance={score.Distance:0.00}, targetDefense={score.TargetDefenseStrength:0.00}, estimatedAttackStrength={score.EstimatedAttackStrength:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, isFrontline={score.IsFrontlineCandidate}, reason='{score.Reason}'");
        }

        private static void LogTargetRejection(int tick, string kingdomName, string armyName, CsmArmyAttackTargetScore score)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || score == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army target rejection: tick={tick}, kingdom='{kingdomName}', army='{armyName}', topRejectedTarget='{score.TargetName}', score={score.Score:0.00}, distance={score.Distance:0.00}, targetDefense={score.TargetDefenseStrength:0.00}, estimatedAttackStrength={score.EstimatedAttackStrength:0.00}, strengthRatio={score.StrengthRatio:0.00}, nearbyEnemyArmyStrength={score.NearbyEnemyArmyStrength:0.00}, nearbyFriendlySupportStrength={score.NearbyFriendlySupportStrength:0.00}, isFrontline={score.IsFrontlineCandidate}, reason='{score.Reason}'");
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
