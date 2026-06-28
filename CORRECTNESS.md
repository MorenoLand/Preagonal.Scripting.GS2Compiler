# GS2Compiler Correctness Tracking

## 2026-06-28

- Test suite: 206/206 xUnit tests passing.
- Fixture bytecode parity: 32 tests passing.
- Advanced corpus compile coverage: 155/155 scripts compile through xUnit.
- Advanced bytecode parity: 39/155 SHA-256 matches and 41/155 byte-size matches against copied C++ advanced baselines.
- Published CLI win-x64 exe: verified against `statements/03_switch.gs2`, byte length 425, baseline SHA-256 match.
- Native AOT win-x64 publish: verified.
- Native AOT win-x64 export names present in published DLL: `get_context`, `compile_code`, `compile_code_no_header`, `delete_context`.
- Native AOT linux-x64 publish from Windows: blocked by .NET cross-OS native compilation limits.
- Browser WASM publish: verified with `wasm-tools`, `wasm-tools-net8`, and `wasm-tools-net9` workloads installed.
- WASI publish: verified with `wasi-experimental` and repo-local `wasi-sdk-25.0-x86_64-windows`.
- WASI runtime smoke: Wasmtime `v46.0.1` starts the component but traps inside Mono before reaching CLI `Main`.
