<center>
    <h1>S&Box Custom Launcher</h1>
    <b>also featuring Runtime patch loader</b>
</center>

Why custom launcher? That way you are able to do anything before source 2 even starts.
You can patch **anything**!

Launcher will search for hook.dll and if found will try to execute
`-.Hook.Main()` (none namespace, class Hook, function Main)

Hook will search for `*.*.DoPatching()` in any `.dll` located in `./assemblyPatches` and
execute all of them.

This project consists of 4 parts:
- [Launcher](/SboxLauncher/README.MD)
- [Hook](/SboxHook/README.MD)
- [Compiler Patch](/PatchCompiler/README.MD)
- [Linux Fix](/LinuxPatch/README.MD)