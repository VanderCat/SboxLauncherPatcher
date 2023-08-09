using System.Reflection;
using Sandbox.Diagnostics;

public static class Hook
{
    private static Logger Log = new Logger("Hook");
    public static void Main()
    {
        Log.Info("Hook is active!");
        foreach (var fileInfo in Directory.CreateDirectory("assemblyPatches").EnumerateFiles())
        {
            if (fileInfo.Extension != ".dll")
            {
                Log.Trace($"{fileInfo.Name} is not a dll. Skipping..");
                continue;
            }
            var assembly = Assembly.LoadFrom(fileInfo.FullName);
            var names = (from type in assembly.GetTypes()
                from method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static)
                where method.Name == "DoPatching"
                select method ).Distinct().ToList();
            if (names.Count < 1)
            {
                Log.Trace($"There is no patches in {fileInfo.Name}. Skipping..");
                continue;
            }
            foreach (var methodInfo in names)
            {
                Log.Warning($"Applying patch {methodInfo.DeclaringType.Name}");
                try
                {
                    methodInfo.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Log.Error($"Could not load patch {methodInfo.DeclaringType.Name} in {fileInfo.Name}!\n{e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
}