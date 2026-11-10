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
                   id.StartsWith(GwpIds.FaithPartyPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 交人、交货和还船前的共同门槛。上下船期间海陆标记仍是旧值，港口待登船也不能提前交割。
        /// 依据：实际游戏DLL复现2026-10-18；玩家送信队跨岸交割离线复现2026-10-18。
        /// </summary>
        internal static bool CanCompletePartyHandover(MobileParty first, MobileParty second) =>
            !first.IsTransitionInProgress && !second.IsTransitionInProgress &&
            !first.StartTransitionNextFrameToExitFromPort && !second.StartTransitionNextFrameToExitFromPort &&
            first.IsCurrentlyAtSea == second.IsCurrentlyAtSea;

        /// <summary>
        /// 能接收回流兵员的最近灰袍领主队：活着、没被俘、不在战斗里。原主队没了时，
        /// 从领主身上抽出去的人交给他，不在城里销毁。
        /// </summary>
        public static MobileParty? FindNearestGreyWardenLordParty(MobileParty from)
        {
            return MobileParty.All
                .Where(party => party?.IsActive == true && IsGreyWardenLordParty(party) &&
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

        // ---- 谁算灰袍：各功能只调这里，不另写一份。---------------------------------

        public static bool IsGreyWardenClan(Clan? clan)
        {
            return clan != null
                   && string.Equals(clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGreyWardenLord(Hero? hero)
        {
            return IsGreyWardenClanMember(hero)
                   && hero!.Occupation == Occupation.Lord;
        }

        /// <summary>
        /// 战场上的灰袍领主：战役里是当前灰袍家族的领主英雄，或成婚时还是灰袍领主、嫁出去的英雄
        /// （<see cref="GwpWardenMarriageBehavior.IsMarriedWarden"/>，用户裁定 2026-10-16：加成跟随她）；自定义战斗没有战役和英雄，
        /// 灰袍指挥官 <see cref="GwpIds.CustomBattleCommanderId"/> 只注册成 BasicCharacterObject，按这个 ID 认
        /// （实机 2026-10-16：自定义战斗里领主加成全部不生效）。
        /// </summary>
        public static bool IsGreyWardenLordAgent(Agent? agent) =>
            agent?.Character is CharacterObject character
                ? IsGreyWardenLord(character.HeroObject) || GwpWardenMarriageBehavior.IsMarriedWarden(character.HeroObject)
                : string.Equals(agent?.Character?.StringId, GwpIds.CustomBattleCommanderId, StringComparison.Ordinal);

        public static bool IsGreyWardenClanMember(Hero? hero)
        {
            return IsGreyWardenClan(hero?.Clan);
        }

        /// <summary>归灰袍家族所有的部队（领主队，以及 ActualClan 设成灰袍的无领主办差队）。</summary>
        public static bool IsGreyWardenClanParty(MobileParty? party)
        {
            return IsGreyWardenClan(party?.ActualClan);
        }

        public static bool IsGreyWardenLordParty(MobileParty? party)
        {
            return party?.IsLordParty == true && IsGreyWardenClanParty(party);
        }

        /// <summary>
        /// 代表灰袍执法的部队：灰袍家族的部队，加上玩家纠察队——它是 CustomPartyComponent，
        /// ActualClan 不是灰袍，只能按编号认。
        /// </summary>
        public static bool IsGreyWardenEnforcementParty(MobileParty? party)
        {
            return party != null
                   && (IsGreyWardenClanParty(party) || IsPatrolParty(party) || IsEnforcementDelayPatrolParty(party));
        }

        /// <summary>玩家入了灰袍（招募时接受）。</summary>
        public static bool IsPlayerGreyWardenMember()
        {
            return Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.IsRecruitedByGreyWardens == true;
        }

        /// <summary>玩家主动执勤：已入会，且把灰袍执勤旗装备在原版旗帜槽；背包持有不算。</summary>
        public static bool IsPlayerGreyWardenOnDuty() =>
            IsPlayerGreyWardenMember() && string.Equals(Hero.MainHero?.BannerItem.Item?.StringId,
                GwpIds.DutyBannerItemId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 当前对话是灰袍在对玩家执法：纠察队、延迟纠察队，或正在追捕玩家的承办人。
        /// 这时只走执法对话，别的灰袍选项都不出。
        /// </summary>
        public static bool IsEnforcementConversation()
        {
            MobileParty? party = MobileParty.ConversationParty;
            if (party == null) return false;
            if (IsPatrolParty(party) || IsEnforcementDelayPatrolParty(party)) return true;
            return CrimePool.GetTask(party.StringId)?.TargetCrime?.Offender?.IsMainParty == true;
        }

        /// <summary>
        /// 代表灰袍出面的人：族长；族长出不了面（死、未成年、被俘）时取第一个能出面的成员。
        /// 赎罪任务的委托人、执法交易的对手都用它。
        /// </summary>
        public static Hero? FindGreyWardenSpokesperson()
        {
            Clan? clan = PoliceStats.GetPoliceClan();
            if (clan == null) return null;
            static bool CanAppear(Hero? h) => h != null && h.IsActive && !h.IsDead && !h.IsChild && !h.IsPrisoner;
            return CanAppear(clan.Leader) ? clan.Leader : clan.Heroes.FirstOrDefault(CanAppear);
        }

        /// <summary>和灰袍领主的普通对话：对方是灰袍领主，而且不是在对玩家执法。</summary>
        public static bool IsOrdinaryGreyWardenLordConversation()
        {
            return IsGreyWardenLord(Hero.OneToOneConversationHero) && !IsEnforcementConversation();
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
            settlement?.IsTown == true || settlement?.IsCastle == true || settlement?.IsVillage == true;

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
        /// 销毁一支灰袍自己造出来的队伍（纠察队、派遣队、练兵队等）；空引用时什么都不做。
        /// 不吞异常：引擎销毁时抛出的错误照常暴露。
        /// </summary>
        public static void TryDestroyParty(MobileParty? party)
        {
            if (party == null) return;
            DestroyPartyAction.Apply(null, party);
        }


        /// <summary>
        /// 让一支原版领主部队去某个聚落并停在那里，直到 <see cref="TryResetAi"/> 放开。
        /// 军团成员先离队，和原版成婚时离开军团同一个做法（MarriageAction）；原版把停决策这一位存进存档，
        /// 读档后接得上。他正在打仗时不动名册与命令，下次再调。
        /// </summary>
        public static void HoldPartyAt(MobileParty party, Settlement settlement)
        {
            if (party.MapEvent != null) return;
            if (party.Army != null && party.Army.LeaderParty != party)
                party.Army = null;
            if (party.DefaultBehavior != AiBehavior.GoToSettlement || party.TargetSettlement != settlement)
            {
#if GWP_DIAGNOSTICS
                bool repairingHeldMovement = party.Ai.DoNotMakeNewDecisions;
                AiBehavior previousBehavior = party.DefaultBehavior;
#endif
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.SetMoveGoToSettlement(settlement, party.NavigationCapability, false);
#if GWP_DIAGNOSTICS
                if (repairingHeldMovement)
                    GwpAiDiagnostics.WriteFieldArrest("HELD_PARTY_MOVEMENT_REPAIRED",
                        "party=" + party.StringId + "; town=" + settlement.StringId +
                        "; currentSettlement=" + (party.CurrentSettlement?.StringId ?? "-") +
                        "; previousBehavior=" + previousBehavior + "; behavior=" + party.DefaultBehavior);
#endif
            }
            party.Ai.SetDoNotMakeNewDecisions(true);
        }

        /// <summary>投案与躲城共用的去处：离部队最近、属于本国且没被围的城镇或城堡。</summary>
        internal static Settlement? NearestOwnFortification(MobileParty? party)
        {
            if (party?.MapFaction == null) return null;
            return Settlement.All
                .Where(settlement => settlement.IsFortification && settlement.MapFaction == party.MapFaction
                    && settlement.SiegeEvent == null)
                .OrderBy(settlement => settlement.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
        }

        /// <summary>按存档里记下的部队编号找回那支部队；找不到（已消灭）时为空。</summary>
        public static MobileParty? FindParty(string partyId) =>
            string.IsNullOrEmpty(partyId) ? null : MobileParty.All.FirstOrDefault(party => party.StringId == partyId);

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
