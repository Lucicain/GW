using System;

using System.Collections.Generic;

using System.Globalization;

using System.Linq;

using TaleWorlds.CampaignSystem;

using TaleWorlds.CampaignSystem.Map;

using TaleWorlds.CampaignSystem.MapEvents;

using TaleWorlds.CampaignSystem.Party;

using TaleWorlds.Core;

using TaleWorlds.Library;

using TaleWorlds.Localization;



namespace GreyWardenPolicePurity

{

    /// <summary>

    /// 大地图上「谁比谁强」的唯一实现。立案门槛、派单、协力编成、现场宣战、野外执法、

    /// 悬赏难度都从这里取数；要改战力口径只改这里。

    ///

    /// 本体战力用于接案与编成；固定目标区域的可参战敌军用于宣战和主动攻击。
    /// 友军只计实际挂靠或原版参战半径内的队伍，不以远方共同任务代替到场。
    /// 依据：用户裁定 2026-10-17。单队数值仍用 PartyBase.EstimatedStrength。
    ///
    /// 战斗场景里的兵力（音乐、战场支援）是另一回事，按 Agent 计算，不走这里。

    /// </summary>

    internal static class GwpStrength

    {

        internal sealed class Threat

        {

            public float Strength { get; set; }

            public float MaximumSpeed { get; set; }

            public string FastestCombatGroupId { get; set; } = string.Empty;

            public float JoiningRadius { get; set; }

            public float ThreatRadius { get; set; }

            public List<string> CombatGroups { get; } = new List<string>();

        }



        internal sealed class LocalBalance

        {

            public MobileParty Actor { get; set; } = null!;

            public MobileParty MovementTarget { get; set; } = null!;

            public float FriendlyLocalStrength { get; set; }

            public float EnemyLocalStrength { get; set; }

            public string FriendlyCombatGroups { get; set; } = string.Empty;

            public float Distance { get; set; }

            public bool StrengthReady { get; set; }

            public string Reason { get; set; } = string.Empty;

        }



        /// <summary>
        /// 一宗案子的目标本身有多强：他所在战斗组（攻城营、已挂靠的军团或本队）。**刻意不含
        /// 身边路过的人**——那一档是临场变量，只在现场宣战和主动接战判断中使用；这里回答的是
        /// "这个人在不在我们量级上"。接案门槛和悬赏难度都用它。
        /// </summary>
        internal static float CaseTarget(CrimeRecord? crime)
        {
            MobileParty? offender = crime?.Offender;
            return offender?.IsActive == true ? CombatGroup(CombatGroupOf(offender)) : 0f;
        }

        /// <summary>一支部队自己的战力：原版 <c>PartyBase.EstimatedStrength</c>。</summary>

        internal static float Party(MobileParty? party)

        {

            return party?.Party == null

                ? 0f

                : Math.Max(0f, party.Party.EstimatedStrength);

        }



        /// <summary>

        /// 这支部队实际跟谁一起行动、一起挨打：攻城营的营长、已挂靠的军团长，否则就是它自己。

        /// 只是名义上属于某个军团、还在赶去会合的部队不算进军团——原版

        /// <c>Army.EstimatedStrength</c> 也只数军团长和已挂靠的成员。曾经这里先看

        /// <c>Army.LeaderParty</c>，赶路中的领主被当成远处那支军团（还把他自己漏掉）；

        /// 野外执法 2026-09-26 撞上的就是同一类问题。

        /// </summary>

        internal static MobileParty CombatGroupOf(MobileParty party)

        {

            return party.BesiegerCamp?.LeaderParty ??

                   party.AttachedTo ??

                   party;

        }



        internal static float TheoreticalBaseSpeed(MobileParty? party)

        {

            if (party?.IsActive != true || Campaign.Current?.Models

                    ?.PartySpeedCalculatingModel == null)

                return 0f;



            ExplainedNumber speed = Campaign.Current.Models

                .PartySpeedCalculatingModel.CalculateBaseSpeed(

                    party, includeDescriptions: true);

            float factors = speed.SumOfFactors;

            // Bannerlord's base-speed model includes the temporary post-battle

            // disorganized penalty as a -0.40 factor. The case threshold must

            // represent the party's normal pursuit capability, not the hour in

            // which it happened to finish a battle.

            if (party.IsDisorganized)

                factors += 0.4f;



            float normalized = speed.BaseNumber * (1f + factors);

            // Wet weather is unusually applied inside Bannerlord's base-speed

            // model rather than CalculateFinalSpeed. Remove only those two

            // explicitly explained environmental lines so the saved threshold

            // remains a theoretical normal-condition speed.

            string cavalryWeatherPenalty =

                new TextObject("{=Cb0k9KM8}Cavalry weather penalty")

                    .ToString();

            string mountedFootmenWeatherPenalty =

                new TextObject(

                    "{=JAKoFNgt}Footmen on horses weather penalty")

                    .ToString();

            foreach ((string name, float number) in speed.GetLines())

            {

                if ((string.Equals(name, cavalryWeatherPenalty,

                         StringComparison.Ordinal) ||

                     string.Equals(name, mountedFootmenWeatherPenalty,

                         StringComparison.Ordinal)) &&

                    number < 0f)

                {

                    normalized -= number;

                }

            }



            float minimum = Campaign.Current.Models

                .PartySpeedCalculatingModel.MinimumSpeed;

            return Math.Max(minimum, normalized);

        }



