# GS2Compiler Correctness Tracking

## 2026-06-28

- Test suite: 181/181 xUnit tests passing.
- Fixture bytecode parity: 26 tests passing.
- Advanced corpus compile coverage: 155/155 scripts compile through xUnit.
- Advanced bytecode parity: not counted yet because the copied corpus has no advanced `.bytecode` baselines.
- Published CLI win-x64 exe: verified against `statements/03_switch.gs2`, byte length 425, baseline SHA-256 match.
- Native AOT win-x64 publish: verified.
- Native AOT linux-x64 publish from Windows: blocked by .NET cross-OS native compilation limits.
