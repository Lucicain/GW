using System; using System.IO; using System.Linq; using System.Text.Json; using Assimp;
class P { static void Main(string[] a) {
  if (a[0]=="--obj2fbx") { using var c=new AssimpContext(); foreach (var o in Directory.GetFiles(a[1],"*.obj")) { var dst=Path.Combine(a[2], Path.GetFileNameWithoutExtension(o).Replace("_manifold0","")+".fbx"); Directory.CreateDirectory(a[2]); var sc=c.ImportFile(o, PostProcessSteps.None); sc.Meshes[0].Name=Path.GetFileNameWithoutExtension(dst); if (sc.RootNode.ChildCount>0) sc.RootNode.Children[0].Name=sc.Meshes[0].Name; Console.WriteLine(dst+" ok="+c.ExportFile(sc,dst,"fbx")+" v="+sc.Meshes[0].VertexCount+" f="+sc.Meshes[0].FaceCount); } return; }
  var man = JsonDocument.Parse(File.ReadAllText(a[1])).RootElement.GetProperty("metameshes").EnumerateArray().ToDictionary(e=>e.GetProperty("name").GetString(), e=>e);
  using var ctx = new AssimpContext(); int bad=0;
  foreach (var f in Directory.GetFiles(a[0], "*.fbx").OrderBy(x=>x)) {
    var sc = ctx.ImportFile(f, PostProcessSteps.JoinIdenticalVertices);
    string n = Path.GetFileNameWithoutExtension(f);
    var subs = man[n].GetProperty("submeshes").EnumerateArray().ToList();
    int expV = subs.Sum(s=>s.GetProperty("verts").GetInt32()), expF = subs.Sum(s=>s.GetProperty("faces").GetInt32());
    int gotV = sc.Meshes.Sum(m=>m.VertexCount), gotF = sc.Meshes.Sum(m=>m.FaceCount);
    int bones = sc.Meshes.SelectMany(m=>m.Bones).Select(b=>b.Name).Distinct().Count();
    bool ok = sc.MeshCount==subs.Count && gotF==expF; // vertex counts differ: FBX drops UV2/tangents and Assimp welds what is left
    if(!ok) bad++;
    var m0=sc.Meshes[0]; Console.Write($"uvch={m0.TextureCoordinateChannelCount} colch={m0.VertexColorChannelCount} normals={m0.HasNormals} tangents={m0.HasTangentBasis} | "); Console.WriteLine($"{(ok?"OK ":"BAD")} {n,-16} meshes {sc.MeshCount}/{subs.Count} verts {gotV}/{expV} faces {gotF}/{expF} bones={bones} mats={string.Join(",", sc.Materials.Select(m=>m.Name).Distinct())}");
  }
  Console.WriteLine("bad=" + bad);
}}
