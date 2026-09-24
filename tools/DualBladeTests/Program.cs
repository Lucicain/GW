using System;
using System.Linq;
using GreyWardenPolicePurity;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

static class Program
{
    private static int _checks;
    static void Check(bool result, string label)
    {
        if (!result) throw new Exception(label);
        _checks++;
    }
    static (Agent agent, GwpDualBladeAiBehavior behavior, GwpDualBladeAgentState state, GwpKickInputComponent kick) Archer()
    {
        var agent = new Agent();
        agent.Equipment[EquipmentIndex.Weapon0] = new MissionWeapon { Item = new ItemObject { StringId = "off", PrimaryWeapon = new WeaponComponentData() } };
        agent.Equipment[EquipmentIndex.Weapon1] = new MissionWeapon { Item = new ItemObject { StringId = "main" } };
        agent.Equipment[EquipmentIndex.Weapon2] = new MissionWeapon { Item = new ItemObject { PrimaryWeapon = new WeaponComponentData { WeaponFlags = WeaponFlags.RangedWeapon | WeaponFlags.NotUsableWithOneHand } } };
        agent.Equipment[EquipmentIndex.Weapon3] = new MissionWeapon { Amount = 20, Item = new ItemObject { PrimaryWeapon = new WeaponComponentData { IsAmmo = true } } };
        var behavior = new GwpDualBladeAiBehavior();
        behavior.OnAgentBuild(agent, new Banner());
        var state = GwpDualBladeAgents.Find(agent)!;
        state.OpeningWieldDone = true;
        var kick = new GwpKickInputComponent(agent);
        agent.AddComponent(kick);
        agent.ImmediateEnemy = new Agent { Action0 = Agent.ActionCodeType.DefendShield, Position = new Vec3 { X = 1 } };
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        return (agent, behavior, state, kick);
    }
    static Agent.EventControlFlag Input(GwpKickInputComponent kick, Agent.EventControlFlag flags = 0)
    {
        var movement = Agent.MovementControlFlag.None;
        var vector = new Vec2();
        kick.OnAIInputSet(ref flags, ref movement, ref vector);
        return flags;
    }
    static Agent.EventControlFlag Grip(Agent agent, Agent.EventControlFlag flags)
    {
        var movement = Agent.MovementControlFlag.None;
        var vector = new Vec2();
        agent.GetComponent<GwpDualBladeFightGripComponent>()!.OnAIInputSet(ref flags, ref movement, ref vector);
        return flags;
    }
    static void Main()
    {
        var (a, behavior, state, kick) = Archer();
        Check((Input(kick) & Agent.EventControlFlag.Kick) != 0, "settled pair still kicks");
        a.Off = EquipmentIndex.None;
        behavior.OnMissionTick(.016f);
        Check(a.Wields.Count == 0, "pending kick reserves hands before its animation starts");
        a.Mission.CurrentTime += .4f;
        behavior.OnMissionTick(.016f);
        Check(a.Wields.SequenceEqual(new[] { "sheathe" }), "pair recovery resumes after request expires");
        Check((Input(kick, Agent.EventControlFlag.Kick) & Agent.EventControlFlag.Kick) == 0, "no new kick inside empty-hand sequence");
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        Check(a.Wields.SequenceEqual(new[] { "sheathe", "wield:0", "wield:1" }), "existing complete pair recovery retained");

        (a, behavior, state, kick) = Archer();
        a.Action1 = Agent.ActionCodeType.WeaponBash;
        var flags = Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath0 | Agent.EventControlFlag.Kick | Agent.EventControlFlag.Run);
        Check(flags == Agent.EventControlFlag.Run, "active bash keeps weapons but preserves movement");
        a.Action1 = Agent.ActionCodeType.Idle;
        Check(Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1)
            == (Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1), "weapon switch passes once the bash ends");
        a.Main = EquipmentIndex.Weapon2; a.Off = EquipmentIndex.None;
        behavior.OnMissionTick(.016f);
        Check((Input(kick, Agent.EventControlFlag.Kick) & Agent.EventControlFlag.Kick) == 0, "bow cannot begin paired-blade bash");

        (a, behavior, state, kick) = Archer();
        flags = Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Kick);
        Check(flags == Agent.EventControlFlag.Wield2 && !kick.HasPendingKick(a.Mission.CurrentTime), "simultaneous new requests prefer weapon change");
        a.EventControlFlags = Agent.EventControlFlag.Kick;
        a.Off = EquipmentIndex.None;
        behavior.OnMissionTick(.016f);
        Check(a.Wields.Count == 0, "native queued kick also reserves hands");

        (a, behavior, state, kick) = Archer();
        a.Off = EquipmentIndex.None;
        a.Action0 = Agent.ActionCodeType.StrikeKnockBack;
        behavior.OnMissionTick(.016f);
        Check(a.Wields.Count == 0, "no pair recovery during knockback reaction");
        a.Action0 = Agent.ActionCodeType.Idle;
        a.Action1 = Agent.ActionCodeType.WeaponBash;
        int diagnostics = GwpFaultTrace.Conflicts;
        Input(kick); Input(kick);
        Check(GwpFaultTrace.Conflicts == diagnostics + 1, "invalid active bash recorded once per agent");

        // Replay agent 650's observed overlap: begin bash, then native asks
        // Wield2+Sheath1 while the 0.35-second mod request is still pending.
        (a, behavior, state, kick) = Archer();
        a.Mission.CurrentTime = 111.913f;
        Input(kick);
        a.Action1 = Agent.ActionCodeType.WeaponBash;
        a.Mission.CurrentTime = 111.951f;
        flags = Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1);
        Check(flags == 0 && !kick.HasPendingKick(a.Mission.CurrentTime), "recorded 650 overlap keeps both hands and cancels repeated Kick");
        a.Mission.CurrentTime = 112.276f;
        Check(Input(kick, Agent.EventControlFlag.Wield2) == 0, "bash retains weapons beyond the input request window");
        a.Action1 = Agent.ActionCodeType.Idle;
        Check(Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1)
            == (Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1), "native bow switch resumes immediately after bash ends");

        (a, behavior, state, kick) = Archer();
        Input(kick);
        a.Mission.CurrentTime += .02f;
        Check(Input(kick, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1)
            == (Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1)
            && !kick.HasPendingKick(a.Mission.CurrentTime), "weapon switch cancels a pending but unaccepted bash");

        // The off-hand blade is bound to the main one, by native's own rules.
        (a, behavior, state, kick) = Archer();
        Check(Grip(a, Agent.EventControlFlag.Sheath1 | Agent.EventControlFlag.Run) == Agent.EventControlFlag.Run,
            "native's lone off-hand clean-up is dropped while paired");
        Check(Grip(a, Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1)
            == (Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Sheath1), "native's bow switch passes as one input");
        Check(Grip(a, Agent.EventControlFlag.Sheath0) == (Agent.EventControlFlag.Sheath0 | Agent.EventControlFlag.Sheath1),
            "a main-hand sheath takes the off-hand blade with it");
        a.IsUsingGameObject = true;
        Check(Grip(a, Agent.EventControlFlag.Sheath1) == Agent.EventControlFlag.Sheath1, "object use is left to native");
        a.IsUsingGameObject = false;
        a.Main = EquipmentIndex.Weapon2; a.Off = EquipmentIndex.None;
        Check(Grip(a, Agent.EventControlFlag.Sheath0) == Agent.EventControlFlag.Sheath0, "no pair, nothing touched");

        // Native draws the main blade from the bow: the off-hand blade follows.
        behavior.OnMissionTick(.016f);
        a.Wields.Clear();
        a.Main = EquipmentIndex.Weapon1;
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        behavior.OnMissionTick(.016f);
        Check(a.Wields.SequenceEqual(new[] { "sheathe", "wield:0", "wield:1" }), "off-hand blade drawn when native draws the main one");

        // Native's banner-bearer penalty on two-handed weapons is cancelled
        // exactly while the blade is in the off hand, and only then.
        (a, behavior, state, kick) = Archer();
        Check(GwpDualBladeAgents.RangedFavorScale(a) == 1f / GwpDualBladeAgents.NativeOffhandTwoHandedPenalty,
            "paired archer's ranged favor cancels native's 1e-6 penalty");
        a.Equipment[EquipmentIndex.Weapon0].Item!.PrimaryWeapon!.WeaponFlags = WeaponFlags.CanBlockRanged;
        Check(GwpDualBladeAgents.RangedFavorScale(a) == 1f, "a CanBlockRanged off hand is not penalised, so no compensation");
        a.Equipment[EquipmentIndex.Weapon0].Item!.PrimaryWeapon!.WeaponFlags = WeaponFlags.None;
        int updates = a.StatUpdates;
        a.Main = EquipmentIndex.Weapon2; a.Off = EquipmentIndex.None;
        behavior.OnMissionTick(.016f);
        Check(GwpDualBladeAgents.RangedFavorScale(a) == 1f && a.StatUpdates == updates + 1,
            "bow in hand: no compensation, stats refreshed on the off-hand change");
        behavior.OnMissionTick(.016f);
        Check(a.StatUpdates == updates + 1, "stats refreshed only when the off hand changes");

        var plain = new Agent { ImmediateEnemy = new Agent { Action0 = Agent.ActionCodeType.DefendShield } };
        Check((Input(new GwpKickInputComponent(plain)) & Agent.EventControlFlag.Kick) != 0, "ordinary soldier input unchanged");
        Console.WriteLine($"PASS: {_checks} checks using production dual-blade and kick code. Native physics requires live testing.");
    }
}
