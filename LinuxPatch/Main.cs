using HarmonyLib;
using Sandbox.Diagnostics;

namespace VanderCat;
//original patch https://github.com/Kaydax/bandaid-tools-patch
public class LinuxPatch
{
    internal static Logger Log = new Logger("Linux Patch");
    
    public static void DoPatching()
    {
        var harmony = new Harmony("com.vandercat.linux");
        var buildJumpList =
            AccessTools.Method(typeof(Sandbox.Camera).Assembly.GetType("Sandbox.JumpListManager"), "BuildJumpList");
        var buildJumpListPrefix = AccessTools.Method(typeof(LinuxPatch), "JumpListPatch");
        harmony.Patch(buildJumpList, prefix: new HarmonyMethod(buildJumpListPrefix));
    }
    
    private static bool JumpListPatch()
    { 
        Log.Info("Since jump list is borked under proton, don't build it"); 
        return false;
    }
}