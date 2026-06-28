using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Preagonal.Scripting.GS2Compiler;

internal static class GS2Compiler
{
	public static CompilerResponse Compile(string code, string type, string name, bool withHeader)
	{
		var parser = new Parser(code);
		var program = parser.Parse();
		if (parser.ErrorMessage != null) return new() { Success = false, ErrMsg = parser.ErrorMessage, ByteCode = [] };
		var bytecode = new BytecodeWriter();
		var emitter = new Emitter(bytecode, program.Constants, program.Enums);
		foreach (var function in program.Functions) emitter.EmitFunction(function);
		var result = bytecode.ToArray();
		return new() { Success = true, ErrMsg = null, ByteCode = withHeader ? Header.Wrap(result, type, name) : result };
	}

	private sealed class Parser
	{
		private readonly Lexer _lexer;
		private Token _current;
		private Token _previous = new(TokenType.End, "", 1, 1);
		private readonly Dictionary<string, Expr> _constants = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Dictionary<string, int>> _enums = new(StringComparer.Ordinal);
		private int _lambdaId = 100;
		public string? ErrorMessage { get; private set; }

		public Parser(string code)
		{
			_lexer = new(code);
			_current = _lexer.Next();
		}

		public ProgramNode Parse()
		{
			List<FunctionNode> functions = [];
			while (_current.Type != TokenType.End)
			{
				if (Match(TokenType.Const)) ParseConst();
				else if (Match(TokenType.Enum)) ParseEnum();
				else if (Match(TokenType.Function)) functions.Add(ParseFunction(false, null));
				else Advance();
			}
			return new(_constants, _enums, functions);
		}

		private void ParseConst()
		{
			var name = Expect(TokenType.Identifier).Text;
			Expect(TokenType.Assign);
			_constants[name] = ParseExpression();
			Expect(TokenType.Semicolon);
		}

		private void ParseEnum()
		{
			var enumName = Match(TokenType.Identifier) ? _previous.Text : "";
			Expect(TokenType.LeftBrace);
			Dictionary<string, int> values = new(StringComparer.Ordinal);
			var index = 0;
			while (_current.Type != TokenType.RightBrace && _current.Type != TokenType.End)
			{
				var name = Expect(TokenType.Identifier).Text;
				if (Match(TokenType.Assign)) index = ParseSignedInt();
				values[name] = index++;
				Match(TokenType.Comma);
			}
			Expect(TokenType.RightBrace);
			Match(TokenType.Semicolon);
			if (enumName.Length > 0) _enums[enumName] = values;
		}

		private FunctionNode ParseFunction(bool isPublic, string? objectName)
		{
			var name = Expect(TokenType.Identifier).Text;
			if (Match(TokenType.Dot))
			{
				objectName = name;
				name = Expect(TokenType.Identifier).Text;
			}
			Expect(TokenType.LeftParen);
			List<string> args = [];
			if (_current.Type != TokenType.RightParen)
			{
				do args.Add(Expect(TokenType.Identifier).Text); while (Match(TokenType.Comma));
			}
			Expect(TokenType.RightParen);
			var body = _current.Type switch
			{
				TokenType.LeftBrace => ParseBlock(),
				TokenType.Semicolon => EmptyBody(),
				TokenType.Function or TokenType.End => [],
				_ => [ParseStatement()]
			};
			return new(name, objectName, isPublic, args, body);
		}

		private List<Stmt> EmptyBody()
		{
			Match(TokenType.Semicolon);
			return [];
		}

		private List<Stmt> ParseBlock()
		{
			List<Stmt> statements = [];
			Expect(TokenType.LeftBrace);
			while (_current.Type != TokenType.RightBrace && _current.Type != TokenType.End)
				statements.Add(ParseStatement());
			Expect(TokenType.RightBrace);
			return statements;
		}

		private Stmt ParseStatement()
		{
			if (Match(TokenType.Return))
			{
				var expr = _current.Type == TokenType.Semicolon ? new NumberExpr("0") : ParseExpression();
				Expect(TokenType.Semicolon);
				return new ReturnStmt(expr);
			}
			if (Match(TokenType.Break))
			{
				Expect(TokenType.Semicolon);
				return new BreakStmt();
			}
			if (Match(TokenType.If))
			{
				Expect(TokenType.LeftParen);
				var condition = ParseExpression();
				Expect(TokenType.RightParen);
				var thenBody = _current.Type == TokenType.LeftBrace ? ParseBlock() : [ParseStatement()];
				var elseBody = Match(TokenType.Else) ? (_current.Type == TokenType.LeftBrace ? ParseBlock() : [ParseStatement()]) : [];
				return new IfStmt(condition, thenBody, elseBody);
			}
			if (Match(TokenType.For)) return ParseForStatement();
			if (Match(TokenType.With))
			{
				Expect(TokenType.LeftParen);
				var target = ParseExpression();
				Expect(TokenType.RightParen);
				return new WithStmt(target, _current.Type == TokenType.LeftBrace ? ParseBlock() : [ParseStatement()]);
			}
			if (Match(TokenType.Switch)) return ParseSwitchStatement();
			var before = _current;
			var exprStatement = new ExprStmt(ParseExpression());
			Expect(TokenType.Semicolon);
			if (before == _current) Advance();
			return exprStatement;
		}

		private Stmt ParseForStatement()
		{
			Expect(TokenType.LeftParen);
			var first = ParseExpression();
			if (Match(TokenType.Colon))
			{
				var source = ParseExpression();
				Expect(TokenType.RightParen);
				return new ForEachStmt(first, source, _current.Type == TokenType.LeftBrace ? ParseBlock() : [ParseStatement()]);
			}
			Expect(TokenType.Semicolon);
			var condition = ParseExpression();
			Expect(TokenType.Semicolon);
			var post = ParseExpression();
			Expect(TokenType.RightParen);
			return new ForStmt(first, condition, post, _current.Type == TokenType.LeftBrace ? ParseBlock() : [ParseStatement()]);
		}

