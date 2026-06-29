$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
dotnet build 'Preagonal.Scripting.GS2Compiler.Cli\Preagonal.Scripting.GS2Compiler.Cli.csproj' -v:minimal | Out-Host
if (!(Test-Path -LiteralPath '.parity-scan')) { New-Item -ItemType Directory -Path '.parity-scan' | Out-Null }
$scratch = (Resolve-Path -LiteralPath '.parity-scan').Path
$sha = [Security.Cryptography.SHA256]::Create()
$rows = @()
Get-ChildItem -LiteralPath 'tests\scripts\advanced' -Filter '*.gs2' -Recurse | ForEach-Object {
	$script = $_.FullName
	$rel = Resolve-Path -LiteralPath $script -Relative
	$baseline = Join-Path $repo (Join-Path 'tests\baselines' (($rel -replace '^\.\\tests\\scripts\\', '') -replace '\.gs2$', '.json'))
	if (Test-Path -LiteralPath $baseline) {
		$out = Join-Path $scratch ([guid]::NewGuid().ToString() + '.bc')
		& dotnet run --no-build --project 'Preagonal.Scripting.GS2Compiler.Cli' -- $script -o $out | Out-Null
		$bytes = [IO.File]::ReadAllBytes($out)
		$hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
		$json = Get-Content -LiteralPath $baseline -Raw | ConvertFrom-Json
		$rows += [pscustomobject]@{
			Rel = $rel
			Size = $bytes.Length
			Base = [int]$json.bytecode_size
			Delta = $bytes.Length - [int]$json.bytecode_size
			HashMatch = $hash -eq $json.bytecode_hash
			SizeMatch = $bytes.Length -eq [int]$json.bytecode_size
		}
	}
}
$rows | Where-Object { -not $_.HashMatch } | Sort-Object @{Expression = { [Math]::Abs($_.Delta) } }, Rel | Select-Object -First 14 | Format-Table -AutoSize
[pscustomobject]@{Total = $rows.Count; Hash = ($rows | Where-Object HashMatch).Count; Size = ($rows | Where-Object SizeMatch).Count}
