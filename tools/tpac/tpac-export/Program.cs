using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Numerics; using System.Text.Json;
using TpacTool.Lib; using TpacTool.IO; using TpacTool.IO.Assimp;

// args: outSources refRoot core_game.tpac skeletons.tpac <mod packages...>
class Lookup : IDependenceResolver
{
    public readonly Dictionary<Guid, AssetItem> Items = new Dictionary<Guid, AssetItem>();
    public void Add(AssetPackage p) { foreach (var i in p.Items) if (!Items.ContainsKey(i.Guid)) Items[i.Guid] = i; }
    public bool Resolve<T>(Guid guid, string name, out T result) where T : class, IDependence
    {
        result = Items.TryGetValue(guid, out var r) ? r as T : null;
        return result != null;
    }
}

// Textures are written to a sibling "textures" folder; point the FBX at it so Blender finds them.
class SiblingTextureFbx : TpacTool.IO.Assimp.FbxExporter
{
    public override void Export(string path)
    {
        foreach (var t in TexturePathMapping.Keys.ToList()) TexturePathMapping[t] = "../textures/" + Path.GetFileName(TexturePathMapping[t]);
        base.Export(path);
    }
}

class P
{
    static float[] V(Vector4 v) => new[] { v.X, v.Y, v.Z, v.W };
    static float[] V3(Vector3 v) => new[] { v.X, v.Y, v.Z };

