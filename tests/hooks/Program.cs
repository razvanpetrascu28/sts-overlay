using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 2) throw new ArgumentException("Pass the game data directory and built mod DLL.");
string gameData = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) => {
    string path = Path.Combine(gameData, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
if (mod.GetType("StsOverlay.UiSmoke") != null) throw new Exception("Release DLL contains UI test fixtures.");
Console.WriteLine("PASS release excludes UI test fixtures");
var harmonyAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameData, "0Harmony.dll"));
Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true)!;
object harmony = Activator.CreateInstance(harmonyType, "spire.stats.hook.check")!;
try {
    harmonyType.GetMethod("PatchAll", new[] { typeof(Assembly) })!.Invoke(harmony, new object[] { mod });
    var methods = (System.Collections.IEnumerable)harmonyType.GetMethod("GetPatchedMethods")!.Invoke(harmony, null)!;
    int count = 0;
    foreach (MethodBase method in methods) { count++; Console.WriteLine($"PASS hook {method.DeclaringType!.Name}.{method.Name}"); }
    if (count < 10) throw new Exception($"Only {count} hooks applied.");
    Console.WriteLine($"{count} game hooks applied successfully against the installed assembly.");
    ObserverChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameData, "sts2.dll")), mod);
} catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
