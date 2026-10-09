namespace Microservices.Common.Errors;

/// <summary>
/// A discriminated result that carries either a success value or an <see cref="Error"/>.
/// Business failures are modelled as values (not exceptions) so control flow stays explicit.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value =>
        IsSuccess ? _value! : throw new InvalidOperationException("Cannot read Value from a failed result.");

    public Error Error =>
        IsFailure ? _error! : throw new InvalidOperationException("Cannot read Error from a successful result.");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    public static Result<T> Ok(T value) => new(value);

    public static Result<T> Fail(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}

/// <summary>Non-generic result for operations that have no return value.</summary>
public sealed class Result
{
    private readonly Error? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public Error Error =>
        IsFailure ? _error! : throw new InvalidOperationException("Cannot read Error from a successful result.");

    private Result() => IsSuccess = true;

    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    public static Result Ok() => new();

    public static Result Fail(Error error) => new(error);

    public static implicit operator Result(Error error) => Fail(error);
}
