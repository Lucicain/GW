using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed class GwpBattleSceneContext : MissionBehavior
    {
        internal bool Ready { get; private set; }
        internal bool MusicEligible { get; private set; }
        internal DefaultBattleMissionAgentSpawnLogic? Spawn { get; private set; }
        private readonly float[] _openingPower = new float[2], _spawnedPower = new float[2];
        private readonly int[] _openingCount = new int[2];
        internal int OpeningCount(BattleSideEnum side) => _openingCount[(int)side];
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        internal bool TryCapture()
        {
            if (Ready) return true;
            if (Mission == null || Mission.MissionEnded || Mission.MainAgent == null
                || Mission.PlayerTeam == null || Mission.PlayerTeam.Side == BattleSideEnum.None
                || GameNetwork.IsMultiplayer
                || Mission.GetMissionBehavior<GreyWardenFieldSparringMissionController>() != null) return false;
            Spawn = Mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
            if (Spawn == null || Spawn.GetTotalNumberOfTroopsForSide(BattleSideEnum.Defender) <= 0
                || Spawn.GetTotalNumberOfTroopsForSide(BattleSideEnum.Attacker) <= 0) return false;
            try
            {
                // The supplier's complete opening roster includes reserves and
                // is available during deployment, before casualty callbacks.
                Count(BattleSideEnum.Defender, out int defenders, out int greyDefenders);
                Count(BattleSideEnum.Attacker, out int attackers, out int greyAttackers);
                MusicEligible = GwpBattleScenePolicy.MusicEligible(greyDefenders, defenders)
                    || GwpBattleScenePolicy.MusicEligible(greyAttackers, attackers);
                Ready = true;
#if GWP_DIAGNOSTICS
                GwpFaultTrace.Write("BATTLE_SCENE_OPENING", details:
                    $"defender={greyDefenders}/{defenders} attacker={greyAttackers}/{attackers} music={MusicEligible}");
#endif
                return true;
            }
            catch (Exception error) { GwpFaultTrace.WriteQuiet(error); return false; }
        }

        private void Count(BattleSideEnum side, out int soldiers, out int wardens)
        {
            soldiers = wardens = 0;
            _openingPower[(int)side] = 0;
            _openingCount[(int)side] = 0;
            foreach (IAgentOriginBase origin in Spawn!.GetAllTroopsForSide(side))
            {
                BasicCharacterObject character = origin.Troop;
                if (character == null) continue;
                _openingCount[(int)side]++;
                _openingPower[(int)side] += Math.Max(.01f, character.GetPower());
                if (character.IsHero) continue;
                soldiers++;
                if (GwpCommon.IsGreyWardenAffiliatedCharacter(character)) wardens++;
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (agent.IsHuman && agent.Team != null && agent.Team.Side != BattleSideEnum.None
                && agent.Origin is not GwpBattleSupportOrigin)
                _spawnedPower[(int)agent.Team.Side] += Math.Max(.01f, agent.Character.GetPower());
        }

        internal float RemainingPower(BattleSideEnum side)
        {
            int reserves = side == BattleSideEnum.Defender ? Spawn!.NumberOfRemainingDefenderTroops
                : Spawn!.NumberOfRemainingAttackerTroops;
            float power = reserves > 0 ? Math.Max(0, _openingPower[(int)side] - _spawnedPower[(int)side]) : 0;
            foreach (Agent agent in Mission.Agents)
                if (agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && agent.Team?.Side == side)
                    power += Math.Max(.01f, agent.CharacterPowerCached);
            return power;
        }
    }
}
