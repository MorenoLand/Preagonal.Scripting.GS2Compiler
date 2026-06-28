namespace Preagonal.Scripting.GS2Compiler;

public static class Interface
{
	public static CompilerResponse CompileCode(string? code, string? type = "weapon", string? name = "npc", bool withHeader = true) =>
		GS2Compiler.Compile(code ?? string.Empty, type ?? "weapon", name ?? "npc", withHeader);
}
