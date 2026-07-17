using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Preserves the stock custom-battle allocation order while replacing its
    /// agent origin with a campaign-safe, non-persistent origin. Mission
    /// conversations require an origin whose battle combatant is a PartyBase;
    /// the stock CustomBattleAgentOrigin instead exposes a
    /// CustomBattleCombatant, which CampaignAgentComponent cannot cast.
    /// </summary>
    internal sealed class GreyWardenSafeTroopSupplier : IMissionTroopSupplier
    {
        private readonly CustomBattleTroopSupplier _inner;
        private readonly PartyBase _ownerParty;
        private readonly bool _isPlayerSide;
        private int _nextRank;

        internal GreyWardenSafeTroopSupplier(
            CustomBattleCombatant combatant,
            PartyBase ownerParty,
            bool isPlayerSide,
            bool isPlayerGeneral,
            bool isSallyOut)
        {
            _inner = new CustomBattleTroopSupplier(
                combatant,
                isPlayerSide,
                isPlayerGeneral,
                isSallyOut);
            _ownerParty = ownerParty;
            _isPlayerSide = isPlayerSide;
        }

        public int NumRemovedTroops => 0;

        public int NumTroopsNotSupplied => _inner.NumTroopsNotSupplied;

        public bool AnyTroopRemainsToBeSupplied =>
            _inner.AnyTroopRemainsToBeSupplied;

        public IEnumerable<IAgentOriginBase> SupplyTroops(
            int numberToAllocate)
        {
            foreach (IAgentOriginBase origin in
                     _inner.SupplyTroops(numberToAllocate))
            {
                yield return CreateSafeOrigin(origin.Troop);
            }
        }

        public IAgentOriginBase SupplyOneTroop()
        {
            IAgentOriginBase origin = _inner.SupplyOneTroop();
            return origin == null ? null! : CreateSafeOrigin(origin.Troop);
        }

        public IEnumerable<IAgentOriginBase> GetAllTroops()
        {
            foreach (IAgentOriginBase origin in _inner.GetAllTroops())
                yield return CreateSafeOrigin(origin.Troop);
        }

        public BasicCharacterObject GetGeneralCharacter()
        {
            return _inner.GetGeneralCharacter();
        }

        public int GetNumberOfPlayerControllableTroops()
        {
            return _inner.GetNumberOfPlayerControllableTroops();
        }

        private IAgentOriginBase CreateSafeOrigin(
            BasicCharacterObject troop)
        {
            return new GreyWardenSafePartyAgentOrigin(
                _ownerParty,
                troop,
                _isPlayerSide,
                _nextRank++);
        }
    }

    /// <summary>
    /// A no-op origin used only inside a friendly sparring mission. Returning
    /// the real PartyBase keeps CampaignAgentComponent and conversation
    /// animations compatible, while no-op casualty callbacks prevent the bout
    /// from changing campaign rosters.
    /// </summary>
    internal sealed class GreyWardenSafePartyAgentOrigin : IAgentOriginBase
    {
        private readonly PartyBase _ownerParty;
        private readonly BasicCharacterObject _troop;
        private readonly bool _isPlayerSide;
        private readonly int _rank;
        private readonly UniqueTroopDescriptor _descriptor;
        private readonly bool _hasThrownWeapon;
        private readonly bool _hasHeavyArmor;
        private readonly bool _hasShield;
        private readonly bool _hasSpear;

        internal GreyWardenSafePartyAgentOrigin(
            PartyBase ownerParty,
            BasicCharacterObject troop,
            bool isPlayerSide,
            int rank)
        {
            _ownerParty = ownerParty;
            _troop = troop;
            _isPlayerSide = isPlayerSide;
            _rank = rank;
            _descriptor = new UniqueTroopDescriptor(
                Game.Current.NextUniqueTroopSeed);
            AgentOriginUtilities.GetDefaultTroopTraits(
                _troop,
                out _hasThrownWeapon,
                out _hasSpear,
                out _hasShield,
                out _hasHeavyArmor);
        }

        public bool IsUnderPlayersCommand => _isPlayerSide;

        public bool IsInSameArmyAsPlayer => _isPlayerSide;

        public uint FactionColor => _ownerParty.PrimaryColorPair.Item1;

        public uint FactionColor2 => _ownerParty.PrimaryColorPair.Item2;

        public IBattleCombatant BattleCombatant => _ownerParty;

        public int UniqueSeed => _descriptor.UniqueSeed;

        public int Seed => _troop.GetDefaultFaceSeed(_rank);

        public Banner Banner => _ownerParty.Banner;

        public BasicCharacterObject Troop => _troop;

        public bool HasThrownWeapon => _hasThrownWeapon;

        public bool HasHeavyArmor => _hasHeavyArmor;

        public bool HasShield => _hasShield;

        public bool HasSpear => _hasSpear;

        public void SetWounded()
        {
        }

        public void SetKilled()
        {
        }

        public void SetRouted(bool isOrderRetreat)
        {
        }

        public void OnAgentRemoved(float agentHealth)
        {
        }

        public void OnScoreHit(
            BasicCharacterObject victim,
            BasicCharacterObject captain,
            int damage,
            bool isFatal,
            bool isTeamKill,
            WeaponComponentData attackerWeapon)
        {
        }

        public void SetBanner(Banner banner)
        {
        }

        public TroopTraitsMask GetTraitsMask()
        {
            return AgentOriginUtilities.GetDefaultTraitsMask(this);
        }
    }
}
