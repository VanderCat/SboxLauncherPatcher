# Compiler Patcher
The main reason why this project even started. 

It patches compiler and whitelist to add reference to any assembly you want and
later on load it.

## Usage
On a first run it will generate config next to itself containing a commented out
example of a config. You can add as much assemblies as you want but keep in mind the compiler
always add them to anything it compiles.

## NLua example:
```toml
[NLua]
addToAssemblyReferences = true
directory = "luamod" 
whitelist = [
    "NLua/*"
]
```

## Dependencies
- `0Harmony.dll` - does not come preinstalled with s&box
- `Mono.Cecil`
- `Sandbox.Access.dll`
- `Sandbox.Engine.dll`
- `Sandbox.System.dll`