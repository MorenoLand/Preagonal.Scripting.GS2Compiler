# Building GS2Compiler

## Tests

```powershell
dotnet test '.\Preagonal.Scripting.GS2Compiler.UnitTests\Preagonal.Scripting.GS2Compiler.UnitTests.csproj'
```

## Windows CLI

```powershell
dotnet publish '.\Preagonal.Scripting.GS2Compiler.Cli\Preagonal.Scripting.GS2Compiler.Cli.csproj' -c Release -r win-x64 --self-contained false -o '.\artifacts\publish\cli-win-x64'
```

## Windows Native AOT DLL

```powershell
dotnet publish '.\Preagonal.Scripting.GS2Compiler.Native\Preagonal.Scripting.GS2Compiler.Native.csproj' -c Release -r win-x64 -o '.\artifacts\publish\native-win-x64'
```

## Browser WASM

```powershell
dotnet workload install wasm-tools wasm-tools-net8 wasm-tools-net9
dotnet publish '.\Preagonal.Scripting.GS2Compiler.Cli\Preagonal.Scripting.GS2Compiler.Cli.csproj' -c Release -r browser-wasm -o '.\artifacts\publish\cli-browser-wasm'
```

## WASI

```powershell
dotnet workload install wasi-experimental
dotnet publish '.\Preagonal.Scripting.GS2Compiler.Cli\Preagonal.Scripting.GS2Compiler.Cli.csproj' -c Release -r wasi-wasm -o '.\artifacts\publish\cli-wasi-wasm' /p:WASI_SDK_PATH='.\artifacts\tools\wasi-sdk-25.0-x86_64-windows\'
```

The .NET 10 WASI SDK target requires wasi-sdk `25.0`; the path must include the trailing slash.
