# Launcher
An somewhat example implementation of custom S&box bootstrapper. It also launches hook.dll if
found.

If built in debug it turns on trace logging (hidden in depth logging for s&box devs)
but it will not be useful for everyone.

## Warning:
**It is not a drop in replacement. It depends on original launcher (`sbox.dll`)!**

## Dependencies:
- `Sandbox.Engine.dll`
- `Sandbox.System.dll`
- `sbox.dll`