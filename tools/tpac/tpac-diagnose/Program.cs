using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TpacTool.Lib;

class P
{
    static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--extract-dual")
        {
            ExtractDual(args[1], args.Skip(2).ToArray());
            return;
        }
        if (args.Length >= 5 && args[0] == "--compare")
        {
            Compare(args[1], args[2], args[3], args[4]);
            return;
        }
        var package = new AssetPackage(args[0], true, false);
        var filter = args.Length > 1 ? new Regex(args[1], RegexOptions.IgnoreCase) : null;
        Console.WriteLine($"package={package.Guid} assets={package.Items.Count}");
        foreach (var item in package.Items)
        {
            var searchableName = item.Name;
            if (item is AnimationClip searchableClip)
                searchableName += " " + searchableClip.UnknownClipName + " " + searchableClip.ClipSource1Name + " " + searchableClip.ClipSource2Name;
            if (item is SkeletalAnimation searchableSkeletal)
            {
                try { searchableName += " " + searchableSkeletal.Definition?.Data?.Name; }
                catch { }
            }
            if (filter != null && !filter.IsMatch(searchableName))
                continue;
            Console.WriteLine($"asset {item.GetType().Name} {item.Name} {item.Guid}");
            if (item is AnimationClip clip)
                Console.WriteLine($"  clip={clip.UnknownClipName} source1={clip.ClipSource1Name} source2={clip.ClipSource2Name} animation={clip.Animation} duration={clip.Duration:R} combat={clip.CombatParameterId}");
            if (item is SkeletalAnimation skeletal)
            {
                try { Console.WriteLine($"  skeletal={skeletal.Definition?.Data?.Name} skeleton={skeletal.Skeleton} bones={skeletal.BoneNum} duration={skeletal.Duration}"); }
                catch (Exception e) { Console.WriteLine($"  skeletal data parse failed: {e.GetType().Name}: {e.Message}"); }
            }
            if (item is Geometry geometry)
            {
                Console.WriteLine($"  resource={geometry.ResourceFile} checksum={geometry.Checksum} uses={geometry.AssetsUsingThis.Count}");
                if (geometry.FbxSetting != null)
                {
                    try
                    {
                        var fs = geometry.FbxSetting.Data;
                        var gs = fs.GeometrySetting;
                        Console.WriteLine($"  fbx blend={fs.BlendShapeImport} unit={fs.ConvertToUnit} b={fs.UnknownBool1}");
                        Console.WriteLine($"  geo bools={gs.UnknownBool1},{gs.UnknownBool2},{gs.UnknownBool3},{gs.UnknownBool4},{gs.UnknownBool5},{gs.UnknownBool6} items1={gs.UnknownItems1.Count} items2={gs.UnknownItems2.Count}");
                        foreach (var setting in gs.UnknownItems1)
                            Console.WriteLine($"    item {setting.UnknownString1} bools={setting.UnknownBool1},{setting.UnknownBool2},{setting.UnknownBool3},{setting.UnknownBool4},{setting.UnknownBool5},{setting.UnknownBool6} s2={setting.UnknownString2} vec={setting.UnknownVec}");
                        foreach (var setting in gs.UnknownItems2)
                            Console.WriteLine($"    item2 {setting.Item1}={setting.Item2}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"  fbx settings parse failed: {e.GetType().Name}: {e.Message}");
                        var raw = geometry.FbxSetting.DebugGetRawData();
                        Console.WriteLine($"  fbx raw len={raw.Length} hex={BitConverter.ToString(raw).Replace("-", "")}");
                    }
                }
            }
            if (item is Metamesh meta)
            {
                Console.WriteLine($"  meshes={meta.Meshes.Count} original={meta.Original}");
                foreach (var mesh in meta.Meshes.OrderBy(m => m.Lod))
                {
                    Console.WriteLine($"  lod={mesh.Lod} name={mesh.Name} mat={mesh.Material.Guid} verts={mesh.VertexCount} faces={mesh.FaceCount} pos={mesh.PositionCount} complete={mesh.IsCompleteMesh} gen={mesh.UnknownUint1} u2={mesh.UnknownInt2} u3={mesh.UnknownInt3} f={mesh.UnknownFloat1} b={mesh.UnknownBool1},{mesh.UnknownBool2},{mesh.UnknownBool3} flags=[{string.Join(',', mesh.Flags ?? new System.Collections.Generic.List<string>())}] mflags=[{string.Join(',', mesh.MaterialFlags)}]");
                    try
                    {
                        var vs = mesh.VertexStream?.Data;
                        if (vs != null)
                        {
                            int badIndex = vs.Indices.Count(i => i < 0 || i >= mesh.VertexCount);
                            int degIndex = 0;
                            for (int i = 0; i + 2 < vs.Indices.Length; i += 3)
                                if (vs.Indices[i] == vs.Indices[i + 1] || vs.Indices[i] == vs.Indices[i + 2] || vs.Indices[i + 1] == vs.Indices[i + 2]) degIndex++;
                            int nonFinitePos = vs.Positions.Count(v => !Finite(v));
                            int nonFiniteNorm = vs.Normals.Count(v => !Finite(v));
                            int nonFiniteUv = vs.Uv1.Count(v => !Finite(v));
                            int nonFiniteTan = vs.Tangents.Count(v => !Finite(v));
                            Console.WriteLine($"    stream idx={vs.Indices.Length} c1={vs.Colors1.Length} c2={vs.Colors2.Length} uv1={vs.Uv1.Length} uv2={vs.Uv2.Length} p={vs.Positions.Length} p2={vs.UnknownAnotherPositions.Length} n={vs.Normals.Length} t={vs.Tangents.Length} bw={vs.BoneWeights.Length} bi={vs.BoneIndices.Length} cn={vs.CompressedNormals.Length} cp={vs.CompressedPositions.Length} ct={vs.CompressedTangents.Length} qt={(vs.TangentTransform == null ? -1 : vs.TangentTransform.Length)} badidx={badIndex} degidx={degIndex} nonfinite={nonFinitePos},{nonFiniteNorm},{nonFiniteUv},{nonFiniteTan}");
                        }
                        var ed = mesh.EditData?.Data;
                        if (ed != null)
                        {
                            int badPosRef = ed.Vertices.Count(v => v.PositionIndex >= ed.Positions.Length);
                            int badFaceRef = ed.Faces.Count(fa => fa.V0 < 0 || fa.V1 < 0 || fa.V2 < 0 || fa.V0 >= ed.Vertices.Length || fa.V1 >= ed.Vertices.Length || fa.V2 >= ed.Vertices.Length);
                            int degFace = ed.Faces.Count(fa => fa.V0 == fa.V1 || fa.V0 == fa.V2 || fa.V1 == fa.V2);
                            int nonFinitePos = ed.Positions.Count(v => !Finite(v));
                            int nonFiniteVert = ed.Vertices.Count(v => !Finite(v.Normal) || !Finite(v.Tangent) || !Finite(v.Binormal) || !Finite(v.Uv) || !Finite(v.SecondUv));
                            int uniqueExactPos = ed.Positions.Select(v => (v.X, v.Y, v.Z)).Distinct().Count();
                            Console.WriteLine($"    edit pos={ed.Positions.Length} uniqueExact={uniqueExactPos} verts={ed.Vertices.Length} faces={ed.Faces.Length} unused={ed.UnusedVec3.Length} morph={ed.MorphFrames.Count} bones={ed.Bones.Length} smooth={ed.FaceSmoothingGroupMasks.Length} badposref={badPosRef} badfaceref={badFaceRef} degface={degFace} nonfinite={nonFinitePos},{nonFiniteVert}");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"    mesh data parse failed: {e.GetType().Name}: {e.Message}");
                    }
                }
            }
        }
    }

