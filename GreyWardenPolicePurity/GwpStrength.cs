using System;


using System.Globalization;

using System.Linq;

using TaleWorlds.CampaignSystem;



using TaleWorlds.CampaignSystem.Party;


using TaleWorlds.Library;

using TaleWorlds.Localization;



namespace GreyWardenPolicePurity

{

    /// <summary>

    /// 大地图上「谁比谁强」的唯一实现。立案门槛、派单、协力编成、现场宣战、野外执法、

    /// 悬赏难度都从这里取数；要改战力口径只改这里。

    ///

    /// 本体战力用于接案与编成；宣战和主动攻击读取原版区域战力计算。
    /// 区域友军、敌军资格与距离折算由原版判断，不以远方共同任务代替到场。
    /// 依据：用户裁定 2026-10-18。单队数值仍用 PartyBase.EstimatedStrength。
    ///
    /// 战斗场景里的兵力（音乐、战场支援）是另一回事，按 Agent 计算，不走这里。

    /// </summary>

    internal static partial class GwpStrength

    {

        internal sealed class LocalBalance

        {

            public MobileParty Actor { get; set; } = null!;

            public MobileParty MovementTarget { get; set; } = null!;

            public float FriendlyLocalStrength { get; set; }

            public float EnemyLocalStrength { get; set; }

            public string NativeComparisons { get; set; } = string.Empty;

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



        internal static LocalBalance Compare(MobileParty actor, MobileParty offender,
            bool includePlayer = false, MobileParty? excludedFriendly = null)
        {
            MobileParty target = CombatGroupOf(offender);
            NativeFrame frame = ReadNativeRegion(actor, offender, includePlayer, excludedFriendly);
            NativeContact? contact = frame.Contacts.FirstOrDefault(item => item.Target == target);
            NativeContact? unsafeContact = UnsafeNativeContact(frame, target);
            NativeContact? decisive = unsafeContact ?? contact;
            bool ready = contact != null && unsafeContact == null;
            return new LocalBalance
            {
                Actor = actor,
                MovementTarget = target,
                FriendlyLocalStrength = decisive?.Friendly ?? CombatGroup(actor),
                EnemyLocalStrength = decisive?.Enemy ?? CombatGroup(target),
                NativeComparisons = string.Join(",", frame.Contacts.Select(item =>
                    item.Target.StringId + ":native=" + item.Friendly.ToString("0.00", CultureInfo.InvariantCulture) +
                    "/" + item.Enemy.ToString("0.00", CultureInfo.InvariantCulture))),
                Distance = actor.Position.ToVec2().Distance(target.Position.ToVec2()),
                StrengthReady = ready,
                Reason = contact == null ? "target_not_in_native_region" : unsafeContact != null
                    ? "native_region_disadvantage:" + unsafeContact.Target.StringId : "native_region_ready"
            };
        }
    }
}
