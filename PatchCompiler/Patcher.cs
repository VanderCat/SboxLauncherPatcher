using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mono.Cecil;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using Sandbox.SolutionGenerator;
using Tommy;

namespace VanderCat;
public class CompilePatcher
{
    // make sure DoPatching() is called at start either by
    // the mod loader or by your injector

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Assembly GetEngine()
    {
        return typeof(Sandbox.Engine.BindCollection).Assembly;
    }

    private static TomlTable Config;
    internal static Logger Log = new Logger("CompilerPatcher");

    private static TomlTable ReadConfig(string path)
    {
        using var sr = new StreamReader(path);
        return TOML.Parse(sr);
    }
    public static void DoPatching()
    {
        Log.Info("Trying to patch compiler");
        Log.Trace($"Reading config at {Environment.CurrentDirectory}");
        if (!File.Exists("./assemblyPatches/compiler.toml"))
        {
            var assembly = Assembly.GetExecutingAssembly();
            var configStream = assembly.GetManifestResourceStream("VanderCat.config.toml");
            using (var resourceFile = new FileStream("./assemblyPatches/compiler.toml", FileMode.Create))
            {
                configStream.Seek(0, SeekOrigin.Begin);
                configStream.CopyTo(resourceFile);
            }
        }
        Config = ReadConfig("./assemblyPatches/compiler.toml");
        Config.GetEnumerator();
        Log.Trace("Adding custom Assembly Resolving");
        PatchAssemblyLoading();
        
        var harmony = new Harmony("com.vandercat.compilerPatch");

        var createCompiler = AccessTools.Method(GetEngine().GetType("Sandbox.CompileGroup"), "CreateCompiler"); // if possible use nameof() here
        var createCompilerPostfix = AccessTools.Method(typeof(CompilePatcher),"AddAllAssembliesToCompileGroup");

        Log.Info("Patching CompileGroup.CreateCompiler() to add custom assemblies");
        harmony.Patch(createCompiler, postfix: new HarmonyMethod(createCompilerPostfix));
        
        var initAssemblyList = AccessTools.Method(typeof(AccessRules), "InitAssemblyList"); // if possible use nameof() here
        var initAssemblyListPostfix =  AccessTools.Method(typeof(CompilePatcher),"AddCustomWhitelistRules");

        Log.Info("Patching AccessRules.InitAssemblyList() to add custom rules");
        harmony.Patch(initAssemblyList, postfix: new HarmonyMethod(initAssemblyListPostfix));
        
        var resolve = AccessTools.Method(typeof(AccessControl), nameof(AccessControl.Resolve), new Type[] {typeof(AssemblyNameReference)});
        //var resolveTranspiler =  AccessTools.Method(typeof(CompilePatcher), nameof(CompilePatcher.skipAssemblyNameCheck));
        var resolvePrefix =  AccessTools.Method(typeof(CompilePatcher), nameof(CompilePatcher.CustomAssemblyResolve));
        
        Log.Info("Replacing AccessControl.Resolve() to skip assembly name check");
        harmony.Patch(resolve, prefix: new HarmonyMethod(resolvePrefix));
        
        var findAssemblyOnDisk = AccessTools.Method(typeof(AccessControl), "FindAssemblyOnDisk", new Type[] {typeof(AssemblyNameReference)});
        //var resolveTranspiler = AccessTools.Method(typeof(CompilePatcher), nameof(CompilePatcher.skipAssemblyNameCheck));
        var findAssemblyOnDiskPrefix =  AccessTools.Method(typeof(CompilePatcher), nameof(CompilePatcher.FindAssemblyOnDiskPrefix));
        
        //Log.Info("Patching AccessControl.FindAssemblyOnDisk()");
        //harmony.Patch(findAssemblyOnDisk, prefix: new HarmonyMethod(findAssemblyOnDiskPrefix));
        
        var addProject = AccessTools.Method(typeof(Generator), nameof(Generator.AddProject));
        var addProjectPostfix =  AccessTools.Method(typeof(CompilePatcher), nameof(AddAssemblyReferences));
        
        Log.Info("Patching Sandbox.SolutionGenerator.Generator.AddProject() to reference our assemblies in .csproj");
        harmony.Patch(addProject, postfix: new HarmonyMethod(addProjectPostfix));
        

        Log.Info("Patching Complete");
    }

    public static void AddAllAssembliesToCompileGroup(ref object __result)
    {
        var compiler = GetEngine().GetType("Sandbox.CompileGroup");
        if (compiler is null)
        {
            Log.Error("Can't find CompileGroup class");
            return;
        }

        var method = AccessTools.Method(GetEngine().GetType("Sandbox.Compiler"), "AddAssemblyReference");
        if (method is null)
        {
            Log.Error("CompileGroup.AddAssemblyReference does not exists!");
            return;
        }

        foreach (string key in Config.Keys)
        {
            var node = Config[key];
            if (!node.IsTable) continue;
            
            var pair = node.AsTable;
            if (!pair["addToAssemblyReferences"].AsBoolean) continue;
            method.Invoke(__result, new object[] {key});
            Log.Trace($"Added {key} to Assembly References!");
        }
    }