		private Stmt ParseSwitchStatement()
		{
			Expect(TokenType.LeftParen);
			var expr = ParseExpression();
			Expect(TokenType.RightParen);
			Expect(TokenType.LeftBrace);
			List<SwitchCase> cases = [];
			while (_current.Type != TokenType.RightBrace && _current.Type != TokenType.End)
			{
				List<Expr?> labels = [];
				while (_current.Type is TokenType.Case or TokenType.Default)
				{
					if (Match(TokenType.Case)) labels.Add(ParseExpression());
					else { Match(TokenType.Default); labels.Add(null); }
					Expect(TokenType.Colon);
				}
				List<Stmt> statements = [];
				while (_current.Type is not TokenType.Case and not TokenType.Default and not TokenType.RightBrace and not TokenType.End)
					statements.Add(ParseStatement());
				if (labels.Count > 1) labels.Reverse();
				cases.Add(new(labels, statements));
			}
			Expect(TokenType.RightBrace);
			return new SwitchStmt(expr, cases);
		}

		private Expr ParseExpression() => ParseAssignment();

		private Expr ParseAssignment()
		{
			var left = ParseConditional();
			var op = _current.Type switch
			{
				TokenType.Assign => "=",
				TokenType.AddAssign => "+=",
				TokenType.SubAssign => "-=",
				TokenType.MulAssign => "*=",
				TokenType.DivAssign => "/=",
				TokenType.ModAssign => "%=",
				TokenType.CatAssign => "@=",
				_ => ""
			};
			if (op.Length == 0) return left;
			Advance();
			return new BinaryExpr(left, op, ParseAssignment());
		}

		private Expr ParseConditional()
		{
			var condition = ParseLogicalOr();
			if (!Match(TokenType.Question)) return condition;
			var whenTrue = ParseExpression();
			Expect(TokenType.Colon);
			return new TernaryExpr(condition, whenTrue, ParseExpression());
		}

		private Expr ParseLogicalOr()
		{
			var expr = ParseLogicalAnd();
			while (Match(TokenType.Or)) expr = new BinaryExpr(expr, "||", ParseLogicalAnd());
			return expr;
		}

		private Expr ParseLogicalAnd()
		{
			var expr = ParseBitwise();
			while (Match(TokenType.And)) expr = new BinaryExpr(expr, "&&", ParseBitwise());
			return expr;
		}

		private Expr ParseBitwise()
		{
			var expr = ParseEquality();
			while (_current.Type is TokenType.BitAnd or TokenType.BitOr or TokenType.ShiftLeft or TokenType.ShiftRight)
			{
				var op = _current.Text;
				Advance();
				expr = new BinaryExpr(expr, op, ParseEquality());
			}
			return expr;
		}

		private Expr ParseEquality()
		{
			var expr = ParseComparison();
			while (_current.Type is TokenType.Equal or TokenType.NotEqual)
			{
				var op = _current.Text;
				Advance();
				expr = new BinaryExpr(expr, op, ParseComparison());
			}
			return expr;
		}

		private Expr ParseComparison()
		{
			var expr = ParseTerm();
			while (_current.Type is TokenType.Less or TokenType.LessEqual or TokenType.Greater or TokenType.GreaterEqual)
			{
				var op = _current.Text;
				Advance();
				expr = new BinaryExpr(expr, op, ParseTerm());
			}
			return expr;
		}

		private Expr ParseTerm()
		{
			var expr = ParseFactor();
			while (_current.Type is TokenType.Plus or TokenType.Minus or TokenType.At)
			{
				var op = _current.Text;
				Advance();
				expr = new BinaryExpr(expr, op, ParseFactor());
			}
			return expr;
		}

		private Expr ParseFactor()
		{
			var expr = ParseUnary();
			while (_current.Type is TokenType.Star or TokenType.Slash or TokenType.Percent or TokenType.Caret)
			{
				var op = _current.Text;
				Advance();
				expr = new BinaryExpr(expr, op, ParseUnary());
			}
			return expr;
		}

		private Expr ParsePostfix()
		{
			Expr expr = ParsePrimary();
			while (true)
			{
				if (Match(TokenType.Dot)) { expr = new MemberExpr(expr, Expect(TokenType.Identifier).Text); continue; }
				if (Match(TokenType.LeftBracket))
				{
					var index = ParseExpression();
					Expect(TokenType.RightBracket);
					expr = new ArrayIndexExpr(expr, index);
					continue;
				}
				if (Match(TokenType.LeftParen))
				{
					List<Expr> args = [];
					if (_current.Type != TokenType.RightParen)
					{
						do args.Add(ParseExpression()); while (Match(TokenType.Comma));
					}
					Expect(TokenType.RightParen);
					expr = expr switch
					{
						IdentifierExpr id => new CallExpr(id.Name, args),
						MemberExpr member => new MethodCallExpr(member.Object, member.Name, args),
						_ => expr
					};
					continue;
				}
				break;
			}
			if (Match(TokenType.Increment)) expr = new UnaryExpr("++", expr);
			else if (Match(TokenType.Decrement)) expr = new UnaryExpr("--", expr);
			return expr;
		}

		private Expr ParseUnary()
		{
			if (Match(TokenType.Minus)) return new UnaryExpr("-", ParseUnary());
			if (Match(TokenType.Not)) return new UnaryExpr("!", ParseUnary());
			return ParsePostfix();
		}

		private Expr ParsePrimary()
		{
			if (Match(TokenType.Number)) return new NumberExpr(_previous.Text);
			if (Match(TokenType.String)) return new StringExpr(_previous.Text);
			if (Match(TokenType.True)) return new BoolExpr(true);
			if (Match(TokenType.False)) return new BoolExpr(false);
			if (Match(TokenType.Null)) return new NullExpr();
			if (Match(TokenType.New))
			{
				var typeName = Expect(TokenType.Identifier).Text;
				Expect(TokenType.LeftParen);
				List<Expr> args = [];
				if (_current.Type != TokenType.RightParen)
					do args.Add(ParseExpression()); while (Match(TokenType.Comma));
				Expect(TokenType.RightParen);
				return new NewObjectExpr(typeName, args);
			}
			if (Match(TokenType.Function))
			{
				Expect(TokenType.LeftParen);
				List<string> args = [];
				if (_current.Type != TokenType.RightParen)
				{
					do args.Add(Expect(TokenType.Identifier).Text); while (Match(TokenType.Comma));
				}
				Expect(TokenType.RightParen);
				return new LambdaExpr($"function_{_lambdaId++}_1", args, ParseBlock());
			}
			if (Match(TokenType.LeftBrace))
			{
				List<Expr> values = [];
				if (_current.Type != TokenType.RightBrace)
					do values.Add(ParseExpression()); while (Match(TokenType.Comma));
				Expect(TokenType.RightBrace);
				return new ArrayLiteralExpr(values);
			}
			if (Match(TokenType.LeftParen))
			{
				var expr = ParseExpression();
				Expect(TokenType.RightParen);
				return expr;
			}
			if (Match(TokenType.Identifier))
			{
				var first = _previous.Text;
				if (Match(TokenType.Scope)) return new EnumExpr(first, Expect(TokenType.Identifier).Text);
				return new IdentifierExpr(first);
			}
			Error("malformed input");
			return new NumberExpr("0");
		}

