using System.Text;

namespace Preagonal.Scripting.GS2Compiler.UnitTests;

public class InterfaceTests
{
	[Fact]
	public void Given_script_that_is_faulty_When_compiling_Then_success_is_false_and_error_message_is_returned()
	{
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() 
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.False(result.Success);
		Assert.Equal("malformed input at line 3: \t\t\t}\n", result.ErrMsg);
	}

	[Fact]
	public void Given_script_that_is_correct_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_that_is_correct_When_compiling_without_header_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
						}
			""";

		var result = Interface.CompileCode(scriptText, withHeader: false);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_continue_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  while (this.i < 3) {
						    this.i++;
						    continue;
						  }
						  for (temp.i = 0; temp.i < 3; temp.i++) {
						    continue;
						  }
						  for (temp.pl: players) {
						    continue;
						  }
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_casts_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.i = int("3.8");
						  temp.f = float("2.5");
						  temp.t = _("Hello");
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_bitwise_invert_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.mask = ~temp.flags;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_shift_assign_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.flags <<= 2;
						  temp.flags >>= 1;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_power_assign_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.value ^= 3;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_script_with_bitwise_xor_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.mask = temp.flags xor 3;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_for_loop_without_init_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.i = 0;
						  for (; temp.i < 3; temp.i++) {
						    continue;
						  }
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_lambda_with_single_statement_body_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.fn = function() temp.value = 1;;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_new_object_with_scoped_type_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.obj = new Gui::Control("Name");
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_scoped_object_function_When_compiling_Then_success_is_true_and_bytecode_is_not_empty()
	{
		const string scriptText =
			"""
						function Gui::Control.onAction() {
						  return;
						}
			""";

		var result = Interface.CompileCode(scriptText);

		Assert.True(result.Success);
		Assert.NotEmpty(result.ByteCode);
	}

	[Fact]
	public void Given_universe_object_function_When_compiling_Then_function_table_matches_reference_name()
	{
		const string scriptText =
			"""
						function universe.onServerListerConnect() {
						  return;
						}
			""";

		var result = Interface.CompileCode(scriptText, withHeader: false);
		var functionNames = ReadFunctionNames(result.ByteCode);

		Assert.True(result.Success);
		Assert.Contains("onServerListerConnect,universe.onServerListerConnect", functionNames);
	}

	[Fact]
	public void Given_math_builtin_calls_When_compiling_Then_direct_opcodes_are_emitted()
	{
		const string scriptText =
			"""
						function onCreated() {
						  temp.a = random(1, 2);
						  temp.b = min(1, 2);
						  temp.c = max(1, 2);
						}
			""";

		var result = Interface.CompileCode(scriptText, withHeader: false);
		var strings = ReadStringTable(result.ByteCode);
		var code = ReadBytecodeSegment(result.ByteCode);

		Assert.True(result.Success);
		Assert.Contains((byte)87, code);
		Assert.Contains((byte)93, code);
		Assert.Contains((byte)94, code);
		Assert.DoesNotContain("random", strings);
		Assert.DoesNotContain("min", strings);
		Assert.DoesNotContain("max", strings);
	}

	private static List<string> ReadFunctionNames(byte[] bytecode)
	{
		var offset = 0;
		while (offset + 8 <= bytecode.Length)
		{
			var type = ReadInt(bytecode, offset);
			var length = ReadInt(bytecode, offset + 4);
			offset += 8;
			if (type == 2) return ReadFunctionSegment(bytecode, offset, length);
			offset += length;
		}
		return [];
	}

	private static List<string> ReadStringTable(byte[] bytecode)
	{
		var segment = ReadSegment(bytecode, 3);
		List<string> strings = [];
		var offset = 0;
		while (offset < segment.Length)
		{
			var start = offset;
			while (offset < segment.Length && segment[offset] != 0) offset++;
			strings.Add(Encoding.UTF8.GetString(segment, start, offset - start));
			offset++;
		}
		return strings;
	}

	private static byte[] ReadBytecodeSegment(byte[] bytecode) => ReadSegment(bytecode, 4);

	private static byte[] ReadSegment(byte[] bytecode, int segmentType)
	{
		var offset = 0;
		while (offset + 8 <= bytecode.Length)
		{
			var type = ReadInt(bytecode, offset);
			var length = ReadInt(bytecode, offset + 4);
			offset += 8;
			if (type == segmentType) return bytecode[offset..(offset + length)];
			offset += length;
		}
		return [];
	}

	private static List<string> ReadFunctionSegment(byte[] bytecode, int offset, int length)
	{
		List<string> names = [];
		var end = offset + length;
		while (offset + 4 < end)
		{
			offset += 4;
			var start = offset;
			while (offset < end && bytecode[offset] != 0) offset++;
			names.Add(Encoding.UTF8.GetString(bytecode, start, offset - start));
			offset++;
		}
		return names;
	}

	private static int ReadInt(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
}
