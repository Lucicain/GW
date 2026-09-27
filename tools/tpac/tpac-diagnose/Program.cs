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
