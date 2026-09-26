using System; using System.Collections.Generic; using System.IO; using System.Linq; using TpacTool.Lib;

// Writes Modding Kit clip packages (<name>_anm.tpac) for the dual-wield clips, copying every field
// of the original clip in gwp_dual_wield_animations.tpac. The package shell is a clip the Kit
// itself saved (template), so the container matches what the Kit writes.
// args: original.tpac  template_anm.tpac  kit_anim_dir(*_geo.tpac with the imported animations)  out_dir  vanilla_animations.tpac  clip names...
class P
{
    static readonly Dictionary<string, string> ActionMap = new()
    {
        ["act_dual2_quick_release_slashright_1h"] = "act_gwd2_quick_release_slashright_1h",
        ["act_dual_quick_release_slashright_1h_balanced"] = "act_gwd_quick_release_slashright_1h_balanced",
        ["act_dual_quick_blocked_slashright_1h_balanced"] = "act_gwd_blocked_slashright_1h_balanced",
        ["act_dual_quick_blocked_slashleft_1h_balanced"] = "act_gwd_quick_blocked_slashleft_1h_balanced",
        ["act_dual_quick_release_slashleft_1h_balanced"] = "act_gwd_quick_release_slashleft_1h_balanced",
    };
    static string Map(string a) => string.IsNullOrEmpty(a) ? a : (ActionMap.TryGetValue(a, out var m) ? m : a);

    static void Main(string[] a)
    {
        var original = new AssetPackage(a[0], true, false); // metadata only: the ROT animation data does not decode ("Frames not equal")
        var origClips = original.Items.OfType<AnimationClip>().ToDictionary(c => c.Name);
        var origAnimNames = original.Items.OfType<SkeletalAnimation>().ToDictionary(s => s.Guid, s => s.Name);
        var kitAnims = new Dictionary<string, Guid>();
        foreach (var f in Directory.GetFiles(a[2], "*_geo.tpac"))
            foreach (var s in new AssetPackage(f, true, false).Items.OfType<SkeletalAnimation>()) kitAnims[s.Name] = s.Guid;
        var vanillaGuids = new AssetPackage(a[4], true, false).Items.OfType<SkeletalAnimation>().Select(s => s.Guid).ToHashSet();

        foreach (var name in a.Skip(5))
        {
            var src = origClips[name];
            Guid anim;
            if (origAnimNames.TryGetValue(src.Animation, out var animName)) anim = kitAnims[animName];
            else if (vanillaGuids.Contains(src.Animation)) anim = src.Animation;
            else throw new Exception($"{name}: animation {src.Animation} is neither ours nor vanilla");

            var pkg = new AssetPackage(a[1], true, true);
            pkg.Guid = Guid.NewGuid();
            var c = pkg.Items.OfType<AnimationClip>().Single();
            c.Guid = Guid.NewGuid();
            c.Name = name;
            c.Duration = src.Duration; c.Source1 = src.Source1; c.Source2 = src.Source2;
            c.Param1 = src.Param1; c.Param2 = src.Param2; c.Param3 = src.Param3; c.Priority = src.Priority;
            c.Animation = anim; c.StepPoints = src.StepPoints;
            c.SoundCode = src.SoundCode; c.VoiceCode = src.VoiceCode; c.FacialAnimationId = src.FacialAnimationId;
            c.BlendsWithAction = Map(src.BlendsWithAction); c.ContinueWithAction = Map(src.ContinueWithAction);
            c.LeftHandPose = src.LeftHandPose; c.RightHandPose = src.RightHandPose; c.CombatParameterId = src.CombatParameterId;
            c.BlendInPeriod = src.BlendInPeriod; c.BlendOutPeriod = src.BlendOutPeriod; c.DoNotInterpolate = src.DoNotInterpolate;
            c.UnknownInt = src.UnknownInt; c.UnknownClipName = src.UnknownClipName;
            c.ClipSource1Name = src.ClipSource1Name; c.ClipSource2Name = src.ClipSource2Name; c.GeneratedIndex = src.GeneratedIndex;
            c.UnknownUInt2 = src.UnknownUInt2; c.UnknownUShort = src.UnknownUShort;
            c.Flags.Clear(); c.Flags.AddRange(src.Flags);
            c.ClipUsages.Clear(); c.ClipUsages.AddRange(src.ClipUsages);
            var outPath = Path.Combine(a[3], name.ToLowerInvariant() + "_anm.tpac");
            pkg.Save(outPath);
            Console.WriteLine($"wrote {outPath}  anim={(origAnimNames.ContainsKey(src.Animation) ? animName : "vanilla " + src.Animation)}");
        }
    }
}
