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

    /// 口径照原版 <c>DefaultMobilePartyAIModel.GetBestInitiativeBehavior</c>（C#反编译，

    /// 2026-07-24/25 核对）：以目标所在战斗组为基础，加上加入半径内圈全额、外到两倍半径

    /// 线性折算的周边可参战部队；已在打仗时改按 <c>MapEvent</c> 实际参战方；正在追同一个

    /// 目标的友军全额计入。单队数值一律用 <c>PartyBase.EstimatedStrength</c>。

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
        /// 身边路过的人**——那一档是临场变量，由协力编成和现场宣战去应对；这里回答的是
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



        internal static Threat

            EnemyAround(

                MobileParty observer, MobileParty offender)

        {

            var snapshot = new Threat();

            if (observer?.IsActive != true ||

                offender?.IsActive != true || offender.Party == null)

                return snapshot;



            MobileParty combatTarget =

                CombatGroupOf(offender);

            MapEvent? mapEvent = combatTarget.MapEvent ?? offender.MapEvent;

            BattleSideEnum targetSide = combatTarget.Party.Side !=

                                        BattleSideEnum.None

                ? combatTarget.Party.Side

                : offender.Party.Side;

            var countedCombatGroups = new HashSet<string>(

                StringComparer.OrdinalIgnoreCase);



            if (mapEvent?.IsFinalized == false &&

                targetSide != BattleSideEnum.None)

            {

                foreach (MapEventParty participant in

                         mapEvent.PartiesOnSide(targetSide))

                {

                    if (participant.Party?.IsActive != true)

                        continue;



                    float strength = Math.Max(

                        0f, participant.Party.EstimatedStrength);

                    snapshot.Strength += strength;

                    string participantId =

                        participant.Party.MobileParty?.StringId ??

                        participant.Party.Id;

                    snapshot.CombatGroups.Add(

                        participantId + ":" + strength.ToString(

                            "0.00", CultureInfo.InvariantCulture));

                    MobileParty? participantMobile =

                        participant.Party.MobileParty;

                    if (participantMobile != null)

                    {

                        countedCombatGroups.Add(

                            CombatGroupKey(participantMobile));

                        IncludeMaximumCombatGroupSpeed(

                            snapshot, participantMobile);

                    }

                }

            }

            else

            {

                float baseStrength =

                    CombatGroup(combatTarget);

                snapshot.Strength = baseStrength;

                countedCombatGroups.Add(CombatGroupKey(combatTarget));

                snapshot.CombatGroups.Add(

                    CombatGroupKey(combatTarget) + ":" +

                    baseStrength.ToString(

                        "0.00", CultureInfo.InvariantCulture));

                IncludeMaximumCombatGroupSpeed(snapshot, combatTarget);

            }



            snapshot.JoiningRadius = Math.Max(0f,

                Campaign.Current?.Models?.EncounterModel

                    ?.GetEncounterJoiningRadius ?? 0f);

            if (snapshot.JoiningRadius <= 0.01f)

                return snapshot;

            // DefaultMobilePartyAIModel uses a joining-radius inner ring and a

            // second, tapered ring out to twice that radius while evaluating

            // local strength. Use the same envelope rather than crediting every

            // nearby party at full strength.

            snapshot.ThreatRadius = snapshot.JoiningRadius * 2f;



            Vec2 center = mapEvent?.IsFinalized == false

                ? mapEvent.Position.ToVec2()

                : combatTarget.Position.ToVec2();

            float observerDistance =

                observer.Position.ToVec2().Distance(center);

            float supportRadiusFactor = 1f + Math.Max(

                0f,

                (observerDistance - 1f) /

                Math.Max(0.01f,

                    (snapshot.JoiningRadius - 1f) * 2f));

            supportRadiusFactor = Math.Min(2f, supportRadiusFactor);

            float effectiveSupportRadius =

                snapshot.JoiningRadius * supportRadiusFactor;

            LocatableSearchData<MobileParty> data =

                MobileParty.StartFindingLocatablesAroundPosition(

                    center, snapshot.ThreatRadius);

            for (MobileParty nearby =

                     MobileParty.FindNextLocatable(ref data);

                 nearby != null;

                 nearby = MobileParty.FindNextLocatable(ref data))

            {

                MobileParty nearbyGroup =

                    CombatGroupOf(nearby);

                string groupKey = CombatGroupKey(nearbyGroup);

                if (countedCombatGroups.Contains(groupKey) ||

                    !CanNearbyCombatGroupJoinTarget(

                        nearbyGroup, combatTarget, mapEvent, targetSide))

                    continue;



                float groupDistance =

                    nearbyGroup.Position.ToVec2().Distance(center);

                if (groupDistance > effectiveSupportRadius)

                    continue;



                float supportFactor = 1f;

                if (groupDistance > snapshot.JoiningRadius &&

                    supportRadiusFactor > 1.0001f)

                {

                    supportFactor = 1f -

                        (groupDistance - snapshot.JoiningRadius) /

                        (snapshot.JoiningRadius *

                         (supportRadiusFactor - 1f));

                    supportFactor = Math.Max(0f,

                        Math.Min(1f, supportFactor));

                }



                float strength = CombatGroup(

                    nearbyGroup) * supportFactor;

                if (strength <= 0f)

                    continue;



                countedCombatGroups.Add(groupKey);

                snapshot.Strength += strength;

                IncludeMaximumCombatGroupSpeed(snapshot, nearbyGroup);

                snapshot.CombatGroups.Add(

                    groupKey + ":" + strength.ToString(

                        "0.00", CultureInfo.InvariantCulture));

            }



            snapshot.Strength = Math.Max(0f, snapshot.Strength);

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



        private static bool CanNearbyCombatGroupJoinTarget(

            MobileParty candidate, MobileParty combatTarget,

            MapEvent? mapEvent, BattleSideEnum targetSide)

        {

            if (candidate?.IsActive != true || candidate.Party == null ||

                candidate.Party.NumberOfHealthyMembers <= 0 ||

                candidate.AttachedTo != null ||

                candidate.Position.IsOnLand !=

                combatTarget.Position.IsOnLand)

                return false;



            // 守军和民兵守在墙里，不会跟着通缉犯满地图跑，所以不计入"办这个案子

            // 要多少兵"。这一条**必须排在战斗分支之前**：原版

            // MapEvent.CanPartyJoinBattle 对城墙边上的守军是放行的，一旦截击队在

            // 城堡门口咬住目标，守军和民兵就会被算进目标战力。

            //

            // 实机代价（castle_S6）：犯人本队 132.37，守军 327.88，民兵 246.30，

            // 合计 706.55，超过全家族能凑出来的 682.70，于是判定"整个灰袍都打不过"

            // 撤案、讲和，那场已经打起来的仗随之失去交战依据，人就走了。

            //

            // 真的已经在场上参战的守军不会被漏掉：它们由

            // EnemyAround 的 mapEvent.PartiesOnSide 那一段直接

            // 计入。这里排除的只是"还没参战、只是理论上可以参战"的那一类。

            if (candidate.IsGarrison || candidate.IsMilitia ||

                candidate.CurrentSettlement?.SiegeEvent != null ||

                candidate.BesiegerCamp?.LeaderParty != null &&

                candidate.BesiegerCamp.LeaderParty != candidate)

                return false;



            if (mapEvent?.IsFinalized == false &&

                targetSide != BattleSideEnum.None)

            {

                return mapEvent.CanPartyJoinBattle(

                    candidate.Party, targetSide);

            }



            if (candidate.MapEvent != null)

                return false;



            IFaction? targetFaction = combatTarget.MapFaction;

            IFaction? candidateFaction = candidate.MapFaction;

            if (targetFaction == null || candidateFaction == null)

                return false;

            return candidateFaction == targetFaction &&

                   candidate.Aggressiveness > 0.01f;

        }



        internal static float FriendlyAround(

            MobileParty actor, MobileParty offender,

            out string friendlyCombatGroups,

            bool includePlayer = false)

        {

            MobileParty movementTarget =

                CombatGroupOf(offender);

            float joiningRadius = Math.Max(0f,

                Campaign.Current?.Models?.EncounterModel

                    ?.GetEncounterJoiningRadius ?? 0f);

            float actorStrength = CombatGroup(actor);

            float strength = actorStrength;

            var strengthGroups = new List<string>

            {

                CombatGroupKey(actor) + ":" +

                actorStrength.ToString("0.00", CultureInfo.InvariantCulture) +

                "@1.00"

            };

            if (joiningRadius <= 0.01f)

            {

                friendlyCombatGroups = string.Join(",", strengthGroups);

                return strength;

            }



            Vec2 targetPosition = movementTarget.Position.ToVec2();

            float actorDistance =

                actor.Position.ToVec2().Distance(targetPosition);

            float supportRadiusFactor = 1f + Math.Max(

                0f,

                (actorDistance - 1f) /

                Math.Max(0.01f, (joiningRadius - 1f) * 2f));

            supportRadiusFactor = Math.Min(2f, supportRadiusFactor);

            float effectiveSupportRadius =

                joiningRadius * supportRadiusFactor;

            var countedGroups = new HashSet<string>(

                StringComparer.OrdinalIgnoreCase)

            {

                CombatGroupKey(actor)

            };



            LocatableSearchData<MobileParty> data =

                MobileParty.StartFindingLocatablesAroundPosition(

                    actor.Position.ToVec2(), joiningRadius * 3f);

            for (MobileParty nearby =

                     MobileParty.FindNextLocatable(ref data);

                 nearby != null;

                 nearby = MobileParty.FindNextLocatable(ref data))

            {

                MobileParty nearbyGroup =

                    CombatGroupOf(nearby);

                string groupKey = CombatGroupKey(nearbyGroup);

                if (countedGroups.Contains(groupKey) ||

                    !CanNearbyFriendlyGroupJoinActor(

                        nearbyGroup, actor, includePlayer))

                    continue;



                float groupDistance =

                    nearbyGroup.Position.ToVec2().Distance(targetPosition);

                if (groupDistance > joiningRadius * 2f)

                    continue;



                bool sharesTarget =

                    IsFriendlyGroupCoordinatingOnTarget(

                        nearbyGroup, movementTarget);

                if (!sharesTarget && groupDistance > effectiveSupportRadius)

                    continue;



                float supportFactor = 1f;

                if (!sharesTarget && groupDistance > joiningRadius &&

                    supportRadiusFactor > 1.0001f)

                {

                    supportFactor = 1f -

                        (groupDistance - joiningRadius) /

                        (joiningRadius * (supportRadiusFactor - 1f));

                    supportFactor = Math.Max(

                        0f, Math.Min(1f, supportFactor));

                }



                float fullStrength =

                    CombatGroup(nearbyGroup);

                float contributedStrength =

                    fullStrength * supportFactor;

                strength += contributedStrength;

                strengthGroups.Add(

                    groupKey + ":" +

                    contributedStrength.ToString(

                        "0.00", CultureInfo.InvariantCulture) +

                    "@" + supportFactor.ToString(

                        "0.00", CultureInfo.InvariantCulture));

                countedGroups.Add(groupKey);

            }

            friendlyCombatGroups = string.Join(",", strengthGroups);

            return Math.Max(0f, strength);

        }



        private static bool IsFriendlyGroupCoordinatingOnTarget(

            MobileParty candidate, MobileParty movementTarget)

        {

            PartyBase? aiTarget = candidate.Army?.LeaderParty?.Ai

                                      ?.AiBehaviorPartyBase ??

                                  candidate.Ai?.AiBehaviorPartyBase;

            if (aiTarget == movementTarget.Party)

                return true;



            MapEvent? targetEvent = movementTarget.MapEvent;

            if (aiTarget?.MapEvent != null && targetEvent != null &&

                aiTarget.MapEvent == targetEvent)

                return true;



            if (PoliceEnforcementBehavior.TryGetAssistanceDuty(candidate,

                    out MobileParty? dutyTarget, out _, out _, out _) &&

                dutyTarget?.IsActive == true)

            {

                return CombatGroupOf(dutyTarget) ==

                       movementTarget;

            }



            return false;

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



            return candidate.MapEvent == null &&

                   candidate.MapFaction != null &&

                   candidate.MapFaction == actor.MapFaction &&

                   candidate.Aggressiveness > 0.01f;

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
