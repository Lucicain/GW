using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System.Linq;

namespace GreyWardenPolicePurity
{
    internal static class GwpCommon
    {
        public const string PatrolIdPrefix = GwpIds.PatrolIdPrefix;
        public const string EnforcementDelayPatrolIdPrefix = GwpIds.EnforcementDelayPatrolIdPrefix;
        public const string TrainingCohortIdPrefix = GwpIds.TrainingCohortIdPrefix;
        public const string HeavyInfantryId = GwpIds.HeavyInfantryId;
        public const string ArcherId = GwpIds.ArcherId;
        public const string KnightId = GwpIds.KnightId;

        public static bool IsPatrolParty(MobileParty? party)
        {
            return party?.StringId?.StartsWith(PatrolIdPrefix, StringComparison.Ordinal) == true;
        }

        public static bool IsTrainingCohortParty(MobileParty? party)
        {
            return party?.StringId?.StartsWith(TrainingCohortIdPrefix,
                StringComparison.Ordinal) == true;
        }

        public static bool IsEnforcementDelayPatrolParty(MobileParty? party)
        {
            return party?.StringId?.StartsWith(EnforcementDelayPatrolIdPrefix, StringComparison.Ordinal) == true;
        }

        /// <summary>
        /// 我们自己建的无领主办差队：练兵队、纠察队、追截/延迟纠察队、招募使者、
        /// 玩家送信队、永恒之火门徒。它们拿不到原版给领主队的补给（进城买粮要求有领主英雄），
        /// 所以一律不吃饭，见 <see cref="GwpLeaderlessFoodModel"/>。
        /// </summary>
        public static bool IsLeaderlessDutyParty(MobileParty? party)
        {
            string? id = party?.StringId;
            if (string.IsNullOrEmpty(id) || party!.LeaderHero != null) return false;
            return id!.StartsWith(TrainingCohortIdPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(PatrolIdPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(EnforcementDelayPatrolIdPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(GwpIds.RecruitmentPatrolPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(GwpWardenDispatchBehavior.DispatchPartyPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(GwpIds.DisciplePartyPrefix, StringComparison.Ordinal) ||
                   id.StartsWith(GwpIds.ZealotCaravanPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 能接收回流兵员的最近灰袍领主队：活着、没被俘、不在战斗里。原主队没了时，
        /// 从领主身上抽出去的人交给他，不在城里销毁。
        /// </summary>
        public static MobileParty? FindNearestGreyWardenLordParty(MobileParty from)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;
            return MobileParty.All
                .Where(party => party?.IsActive == true && party.IsLordParty &&
                    party.ActualClan == policeClan &&
                    party.LeaderHero?.IsActive == true &&
                    party.LeaderHero.IsPrisoner != true &&
                    party.MapEvent == null)
                .OrderBy(party => party.GetPosition2D.Distance(from.GetPosition2D))
                .FirstOrDefault();
        }

        public static bool ShouldIgnoreCrimeTracking(MobileParty? party)
        {
            return party?.IsPatrolParty == true;
        }

        /// <summary>
        /// 目标已经躲进定居点。此时不能用原版跟随去追：
        /// <c>MobilePartyAi.GetFollowBehavior</c> 会把跟随转成 <c>GoToSettlement</c>
        /// 并一路进城，承办人会在城里满足宣战距离，随后可能当场被守军俘虏。
        /// 围堵与驱逐仍由 <c>HandleShelteredCriminal</c> 按既有流程负责。
        /// </summary>
        public static bool IsShelteredOffender(MobileParty? offender)
        {
            return offender?.IsActive == true && offender.CurrentSettlement != null;
        }

        public static bool IsGreyWardenLord(Hero? hero)
        {
            return IsGreyWardenClanMember(hero)
                   && hero!.Occupation == Occupation.Lord;
        }

        public static bool IsGreyWardenClanMember(Hero? hero)
        {
            return hero?.Clan != null
                   && string.Equals(
                       hero.Clan.StringId,
                       GwpIds.PoliceClanId,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 战场上这个兵能不能替本方接灰袍援军：得是灰袍的人（灰袍兵种或灰袍家族英雄），而且出自灰袍的部队——
        /// 灰袍家族的部队、信众村民兵。玩家自己部队里的灰袍兵只有玩家本人合资格（入会且声望够）时才算：
        /// 玩家不是灰袍的人，就不该有灰袍援军（用户裁定 2026-10-06；实机：玩家攻击狂信者队被打倒后，
        /// 自己部队里的灰袍兵替玩家这一方接到了援军）。自定义战斗没有战役，按兵种判断。
        /// </summary>
        public static bool IsGreyWardenSupportAgent(Agent agent, bool playerEligible)
        {
            if (!IsGreyWardenAffiliatedCharacter(agent.Character))
                return false;
            if (Campaign.Current == null)
                return true;
            PartyBase? party = agent.Origin switch
            {
                PartyAgentOrigin origin => origin.Party,
                PartyGroupAgentOrigin origin => origin.Party,
                SimpleAgentOrigin origin => origin.Party,
                _ => null
            };
            MobileParty? mobile = party?.MobileParty;
            if (mobile == null)
                return false;
            if (mobile.IsMainParty)
                return playerEligible;
            return string.Equals(mobile.ActualClan?.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase) ||
                   (mobile.IsMilitia && GwpFireswornBehavior.IsPreachedVillage(mobile.HomeSettlement));
        }

        public static bool IsGreyWardenAffiliatedCharacter(
            BasicCharacterObject? character)
        {
            if (character == null)
                return false;

            if (IsGreyWardenTroopId(character.StringId)
                || string.Equals(
                    character.StringId,
                    GwpIds.CustomBattleCommanderId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return character is CharacterObject campaignCharacter
                   && IsGreyWardenClanMember(campaignCharacter.HeroObject);
        }

        public static bool IsGreyWardenTroop(CharacterObject? character)
        {
            if (character == null || character.HeroObject != null)
                return false;

            return IsGreyWardenTroopId(character.StringId);
        }

        public static bool IsGreyWardenTroopId(string? characterId)
        {
            return !string.IsNullOrWhiteSpace(characterId)
                   && characterId!.StartsWith("gw", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDeserterParty(MobileParty? party)
        {
            return party?.ActualClan != null &&
                   string.Equals(party.ActualClan.StringId, "deserters", StringComparison.OrdinalIgnoreCase);
        }

        public static bool RemoveGreyWardenTroops(TroopRoster? roster)
        {
            if (roster == null || roster.TotalRegulars <= 0)
                return false;

            bool removedAny = false;
            foreach (TroopRosterElement element in roster.GetTroopRoster().ToList())
            {
                if (!IsGreyWardenTroop(element.Character) || element.Number <= 0)
                    continue;

                roster.AddToCounts(
                    element.Character,
                    -element.Number,
                    insertAtFront: false,
                    -element.WoundedNumber);
                removedAny = true;
            }

            return removedAny;
        }

        public static Settlement? FindNearestTown(MobileParty? party)
        {
            return party == null ? null : FindNearestTown(party.GetPosition2D);
        }

        public static Settlement? FindNearestTown(Vec2 position)
        {
            Settlement? nearest = null;
            float minDistance = float.MaxValue;

            foreach (Settlement settlement in Settlement.All)
            {
                if (!settlement.IsTown) continue;

                float distance = position.Distance(settlement.GetPosition2D);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = settlement;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 用于玩家可见的位置描述：城镇、城堡和村庄都参与比较，
        /// 但不把藏身处等特殊地点当作普通地理参照。
        /// </summary>
        public static bool IsOrdinarySettlement(Settlement? settlement) =>
            settlement?.IsTown == true || settlement?.IsVillage == true;

        public static Settlement? FindNearestSettlement(Vec2 position)
        {
            Settlement? nearest = null;
            float minDistance = float.MaxValue;

            foreach (Settlement settlement in Settlement.All)
            {
                if (!IsOrdinarySettlement(settlement)) continue;

                float distance = position.Distance(settlement.GetPosition2D);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = settlement;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 保留正常定居点；若原版状态给出藏身处等特殊地点，则以该地点为
        /// 坐标重新选择最近的城镇、城堡或村庄，保证玩家看到的地点可导航。
        /// </summary>
        public static Settlement? NormalizePlayerFacingSettlement(
            Settlement? settlement,
            Vec2 fallbackPosition)
        {
            if (IsOrdinarySettlement(settlement)) return settlement;
            return FindNearestSettlement(
                settlement?.GetPosition2D ?? fallbackPosition);
        }

        /// <summary>
        /// 销毁一支灰袍自己造出来的队伍（纠察队、派遣队、练兵队等）。
        ///
        /// 这些调用几乎都在清理流程里，而引擎在队伍正处于战斗、已被别处销毁、
        /// 或正卡在某个动作中途时会抛。清理途中抛出去会把后面还没清的一起带停，
        /// 所以照旧吞掉——但记一条，否则"队伍该消失却还在地图上"这类问题
        /// 排查时完全无从下手。
        ///
        /// 调用点信息由编译器在**各个调用处**填好再透传给诊断，不能让它们
        /// 全部记成本方法自己的位置——那样等于没记。
        /// </summary>
        public static void TryDestroyParty(MobileParty? party)
        {
            if (party == null) return;
            DestroyPartyAction.Apply(null, party);
        }

        public static void TrySetNeutral(IFaction? left, IFaction? right)
        {
            if (left == null || right == null) return;
            if (!FactionManager.IsAtWarAgainstFaction(left, right)) return;

            FactionManager.SetNeutral(left, right);
        }

        public static void TryResetAi(MobileParty? party)
        {
            if (party == null || !party.IsActive) return;
            party.Ai.SetDoNotMakeNewDecisions(false);
            party.Ai.RethinkAtNextHourlyTick = true;
        }

        public static void TryFinishPlayerEncounter()
        {
            if (!PlayerEncounter.IsActive) return;
            PlayerEncounter.LeaveEncounter = true;
            PlayerEncounter.Finish(false);
        }
    }
}
