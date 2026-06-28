using Antlr4.Runtime;
using Xunit;

namespace Preagonal.Scripting.GS2Compiler.UnitTests;

public class AntlrGrammarTests
{
	public static IEnumerable<object[]> ParserFixtures()
	{
		var root = BaselineTests.FindRepoRoot();
		yield return [Path.Combine(root, "tests", "scripts", "statements", "03_switch.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "advanced", "graalx", "weaponSystems_B2.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "advanced", "loginserver", "weapon-Rescripted_IRC_Login3.gs2")];
	}

	[Theory]
	[MemberData(nameof(ParserFixtures))]
	public void Given_fixture_When_parsed_with_generated_antlr_parser_Then_no_syntax_errors(string scriptPath)
	{
		var input = CharStreams.fromString(File.ReadAllText(scriptPath));
		var lexer = new GS2Lexer(input);
		var tokens = new CommonTokenStream(lexer);
		var parser = new GS2Parser(tokens);
		lexer.RemoveErrorListeners();
		parser.RemoveErrorListeners();
		var listener = new ThrowingErrorListener();
		lexer.AddErrorListener(listener);
		parser.AddErrorListener(listener);
		parser.script();
		Assert.Equal(0, parser.NumberOfSyntaxErrors);
	}

	private sealed class ThrowingErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
	{
		public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) => throw new InvalidOperationException($"{line}:{charPositionInLine} {msg}");
		public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e) => throw new InvalidOperationException($"{line}:{charPositionInLine} {msg}");
	}
}
