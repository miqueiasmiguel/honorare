namespace App;

internal readonly record struct Result
{
    public bool IsSuccess { get; }
    public DomainError? Error { get; }

    private Result(bool isSuccess, DomainError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);

    public static Result Fail(DomainError error) => new(false, error);

    public bool IsFailure => !IsSuccess;
}

internal readonly record struct Result<T>
{
    public T? Value { get; }
    public DomainError? Error { get; }

    private Result(T? value, DomainError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(DomainError error) => new(default, error);
}
