using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // Temporary scene troops inherit the supported unit's real combatant and
    // command affiliation, but never add/remove campaign roster members.
    internal sealed class GwpBattleSupportOrigin : IAgentOriginBase
    {
        private readonly IAgentOriginBase _owner, _traits;
        private readonly UniqueTroopDescriptor _id = new(Game.Current.NextUniqueTroopSeed);
        internal GwpBattleSupportOrigin(BasicCharacterObject troop, IAgentOriginBase owner)
        { Troop = troop; _owner = owner; _traits = new BasicBattleAgentOrigin(troop); }
        public BasicCharacterObject Troop { get; }
        public bool IsUnderPlayersCommand => _owner.IsUnderPlayersCommand;
        public bool IsInSameArmyAsPlayer => _owner.IsInSameArmyAsPlayer;
        public IBattleCombatant BattleCombatant => _owner.BattleCombatant;
        public uint FactionColor => _owner.FactionColor;
        public uint FactionColor2 => _owner.FactionColor2;
        public int UniqueSeed => _id.UniqueSeed;
        public int Seed => Troop.GetDefaultFaceSeed(UniqueSeed);
        public Banner Banner => _owner.Banner;
        public bool HasThrownWeapon => _traits.HasThrownWeapon;
        public bool HasHeavyArmor => _traits.HasHeavyArmor;
        public bool HasShield => _traits.HasShield;
        public bool HasSpear => _traits.HasSpear;
        public TroopTraitsMask GetTraitsMask() => _traits.GetTraitsMask();
        public void SetWounded() { }
        public void SetKilled() { }
        public void SetRouted(bool isOrderRetreat) { }
        public void OnAgentRemoved(float agentHealth) { }
        public void SetBanner(Banner banner) { }
        public void OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain,
            int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }
    }
}
