namespace EventGO.Application.Authentication;

public sealed record AuthOperationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors)
{
    public static AuthOperationResult Success() =>
        new(true, Array.Empty<string>());

    public static AuthOperationResult Failure(params string[] errors) =>
        new(false, errors);
}

public sealed record AuthOperationResult<T>(
    bool Succeeded,
    T? Value,
    IReadOnlyList<string> Errors)
{
    public static AuthOperationResult<T> Success(T value) =>
        new(true, value, Array.Empty<string>());

    public static AuthOperationResult<T> Failure(params string[] errors) =>
        new(false, default, errors);
}