        internal static Threat EnemyAround(MobileParty observer, MobileParty offender)
        {
            var snapshot = new Threat();
            if (observer?.IsActive != true || offender?.IsActive != true || offender.Party == null)
                return snapshot;
            MobileParty combatTarget = CombatGroupOf(offender);
            MapEvent? battle = combatTarget.MapEvent ?? offender.MapEvent;
            BattleSideEnum targetSide = combatTarget.Party.Side != BattleSideEnum.None
                ? combatTarget.Party.Side : offender.Party.Side;
            var countedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (battle?.IsFinalized == false && targetSide != BattleSideEnum.None)
            {
                foreach (MapEventParty participant in battle.PartiesOnSide(targetSide))
                {
                    if (participant.Party?.IsActive != true) continue;
                    snapshot.Strength += Math.Max(0f, participant.Party.EstimatedStrength);
                    MobileParty? member = participant.Party.MobileParty;
                    if (member == null) continue;
                    countedGroups.Add(CombatGroupKey(member));
                    snapshot.CombatGroups.Add(member.StringId + ":" + participant.Party.EstimatedStrength.ToString("0.00", CultureInfo.InvariantCulture));
                    IncludeMaximumCombatGroupSpeed(snapshot, member);
                }
            }
            else
            {
                snapshot.Strength = CombatGroup(combatTarget);
                countedGroups.Add(CombatGroupKey(combatTarget));
                snapshot.CombatGroups.Add(CombatGroupKey(combatTarget) + ":" + snapshot.Strength.ToString("0.00", CultureInfo.InvariantCulture));
                IncludeMaximumCombatGroupSpeed(snapshot, combatTarget);
            }
            snapshot.JoiningRadius = Math.Max(0f, Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius);
            snapshot.ThreatRadius = GwpTuning.Enforcement.RegionalThreatRadius;
            Vec2 center = battle?.IsFinalized == false
                ? battle.Position.ToVec2() : combatTarget.Position.ToVec2();
            LocatableSearchData<MobileParty> data = MobileParty.StartFindingLocatablesAroundPosition(
                center, snapshot.ThreatRadius);
            for (MobileParty nearby = MobileParty.FindNextLocatable(ref data); nearby != null;
                 nearby = MobileParty.FindNextLocatable(ref data))
            {
                MobileParty group = CombatGroupOf(nearby);
                string key = CombatGroupKey(group);
                if (countedGroups.Contains(key) ||
                    group.Position.ToVec2().Distance(center) > snapshot.ThreatRadius ||
                    !CanNearbyCombatGroupJoinTarget(group, combatTarget, battle, targetSide, observer)) continue;
                float contribution = CombatGroup(group);
                snapshot.Strength += contribution;
                snapshot.CombatGroups.Add(key + ":" + contribution.ToString("0.00", CultureInfo.InvariantCulture) + "@1.00");
                countedGroups.Add(key);
                IncludeMaximumCombatGroupSpeed(snapshot, group);
            }
            return snapshot;
        }



        private static void IncludeMaximumCombatGroupSpeed(

            Threat snapshot, MobileParty party)

        {

            MobileParty movementGroup =

                CombatGroupOf(party);

            float speed = TheoreticalBaseSpeed(movementGroup);

            if (speed <= snapshot.MaximumSpeed)

                return;



            snapshot.MaximumSpeed = speed;

            snapshot.FastestCombatGroupId =

                CombatGroupKey(movementGroup);

        }



        internal static float CombatGroup(

            MobileParty combatTarget)

        {

            if (combatTarget?.IsActive != true ||

                combatTarget.Party == null)

                return 0f;



            if (combatTarget.BesiegerCamp != null)

            {

                float siegeStrength = combatTarget.BesiegerCamp

                    .GetInvolvedPartiesForEventType()

                    .Sum(party =>

                        Math.Max(0f, party.EstimatedStrength));

                if (siegeStrength > 0f)

                    return siegeStrength;

            }



            Army? army = combatTarget.Army;

            return army != null &&

                   (combatTarget.AttachedTo != null ||

                    army.LeaderParty == combatTarget)

                ? Math.Max(0f, army.EstimatedStrength)

                : Party(combatTarget);

        }



        internal static string CombatGroupKey(MobileParty party) =>

            CombatGroupOf(party).StringId;