    // BC5 keeps only X and Y; the PNG comes out with blue = 0. Rebuild Z = sqrt(1 - x^2 - y^2).
    static void RebuildNormalZ(string png)
    {
        byte[] data;
        int w, h;
        using (var bmp = new System.Drawing.Bitmap(png))
        {
            w = bmp.Width; h = bmp.Height;
            var rect = new System.Drawing.Rectangle(0, 0, w, h);
            using var copy = bmp.Clone(rect, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var bd = copy.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, copy.PixelFormat);
            data = new byte[bd.Stride * h];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, data, 0, data.Length);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * bd.Stride + x * 3; // BGR
                    float nx = data[i + 2] / 127.5f - 1f, ny = data[i + 1] / 127.5f - 1f;
                    float nz = MathF.Sqrt(MathF.Max(0f, 1f - nx * nx - ny * ny));
                    data[i] = (byte)MathF.Round((nz + 1f) * 127.5f);
                }
            System.Runtime.InteropServices.Marshal.Copy(data, 0, bd.Scan0, data.Length);
            copy.UnlockBits(bd);
            copy.Save(png + ".tmp", System.Drawing.Imaging.ImageFormat.Png);
        }
        File.Delete(png);
        File.Move(png + ".tmp", png);
    }

    static void Main(string[] a)
    {
        string outSrc = a[0], refRoot = a[1];
        var mgr = new AssetManager(); var look = new Lookup(); DefaultDependenceResolver.Instance = look;
        var core = new AssetPackage(a[2], true, false); mgr.AddPackage(core);
        var skp = new AssetPackage(a[3], true, false);
        var pk = a.Skip(4).Select(p => new AssetPackage(p, true, false)).ToList();
        foreach (var q in pk) { try { mgr.AddPackage(q); } catch (Exception e) { Console.WriteLine("resolver skip " + q.Guid + ": " + e.Message); } }
        look.Add(core); foreach (var q in pk) look.Add(q); DefaultDependenceResolver.Instance = look;
        var human = core.Items.OfType<Skeleton>().First(s => s.Name == "human_skeleton");
        var horse = skp.Items.OfType<Skeleton>().First(s => s.Name == "horse_skeleton");
        Console.WriteLine("assimp=" + AssimpModelExporter.InitAssimp());
        var allMats = pk.SelectMany(p => p.Items.OfType<Material>()).ToDictionary(m => m.Guid, m => m.Name);
        var opt = ModelExporter.ModelExportOption.ExportAllLod;
        var unresolved = new HashSet<Guid>();

        for (int pi = 0; pi < pk.Count; pi++)
        {
            var p = pk[pi];
            string pname = Path.GetFileNameWithoutExtension(a[4 + pi]);
            bool toSources = pname == "gwp_inherited_legacy_assets" || pname == "gwp_dual_wield_animations";
            string rdir = Path.Combine(refRoot, pname);
            Directory.CreateDirectory(rdir);
            var manifest = new Dictionary<string, object>();

            manifest["materials"] = p.Items.OfType<Material>().Select(m => new
            {
                name = m.Name, guid = m.Guid,
                shader = m.Shader.TryGetItem(out var sh) ? sh.Name : m.Shader.Guid.ToString(),
                blend = m.BlendMode, flags = m.Flags, vertexLayout = m.VertexLayoutFlags, shaderFlags = m.ShaderMaterialFlags, alphaTest = m.AlphaTest,
                textures = m.Textures.ToDictionary(t => t.Key.ToString(), t => t.Value.TryGetItem(out var tx) ? tx.Name : t.Value.Guid.ToString()),
                extra = m.ExtraMaterialSettings == null ? null : new
                {
                    m.ExtraMaterialSettings.AreamapScale, m.ExtraMaterialSettings.AreamapAmount, m.ExtraMaterialSettings.DetailnormalScale,
                    m.ExtraMaterialSettings.NormalmapPower,
                    meshVectorArgument = V(m.ExtraMaterialSettings.MeshVectorArgument), meshVectorArgument2 = V(m.ExtraMaterialSettings.MeshVectorArgument2),
                    factorColor = V(m.ExtraMaterialSettings.MeshFactorColorMultiplier), factor2Color = V(m.ExtraMaterialSettings.MeshFactor2ColorMultiplier),
                    m.ExtraMaterialSettings.RenderOrder, m.ExtraMaterialSettings.MipmapBias, m.ExtraMaterialSettings.SpecularCoef,
                    m.ExtraMaterialSettings.GlossCoef, m.ExtraMaterialSettings.ParallaxAmount, m.ExtraMaterialSettings.ParallaxOffset,
                    m.ExtraMaterialSettings.AmbientOcclusionCoef, m.ExtraMaterialSettings.ExposureCompensation
                }
            }).ToList();

            var meshes = new List<object>();
            foreach (var mm in p.Items.OfType<Metamesh>())
            {
                bool skinned = mm.Meshes.Any(m => (m.VertexStream?.Data?.BoneWeights?.Length ?? 0) > 0);
                var skel = (!skinned || mm.Name.Contains("shield")) ? null : (mm.Name.Contains("harness") ? horse : human);
                meshes.Add(new
                {
                    name = mm.Name, skeleton = skel?.Name,
                    submeshes = mm.Meshes.Select(m =>
                    {
                        string mat = allMats.TryGetValue(m.Material.Guid, out var mn) ? mn : (m.SecondMaterial != null && allMats.TryGetValue(m.SecondMaterial.Guid, out var mn2) ? mn2 : null);
                        if (mat == null && m.Material.Guid != Guid.Empty) unresolved.Add(m.Material.Guid);
                        return new
                        {
                            m.Name, m.Lod, verts = m.VertexCount, faces = m.FaceCount, materialGuid = m.Material.Guid, material = mat,
                            secondMaterialGuid = m.SecondMaterial?.Guid, flags = m.Flags, materialFlags = m.MaterialFlags,
                            factorColor = V(m.FactorColor), factor2Color = V(m.Factor2Color),
                            vectorArgument = V(m.VectorArgument), vectorArgument2 = V(m.VectorArgument2),
                            cloth = m.ClothingMaterial == null ? null : new
                            {
                                m.ClothingMaterial.Name, m.ClothingMaterial.BendingStiffness,
                                m.ClothingMaterial.ShearingStiffness, m.ClothingMaterial.StretchingStiffness
                            }
                        };
                    }).ToList()
                });
                Directory.CreateDirectory(Path.Combine(rdir, "dae"));
                ModelExporter.ExportToFile(Path.Combine(rdir, "dae", mm.Name + ".dae"), mm, skel, opt);
                // Each submesh keeps its one real material in either slot; the FBX exporter resolves both.
                foreach (var m in mm.Meshes)
                {
                    if (m.Material == null || m.Material.Guid == Guid.Empty) m.Material = m.SecondMaterial;
                    if (m.SecondMaterial == null || m.SecondMaterial.Guid == Guid.Empty) m.SecondMaterial = m.Material;
                }
                if (toSources)
                {
                    Directory.CreateDirectory(Path.Combine(outSrc, "models"));
                    try { ModelExporter.ExportToFile(new SiblingTextureFbx(), Path.Combine(outSrc, "models", mm.Name + ".fbx"), mm, skel, null, null, opt); }
                    catch (Exception e) { Console.WriteLine("fbx fail " + mm.Name + ": " + e.Message); }
                }
                Console.WriteLine($"model {mm.Name} skel={skel?.Name}");
            }
            manifest["metameshes"] = meshes;

            var tdir = toSources ? Path.Combine(outSrc, "textures") : Path.Combine(rdir, "textures");
            var texList = new List<object>();
            foreach (var t in p.Items.OfType<Texture>())
            {
                Directory.CreateDirectory(tdir);
                TextureExporter.ExportToFile(Path.Combine(tdir, t.Name + ".png"), t);
                if (t.Format.ToString() == "BC5") RebuildNormalZ(Path.Combine(tdir, t.Name + ".png"));
                texList.Add(new { t.Name, t.Guid, format = t.Format.ToString(), t.Width, t.Height, mips = t.MipmapCount });
            }
            manifest["textures"] = texList;

            var phys = new List<object>();
            foreach (var ps in p.Items.OfType<PhysicsShape>())
            {
                var datas = ps.TypelessDataSegments.Select(d => d.GetType().GetProperty("Data")?.GetValue(d)).ToList();
                string types = string.Join(",", datas.Select(d => d?.GetType().Name ?? "null"));
                var pd = datas.OfType<PhysicsDescriptionData>().FirstOrDefault();
                if (pd == null) { phys.Add(new { ps.Name, note = "no description data", types }); Console.WriteLine($"physics {ps.Name} types={types}"); continue; }
                Directory.CreateDirectory(Path.Combine(rdir, "physics"));
                int k = 0;
                foreach (var mf in pd.Manifolds)
                {
                    using var w = new StreamWriter(Path.Combine(rdir, "physics", $"{ps.Name}_manifold{k++}.obj"));
                    foreach (var v in mf.Vertices) w.WriteLine($"v {v.X} {v.Y} {v.Z}");
                    foreach (var f in mf.Faces)
                    {
                        if (f.Index4 >= 0 && f.Index4 != f.Index3) w.WriteLine($"f {f.Index1 + 1} {f.Index2 + 1} {f.Index3 + 1} {f.Index4 + 1}");
                        else w.WriteLine($"f {f.Index1 + 1} {f.Index2 + 1} {f.Index3 + 1}");
                    }
                }
                phys.Add(new
                {
                    ps.Name, types, descName = pd.Name,
                    capsules = pd.Capsules.Select(c => new { c.Radius, center = V3(c.Center), axis = V3(c.Axis), c.PhysicsMaterialName, c.Flags }).ToList(),
                    spheres = pd.Spheres.Select(s => new { s.Radius, center = V3(s.Center), s.PhysicsMaterialName, s.Flags }).ToList(),
                    manifolds = pd.Manifolds.Select(m => new { verts = m.Vertices.Length, faces = m.Faces.Length, materials = m.UnknownPhysicsMaterials }).ToList()
                });
                Console.WriteLine($"physics {ps.Name} caps={pd.Capsules.Count} sph={pd.Spheres.Count} man={pd.Manifolds.Count}");
            }
            manifest["physicsShapes"] = phys;

            var anims = new List<object>();
            foreach (var sa in p.Items.OfType<SkeletalAnimation>())
            {
                string safe = sa.Name.Replace('|', '_');
                Directory.CreateDirectory(Path.Combine(outSrc, "animations"));
                try { AssimpModelExporter.ExportToFile(Path.Combine(outSrc, "animations", safe + ".fbx"), null, human, sa, null, 0); }
                catch (Exception e) { Console.WriteLine("anim fbx fail " + sa.Name + ": " + e.Message); }
                Directory.CreateDirectory(Path.Combine(rdir, "dae"));
                ModelExporter.ExportToFile(Path.Combine(rdir, "dae", safe + ".dae"), null, human, sa, null, 0);
                anims.Add(new { sa.Name, sa.Guid, sa.BoneNum, sa.Duration, skeletonGuid = sa.Skeleton });
            }
            manifest["skeletalAnimations"] = anims;

            var itemsByGuid = pk.SelectMany(q => q.Items).GroupBy(i => i.Guid).ToDictionary(g => g.Key, g => g.First().Name);
            manifest["animationClips"] = p.Items.OfType<AnimationClip>().Select(c => new
            {
                c.Name, c.Duration, c.Source1, c.Source2, c.Param1, c.Param2, c.Param3, c.Priority,
                animation = c.Animation, animationName = itemsByGuid.TryGetValue(c.Animation, out var an) ? an : null,
                stepPoints = V(c.StepPoints), c.SoundCode, c.VoiceCode, c.FacialAnimationId, c.BlendsWithAction, c.ContinueWithAction,
                c.LeftHandPose, c.RightHandPose, c.CombatParameterId, c.BlendInPeriod, c.BlendOutPeriod, c.DoNotInterpolate,
                c.GeneratedIndex, c.UnknownClipName, c.ClipSource1Name, c.ClipSource2Name
            }).ToList();

            File.WriteAllText(Path.Combine(rdir, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"== {pname} done");
        }
        File.WriteAllLines(Path.Combine(refRoot, "unresolved-material-guids.txt"), unresolved.Select(g => g.ToString()));
        Console.WriteLine("unresolved material guids=" + unresolved.Count);
    }
}
