using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal interface IGwpSceneReinforcementSource
    {
        bool TryGetReinforcementFrame(BattleSideEnum side, BasicCharacterObject troop, out MatrixFrame frame);
    }

    /// <summary>共享战场名册入口：原版野战供应器与城内真实预备兵向援军/音乐提供同一份人数和来源。</summary>
    internal interface IGwpBattleSceneSource
    {
        bool IsDeploymentOver { get; }
        event Action<BattleSideEnum, int> OnReinforcementsSpawned;
        bool OpeningAgentsAlreadySpawned { get; }
        int NumberOfAgents { get; }
        int NumberOfRemainingDefenderTroops { get; }
        int NumberOfRemainingAttackerTroops { get; }
        int GetTotalNumberOfTroopsForSide(BattleSideEnum side);
        IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum side);
    }

    internal sealed class GwpNativeBattleSceneSource : IGwpBattleSceneSource
    {
        private readonly DefaultBattleMissionAgentSpawnLogic _spawn;
        internal GwpNativeBattleSceneSource(DefaultBattleMissionAgentSpawnLogic spawn) => _spawn = spawn;
        public bool IsDeploymentOver => _spawn.IsDeploymentOver;
        public event Action<BattleSideEnum, int> OnReinforcementsSpawned
        {
            add => _spawn.OnReinforcementsSpawned += value;
            remove => _spawn.OnReinforcementsSpawned -= value;
        }
        public bool OpeningAgentsAlreadySpawned => false;
        public int NumberOfAgents => _spawn.NumberOfAgents;
        public int NumberOfRemainingDefenderTroops => _spawn.NumberOfRemainingDefenderTroops;
        public int NumberOfRemainingAttackerTroops => _spawn.NumberOfRemainingAttackerTroops;
        public int GetTotalNumberOfTroopsForSide(BattleSideEnum side) => _spawn.GetTotalNumberOfTroopsForSide(side);
        public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum side) => _spawn.GetAllTroopsForSide(side);
    }
}