        private static bool CanNearbyCombatGroupJoinTarget(MobileParty candidate, MobileParty combatTarget,
            MapEvent? mapEvent, BattleSideEnum targetSide, MobileParty observer)
        {
            if (candidate?.IsActive != true || candidate.Party == null || candidate.Party.NumberOfHealthyMembers <= 0 ||
                candidate.AttachedTo != null || candidate.Position.IsOnLand != combatTarget.Position.IsOnLand ||
                candidate.IsGarrison || candidate.IsMilitia || candidate.CurrentSettlement?.SiegeEvent != null ||
                candidate.BesiegerCamp?.LeaderParty != null && candidate.BesiegerCamp.LeaderParty != candidate ||
                candidate.MapEvent?.IsFinalized == false && candidate.MapEvent != mapEvent) return false;
            if (mapEvent?.IsFinalized == false && targetSide != BattleSideEnum.None)
                return mapEvent.CanPartyJoinBattle(candidate.Party, targetSide);
            IFaction? faction = candidate.MapFaction;
            return faction != null && combatTarget.MapFaction != null &&
                (faction == combatTarget.MapFaction || observer.MapFaction != null && faction.IsAtWarWith(observer.MapFaction)) &&
                (candidate.IsLordParty || candidate.Aggressiveness > 0.01f);
        }



        internal static float FriendlyAround(MobileParty actor, MobileParty offender,
            out string friendlyCombatGroups, bool includePlayer = false)
        {
            MobileParty target = CombatGroupOf(offender);
            float strength = CombatGroup(actor);
            var counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CombatGroupKey(actor) };
            var groups = new List<string> { CombatGroupKey(actor) + ":" + strength.ToString("0.00", CultureInfo.InvariantCulture) + "@1.00" };
            // Enemy risk uses a fixed target-centred region. Friendly reinforcements
            // must already be within the native joining radius of the acting party;
            // promising to help, sharing an objective, or joining Army is not arrival.
            float joiningRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
            LocatableSearchData<MobileParty> data = MobileParty.StartFindingLocatablesAroundPosition(actor.Position.ToVec2(), joiningRadius);
            for (MobileParty nearby = MobileParty.FindNextLocatable(ref data); nearby != null;
                 nearby = MobileParty.FindNextLocatable(ref data))
            {
                MobileParty group = CombatGroupOf(nearby);
                string key = CombatGroupKey(group);
                if (counted.Contains(key) || !CanNearbyFriendlyGroupJoinActor(group, actor, includePlayer) ||
                    group.Position.ToVec2().Distance(actor.Position.ToVec2()) > joiningRadius ||
                    group.Position.ToVec2().Distance(target.Position.ToVec2()) > GwpTuning.Enforcement.RegionalThreatRadius) continue;
                float contribution = CombatGroup(group);
                strength += contribution;
                groups.Add(key + ":" + contribution.ToString("0.00", CultureInfo.InvariantCulture) + "@1.00");
                counted.Add(key);
            }
            friendlyCombatGroups = string.Join(",", groups);
            return Math.Max(0f, strength);
        }







        private static bool CanNearbyFriendlyGroupJoinActor(

            MobileParty candidate, MobileParty actor, bool includePlayer = false)

        {

            if (candidate?.IsActive != true || candidate.Party == null ||

                candidate.Party.NumberOfHealthyMembers <= 0 ||

                candidate.AttachedTo != null ||

                candidate.Position.IsOnLand != actor.Position.IsOnLand ||

                candidate.IsGarrison || candidate.IsMilitia ||

                candidate.CurrentSettlement?.SiegeEvent != null ||

                candidate.BesiegerCamp?.LeaderParty != null &&

                candidate.BesiegerCamp.LeaderParty != candidate)

                return false;



            // The player who asked for this help is one of the parties that will

            // actually fight. Being already engaged does not disqualify him - that

            // is the very battle the Wardens are being brought in to join.

            if (includePlayer && candidate.IsMainParty)

                return true;



            return (candidate.MapEvent == null || candidate.MapEvent.IsFinalized) &&

                   candidate.MapFaction != null &&

                   candidate.MapFaction == actor.MapFaction &&

                   (candidate.IsLordParty || candidate.Aggressiveness > 0.01f);

        }



        internal static LocalBalance

            Compare(

                MobileParty actor, MobileParty offender,

                bool includePlayer = false)

        {

            MobileParty movementTarget =

                CombatGroupOf(offender);

            Threat enemy =

                EnemyAround(actor, offender);

            float friendlyStrength =

                FriendlyAround(

                    actor, offender, out string friendlyCombatGroups,

                    includePlayer);

            float distance = actor.Position.ToVec2().Distance(

                movementTarget.Position.ToVec2());

            bool strengthReady =

                friendlyStrength > enemy.Strength;

            return new LocalBalance

            {

                Actor = actor,

                MovementTarget = movementTarget,

                FriendlyLocalStrength = friendlyStrength,

                EnemyLocalStrength = enemy.Strength,

                FriendlyCombatGroups = friendlyCombatGroups,

                Distance = distance,

                StrengthReady = strengthReady,

                Reason = strengthReady

                    ? "friendly_local_strength_ready"

                    : "friendly_local_strength_not_ready"

            };

        }

    }

}
