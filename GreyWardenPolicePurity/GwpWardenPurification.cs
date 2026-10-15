using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 纯化（用户裁定 2026-10-15，取代 10-07 的「原版兵留在灰袍队里」）：不论从哪条路进了灰袍领主队——要人那里招的、
    /// 雇的佣兵、招降的俘虏、战后解救的——非灰袍兵都按等级换成灰袍兵，他身上那套装备交进这支部队的军械库。
    /// 守村队里的原版民兵、地牢里的俘虏不在这里，它们本来就会被烧掉。每小时一次，正在打仗的部队等仗打完。
    /// </summary>
    internal static class GwpWardenPurification
    {
        internal static void Purify(MobileParty party)
        {
            if (!GwpCommon.IsGreyWardenLordParty(party) || party.MapEvent != null)
                return;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
            {
                CharacterObject troop = element.Character;
                if (troop == null || troop.IsHero || GwpCommon.IsGreyWardenTroop(troop) || element.Number <= 0)
                    continue;
                CharacterObject? warden = Counterpart(troop);
                if (warden == null)
                    continue;
                int converted = GwpTroopTransfer.Convert(party.MemberRoster, troop, warden);
                int kitItems = GwpFireswornBehavior.DepositTroopKits(party, troop, converted);
                GwpAiDiagnostics.WriteAction(party, "WARDEN_PURIFIED",
                    "from=" + troop.StringId + "; tier=" + troop.Tier + "; to=" + warden.StringId +
                    "; count=" + converted + "; wounded=" + element.WoundedNumber + "; kitItems=" + kitItems);
            }
        }

        /// <summary>
        /// 按等级对应灰袍兵种。灰袍兵种树只有 2 阶狂信者、3 阶新兵、4 阶灰袍兵、6 阶重步兵 / 弓手 / 骑士：
        /// 1–2 阶成狂信者，3 阶成新兵，4 阶成灰袍兵，5 阶以上按兵种——骑马的成骑士，远程的成弓手，其余成重步兵。
        /// （依据：用户裁定 2026-10-15）
        /// </summary>
        internal static CharacterObject? Counterpart(CharacterObject troop)
        {
            string id = troop.Tier <= 2 ? GwpIds.ZealotId
                : troop.Tier == 3 ? GwpIds.NewRecruitId
                : troop.Tier == 4 ? GwpIds.PoliceRecruitId
                : troop.IsMounted ? GwpIds.KnightId
                : troop.IsRanged ? GwpIds.ArcherId
                : GwpIds.HeavyInfantryId;
            return CharacterObject.Find(id);
        }
    }
}