    static void ExtractDual(string outputPath, string[] inputPaths)
    {
        // Keep external animation segments opaque. Some current Bannerlord/ROT
        // optimized-animation payloads are newer than TpacTool's decoder, but
        // AssetPackage.Save can copy their compressed byte ranges losslessly.
        var packages = inputPaths.Select(path => new AssetPackage(path, true, false)).ToList();
        var clips = packages.SelectMany(p => p.Items).OfType<AnimationClip>()
            .Where(IsDualClip)
            .GroupBy(c => c.Guid)
            .Select(g => g.First())
            .ToList();
        foreach (var clip in clips)
        {
            if (clip.CombatParameterId == "1h_dual_others"
                || clip.CombatParameterId == "onehanded_dual_thrust"
                || clip.CombatParameterId == "onehanded_dual_right"
                || clip.CombatParameterId == "onehanded_dual_right_balanced")
                clip.CombatParameterId = "gwp_" + clip.CombatParameterId;
        }

        var referencedAnimations = clips.Select(c => c.Animation)
            .Where(g => g != Guid.Empty)
            .ToHashSet();
        var skeletal = packages.SelectMany(p => p.Items).OfType<SkeletalAnimation>()
            .Where(a => referencedAnimations.Contains(a.Guid))
            .GroupBy(a => a.Guid)
            .Select(g => g.First())
            .ToList();

        var output = new AssetPackage(Guid.Parse("94ca4c3d-685b-4cae-9f10-67edc31bacdf"));
        output.Items.AddRange(skeletal);
        output.Items.AddRange(clips);
        output.Save(outputPath);

        var copiedAnimationIds = skeletal.Select(a => a.Guid).ToHashSet();
        var nativeDependencies = referencedAnimations.Where(g => !copiedAnimationIds.Contains(g)).ToList();
        Console.WriteLine($"output={outputPath}");
        Console.WriteLine($"clips={clips.Count} skeletal={skeletal.Count} native_animation_dependencies={nativeDependencies.Count}");
        foreach (var a in skeletal)
            Console.WriteLine($"skeletal {a.Name} {a.Guid}");
        foreach (var c in clips.OrderBy(c => c.Name))
            Console.WriteLine($"clip {c.Name} {c.Guid} animation={c.Animation} source1={c.ClipSource1Name} source2={c.ClipSource2Name}");
        foreach (var guid in nativeDependencies)
            Console.WriteLine($"native-animation {guid}");
    }

