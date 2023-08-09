using System.Reflection;

namespace SboxLauncherAlt;

public static class Program
{
    private static string HookDllName { get; } = "hook";
    private static void LoadHook()
    {
       //var log = new Logger("Launcher/Hook");
        
        var hookPath = $".\\{HookDllName}.dll";
        if (!File.Exists(hookPath)) return;
        
        //log.Info($"{HookDllName}.dll found! Loading...");
        
        var hookDll = Assembly.LoadFrom(hookPath);
        var hookClass = hookDll.GetType("Hook");
        if (hookClass is null)
        {
            //log.Error("There is no such type as Hook in hook.dll!");
            return;
        }
        
        var hookMethod = hookClass.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Instance | BindingFlags.Static);
        if (hookMethod is null)
        {
            //log.Error("There is no such method as Hook.Main() in hook.dll!");
            return;
        }
        
        hookMethod.Invoke(null, null);
    }
    private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var name = $"bin\\managed\\{args.Name.Split(',')[0]}.dll";

        return File.Exists(name) ? Assembly.LoadFrom(name) : null;
    }
    
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

        LoadHook();
        
        return Sandbox.Program.Main();
    }
}