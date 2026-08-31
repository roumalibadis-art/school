namespace USTHBStudy.Application.Common;

/// <summary>
/// Outcome of an operation that can fail with expected business errors (as opposed to
/// exceptional faults). Keeps happy-path control flow free of exceptions.
/// </summary>
public class Result
{
    protected Result(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }
    public bool Failed => !Succeeded;
    public IReadOnlyList<string> Errors { get; }
    public string? FirstError => Errors.Count > 0 ? Errors[0] : null;

    public static Result Success() => new(true, Array.Empty<string>());

    public static Result Failure(params string[] errors) =>
        new(false, errors.Length == 0 ? new[] { "Operation failed." } : errors);

    public static Result Failure(IEnumerable<string> errors) => Failure(errors.ToArray());

    public static Result<T> Success<T>(T value) => new(value, true, Array.Empty<string>());

    public static Result<T> Failure<T>(params string[] errors) =>
        new(default, false, errors.Length == 0 ? new[] { "Operation failed." } : errors);
}

public sealed class Result<T> : Result
{
    internal Result(T? value, bool succeeded, IReadOnlyList<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public T? Value { get; }
}
