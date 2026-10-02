using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using TpacTool.Lib;

class P
{
    static void Main(string[] args)
    {
        if (args.Length == 5 && args[0] == "--verify-cloth")
        {
            VerifyCloth(args[1], args[2], args[3], args[4]);
            return;
        }
        if (args.Length == 6 && args[0] == "--restore-cloth")
        {
            RestoreCloth(args[1], args[2], args[3], args[4], args[5]);
            return;
        }
        if (args.Length >= 3 && args[0] == "--extract-dual")
        {
            ExtractDual(args[1], args.Skip(2).ToArray());
            return;
        }
        if (args.Length == 4 && args[0] == "--cloth-settings")
        {
            // args: original package, rebuilt package, metamesh name - every cloth field side by side
            var oa = new AssetPackage(args[1], true, false).Items.OfType<Metamesh>().Single(x => x.Name == args[3]);
            var ob = new AssetPackage(args[2], true, false).Items.OfType<Metamesh>().Single(x => x.Name == args[3]);
            Console.WriteLine($"{args[3]} body: {oa.UnknownString} -> {ob.UnknownString}");
            var ca = oa.Meshes.Where(x => x.Flags.Contains("uses_cloth_simulation")).OrderBy(x => x.Lod).ThenBy(x => x.FaceCount).ToList();
            var cb = ob.Meshes.Where(x => x.Flags.Contains("uses_cloth_simulation")).OrderBy(x => x.Lod).ThenBy(x => x.FaceCount).ToList();
            for (int i = 0; i < Math.Max(ca.Count, cb.Count); i++)
            {
                var a = i < ca.Count ? ca[i] : null; var b = i < cb.Count ? cb[i] : null;
                Console.WriteLine($"  {a?.Name} -> {b?.Name}  distance {a?.UnknownFloat1:R} -> {b?.UnknownFloat1:R}");
                foreach (var prop in typeof(ClothingMaterial).GetProperties())
                {
                    var va = a == null ? null : prop.GetValue(a.ClothingMaterial); var vb = b == null ? null : prop.GetValue(b.ClothingMaterial);
                    Console.WriteLine($"    {(Equals(va, vb) ? " " : "*")} {prop.Name,-28} {va} -> {vb}");
                }
            }
            return;
        }
        if (args.Length == 4 && args[0] == "--cloth-gradient")
        {
            ClothGradient(args[1], args[2], args[3]);
            return;
        }
        if (args.Length == 5 && args[0] == "--dump-segment")
        {
            // args: package, item name, type guid, output file (all segments of that type, concatenated with an index file)
            var pkg = new AssetPackage(args[1], true, false);
            var type = Guid.Parse(args[3]);
            int k = 0;
            foreach (var item in pkg.Items.Where(i => i.Name == args[2]))
            foreach (var seg in item.TypelessDataSegments.Where(x => x.TypeGuid == type))
            {
                var raw = seg.GetType().GetMethod("DebugGetRawData")?.Invoke(seg, null) as byte[];
                var outPath = args[4] + "." + (k++) + ".bin";
                System.IO.File.WriteAllBytes(outPath, raw ?? Array.Empty<byte>());
                Console.WriteLine($"{outPath} owner={seg.OwnerGuid} bytes={raw?.Length}");
            }
            return;
        }
        if (args.Length == 3 && args[0] == "--segments")
        {
            var pkg = new AssetPackage(args[1], true, false);
            foreach (var item in pkg.Items.Where(i => i.Name == args[2]))
            {
                Console.WriteLine($"{item.GetType().Name} {item.Name} version={item.Version} segments={item.TypelessDataSegments.Count}");
                foreach (var seg in item.TypelessDataSegments)
                {
                    var props = string.Join(' ', seg.GetType().GetProperties().Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string) || p.PropertyType == typeof(Guid))
                        .Select(p => { try { return $"{p.Name}={p.GetValue(seg)}"; } catch { return p.Name + "=?"; } }));
                    var data = seg.GetType().GetProperty("Data")?.GetValue(seg);
                    var raw = seg.GetType().GetMethod("DebugGetRawData")?.Invoke(seg, null) as byte[];
                    Console.WriteLine($"  {seg.GetType().Name} data={data?.GetType().Name ?? "null"} raw={raw?.Length} {props}");
                }
            }
            return;
        }
        if (args.Length >= 3 && args[0] == "--dump-helpers")
        {
            // args: output folder, packages... - every metamesh with a cloth simulation mesh, LOD0 geometry of both as CSV
            DumpHelpers(args[1], args.Skip(2).ToArray());
            return;
        }
        if (args.Length >= 2 && args[0] == "--cloth-alpha")
        {
            ClothAlpha(args[1], args.Length > 2 ? new Regex(args[2], RegexOptions.IgnoreCase) : null);
            return;
        }
        if (args.Length == 3 && args[0] == "--compare-physics")
        {
            ComparePhysics(args[1], args[2]);
            return;
        }
        if (args.Length == 4 && args[0] == "--compare-material")
        {
            CompareMaterials(args[1], args[2], new Regex(args[3], RegexOptions.IgnoreCase));
            return;
        }
        if (args.Length >= 5 && args[0] == "--compare-skin")
        {
            CompareSkin(args[1], args[2], args[3], args[4]);
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
                Console.WriteLine($"  meshes={meta.Meshes.Count} original={meta.Original} body={meta.UnknownString} clothMesh={meta.ClothMetamesh} clothString={meta.ClothString}");
                foreach (var mesh in meta.Meshes.OrderBy(m => m.Lod))
                {
                    Console.WriteLine($"  lod={mesh.Lod} name={mesh.Name} mat={mesh.Material.Guid} verts={mesh.VertexCount} faces={mesh.FaceCount} pos={mesh.PositionCount} complete={mesh.IsCompleteMesh} gen={mesh.UnknownUint1} u2={mesh.UnknownInt2} u3={mesh.UnknownInt3} f={mesh.UnknownFloat1} b={mesh.UnknownBool1},{mesh.UnknownBool2},{mesh.UnknownBool3} flags=[{string.Join(',', mesh.Flags ?? new System.Collections.Generic.List<string>())}] mflags=[{string.Join(',', mesh.MaterialFlags)}]");
                    if (!string.IsNullOrEmpty(mesh.ClothingMaterial.Name) || (mesh.Flags != null && mesh.Flags.Contains("uses_cloth_simulation")))
                    {
                        var cloth = mesh.ClothingMaterial;
                        Console.WriteLine($"    cloth preset={cloth.Name} bend={cloth.BendingStiffness:R} shear={cloth.ShearingStiffness:R} stretch={cloth.StretchingStiffness:R} anchor={cloth.AnchorStiffness:R} damping={cloth.Damping:R} gravity={cloth.Gravity:R} inertia={cloth.LinearInertia:R} drag={cloth.AirDragMultiplier:R} wind={cloth.Wind:R} maxvel={cloth.MaxLinearVelocity:R} velmult={cloth.LinearVelocityMultiplier:R}");
                    }
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
                            if (!string.IsNullOrEmpty(mesh.ClothingMaterial.Name) || (mesh.Flags != null && mesh.Flags.Contains("uses_cloth_simulation")))
                                Console.WriteLine($"    cloth-alpha=[{string.Join(',', ed.Vertices.GroupBy(v => v.Color.A).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))}] second-alpha=[{string.Join(',', ed.Vertices.GroupBy(v => v.SecondColor.A).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))}]");
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

    static void RestoreCloth(string originalPath, string originalName, string rebuiltPath, string rebuiltName, string outputPath)
    {
        var original = new AssetPackage(originalPath, true, false);
        var rebuilt = new AssetPackage(rebuiltPath, true, false);
        var oldMesh = original.Items.OfType<Metamesh>().Single(x => x.Name == originalName);
        var newMesh = rebuilt.Items.OfType<Metamesh>().Single(x => x.Name == rebuiltName);
        if (oldMesh.ClothMetamesh != Guid.Empty)
            throw new NotSupportedException($"{originalName}: separate cloth simulation mesh is not copied by this command");
        var oldCloth = oldMesh.Meshes.Where(x => x.Flags.Contains("uses_cloth_simulation")).ToArray();
        if (oldCloth.Length == 0) throw new InvalidOperationException($"No original cloth meshes in {originalName}");
        foreach (var source in oldCloth)
        {
            var candidates = newMesh.Meshes.Where(x => x.Lod == source.Lod && x.FaceCount == source.FaceCount && x.PositionCount == source.PositionCount).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException($"Could not uniquely match {source.Name}: {candidates.Length} candidates");
            var target = candidates[0];
            target.Flags.Remove("uses_cloth_simulation");
            target.Flags.Add("uses_cloth_simulation");
            target.UnknownFloat1 = source.UnknownFloat1;
            target.ClothingMaterial = source.ClothingMaterial;
            Console.WriteLine($"restored {source.Name} -> {target.Name}: preset={source.ClothingMaterial.Name} distance={source.UnknownFloat1:R}");
        }
        newMesh.UnknownString = oldMesh.UnknownString;
        newMesh.ClothMetamesh = oldMesh.ClothMetamesh;
        newMesh.ClothUint = oldMesh.ClothUint;
        newMesh.ClothString = oldMesh.ClothString;
        rebuilt.Save(outputPath);
        Console.WriteLine($"restored collision body={newMesh.UnknownString}; output={outputPath}");
    }

    static void VerifyCloth(string originalPath, string originalName, string rebuiltPath, string rebuiltName)
    {
        var original = new AssetPackage(originalPath, true, false);
        var rebuilt = new AssetPackage(rebuiltPath, true, false);
        var oldMesh = original.Items.OfType<Metamesh>().Single(x => x.Name == originalName);
        var newMesh = rebuilt.Items.OfType<Metamesh>().Single(x => x.Name == rebuiltName);
        if (oldMesh.UnknownString != newMesh.UnknownString || oldMesh.ClothMetamesh != newMesh.ClothMetamesh
            || oldMesh.ClothUint != newMesh.ClothUint || oldMesh.ClothString != newMesh.ClothString)
            throw new InvalidOperationException($"{originalName}: collision body or cloth mesh reference differs");
        var oldCloth = oldMesh.Meshes.Where(x => x.Flags.Contains("uses_cloth_simulation")).ToArray();
        var newCloth = newMesh.Meshes.Where(x => x.Flags.Contains("uses_cloth_simulation")).ToArray();
        if (oldCloth.Length != newCloth.Length) throw new InvalidOperationException($"{originalName}: cloth submesh count differs");
        foreach (var source in oldCloth)
        {
            var target = newCloth.Single(x => x.Lod == source.Lod && x.FaceCount == source.FaceCount && x.PositionCount == source.PositionCount);
            if (source.UnknownFloat1 != target.UnknownFloat1) throw new InvalidOperationException($"{source.Name}: distance differs");
            foreach (var property in typeof(ClothingMaterial).GetProperties())
                if (!Equals(property.GetValue(source.ClothingMaterial), property.GetValue(target.ClothingMaterial)))
                    throw new InvalidOperationException($"{source.Name}: {property.Name} differs");
            var a = source.EditData.Data;
            var b = target.EditData.Data;
            var mapping = MatchPositions(a.Positions, b.Positions, 1e-5f);
            if (mapping == null) throw new InvalidOperationException($"{source.Name}: positions differ");
            var identity = Enumerable.Range(0, b.Positions.Length).ToArray();
            if (MultisetDelta(a.Faces.Select(f => FaceIndexKey(a, f, mapping)), b.Faces.Select(f => FaceIndexKey(b, f, identity))) != "0/0")
                throw new InvalidOperationException($"{source.Name}: triangle topology differs");
            var alphaA = AlphaByPosition(a, mapping);
            var alphaB = AlphaByPosition(b, identity);
            if (alphaA.Count != alphaB.Count || alphaA.Any(pair => !alphaB.TryGetValue(pair.Key, out var value) || value != pair.Value))
                throw new InvalidOperationException($"{source.Name}: cloth vertex alpha differs");
            Console.WriteLine($"PASS {source.Name} -> {target.Name}: parameters, topology, alpha");
        }
        Console.WriteLine($"PASS {originalName}: collision body and {oldCloth.Length} cloth submeshes");
    }

    static void Compare(string pathA, string nameA, string pathB, string nameB)
    {
        var pa = new AssetPackage(pathA, true, false);
        var pb = new AssetPackage(pathB, true, false);
        var a = pa.Items.OfType<Metamesh>().Single(x => x.Name == nameA);
        var b = pb.Items.OfType<Metamesh>().Single(x => x.Name == nameB);
        foreach (var ma in a.Meshes.OrderBy(x => x.Lod))
        {
            var mb = b.Meshes.SingleOrDefault(x => x.Name == ma.Name && x.FaceCount == ma.FaceCount)
                ?? b.Meshes.SingleOrDefault(x => x.Lod == ma.Lod && x.FaceCount == ma.FaceCount && x.PositionCount == ma.PositionCount);
            if (mb == null) { Console.WriteLine($"mesh={ma.Name} missing-in-B"); continue; }
            var ea = ma.EditData.Data;
            var eb = mb.EditData.Data;
            int n = Math.Min(ea.Vertices.Length, eb.Vertices.Length);
            int posMismatch = 0, normalMismatch = 0, uvMismatch = 0, tbnMismatch = 0, colorMismatch = 0;
            float maxPos = 0, maxNormal = 0, maxUv = 0, maxTbn = 0;
            for (int i = 0; i < n; i++)
            {
                var va = ea.Vertices[i]; var vb = eb.Vertices[i];
                var posa = ea.Positions[va.PositionIndex]; var posb = eb.Positions[vb.PositionIndex];
                float dp = MaxAbs(posa - posb); maxPos = Math.Max(maxPos, dp); if (dp > 1e-5f) posMismatch++;
                float dn = MaxAbs(va.Normal - vb.Normal); maxNormal = Math.Max(maxNormal, dn); if (dn > 1e-5f) normalMismatch++;
                float du = MaxAbs(va.Uv - vb.Uv); maxUv = Math.Max(maxUv, du); if (du > 1e-5f) uvMismatch++;
                float dt = Math.Max(MaxAbs(va.Tangent - vb.Tangent), MaxAbs(va.Binormal - vb.Binormal)); maxTbn = Math.Max(maxTbn, dt); if (dt > 1e-5f) tbnMismatch++;
                if (va.Color.A != vb.Color.A || va.SecondColor.A != vb.SecondColor.A) colorMismatch++;
            }
            int faceN = Math.Min(ea.Faces.Length, eb.Faces.Length);
            int faceMismatch = 0;
            for (int i = 0; i < faceN; i++)
            {
                var fa = ea.Faces[i]; var fb = eb.Faces[i];
                if (fa.V0 != fb.V0 || fa.V1 != fb.V1 || fa.V2 != fb.V2) faceMismatch++;
            }
            Console.WriteLine($"mesh={ma.Name} counts A={ea.Positions.Length}/{ea.Vertices.Length}/{ea.Faces.Length} B={eb.Positions.Length}/{eb.Vertices.Length}/{eb.Faces.Length} posMismatch={posMismatch}/{n} maxPos={maxPos:R} normalMismatch={normalMismatch} maxNormal={maxNormal:R} uvMismatch={uvMismatch} maxUv={maxUv:R} tbnMismatch={tbnMismatch} maxTbn={maxTbn:R} colorMismatch={colorMismatch} faceMismatch={faceMismatch}/{faceN}");
            if (!string.IsNullOrEmpty(ma.ClothingMaterial.Name))
            {
                Console.WriteLine($"  cloth unordered position Aonly/Bonly={MultisetDelta(ea.Positions.Select(p => PositionKey(p)), eb.Positions.Select(p => PositionKey(p)))} alphaAtPosition Aonly/Bonly={MultisetDelta(ea.Vertices.Select(v => PositionKey(ea.Positions[v.PositionIndex]) + ':' + v.Color.A), eb.Vertices.Select(v => PositionKey(eb.Positions[v.PositionIndex]) + ':' + v.Color.A))} triangles Aonly/Bonly={MultisetDelta(ea.Faces.Select(f => FaceKey(ea, f)), eb.Faces.Select(f => FaceKey(eb, f)))}");
                Console.WriteLine($"  cloth rounded-4 position Aonly/Bonly={MultisetDelta(ea.Positions.Select(p => PositionKey(p, 4)), eb.Positions.Select(p => PositionKey(p, 4)))} triangles Aonly/Bonly={MultisetDelta(ea.Faces.Select(f => FaceKey(ea, f, 4)), eb.Faces.Select(f => FaceKey(eb, f, 4)))} maxNearestPosition={MaxNearestPosition(ea.Positions, eb.Positions):R}");
                var positionMap = MatchPositions(ea.Positions, eb.Positions, 1e-5f);
                if (positionMap != null)
                {
                    var identity = Enumerable.Range(0, eb.Positions.Length).ToArray();
                    var alphaA = AlphaByPosition(ea, positionMap);
                    var alphaB = AlphaByPosition(eb, identity);
                    var alphaDiff = alphaA.Keys.Union(alphaB.Keys).Count(k => !alphaA.TryGetValue(k, out var left) || !alphaB.TryGetValue(k, out var right) || left != right);
                    Console.WriteLine($"  cloth mapped triangles Aonly/Bonly={MultisetDelta(ea.Faces.Select(f => FaceIndexKey(ea, f, positionMap)), eb.Faces.Select(f => FaceIndexKey(eb, f, identity)))} alphaPositionsDifferent={alphaDiff}");
                }
                else Console.WriteLine("  cloth positions could not be matched one-to-one within 1e-5");
            }
        }
    }

    // Skin weights per position, matched by nearest position (seams duplicate positions, so not one-to-one).
    // Both packages use the vanilla skeleton order, so bone indices compare directly.
    static void CompareSkin(string pathA, string nameA, string pathB, string nameB)
    {
        var pa = new AssetPackage(pathA, true, false);
        var pb = new AssetPackage(pathB, true, false);
        var a = pa.Items.OfType<Metamesh>().Single(x => x.Name == nameA);
        var b = pb.Items.OfType<Metamesh>().Single(x => x.Name == nameB);
        foreach (var ma in a.Meshes.OrderBy(x => x.Lod).ThenBy(x => x.Name))
        {
            bool cloth = ma.Flags.Contains("uses_cloth_simulation");
            var mb = b.Meshes.Where(x => x.Lod == ma.Lod && x.Flags.Contains("uses_cloth_simulation") == cloth)
                .OrderBy(x => Math.Abs(x.PositionCount - ma.PositionCount)).FirstOrDefault();
            if (mb == null) { Console.WriteLine($"mesh={ma.Name} missing-in-B"); continue; }
            var ea = ma.EditData.Data;
            var eb = mb.EditData.Data;
            int far = 0, setDiff = 0, weightDiff = 0;
            float maxDist = 0, maxWeight = 0;
            var patterns = new Dictionary<string, int>();
            for (int i = 0; i < ea.Positions.Length; i++)
            {
                int j = -1; float best = float.MaxValue;
                for (int k = 0; k < eb.Positions.Length; k++)
                {
                    var d = new Vector3(ea.Positions[i].X - eb.Positions[k].X, ea.Positions[i].Y - eb.Positions[k].Y, ea.Positions[i].Z - eb.Positions[k].Z).Length();
                    if (d < best) { best = d; j = k; }
                }
                maxDist = Math.Max(maxDist, best);
                if (best > 1e-4f) { far++; continue; }
                var sa = Skin(ea.Bones[i]); var sb = Skin(eb.Bones[j]);
                bool sameSet = sa.Keys.OrderBy(x => x).SequenceEqual(sb.Keys.OrderBy(x => x));
                float diff = sa.Keys.Union(sb.Keys).Max(key => Math.Abs((sa.TryGetValue(key, out var x) ? x : 0) - (sb.TryGetValue(key, out var y) ? y : 0)));
                maxWeight = Math.Max(maxWeight, diff);
                if (!sameSet) setDiff++;
                if (diff > 0.05f) weightDiff++;
                if (!sameSet || diff > 0.05f)
                {
                    string key = Describe(sa) + " -> " + Describe(sb);
                    patterns[key] = patterns.TryGetValue(key, out var c) ? c + 1 : 1;
                }
            }
            if (cloth)
            {
                // Every vertex colour channel, grouped by position (seams keep several vertices per position).
                Dictionary<string, string> Colors(MeshEditData e) => e.Vertices
                    .GroupBy(v => PositionKey(e.Positions[v.PositionIndex], 4))
                    .ToDictionary(g => g.Key, g => string.Join(';', g.Select(v =>
                        $"{v.Color.R},{v.Color.G},{v.Color.B},{v.Color.A}/{v.SecondColor.R},{v.SecondColor.G},{v.SecondColor.B},{v.SecondColor.A}").Distinct().OrderBy(x => x)));
                var ca = Colors(ea); var cb = Colors(eb);
                var differing = ca.Where(kv => !cb.TryGetValue(kv.Key, out var other) || other != kv.Value).ToList();
                Console.WriteLine($"  cloth colours: positions={ca.Count}/{cb.Count} differing={differing.Count}");
                foreach (var kv in differing.Take(5))
                    Console.WriteLine($"    {kv.Key}: A={kv.Value} B={(cb.TryGetValue(kv.Key, out var bv) ? bv : "-")}");
                var sampleA = ea.Vertices.Select(v => $"{v.Color.R},{v.Color.G},{v.Color.B}/{v.SecondColor.R},{v.SecondColor.G},{v.SecondColor.B},{v.SecondColor.A}").Distinct().Take(4);
                Console.WriteLine($"  sample RGB/second A: {string.Join(' ', sampleA)}");
            }
            // Co-located positions inside B (import splits positions at seams): do their skins agree?
            int splitSeams = 0, splitDisagree = 0;
            var groupsB = Enumerable.Range(0, eb.Positions.Length).GroupBy(k => PositionKey(eb.Positions[k], 4)).Where(g => g.Count() > 1);
            foreach (var g in groupsB)
            {
                splitSeams++;
                var skins = g.Select(k => Describe(Skin(eb.Bones[k]))).Distinct().ToList();
                if (skins.Count > 1)
                {
                    splitDisagree++;
                    if (splitDisagree <= 4) Console.WriteLine($"    B seam {g.Key}: {string.Join(" | ", skins)}");
                }
            }
            var groupsA = Enumerable.Range(0, ea.Positions.Length).GroupBy(k => PositionKey(ea.Positions[k], 4)).Where(g => g.Count() > 1).ToList();
            int aDisagree = groupsA.Count(g => g.Select(k => Describe(Skin(ea.Bones[k]))).Distinct().Count() > 1);
            Console.WriteLine($"  seams: A co-located groups={groupsA.Count} disagreeing={aDisagree}; B co-located groups={splitSeams} disagreeing={splitDisagree}");
            Console.WriteLine($"mesh={ma.Name} -> {mb.Name} cloth={cloth} posA={ea.Positions.Length} posB={eb.Positions.Length} unmatched={far} maxDist={maxDist:R} boneSetDiff={setDiff} weightDiff>0.05={weightDiff} maxWeightDiff={maxWeight:R}");
            foreach (var pattern in patterns.OrderByDescending(x => x.Value).Take(6))
                Console.WriteLine($"    {pattern.Value} x {pattern.Key}");
        }
    }

    static void CompareMaterials(string pathA, string pathB, Regex filter)
    {
        var a = new AssetPackage(pathA, true, false).Items.OfType<Material>().Where(m => filter.IsMatch(m.Name)).ToList();
        var b = new AssetPackage(pathB, true, false).Items.OfType<Material>().ToList();
        string Describe(Material m)
        {
            var x = m.ExtraMaterialSettings;
            string extra = x == null ? "-" : string.Join(' ', x.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(x)}"));
            return $"shader={m.Shader?.Guid} blend={m.BlendMode} alphaTest={m.AlphaTest} flags=[{string.Join(',', m.Flags)}] layout=[{string.Join(',', m.VertexLayoutFlags)}] shaderFlags=[{string.Join(',', m.ShaderMaterialFlags)}] textures=[{string.Join(',', m.Textures.Select(t => $"{t.Key}:{(t.Value.TryGetItem(out var tex) ? tex.Name : t.Value.Guid.ToString())}"))}] extra: {extra}";
        }
        foreach (var ma in a.OrderBy(m => m.Name))
        {
            var mb = b.FirstOrDefault(m => string.Equals(m.Name, ma.Name, StringComparison.OrdinalIgnoreCase));
            string da = Describe(ma), db = mb == null ? "missing" : Describe(mb);
            Console.WriteLine($"material={ma.Name} {(da == db ? "SAME" : "DIFFERENT")}");
            if (da != db || Environment.GetEnvironmentVariable("GWP_SHOW_MATERIAL") == "1") { Console.WriteLine($"  A: {da}"); Console.WriteLine($"  B: {db}"); }
        }
    }

    // Vertical max-distance ramp on the LOD0 cloth submeshes: vertices already pinned (alpha 0) stay pinned; every
    // other vertex gets alpha = (top - z) / (top - bottom) over the cloth's own height, so the top edge is 0 and the
    // bottom edge 255. Only vertex colour alpha changes. The solver multiplies alpha/255 by the submesh max distance
    // (Shaders/Sources/gpucloth_solver.rs).
    static void ClothGradient(string path, string metameshName, string outputPath)
    {
        var package = new AssetPackage(path, true, false);
        var metamesh = package.Items.OfType<Metamesh>().Single(x => x.Name == metameshName);
        foreach (var mesh in metamesh.Meshes.Where(x => x.Lod == 0 && x.Flags.Contains("uses_cloth_simulation")))
        {
            var data = mesh.EditData.Data;
            float top = data.Positions.Max(p => p.Z), bottom = data.Positions.Min(p => p.Z);
            int pinned = 0, changed = 0;
            var before = data.Vertices.Select(v => (int)v.Color.A).ToArray();
            for (int i = 0; i < data.Vertices.Length; i++)
            {
                var vertex = data.Vertices[i];
                if (vertex.Color.A == 0) { pinned++; continue; }
                float z = data.Positions[vertex.PositionIndex].Z;
                byte alpha = (byte)Math.Round(255f * Math.Clamp((top - z) / (top - bottom), 0f, 1f));
                if (alpha != vertex.Color.A) changed++;
                var color = vertex.Color;
                color.A = alpha;
                vertex.Color = color;
                data.Vertices[i] = vertex;
            }
            var after = data.Vertices.Select(v => (int)v.Color.A).ToArray();
            Console.WriteLine($"{mesh.Name}: z top={top:0.000} bottom={bottom:0.000} vertices={data.Vertices.Length} pinned kept={pinned} changed={changed}");
            Console.WriteLine($"  before: 0={before.Count(a => a == 0)} 255={before.Count(a => a == 255)} between={before.Count(a => a > 0 && a < 255)}");
            Console.WriteLine($"  after:  0={after.Count(a => a == 0)} 255={after.Count(a => a == 255)} between={after.Count(a => a > 0 && a < 255)} distinct={after.Distinct().Count()}");
            mesh.EditData.Data = data;
        }
        package.Save(outputPath);
        Console.WriteLine($"saved {outputPath}");
    }

    // Metameshes that simulate on a separate helper: write LOD0 render and helper geometry (position, alpha, material) to CSV.
    static void DumpHelpers(string outputFolder, string[] paths)
    {
        var packages = paths.Select(x => new AssetPackage(x, true, false)).ToList();
        var byGuid = new Dictionary<Guid, Metamesh>();
        foreach (var mm in packages.SelectMany(x => x.Items.OfType<Metamesh>())) byGuid[mm.Guid] = mm;
        System.IO.Directory.CreateDirectory(outputFolder);
        foreach (var mm in byGuid.Values.Where(x => x.ClothMetamesh != Guid.Empty).OrderBy(x => x.Name))
        {
            byGuid.TryGetValue(mm.ClothMetamesh, out var helper);
            var cloth = mm.Meshes.Where(x => x.Lod == 0 && x.Flags.Contains("uses_cloth_simulation")).ToList();
            Console.WriteLine($"{mm.Name} -> {helper?.Name ?? mm.ClothMetamesh.ToString()} cloth={string.Join(',', cloth.Select(x => $"{x.Name}:{x.ClothingMaterial?.Name}:{x.UnknownFloat1:R}"))}");
            if (helper == null) continue;
            using var w = new System.IO.StreamWriter(System.IO.Path.Combine(outputFolder, mm.Name + ".csv"));
            w.WriteLine("kind,part,x,y,z,alpha,a,b,c");
            void Write(string part, Mesh mesh)
            {
                var e = mesh.EditData?.Data;
                if (e == null) return;
                foreach (var v in e.Vertices)
                {
                    var q = e.Positions[v.PositionIndex];
                    w.WriteLine(string.Format(CultureInfo.InvariantCulture, "v,{0},{1},{2},{3},{4},,,", part, q.X, q.Y, q.Z, v.Color.A));
                }
                foreach (var f in e.Faces) w.WriteLine($"f,{part},,,,,{f.V0},{f.V1},{f.V2}");
            }
            foreach (var mesh in mm.Meshes.Where(x => x.Lod == 0))
                Write((mesh.Flags.Contains("uses_cloth_simulation") ? "cloth:" : "body:") + mesh.Name, mesh);
            foreach (var mesh in helper.Meshes.Where(x => x.Lod == 0))
                Write("helper:" + mesh.Name, mesh);
        }
    }

    // Distribution of cloth vertex alpha (max-distance weight) on every LOD0 cloth submesh of a package.
    static void ClothAlpha(string path, Regex? filter)
    {
        var package = new AssetPackage(path, true, false);
        int meshes = 0, withGradient = 0;
        foreach (var mm in package.Items.OfType<Metamesh>().Where(m => filter == null || filter.IsMatch(m.Name)))
        foreach (var mesh in mm.Meshes.Where(x => x.Lod == 0 && (x.Flags.Contains("uses_cloth_simulation") || !string.IsNullOrEmpty(x.ClothingMaterial?.Name) || Environment.GetEnvironmentVariable("GWP_ALL_MESHES") == "1")))
        {
            var e = mesh.EditData?.Data;
            if (e == null) continue;
            var alphas = e.Vertices.Select(v => (int)v.Color.A).ToArray();
            int zero = alphas.Count(a => a == 0), full = alphas.Count(a => a == 255), mid = alphas.Length - zero - full;
            meshes++; if (mid > 0) withGradient++;
            Console.WriteLine($"{mesh.Name} flag={mesh.Flags.Contains("uses_cloth_simulation")} distance={mesh.UnknownFloat1:R} preset={mesh.ClothingMaterial?.Name} verts={alphas.Length} alpha0={zero} alpha255={full} between={mid} distinct={alphas.Distinct().Count()}");
        }
        Console.WriteLine($"cloth submeshes={meshes} with intermediate alpha={withGradient}");
    }

    static void ComparePhysics(string pathA, string pathB)
    {
        PhysicsDescriptionData Data(PhysicsShape ps) => ps.TypelessDataSegments
            .Select(d => d.GetType().GetProperty("Data")?.GetValue(d)).OfType<PhysicsDescriptionData>().FirstOrDefault();
        string V(System.Numerics.Vector3 v) => $"{v.X:0.0000},{v.Y:0.0000},{v.Z:0.0000}";
        List<string> Describe(PhysicsDescriptionData d)
        {
            var lines = new List<string>();
            lines.AddRange(d.Capsules.Select(c => $"capsule r={c.Radius:0.0000} c={V(c.Center)} a={V(c.Axis)} mat={c.PhysicsMaterialName} flags={string.Join(',', c.Flags)}"));
            lines.AddRange(d.Spheres.Select(x => $"sphere {string.Join(' ', x.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(x)}"))}"));
            foreach (var m in d.Manifolds)
            {
                var verts = m.Vertices.Select(V).OrderBy(x => x).ToList();
                var faces = m.Faces.Select(f =>
                {
                    var idx = new[] { f.Index1, f.Index2, f.Index3, f.Index4 }.Where(i => i >= 0 && i < m.Vertices.Length).Select(i => V(m.Vertices[i])).OrderBy(x => x);
                    return $"[{string.Join('|', idx)}] mat={(f.PhysicsMaterialIndex >= 0 && f.PhysicsMaterialIndex < m.UnknownPhysicsMaterials.Count ? m.UnknownPhysicsMaterials[f.PhysicsMaterialIndex] : f.PhysicsMaterialIndex.ToString())} flags={string.Join(',', f.Flags ?? new List<string>())}";
                }).OrderBy(x => x).ToList();
                lines.Add($"manifold verts={m.Vertices.Length} faces={m.Faces.Length} materials={string.Join(',', m.UnknownPhysicsMaterials)} uint={m.UnknownUint}");
                if (Environment.GetEnvironmentVariable("GWP_RAW_PHYSICS") == "1")
                {
                    for (int i = 0; i < m.Vertices.Length; i++) Console.WriteLine($"    raw v{i} {V(m.Vertices[i])}");
                    foreach (var f in m.Faces) Console.WriteLine($"    raw f {f.Index1},{f.Index2},{f.Index3},{f.Index4} mat={f.PhysicsMaterialIndex}");
                }
                lines.AddRange(verts.Select(x => "  v " + x));
                lines.AddRange(faces.Select(x => "  f " + x));
            }
            return lines;
        }
        var a = new AssetPackage(pathA, true, false).Items.OfType<PhysicsShape>().ToList();
        var b = new AssetPackage(pathB, true, false).Items.OfType<PhysicsShape>().ToList();
        foreach (var sa in a.OrderBy(x => x.Name))
        {
            var sb = b.FirstOrDefault(x => x.Name == sa.Name);
            var da = Data(sa); var db = sb == null ? null : Data(sb);
            if (da == null || db == null) { Console.WriteLine($"physics={sa.Name} data A={(da != null)} B={(db != null)}"); continue; }
            var la = Describe(da); var lb = Describe(db);
            bool same = la.SequenceEqual(lb);
            string Segs(PhysicsShape ps) => string.Join(",", ps.TypelessDataSegments.Select(d => { var v = d.GetType().GetProperty("Data")?.GetValue(d); return v == null ? "null" : v.GetType().Name; }));
            Console.WriteLine($"  segments A=[{Segs(sa)}] B=[{Segs(sb!)}]");
            foreach (var segA in sa.TypelessDataSegments)
            {
                var va = segA.GetType().GetProperty("Data")?.GetValue(segA);
                if (va == null || va is PhysicsDescriptionData) continue;
                var segB = sb!.TypelessDataSegments.FirstOrDefault(d => d.GetType().GetProperty("Data")?.GetValue(d)?.GetType() == va.GetType());
                string Bytes(object loader) => loader.GetType().GetMethod("DebugGetRawData")?.Invoke(loader, null) is byte[] b
                    ? b.Length + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)).Substring(0, 16) : "?";
                Console.WriteLine($"  {va.GetType().Name}: A={Bytes(segA)} B={(segB == null ? "missing" : Bytes(segB))}");
            }
            Console.WriteLine($"physics={sa.Name} caps={da.Capsules.Count}/{db.Capsules.Count} spheres={da.Spheres.Count}/{db.Spheres.Count} manifolds={da.Manifolds.Count}/{db.Manifolds.Count} {(same ? "SAME" : "DIFFERENT")}");
            if (!same)
            {
                foreach (var x in la.Except(lb).Take(12)) Console.WriteLine($"  A only: {x}");
                foreach (var x in lb.Except(la).Take(12)) Console.WriteLine($"  B only: {x}");
            }
        }
    }

    static Dictionary<int, float> Skin(MeshEditData.Bone bone)
    {
        var result = new Dictionary<int, float>();
        void Add(byte index, float weight) { if (weight > 1e-4f) result[index] = (result.TryGetValue(index, out var w) ? w : 0) + weight; }
        Add(bone.B0, bone.W0); Add(bone.B1, bone.W1); Add(bone.B2, bone.W2); Add(bone.B3, bone.W3);
        if (result.Count == 0) result[-1] = 1;
        return result;
    }

    static string Describe(Dictionary<int, float> skin) =>
        string.Join(' ', skin.OrderByDescending(x => x.Value).Select(x => $"{x.Key}:{x.Value:0.00}"));

    static string PositionKey(Vector4 p) => PositionKey(p, 5);
    static string PositionKey(Vector4 p, int decimals) => string.Join(',',
        Math.Round(p.X, decimals).ToString("F" + decimals, CultureInfo.InvariantCulture),
        Math.Round(p.Y, decimals).ToString("F" + decimals, CultureInfo.InvariantCulture),
        Math.Round(p.Z, decimals).ToString("F" + decimals, CultureInfo.InvariantCulture));

    static string FaceKey(MeshEditData data, MeshEditData.Face face, int decimals = 5)
    {
        var vertices = new[] { face.V0, face.V1, face.V2 }
            .Select(i => PositionKey(data.Positions[data.Vertices[i].PositionIndex], decimals)).ToArray();
        Array.Sort(vertices, StringComparer.Ordinal);
        return string.Join('|', vertices);
    }

    static string MultisetDelta(IEnumerable<string> a, IEnumerable<string> b)
    {
        var counts = new Dictionary<string, int>();
        foreach (var key in a) counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        foreach (var key in b) counts[key] = counts.TryGetValue(key, out var count) ? count - 1 : -1;
        return $"{counts.Values.Where(v => v > 0).Sum()}/{-counts.Values.Where(v => v < 0).Sum()}";
    }

    static float MaxNearestPosition(Vector4[] a, Vector4[] b)
    {
        float largest = 0;
        foreach (var p in a)
        {
            float nearest = float.MaxValue;
            foreach (var q in b)
            {
                var delta = new Vector3(p.X - q.X, p.Y - q.Y, p.Z - q.Z);
                nearest = Math.Min(nearest, delta.Length());
            }
            largest = Math.Max(largest, nearest);
        }
        return largest;
    }

    static int[] MatchPositions(Vector4[] a, Vector4[] b, float tolerance)
    {
        if (a.Length != b.Length) return null;
        var result = new int[a.Length];
        var used = new HashSet<int>();
        for (int i = 0; i < a.Length; i++)
        {
            int nearestIndex = -1;
            float nearest = float.MaxValue;
            for (int j = 0; j < b.Length; j++)
            {
                var delta = new Vector3(a[i].X - b[j].X, a[i].Y - b[j].Y, a[i].Z - b[j].Z);
                var distance = delta.Length();
                if (distance < nearest) { nearest = distance; nearestIndex = j; }
            }
            if (nearest > tolerance || !used.Add(nearestIndex)) return null;
            result[i] = nearestIndex;
        }
        return result;
    }

    static string FaceIndexKey(MeshEditData data, MeshEditData.Face face, int[] positionMap)
    {
        var indices = new[] { face.V0, face.V1, face.V2 }
            .Select(i => positionMap[(int)data.Vertices[i].PositionIndex]).ToArray();
        Array.Sort(indices);
        return string.Join(',', indices);
    }

    static Dictionary<int, string> AlphaByPosition(MeshEditData data, int[] positionMap) =>
        data.Vertices.GroupBy(v => positionMap[(int)v.PositionIndex])
            .ToDictionary(g => g.Key, g => string.Join(',', g.Select(v => v.Color.A).Distinct().OrderBy(a => a)));

    static float MaxAbs(Vector2 v) => Math.Max(Math.Abs(v.X), Math.Abs(v.Y));
    static float MaxAbs(Vector4 v) => Math.Max(Math.Max(Math.Abs(v.X), Math.Abs(v.Y)), Math.Max(Math.Abs(v.Z), Math.Abs(v.W)));
}
