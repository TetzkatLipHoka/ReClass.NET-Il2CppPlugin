# ReClass.NET Il2Cpp Plugin

Annotates pointers to Unity IL2CPP objects in [ReClass.NET](https://github.com/TetzkatLipHoka/ReClass.NET)
with their class name (`Namespace.Outer.Name`) and shows the value of `System.String` objects.

It needs no dump or metadata: an `Il2CppObject` starts with its `Il2CppClass*`, and the class carries
name, namespace, declaring type and parent pointers at fixed offsets (measured on metadata v29 / v31 / v39,
x64: name 0x10, namespace 0x18, declaringType 0x50, parent 0x58). A pointer is only reported when the
whole parent chain looks like classes, so plain data is not mislabeled.

Built for the PS4 / PS5 workflow of [Il2CppDumper ps4tools](https://github.com/TetzkatLipHoka/Il2CppDumper),
works for any x64 IL2CPP process ReClass.NET can read.

## Build

Expects the ReClass.NET fork as sibling checkout `..\ReClass.NET` (project reference), .NET 10 SDK.

```
dotnet build Il2CppPlugin.csproj -c Release -p:Platform=x64
```

Copy `bin\x64\Release\Il2CppPlugin.dll` into the `Plugins` folder of ReClass.NET.