		private int ParseSignedInt()
		{
			var sign = Match(TokenType.Minus) ? -1 : 1;
			var token = Expect(TokenType.Number);
			return sign * int.Parse(token.Text, CultureInfo.InvariantCulture);
		}

		private bool Match(TokenType type)
		{
			if (_current.Type != type) return false;
			Advance();
			return true;
		}

		private Token Expect(TokenType type)
		{
			if (_current.Type == type)
			{
				var token = _current;
				Advance();
				return token;
			}
			Error("malformed input");
			return new(type, "", _current.Line, _current.Column);
		}

		private void Advance()
		{
			_previous = _current;
			_current = _lexer.Next();
		}

		private void Error(string message)
		{
			if (ErrorMessage != null) return;
			ErrorMessage = $"{message} at line {_current.Line}: {_current.LineText}\n";
		}
	}

	private sealed class Emitter
	{
		private readonly BytecodeWriter _bytecode;
		private readonly Dictionary<string, Expr> _constants;
		private readonly Dictionary<string, Dictionary<string, int>> _enums;
		private readonly Stack<List<int>> _breakPatches = new();

		public Emitter(BytecodeWriter bytecode, Dictionary<string, Expr> constants, Dictionary<string, Dictionary<string, int>> enums)
		{
			_bytecode = bytecode;
			_constants = constants;
			_enums = enums;
		}

		public void EmitFunction(FunctionNode node, bool patchToFinal = true)
		{
			_bytecode.Emit(Op.SetIndex);
			var jmpLoc = _bytecode.EmitNumberOperandPlaceholder();
			var functionName = node.Public ? "public." : "";
			if (!string.IsNullOrEmpty(node.ObjectName)) functionName += node.ObjectName + ".";
			functionName += node.Name;
			_bytecode.AddFunction(functionName, _bytecode.OpIndex, 0);
			_bytecode.Emit(Op.TypeArray);
			for (var i = node.Args.Count - 1; i >= 0; --i) Emit(new IdentifierExpr(node.Args[i]));
			_bytecode.Emit(Op.FuncParamsEnd);
			_bytecode.Emit(Op.Jmp);
			if (node.Body.Exists(ContainsCall)) _bytecode.Emit(Op.CmdCall);
			foreach (var statement in node.Body) EmitStatement(statement);
			if (node.Body.Count == 0 || node.Body[^1] is not ReturnStmt)
			{
				_bytecode.Emit(Op.TypeNumber);
				_bytecode.EmitDynamicNumber(0);
				_bytecode.Emit(Op.Ret);
			}
			if (patchToFinal) _bytecode.AddPrejumpPatch(jmpLoc);
			else _bytecode.PatchShort(jmpLoc, _bytecode.OpIndex);
		}

		private void Emit(Expr expr) => Emit(expr, false, true, 0);

		private void EmitStatement(Stmt statement)
		{
			if (statement is ExprStmt exprStatement)
			{
				Emit(exprStatement.Expression);
				if (exprStatement.Expression is CallExpr or MethodCallExpr) _bytecode.Emit(Op.IndexDec);
			}
			else if (statement is ReturnStmt returnStatement)
			{
				Emit(returnStatement.Expression);
				_bytecode.Emit(Op.Ret);
			}
			else if (statement is IfStmt ifStatement) EmitIf(ifStatement);
			else if (statement is ForStmt forStatement) EmitFor(forStatement);
			else if (statement is ForEachStmt forEachStatement) EmitForEach(forEachStatement);
			else if (statement is WithStmt withStatement) EmitWith(withStatement);
			else if (statement is SwitchStmt switchStatement) EmitSwitch(switchStatement);
			else if (statement is BreakStmt) EmitBreak();
		}

		private void EmitIf(IfStmt statement)
		{
			Emit(statement.Condition, false, false, 0);
			if (!IsBooleanExpr(statement.Condition)) _bytecode.Emit(Op.ConvToFloat);
			_bytecode.Emit(Op.If);
			var failLoc = _bytecode.EmitNumberOperandPlaceholder();
			foreach (var stmt in statement.ThenBody) EmitStatement(stmt);
			_bytecode.PatchShort(failLoc, _bytecode.OpIndex + (statement.ElseBody.Count > 0 ? 1 : 0));
			if (statement.ElseBody.Count > 0)
			{
				_bytecode.Emit(Op.SetIndex);
				var exitLoc = _bytecode.EmitNumberOperandPlaceholder();
				foreach (var stmt in statement.ElseBody) EmitStatement(stmt);
				_bytecode.PatchShort(exitLoc, _bytecode.OpIndex);
			}
		}

		private void EmitFor(ForStmt statement)
		{
			Emit(statement.Init);
			var start = _bytecode.OpIndex;
			Emit(statement.Condition, false, false, 0);
			if (!IsBooleanExpr(statement.Condition)) _bytecode.Emit(Op.ConvToFloat);
			_bytecode.Emit(Op.If);
			var breakLoc = _bytecode.EmitNumberOperandPlaceholder();
			_bytecode.Emit(Op.CmdCall);
			foreach (var stmt in statement.Body) EmitStatement(stmt);
			Emit(statement.Post);
			_bytecode.Emit(Op.SetIndex);
			_bytecode.EmitDynamicNumber(start);
			_bytecode.PatchShort(breakLoc, _bytecode.OpIndex);
		}

		private void EmitForEach(ForEachStmt statement)
		{
			Emit(statement.Name);
			Emit(statement.Source);
			_bytecode.Emit(Op.ConvToObject);
			_bytecode.Emit(Op.TypeNumber);
			_bytecode.EmitDynamicNumber(0);
			var start = _bytecode.OpIndex;
			_bytecode.Emit(Op.Foreach);
			var breakLoc = _bytecode.EmitNumberOperandPlaceholder();
			_bytecode.Emit(Op.CmdCall);
			foreach (var stmt in statement.Body) EmitStatement(stmt);
			_bytecode.Emit(Op.Inc);
			_bytecode.Emit(Op.SetIndex);
			_bytecode.EmitDynamicNumber(start);
			_bytecode.PatchShort(breakLoc, _bytecode.OpIndex);
			_bytecode.Emit(Op.IndexDec);
		}

