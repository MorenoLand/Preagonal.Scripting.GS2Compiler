# GS2 parity progress

Current advanced fixture score: 52 / 155 byte-for-byte hashes, 53 / 155 byte sizes.

Locked advanced parity fixtures:
- `tests/scripts/advanced/g2k1/weaponRailroad Destroyer.gs2`
- `tests/scripts/advanced/g2k1/weaponShovel.gs2`
- `tests/scripts/advanced/g2k1/weaponMemeCentral.gs2`
- `tests/scripts/advanced/g2k1/weaponCatch Net.gs2`
- `tests/scripts/advanced/g2k1/weaponMining Bomb.gs2`
- `tests/scripts/advanced/g2k1/weaponBasket.gs2`
- `tests/scripts/advanced/g2k1/weaponCandies.gs2`
- `tests/scripts/advanced/graalx/weaponItems_Chemicals.gs2`
- `tests/scripts/advanced/graalx/weaponKinetaro.gs2`
- `tests/scripts/advanced/graalx/weapon-BuySys.gs2`
- `tests/scripts/advanced/graalx/weapon-NBuySys.gs2`
- `tests/scripts/advanced/g2k1/weaponMetal Axe.gs2`
- `tests/scripts/advanced/graalx/weaponVulcan.gs2`
- `tests/scripts/advanced/graalx/weapon-Gravity.gs2`
- `tests/scripts/advanced/graalx/weaponSystems_B2.gs2`
- `tests/scripts/advanced/graalx/weaponRWA_Plane.gs2`
- `tests/scripts/advanced/graalx/weaponRC_AttributesWindow.gs2`
- `tests/scripts/advanced/graalx/weaponSystems_TradeMenu.gs2`
- `tests/scripts/advanced/graalx/weaponSystems_IRC.gs2`
- `tests/scripts/advanced/graalx/weaponWeapons_Grenade.gs2`
- `tests/scripts/advanced/loginserver/weapon-Rescripted_IRC_Login3.gs2`

Verification:
- `dotnet test Preagonal.Scripting.GS2Compiler.sln`
- `dotnet publish Preagonal.Scripting.GS2Compiler.Cli/Preagonal.Scripting.GS2Compiler.Cli.csproj -c Release -r win-x64 --self-contained true`
- `dotnet publish Preagonal.Scripting.GS2Compiler.Native/Preagonal.Scripting.GS2Compiler.Native.csproj -c Release -r win-x64`
