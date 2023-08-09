using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NativeEngine;
using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Engine;
namespace VanderCat.Launcher;

public static class Program
{
    private static string? GamePath { get; set; }

    private static string? ManagedDllPath { get; set; }

    private static string? NativeDllPath { get; set; }

    [STAThread]
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);;
        
        GamePath = Path.GetDirectoryName(Environment.ProcessPath);
        ManagedDllPath = GamePath + "\\bin\\managed\\";
        NativeDllPath = GamePath + "\\bin\\win64\\";
        var path = Environment.GetEnvironmentVariable("PATH");
        path = NativeDllPath + ";" + path;
        Environment.SetEnvironmentVariable("PATH", path);

        LoadHook();

        return LaunchGame();
    }
    
    public static void EnableTraceLogging()
    {
        typeof(Logger).Assembly.GetType("Sandbox.Diagnostics.Logging")
            .GetMethod("SetRule", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] {"*", LogLevel.Trace});
    }

    private static void LoadHook()
    {
        var log = new Logger("Launcher/Hook");
        
        var hookPath = $"{GamePath}\\{HookDllName}.dll";
        if (!File.Exists(hookPath)) return;
        
        log.Info($"{HookDllName}.dll found! Loading...");
        
        var hookDll = Assembly.LoadFrom(hookPath);
        var hookClass = hookDll.GetType("Hook");
        if (hookClass is null)
        {
            log.Error("There is no such type as Hook in hook.dll!");
            return;
        }
        
        var hookMethod = hookClass.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Instance | BindingFlags.Static);
        if (hookMethod is null)
        {
            log.Error("There is no such method as Hook.Main() in hook.dll!");
            return;
        }
        
        hookMethod.Invoke(null, null);
    }
    
    private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var currentDirectory = Environment.CurrentDirectory;
        var trim = args.Name.Split(',')[0];
        var name = $"{ManagedDllPath}\\{trim}.dll"
            .Replace(".resources.dll", ".dll");

        return File.Exists(name) ? Assembly.LoadFrom(name) : null;
    }
    
    private static int LaunchGame()
    {
#if DEBUG
        EnableTraceLogging();
#endif
        var log = new Logger("Launcher");
        log.Info("Starting the game!");
        
        var engineAssembly = typeof(CookieContainer).Assembly;
        
        var netCore = engineAssembly.GetType("NetCore");

        if (netCore is null)
        {
            log.Error($"Can't find NetCore in {engineAssembly.GetName().Name}.dll!");
            return -1;
        }
        
        netCore
            .GetMethod("InitializeInterop", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] {GamePath});
        
        var engineGlobal = engineAssembly.GetType("NativeEngine.EngineGlobal");
        
        if (engineGlobal is null)
        {
            log.Error($"Can't find NativeEngine.EngineGlobal in {engineAssembly.GetName().Name}.dll!");
            return -1;
        }
        
        engineGlobal
            .GetMethod("Plat_SetModuleFilename", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] {GamePath + "\\sbox.exe"});
        
        var sourceEngineApp = new SourceEngineApp(GamePath);
        sourceEngineApp.Initialize();
        sourceEngineApp.RunLoop();
        sourceEngineApp.Shutdown();
        return 0;
    }
}