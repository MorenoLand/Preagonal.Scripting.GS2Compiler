using Antlr4.Runtime;
using Xunit;

namespace Preagonal.Scripting.GS2Compiler.UnitTests;

public class AntlrGrammarTests
{
	public static IEnumerable<object[]> ParserFixtures()
	{
		var root = BaselineTests.FindRepoRoot();
		yield return [Path.Combine(root, "tests", "scripts", "statements", "03_switch.gs2")];
		yield return [Path.Combine(root, "tests", "scripts", "advanced", "loginserver", "weapon-Rescripted_IRC_Login3.gs2")];
		foreach (var fixture in BaselineTests.AdvancedParityScripts()) yield return fixture;
	}

	[Theory]
	[MemberData(nameof(ParserFixtures))]
	public void Given_fixture_When_parsed_with_generated_antlr_parser_Then_no_syntax_errors(string scriptPath)
	{
		AssertParses(File.ReadAllText(scriptPath));
	}

	[Fact]
	public void Given_cast_script_When_parsed_with_generated_antlr_parser_Then_no_syntax_errors()
	{
		AssertParses("function onCreated() { temp.i = int(\"3.8\"); temp.f = float(\"2.5\"); temp.t = _(\"Hello\"); temp.mask = ~temp.flags; temp.flags <<= 2; temp.flags >>= 1; temp.value ^= 3; }");
	}

	private static void AssertParses(string code)
	{
		var input = CharStreams.fromString(code);
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