    public static void AddCustomWhitelistRules(ref AccessRules __instance)
    {
        foreach (string key in Config.Keys)
        {
            var node = Config[key];
            if (!node.IsTable) continue;

            var whitelist = new List<string>();
            foreach (TomlNode arrayNode in node["whitelist"].AsArray)
            {
                if (!arrayNode.IsString) continue;
                whitelist.Add(arrayNode.AsString);
            }
            AccessTools.Method(typeof(AccessRules), "AddRange")
                .Invoke(__instance, new object[] {whitelist});
            Log.Trace($"Enforced {key} whitelist rules!");
            __instance.AssemblyWhitelist.Add(key);
            Log.Trace($"Enforced {key} as whitelisted assembly!");
        }
    }

    private static void PatchAssemblyLoading()
    {
        var resolveEventHandler = new ResolveEventHandler(AssemblyResolve);
        AppDomain.CurrentDomain.AssemblyResolve += resolveEventHandler;
    }
    
    private static Assembly? AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var trimList = args.Name.Split(',');
        if (trimList.Length < 2) return null;
        var trim = trimList[0];

        foreach (string key in Config.Keys)
        {
            var node = Config[key];
            if (!node.IsTable) continue;
            
            var pair = node.AsTable;
            
            var directory = (string) pair["directory"].AsString;
            if (directory is null) continue;
            
            var name = $"{Path.GetDirectoryName(Environment.ProcessPath)}\\{directory}\\{trim}.dll".Replace(".resources.dll", ".dll");;

            if (!File.Exists(name)) continue;
        
            return Assembly.LoadFrom(name);   
        }

        return null;
    }

    public static IEnumerable<CodeInstruction> skipAssemblyNameCheck(IEnumerable<CodeInstruction> instructions)
    {
        var startIndex = -1;

        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode != OpCodes.Throw) continue;
            startIndex = i;
        }

        if (startIndex <= -1) return codes.AsEnumerable();
        
        codes[startIndex - 6].opcode = OpCodes.Ldc_I4_1;

        return codes.AsEnumerable();
    }

    public static bool CustomAssemblyResolve(
        AccessControl __instance,
        AssemblyNameReference name,
        ref ConcurrentDictionary<AssemblyNameReference, Mono.Cecil.AssemblyDefinition>? ___Assemblies,
        ref AssemblyDefinition __result
        )
    {
        if (___Assemblies is not null)
        {
            AssemblyDefinition? assemblyDefinition;
            if (___Assemblies.TryGetValue(name, out assemblyDefinition))
            {
                __result = assemblyDefinition;
                return false;
            }
            var keyValuePair = (from x in ___Assemblies
                where x.Key.Name.Equals(name.Name, StringComparison.OrdinalIgnoreCase)
                orderby x.Key.Version descending
                select x).FirstOrDefault<KeyValuePair<AssemblyNameReference, AssemblyDefinition>>();
            if (keyValuePair.Value is not null)
            {
                __result = keyValuePair.Value;
                return false;
            }
        }

        var globalAssemblyCache = Traverse.Create<AccessControl>().Field("GlobalAssemblyCache")
            .GetValue<ConcurrentDictionary<AssemblyNameReference, AssemblyDefinition>>();
        if (!globalAssemblyCache.TryGetValue(name, out var assemblyDefinition2))
        {
            lock (globalAssemblyCache)
            {
                assemblyDefinition2 = globalAssemblyCache.GetOrAdd(
                    name, 
                    (key) => 
                        (AssemblyDefinition) AccessTools.Method(
                            typeof(AccessControl), 
                            "FindAssemblyOnDisk"
                            ).Invoke(__instance,new object[]{key}));
            }
        }
        __result = assemblyDefinition2;
        return false;
    }

    public static bool FindAssemblyOnDiskPrefix(
        AccessControl __instance,
        AssemblyNameReference name,
        ref AssemblyDefinition __result
    )
    {
        Log.Trace($"Trying to find Assembly {name.Name} at the {Path.GetDirectoryName(typeof(object).Assembly.Location)} or at {Path.GetDirectoryName(typeof(IAssemblyResolver).Assembly.Location)}");
        /*var text2 = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), name.Name + ".dll");

        if (!File.Exists(text2))
        {
            text2 = Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), text);
        }

        if (!File.Exists(text2))
        {
            throw (Exception) AccessTools.Method(typeof(AccessControl), "NotResolved").Invoke(__instance, new object[]{name});
        }

        byte[] array = File.ReadAllBytes(text2);

        MemoryStream memoryStream = new MemoryStream(array);
        ReaderParameters readerParameters = new ReaderParameters {
            ReadingMode = ReadingMode.Immediate,
            InMemory = true,
            AssemblyResolver = this
        };
        __result = Mono.Cecil.AssemblyDefinition.ReadAssembly(memoryStream, readerParameters);
        return true;*/
        return true;
    }

    public static void AddAssemblyReferences(
        ref ProjectInfo __result
    )
    {
        foreach (string key in Config.Keys)
        {
            var node = Config[key];
            if (!node.IsTable) continue;
            
            var pair = node.AsTable;
            if (!pair["addToAssemblyReferences"].AsBoolean) continue;
            __result.References.Add(key+".dll");
        }
    }
}