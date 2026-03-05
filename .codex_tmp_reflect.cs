using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

string dll = Environment.GetEnvironmentVariable("GWP_DLL")!;
string dir = Path.GetDirectoryName(dll)!;
AssemblyLoadContext.Default.Resolving += (ctx, asmName) =>
{
    string p = Path.Combine(dir, asmName.Name + ".dll");
    if (File.Exists(p)) return ctx.LoadFromAssemblyPath(p);
    return null;
};

var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
var types = asm.GetTypes().Where(t => t.FullName != null && (
    t.FullName.Contains("Action") || t.FullName.Contains("MapEvent") || t.FullName.Contains("Encounter")
));

foreach (var t in types.OrderBy(t=>t.FullName))
{
    var methods = t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)
        .Where(m => m.Name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0
                 || m.Name.IndexOf("MapEvent", StringComparison.OrdinalIgnoreCase) >= 0
                 || m.Name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0
                 || m.Name.IndexOf("Engage", StringComparison.OrdinalIgnoreCase) >= 0
                 || m.Name.IndexOf("Encounter", StringComparison.OrdinalIgnoreCase) >= 0
                 || m.Name.IndexOf("Siege", StringComparison.OrdinalIgnoreCase) >= 0)
        .ToList();
    if (methods.Count == 0) continue;

    Console.WriteLine($"TYPE {t.FullName}");
    foreach (var m in methods.OrderBy(m=>m.Name))
    {
        string pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({pars})");
    }
}
