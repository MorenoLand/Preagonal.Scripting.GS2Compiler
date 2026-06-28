using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Preagonal.Scripting.GS2Compiler.UnitTests;

public class BaselineTests
{
	public static IEnumerable<object[]> BasicScripts()
	{
		var root = FindRepoRoot();
		foreach (var category in new[] { "basic", "expressions", "functions" })
		foreach (var script in Directory.EnumerateFiles(Path.Combine(root, "tests", "scripts", category), "*.gs2", SearchOption.TopDirectoryOnly))
			yield return [script];
		yield return [Path.Combine(root, "tests", "scripts", "statements", "01_conditionals.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "statements", "02_loops.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "statements", "03_switch.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "statements", "04_with.gs2")];
	}

	[Theory]
	[MemberData(nameof(BasicScripts))]
	public void Given_basic_fixture_When_compiling_Then_bytecode_matches_baseline(string scriptPath)
	{
		var root = FindRepoRoot();
		var relative = Path.GetRelativePath(Path.Combine(root, "tests", "scripts"), scriptPath);
		var baselinePath = Path.ChangeExtension(Path.Combine(root, "tests", "baselines", relative), ".json");
		var baseline = JsonSerializer.Deserialize<BaselineData>(File.ReadAllText(baselinePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

		var result = Interface.CompileCode(File.ReadAllText(scriptPath), withHeader: false);
		var hash = Convert.ToHexString(SHA256.HashData(result.ByteCode)).ToLowerInvariant();

		Assert.Equal(baseline.CompilationSuccess, result.Success);
		Assert.Equal(baseline.BytecodeSize, result.ByteCode.Length);
		Assert.Equal(baseline.BytecodeHash, hash);
	}

	private static string FindRepoRoot()
	{
		var dir = AppContext.BaseDirectory;
		while (dir != null && !File.Exists(Path.Combine(dir, "Preagonal.Scripting.GS2Compiler.sln")))
			dir = Directory.GetParent(dir)?.FullName;
		return dir ?? throw new DirectoryNotFoundException("Could not find repository root.");
	}

	private sealed class BaselineData
	{
		[JsonPropertyName("bytecode_hash")]
		public string BytecodeHash { get; set; } = "";
		[JsonPropertyName("bytecode_size")]
		public int BytecodeSize { get; set; }
		[JsonPropertyName("compilation_success")]
		public bool CompilationSuccess { get; set; }
	}
}