		private void EmitWith(WithStmt statement)
		{
			Emit(statement.Target);
			_bytecode.Emit(Op.ConvToObject);
			_bytecode.Emit(Op.With);
			var exitLoc = _bytecode.EmitNumberOperandPlaceholder();
			foreach (var stmt in statement.Body) EmitStatement(stmt);
			_bytecode.Emit(Op.WithEnd);
			_bytecode.PatchShort(exitLoc, _bytecode.OpIndex);
		}

		private void EmitSwitch(SwitchStmt statement)
		{
			_bytecode.Emit(Op.SetIndex);
			var caseTestLoc = _bytecode.EmitNumberOperandPlaceholder();
			List<int> caseStarts = [];
			List<int> breakPatches = [];
			_breakPatches.Push(breakPatches);
			foreach (var switchCase in statement.Cases)
			{
				var start = _bytecode.OpIndex;
				foreach (var _ in switchCase.Labels) caseStarts.Add(start);
				foreach (var stmt in switchCase.Body) EmitStatement(stmt);
			}
			_breakPatches.Pop();
			_bytecode.PatchShort(caseTestLoc, _bytecode.OpIndex);
			Emit(statement.Expression);
			var caseIndex = 0;
			foreach (var switchCase in statement.Cases)
			foreach (var label in switchCase.Labels)
			{
				if (label != null)
				{
					_bytecode.Emit(Op.CopyLastOp);
					Emit(label);
					_bytecode.Emit(Op.Eq);
					_bytecode.Emit(Op.SetIndexTrue);
				}
				else _bytecode.Emit(Op.SetIndex);
				_bytecode.EmitDynamicNumber(caseStarts[caseIndex++]);
			}
			foreach (var patch in breakPatches) _bytecode.PatchShort(patch, _bytecode.OpIndex);
			_bytecode.Emit(Op.IndexDec);
		}

		private void EmitBreak()
		{
			if (_breakPatches.Count == 0) return;
			_bytecode.Emit(Op.SetIndex);
			_breakPatches.Peek().Add(_bytecode.EmitNumberOperandPlaceholder());
		}