    static bool IsDualClip(AnimationClip clip)
    {
        return ContainsDual(clip.Name)
            || ContainsDual(clip.UnknownClipName)
            || ContainsDual(clip.ClipSource1Name)
            || ContainsDual(clip.ClipSource2Name);
    }

    static bool ContainsDual(string value) =>
        !string.IsNullOrEmpty(value) && value.IndexOf("dual", StringComparison.OrdinalIgnoreCase) >= 0;

    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    static bool Finite(Vector2 v) => Finite(v.X) && Finite(v.Y);
    static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
    static bool Finite(Vector4 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z) && Finite(v.W);

    static void Compare(string pathA, string nameA, string pathB, string nameB)
    {
        var pa = new AssetPackage(pathA, true, false);
        var pb = new AssetPackage(pathB, true, false);
        var a = pa.Items.OfType<Metamesh>().Single(x => x.Name == nameA);
        var b = pb.Items.OfType<Metamesh>().Single(x => x.Name == nameB);
        foreach (var ma in a.Meshes.OrderBy(x => x.Lod))
        {
            var mb = b.Meshes.SingleOrDefault(x => x.Lod == ma.Lod);
            if (mb == null) { Console.WriteLine($"lod={ma.Lod} missing-in-B"); continue; }
            var ea = ma.EditData.Data;
            var eb = mb.EditData.Data;
            int n = Math.Min(ea.Vertices.Length, eb.Vertices.Length);
            int posMismatch = 0, normalMismatch = 0, uvMismatch = 0, tbnMismatch = 0;
            float maxPos = 0, maxNormal = 0, maxUv = 0, maxTbn = 0;
            for (int i = 0; i < n; i++)
            {
                var va = ea.Vertices[i]; var vb = eb.Vertices[i];
                var posa = ea.Positions[va.PositionIndex]; var posb = eb.Positions[vb.PositionIndex];
                float dp = MaxAbs(posa - posb); maxPos = Math.Max(maxPos, dp); if (dp > 1e-5f) posMismatch++;
                float dn = MaxAbs(va.Normal - vb.Normal); maxNormal = Math.Max(maxNormal, dn); if (dn > 1e-5f) normalMismatch++;
                float du = MaxAbs(va.Uv - vb.Uv); maxUv = Math.Max(maxUv, du); if (du > 1e-5f) uvMismatch++;
                float dt = Math.Max(MaxAbs(va.Tangent - vb.Tangent), MaxAbs(va.Binormal - vb.Binormal)); maxTbn = Math.Max(maxTbn, dt); if (dt > 1e-5f) tbnMismatch++;
            }
            int faceN = Math.Min(ea.Faces.Length, eb.Faces.Length);
            int faceMismatch = 0;
            for (int i = 0; i < faceN; i++)
            {
                var fa = ea.Faces[i]; var fb = eb.Faces[i];
                if (fa.V0 != fb.V0 || fa.V1 != fb.V1 || fa.V2 != fb.V2) faceMismatch++;
            }
            Console.WriteLine($"lod={ma.Lod} counts A={ea.Positions.Length}/{ea.Vertices.Length}/{ea.Faces.Length} B={eb.Positions.Length}/{eb.Vertices.Length}/{eb.Faces.Length} posMismatch={posMismatch}/{n} maxPos={maxPos:R} normalMismatch={normalMismatch} maxNormal={maxNormal:R} uvMismatch={uvMismatch} maxUv={maxUv:R} tbnMismatch={tbnMismatch} maxTbn={maxTbn:R} faceMismatch={faceMismatch}/{faceN}");
        }
    }

    static float MaxAbs(Vector2 v) => Math.Max(Math.Abs(v.X), Math.Abs(v.Y));
    static float MaxAbs(Vector4 v) => Math.Max(Math.Max(Math.Abs(v.X), Math.Abs(v.Y)), Math.Max(Math.Abs(v.Z), Math.Abs(v.W)));
}
