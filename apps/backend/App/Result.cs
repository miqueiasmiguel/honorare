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
