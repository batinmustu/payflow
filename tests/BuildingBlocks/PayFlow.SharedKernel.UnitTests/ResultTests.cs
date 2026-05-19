namespace PayFlow.SharedKernel.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_marks_result_as_successful_with_no_error_code()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_marks_result_as_failed_and_records_error_code()
    {
        var result = Result.Failure("ROUTING_EXHAUSTED");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ROUTING_EXHAUSTED");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_rejects_blank_error_codes(string blank)
    {
        var act = () => Result.Failure(blank);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Failure_rejects_null_error_code()
    {
        var act = () => Result.Failure(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generic_success_carries_the_value()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Generic_failure_does_not_expose_a_value()
    {
        var result = Result<int>.Failure("AMOUNT_INVALID");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AMOUNT_INVALID");

        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AMOUNT_INVALID*");
    }

    [Fact]
    public void Static_helpers_on_non_generic_Result_match_generic_factory_methods()
    {
        var fromHelper = Result.Success(7);
        var fromFactory = Result<int>.Success(7);

        fromHelper.Should().Be(fromFactory);
    }
}