		private void Emit(Expr expr, bool copyAssignmentTarget, bool logicalInline, int suppressedLogicalPatchOffset)
		{
			switch (expr)
			{
				case BinaryExpr { Op: "=", Left: ArrayIndexExpr left, Right: var right }:
					EmitArrayIndex(left, true);
					if (copyAssignmentTarget) _bytecode.Emit(Op.CopyLastOp);
					if (right is BinaryExpr { Op: "=" }) Emit(right, true, true, 0);
					else Emit(right);
					_bytecode.Emit(Op.ArrayAssign);
					break;
				case BinaryExpr { Op: "=", Left: var left, Right: var right }:
					Emit(left);
					if (copyAssignmentTarget) _bytecode.Emit(Op.CopyLastOp);
					if (right is BinaryExpr { Op: "=" }) Emit(right, true, true, 0);
					else Emit(right);
					_bytecode.Emit(Op.Assign);
					break;
				case BinaryExpr { Op: var op, Left: ArrayIndexExpr left, Right: var right } when IsCompoundAssign(op):
					EmitArrayIndex(left, true);
					_bytecode.Emit(Op.CopyLastOp);
					_bytecode.Emit(op == "@=" ? Op.ConvToString : Op.ConvToFloat);
					Emit(right);
					if (op != "@=" && NeedsNumericConversion(right)) _bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(BinaryOpcode(op[0].ToString()));
					_bytecode.Emit(Op.ArrayAssign);
					break;
				case BinaryExpr { Op: var op, Left: var left, Right: var right } when IsCompoundAssign(op):
					Emit(left);
					_bytecode.Emit(Op.CopyLastOp);
					_bytecode.Emit(op == "@=" ? Op.ConvToString : Op.ConvToFloat);
					Emit(right);
					if (op != "@=" && NeedsNumericConversion(right)) _bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(BinaryOpcode(op[0].ToString()));
					_bytecode.Emit(Op.Assign);
					break;
				case BinaryExpr { Op: "&&" or "||" } logical:
					Emit(logical.Left, false, false, logical.Left is BinaryExpr { Op: "&&" or "||" } ? 1 : 0);
					if (!IsBooleanExpr(logical.Left)) _bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(logical.Op == "&&" ? Op.And : Op.Or);
					var loc = _bytecode.EmitNumberOperandPlaceholder();
					Emit(logical.Right, false, false, 0);
					if (!IsBooleanExpr(logical.Right)) _bytecode.Emit(Op.ConvToFloat);
					if (logicalInline) _bytecode.Emit(Op.InlineConditional);
					_bytecode.PatchShort(loc, _bytecode.OpIndex - (logicalInline ? 1 : 0) + (!logicalInline ? suppressedLogicalPatchOffset : 0));
					break;
				case TernaryExpr ternary:
					Emit(ternary.Condition, false, false, 0);
					if (!IsBooleanExpr(ternary.Condition)) _bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(Op.If);
					var failLoc = _bytecode.EmitNumberOperandPlaceholder();
					Emit(ternary.WhenTrue);
					_bytecode.PatchShort(failLoc, _bytecode.OpIndex + 1);
					_bytecode.Emit(Op.SetIndex);
					var successLoc = _bytecode.EmitNumberOperandPlaceholder();
					Emit(ternary.WhenFalse);
					_bytecode.PatchShort(successLoc, _bytecode.OpIndex);
					break;
				case BinaryExpr binary:
					Emit(binary.Left);
					if ((IsNumericOp(binary.Op) || IsComparisonOp(binary.Op)) && NeedsNumericConversion(binary.Left)) _bytecode.Emit(Op.ConvToFloat);
					if (binary.Op == "&" && binary.Left is not NumberExpr) _bytecode.Emit(Op.ConvToFloat);
					Emit(binary.Right);
					if ((IsNumericOp(binary.Op) || IsComparisonOp(binary.Op)) && NeedsNumericConversion(binary.Right)) _bytecode.Emit(Op.ConvToFloat);
					if (binary.Op == "@") _bytecode.Emit(Op.ConvToString);
					_bytecode.Emit(BinaryOpcode(binary.Op));
					break;
				case MemberExpr member:
					Emit(member.Object);
					if (member.Object is not IdentifierExpr { Name: "temp" } and not ArrayIndexExpr) _bytecode.Emit(Op.ConvToObject);
					_bytecode.Emit(Op.TypeVar);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(member.Name));
					_bytecode.Emit(Op.MemberAccess);
					break;
				case ArrayIndexExpr index:
					EmitArrayIndex(index, false);
					break;
				case IdentifierExpr id when _constants.TryGetValue(id.Name, out var constant):
					Emit(constant);
					break;
				case IdentifierExpr { Name: "temp" }:
					_bytecode.Emit(Op.Temp);
					break;
				case IdentifierExpr id:
					_bytecode.Emit(Op.TypeVar);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(id.Name));
					break;
				case EnumExpr enumExpr:
					_bytecode.Emit(Op.TypeNumber);
					_bytecode.EmitDynamicNumber(_enums.TryGetValue(enumExpr.EnumName, out var members) && members.TryGetValue(enumExpr.MemberName, out var value) ? value : 0);
					break;
				case NumberExpr number:
					_bytecode.Emit(Op.TypeNumber);
					if (number.Text.Contains('.', StringComparison.Ordinal)) _bytecode.EmitDoubleNumber(number.Text);
					else _bytecode.EmitDynamicNumber(int.Parse(number.Text, CultureInfo.InvariantCulture));
					break;
				case StringExpr str:
					_bytecode.Emit(Op.TypeString);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(str.Value));
					break;
				case BoolExpr boolean:
					_bytecode.Emit(boolean.Value ? Op.TypeTrue : Op.TypeFalse);
					break;
				case NullExpr:
					_bytecode.Emit(Op.TypeNull);
					break;
				case UnaryExpr { Op: "-", Expression: NumberExpr number }:
					_bytecode.Emit(Op.TypeNumber);
					if (number.Text.Contains('.', StringComparison.Ordinal)) _bytecode.EmitDoubleNumber("-" + number.Text);
					else _bytecode.EmitDynamicNumber(-int.Parse(number.Text, CultureInfo.InvariantCulture));
					break;
				case UnaryExpr { Op: "-", Expression: var unaryValue }:
					Emit(unaryValue);
					_bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(Op.UnarySub);
					break;
				case UnaryExpr { Op: "!", Expression: var notValue }:
					Emit(notValue, false, false, 0);
					if (!IsBooleanExpr(notValue)) _bytecode.Emit(Op.ConvToFloat);
					_bytecode.Emit(Op.Not);
					break;
				case UnaryExpr { Op: "++", Expression: var incValue }:
					Emit(incValue);
					_bytecode.Emit(Op.Inc);
					_bytecode.Emit(Op.IndexDec);
					break;
				case UnaryExpr { Op: "--", Expression: var decValue }:
					Emit(decValue);
					_bytecode.Emit(Op.Dec);
					_bytecode.Emit(Op.IndexDec);
					break;
				case CallExpr call:
					_bytecode.Emit(Op.TypeArray);
					for (var i = call.Args.Count - 1; i >= 0; --i) Emit(call.Args[i]);
					_bytecode.Emit(Op.TypeVar);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(call.Name));
					_bytecode.Emit(Op.Call);
					break;
				case MethodCallExpr call:
					_bytecode.Emit(Op.TypeArray);
					for (var i = call.Args.Count - 1; i >= 0; --i) Emit(call.Args[i]);
					Emit(call.Object);
					if (call.Object is not IdentifierExpr { Name: "temp" }) _bytecode.Emit(Op.ConvToObject);
					_bytecode.Emit(Op.TypeVar);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(call.Name));
					_bytecode.Emit(Op.MemberAccess);
					_bytecode.Emit(Op.Call);
					break;
				case NewObjectExpr obj:
					var typeNameIndex = _bytecode.GetString(obj.TypeName);
					if (obj.Args.Count == 1)
					{
						Emit(obj.Args[0]);
						_bytecode.Emit(Op.InlineNew);
					}
					else
					{
						_bytecode.Emit(Op.TypeVar);
						_bytecode.EmitDynamicStringIndex(_bytecode.GetString("unknown_object"));
					}
					_bytecode.Emit(Op.TypeString);
					_bytecode.EmitDynamicStringIndex(typeNameIndex);
					_bytecode.Emit(Op.NewObject);
					break;
				case LambdaExpr lambda:
					EmitFunction(new(lambda.Name, null, true, lambda.Args, lambda.Body), false);
					_bytecode.Emit(Op.This);
					_bytecode.Emit(Op.TypeVar);
					_bytecode.EmitDynamicStringIndex(_bytecode.GetString(lambda.Name));
					_bytecode.Emit(Op.MemberAccess);
					_bytecode.Emit(Op.ConvToObject);
					break;
				case ArrayLiteralExpr array:
					_bytecode.Emit(Op.TypeArray);
					for (var i = array.Values.Count - 1; i >= 0; --i) Emit(array.Values[i]);
					_bytecode.Emit(Op.ArrayEnd);
					break;
			}
		}

		private void EmitArrayIndex(ArrayIndexExpr expr, bool assignmentTarget)
		{
			Emit(expr.Target);
			if (expr.Target is not ArrayIndexExpr) _bytecode.Emit(Op.ConvToObject);
			Emit(expr.Index);
			if (!IsNumberExpr(expr.Index)) _bytecode.Emit(Op.ConvToFloat);
			if (!assignmentTarget) _bytecode.Emit(Op.Array);
		}

		private static bool IsCompoundAssign(string op) => op is "+=" or "-=" or "*=" or "/=" or "%=" or "@=";

		private static bool IsNumericOp(string op) => op is "+" or "-" or "*" or "/" or "%" or "^";

		private static bool IsComparisonOp(string op) => op is "<" or "<=" or "=<" or ">" or ">=" or "=>";

		private static bool NeedsNumericConversion(Expr expr) => expr is IdentifierExpr or MemberExpr or CallExpr;

		private static bool IsNumberExpr(Expr expr) => expr is NumberExpr or UnaryExpr { Op: "-", Expression: NumberExpr };

		private static bool IsBooleanExpr(Expr expr) => expr switch
		{
			BinaryExpr { Op: "==" or "!=" or "<" or "<=" or "=<" or ">" or ">=" or "=>" or "&&" or "||" } => true,
			UnaryExpr { Op: "!" } => true,
			_ => false
		};

		private static bool ContainsCall(Stmt statement) => statement switch
		{
			ExprStmt expr => ContainsCall(expr.Expression),
			ReturnStmt expr => ContainsCall(expr.Expression),
			IfStmt stmt => stmt.ThenBody.Exists(ContainsCall) || stmt.ElseBody.Exists(ContainsCall) || ContainsCall(stmt.Condition),
			ForStmt stmt => ContainsCall(stmt.Init) || ContainsCall(stmt.Condition) || ContainsCall(stmt.Post) || stmt.Body.Exists(ContainsCall),
			ForEachStmt stmt => ContainsCall(stmt.Name) || ContainsCall(stmt.Source) || stmt.Body.Exists(ContainsCall),
			WithStmt stmt => ContainsCall(stmt.Target) || stmt.Body.Exists(ContainsCall),
			SwitchStmt stmt => ContainsCall(stmt.Expression) || stmt.Cases.Exists(c => c.Body.Exists(ContainsCall) || c.Labels.Exists(label => label != null && ContainsCall(label))),
			_ => false
		};

		private static bool ContainsCall(Expr expr) => expr switch
		{
			CallExpr => true,
			MethodCallExpr => true,
			BinaryExpr binary => ContainsCall(binary.Left) || ContainsCall(binary.Right),
			UnaryExpr unary => ContainsCall(unary.Expression),
			MemberExpr member => ContainsCall(member.Object),
			_ => false
		};

		private static Op BinaryOpcode(string op) => op switch
		{
			"+" => Op.Add,
			"-" => Op.Sub,
			"*" => Op.Mul,
			"/" => Op.Div,
			"%" => Op.Mod,
			"^" => Op.Pow,
			"@" => Op.Join,
			"==" => Op.Eq,
			"!=" => Op.Neq,
			"<" => Op.Lt,
			"<=" or "=<" => Op.Lte,
			">" => Op.Gt,
			">=" or "=>" => Op.Gte,
			"&" => Op.BitAnd,
			"|" => Op.BitOr,
			"<<" => Op.ShiftLeft,
			">>" => Op.ShiftRight,
			"&&" => Op.And,
			"||" => Op.Or,
			_ => Op.None
		};
	}

	private sealed class BytecodeWriter
	{
		private readonly List<byte> _code = [];
		private readonly List<string> _strings = [];
		private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
		private readonly List<FunctionEntry> _functions = [];
		private readonly HashSet<string> _functionSet = new(StringComparer.Ordinal);
		private readonly List<int> _prejumpPatches = [];
		private Op _lastOp = Op.None;
		public int OpIndex { get; private set; }

		public int GetString(string value)
		{
			if (_stringIndex.TryGetValue(value, out var index)) return index;
			index = _strings.Count;
			_strings.Add(value);
			_stringIndex[value] = index;
			return index;
		}

		public void AddFunction(string name, int opIndex, int jmpLoc)
		{
			if (_functionSet.Add(name)) _functions.Add(new(name, opIndex, jmpLoc));
		}

		public void AddPrejumpPatch(int pos) => _prejumpPatches.Add(pos);

		public void Emit(Op op)
		{
			_code.Add((byte)op);
			_lastOp = op;
			OpIndex++;
		}

		public void EmitNumberOperand(int value)
		{
			_code.Add(0xF4);
			WriteShort(_code, value);
		}

		public int EmitNumberOperandPlaceholder()
		{
			_code.Add(0xF4);
			var pos = _code.Count;
			WriteShort(_code, 0);
			return pos;
		}

		public void PatchShort(int pos, int value)
		{
			_code[pos] = (byte)((value >> 8) & 0xFF);
			_code[pos + 1] = (byte)(value & 0xFF);
		}

		public void EmitDynamicStringIndex(int value)
		{
			if (value <= byte.MaxValue) { _code.Add(0xF0); _code.Add((byte)value); }
			else if (value <= ushort.MaxValue) { _code.Add(0xF1); WriteShort(_code, value); }
			else { _code.Add(0xF2); WriteInt(_code, value); }
		}

		public void EmitDynamicNumber(int value)
		{
			var offset = _lastOp == Op.TypeNumber || _lastOp == Op.SetIndex || _lastOp == Op.SetIndexTrue ? 3 : 0;
			if (value >= sbyte.MinValue && value <= sbyte.MaxValue) { _code.Add((byte)(0xF0 + offset)); _code.Add(unchecked((byte)(sbyte)value)); }
			else if (value >= short.MinValue && value <= short.MaxValue) { _code.Add((byte)(0xF1 + offset)); WriteShort(_code, value); }
			else { _code.Add((byte)(0xF2 + offset)); WriteInt(_code, value); }
		}

		public void EmitDoubleNumber(string value)
		{
			_code.Add(0xF6);
			WriteCString(_code, value);
		}

		public byte[] ToArray()
		{
			Emit(Op.Ret);
			foreach (var loc in _prejumpPatches) PatchShort(loc, OpIndex);
			List<byte> result = [];
			WriteSegment(result, 1, [0, 0, 0, 0]);
			List<byte> functions = [];
			foreach (var function in OrderedFunctions())
			{
				WriteInt(functions, function.OpIndex);
				WriteCString(functions, function.Name);
			}
			WriteSegment(result, 2, functions);
			List<byte> strings = [];
			foreach (var str in _strings) WriteCString(strings, str);
			WriteSegment(result, 3, strings);
			WriteSegment(result, 4, _code);
			result.Add(10);
			return [.. result];
		}

		private IEnumerable<FunctionEntry> OrderedFunctions()
		{
			HashSet<string> emitted = new(StringComparer.Ordinal);
			foreach (var str in _strings)
				foreach (var fn in _functions)
					if (fn.Name == str && emitted.Add(fn.Name)) yield return fn;
			foreach (var fn in _functions)
				if (emitted.Add(fn.Name)) yield return fn;
		}

		private static void WriteSegment(List<byte> target, int type, IReadOnlyCollection<byte> bytes)
		{
			WriteInt(target, type);
			WriteInt(target, bytes.Count);
			target.AddRange(bytes);
		}

		private static void WriteCString(List<byte> target, string value)
		{
			target.AddRange(Encoding.UTF8.GetBytes(value));
			target.Add(0);
		}

		private static void WriteShort(List<byte> target, int value)
		{
			target.Add((byte)((value >> 8) & 0xFF));
			target.Add((byte)(value & 0xFF));
		}

		private static void WriteInt(List<byte> target, int value)
		{
			target.Add((byte)((value >> 24) & 0xFF));
			target.Add((byte)((value >> 16) & 0xFF));
			target.Add((byte)((value >> 8) & 0xFF));
			target.Add((byte)(value & 0xFF));
		}
	}

	private static class Header
	{
		public static byte[] Wrap(byte[] bytecode, string type, string name)
		{
			var info = Encoding.UTF8.GetBytes($"{type},{name},0,");
			List<byte> result = [0xAC, (byte)((info.Length >> 8) & 0xFF), (byte)(info.Length & 0xFF)];
			result.AddRange(info);
			result.AddRange(bytecode);
			return [.. result];
		}
	}

	private sealed class Lexer
	{
		private readonly string _code;
		private int _pos;
		private int _line = 1;
		private int _lineStart;
		public Lexer(string code) => _code = code;

		public Token Next()
		{
			while (_pos < _code.Length)
			{
				var c = _code[_pos];
				if (c is ' ' or '\t' or '\r') { _pos++; continue; }
				if (c == '\n') { _pos++; _line++; _lineStart = _pos; continue; }
				if (c == '/' && Peek(1) == '/') { while (_pos < _code.Length && _code[_pos] != '\n') _pos++; continue; }
				if (c == '/' && Peek(1) == '*') { _pos += 2; while (_pos + 1 < _code.Length && !(_code[_pos] == '*' && _code[_pos + 1] == '/')) { if (_code[_pos++] == '\n') { _line++; _lineStart = _pos; } } _pos += _pos + 1 < _code.Length ? 2 : 0; continue; }
				break;
			}
			if (_pos >= _code.Length) return Make(TokenType.End, "");
			var ch = _code[_pos];
			if (char.IsDigit(ch)) return Number();
			if (IsIdentStart(ch)) return Identifier();
			if (ch == '"') return String();
			if (ch == ':' && Peek(1) == ':') { _pos += 2; return Make(TokenType.Scope, "::"); }
			if (ch == ':' && Peek(1) == '=') { _pos += 2; return Make(TokenType.Assign, ":="); }
			if (ch == '+' && Peek(1) == '=') { _pos += 2; return Make(TokenType.AddAssign, "+="); }
			if (ch == '-' && Peek(1) == '=') { _pos += 2; return Make(TokenType.SubAssign, "-="); }
			if (ch == '*' && Peek(1) == '=') { _pos += 2; return Make(TokenType.MulAssign, "*="); }
			if (ch == '/' && Peek(1) == '=') { _pos += 2; return Make(TokenType.DivAssign, "/="); }
			if (ch == '%' && Peek(1) == '=') { _pos += 2; return Make(TokenType.ModAssign, "%="); }
			if (ch == '@' && Peek(1) == '=') { _pos += 2; return Make(TokenType.CatAssign, "@="); }
			if (ch == '=' && Peek(1) == '=') { _pos += 2; return Make(TokenType.Equal, "=="); }
			if (ch == '!' && Peek(1) == '=') { _pos += 2; return Make(TokenType.NotEqual, "!="); }
			if (ch == '<' && Peek(1) == '>') { _pos += 2; return Make(TokenType.NotEqual, "!="); }
			if (ch == '<' && Peek(1) == '=') { _pos += 2; return Make(TokenType.LessEqual, "<="); }
			if (ch == '=' && Peek(1) == '<') { _pos += 2; return Make(TokenType.LessEqual, "=<"); }
			if (ch == '>' && Peek(1) == '=') { _pos += 2; return Make(TokenType.GreaterEqual, ">="); }
			if (ch == '=' && Peek(1) == '>') { _pos += 2; return Make(TokenType.GreaterEqual, "=>"); }
			if (ch == '&' && Peek(1) == '&') { _pos += 2; return Make(TokenType.And, "&&"); }
			if (ch == '|' && Peek(1) == '|') { _pos += 2; return Make(TokenType.Or, "||"); }
			if (ch == '<' && Peek(1) == '<') { _pos += 2; return Make(TokenType.ShiftLeft, "<<"); }
			if (ch == '>' && Peek(1) == '>') { _pos += 2; return Make(TokenType.ShiftRight, ">>"); }
			if (ch == '+' && Peek(1) == '+') { _pos += 2; return Make(TokenType.Increment, "++"); }
			if (ch == '-' && Peek(1) == '-') { _pos += 2; return Make(TokenType.Decrement, "--"); }
			_pos++;
			return ch switch
			{
				'=' => Make(TokenType.Assign, "="),
				';' => Make(TokenType.Semicolon, ";"),
				',' => Make(TokenType.Comma, ","),
				':' => Make(TokenType.Colon, ":"),
				'?' => Make(TokenType.Question, "?"),
				'.' => Make(TokenType.Dot, "."),
				'{' => Make(TokenType.LeftBrace, "{"),
				'}' => Make(TokenType.RightBrace, "}"),
				'(' => Make(TokenType.LeftParen, "("),
				')' => Make(TokenType.RightParen, ")"),
				'[' => Make(TokenType.LeftBracket, "["),
				']' => Make(TokenType.RightBracket, "]"),
				'-' => Make(TokenType.Minus, "-"),
				'+' => Make(TokenType.Plus, "+"),
				'*' => Make(TokenType.Star, "*"),
				'/' => Make(TokenType.Slash, "/"),
				'%' => Make(TokenType.Percent, "%"),
				'^' => Make(TokenType.Caret, "^"),
				'@' => Make(TokenType.At, "@"),
				'!' => Make(TokenType.Not, "!"),
				'<' => Make(TokenType.Less, "<"),
				'>' => Make(TokenType.Greater, ">"),
				'&' => Make(TokenType.BitAnd, "&"),
				'|' => Make(TokenType.BitOr, "|"),
				_ => Make(TokenType.Unknown, ch.ToString())
			};
		}

		private Token Number()
		{
			var start = _pos;
			while (_pos < _code.Length && char.IsDigit(_code[_pos])) _pos++;
			if (_pos < _code.Length && _code[_pos] == '.') { _pos++; while (_pos < _code.Length && char.IsDigit(_code[_pos])) _pos++; }
			return Make(TokenType.Number, _code[start.._pos]);
		}

		private Token Identifier()
		{
			var start = _pos++;
			while (_pos < _code.Length && IsIdentPart(_code[_pos])) _pos++;
			var text = _code[start.._pos];
			return Make(text switch
			{
				"const" => TokenType.Const,
				"enum" => TokenType.Enum,
				"function" => TokenType.Function,
				"return" => TokenType.Return,
				"if" => TokenType.If,
				"else" => TokenType.Else,
				"for" => TokenType.For,
				"with" => TokenType.With,
				"new" => TokenType.New,
				"switch" => TokenType.Switch,
				"case" => TokenType.Case,
				"default" => TokenType.Default,
				"break" => TokenType.Break,
				"true" => TokenType.True,
				"false" => TokenType.False,
				"null" => TokenType.Null,
				_ => TokenType.Identifier
			}, text);
		}

		private Token String()
		{
			_pos++;
			StringBuilder builder = new();
			while (_pos < _code.Length && _code[_pos] != '"')
			{
				if (_code[_pos] == '\\' && _pos + 1 < _code.Length)
				{
					_pos++;
					builder.Append(_code[_pos] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '"' => '"', '\\' => '\\', var c => c });
					_pos++;
				}
				else builder.Append(_code[_pos++]);
			}
			if (_pos < _code.Length) _pos++;
			return Make(TokenType.String, builder.ToString());
		}

		private char Peek(int offset) => _pos + offset < _code.Length ? _code[_pos + offset] : '\0';
		private Token Make(TokenType type, string text) => new(type, text, _line, _pos - _lineStart + 1) { LineText = CurrentLine() };
		private string CurrentLine()
		{
			var end = _code.IndexOf('\n', _lineStart);
			if (end < 0) end = _code.Length;
			return _code[_lineStart..end];
		}
		private static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '$';
		private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';
	}

	private enum TokenType { Unknown, End, Identifier, Number, String, Const, Enum, Function, Return, If, Else, For, With, New, Switch, Case, Default, Break, True, False, Null, Assign, AddAssign, SubAssign, MulAssign, DivAssign, ModAssign, CatAssign, Semicolon, Comma, Colon, Question, Dot, Scope, LeftBrace, RightBrace, LeftParen, RightParen, LeftBracket, RightBracket, Minus, Plus, Star, Slash, Percent, Caret, At, Not, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, And, Or, BitAnd, BitOr, ShiftLeft, ShiftRight, Increment, Decrement }
	private sealed record Token(TokenType Type, string Text, int Line, int Column) { public string LineText { get; init; } = ""; }
	private sealed record ProgramNode(Dictionary<string, Expr> Constants, Dictionary<string, Dictionary<string, int>> Enums, List<FunctionNode> Functions);
	private sealed record FunctionNode(string Name, string? ObjectName, bool Public, List<string> Args, List<Stmt> Body);
	private abstract record Stmt;
	private sealed record ExprStmt(Expr Expression) : Stmt;
	private sealed record ReturnStmt(Expr Expression) : Stmt;
	private sealed record IfStmt(Expr Condition, List<Stmt> ThenBody, List<Stmt> ElseBody) : Stmt;
	private sealed record ForStmt(Expr Init, Expr Condition, Expr Post, List<Stmt> Body) : Stmt;
	private sealed record ForEachStmt(Expr Name, Expr Source, List<Stmt> Body) : Stmt;
	private sealed record WithStmt(Expr Target, List<Stmt> Body) : Stmt;
	private sealed record SwitchStmt(Expr Expression, List<SwitchCase> Cases) : Stmt;
	private sealed record BreakStmt : Stmt;
	private sealed record SwitchCase(List<Expr?> Labels, List<Stmt> Body);
	private abstract record Expr;
	private sealed record BinaryExpr(Expr Left, string Op, Expr Right) : Expr;
	private sealed record TernaryExpr(Expr Condition, Expr WhenTrue, Expr WhenFalse) : Expr;
	private sealed record UnaryExpr(string Op, Expr Expression) : Expr;
	private sealed record MemberExpr(Expr Object, string Name) : Expr;
	private sealed record ArrayIndexExpr(Expr Target, Expr Index) : Expr;
	private sealed record IdentifierExpr(string Name) : Expr;
	private sealed record EnumExpr(string EnumName, string MemberName) : Expr;
	private sealed record NumberExpr(string Text) : Expr;
	private sealed record StringExpr(string Value) : Expr;
	private sealed record BoolExpr(bool Value) : Expr;
	private sealed record NullExpr : Expr;
	private sealed record CallExpr(string Name, List<Expr> Args) : Expr;
	private sealed record MethodCallExpr(Expr Object, string Name, List<Expr> Args) : Expr;
	private sealed record NewObjectExpr(string TypeName, List<Expr> Args) : Expr;
	private sealed record LambdaExpr(string Name, List<string> Args, List<Stmt> Body) : Expr;
	private sealed record ArrayLiteralExpr(List<Expr> Values) : Expr;
	private sealed record FunctionEntry(string Name, int OpIndex, int JmpLoc);

	private enum Op : byte
	{
		None = 0,
		SetIndex = 1,
		SetIndexTrue = 2,
		Or = 3,
		If = 4,
		And = 5,
		Call = 6,
		CmdCall = 9,
		Jmp = 10,
		TypeNumber = 20,
		TypeString = 21,
		TypeVar = 22,
		TypeArray = 23,
		TypeTrue = 24,
		TypeFalse = 25,
		TypeNull = 26,
		IndexDec = 32,
		ConvToFloat = 33,
		ConvToString = 34,
		MemberAccess = 35,
		ConvToObject = 36,
		ArrayEnd = 37,
		InlineNew = 40,
		NewObject = 42,
		Assign = 50,
		FuncParamsEnd = 51,
		Inc = 52,
		Dec = 53,
		CopyLastOp = 30,
		Add = 60,
		Sub = 61,
		Mul = 62,
		Div = 63,
		Mod = 64,
		Pow = 65,
		Not = 68,
		UnarySub = 69,
		Eq = 70,
		Neq = 71,
		Lt = 72,
		Gt = 73,
		Lte = 74,
		Gte = 75,
		BitOr = 76,
		BitAnd = 77,
		ShiftLeft = 101,
		ShiftRight = 102,
		Join = 113,
		Array = 131,
		ArrayAssign = 132,
		ArrayMultiDim = 133,
		ArrayMultiDimAssign = 134,
		With = 150,
		WithEnd = 151,
		Foreach = 163,
		InlineConditional = 44,
		Ret = 7,
		This = 180,
		Temp = 189
	}
}
