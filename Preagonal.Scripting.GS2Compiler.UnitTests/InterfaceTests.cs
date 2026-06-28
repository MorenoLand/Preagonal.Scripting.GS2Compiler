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
}
