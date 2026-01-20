using System.Diagnostics.CodeAnalysis;

namespace Suruga.Primitives;

public readonly record struct Result<T>
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful { get; }

    public T? Value { get; }

    public Exception? Error { get; }

    private Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        
        Value = value;
        Error = null;
        IsSuccessful = true;
    }

    private Result(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        
        Error = error;
        Value = default;
        IsSuccessful = false;
    }

    internal static Result<T> Success(T value)
        => new(value);

    internal static Result<T> Failure(Exception error)
        => new(error);

    public static Result<T> Failure(string error)
        => new(new Exception(error));
    
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = Value;
        return IsSuccessful;
    }
}

internal readonly record struct Result
{
    internal static Result Success => new(true, null);
    
    [MemberNotNullWhen(false, nameof(Error))]
    internal bool IsSuccessful { get; }

    internal Exception? Error { get; }
    
    private Result(bool success, Exception? error)
    {
        IsSuccessful = success;
        Error = error;
    }

    internal static Result Failure(Exception error)
        => new(false, error);

    internal static Result Failure(string error)
        => new(false, new Exception(error));
}
