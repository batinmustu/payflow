namespace PayFlow.SharedKernel;

public readonly record struct Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);

    public static Result Failure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new Result(false, errorCode);
    }

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(string errorCode) => Result<T>.Failure(errorCode);
}

public readonly record struct Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value, string? errorCode)
    {
        IsSuccess = isSuccess;
        _value = value;
        ErrorCode = errorCode;
    }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed Result (code: {ErrorCode}).");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new Result<T>(false, default, errorCode);
    }
}
