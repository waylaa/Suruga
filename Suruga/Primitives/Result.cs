using System.Diagnostics.CodeAnalysis;

namespace Suruga.Primitives;

/// <summary>
/// Represents the outcome of an operation, encapsulating either a successful value
/// of type <typeparamref name="T"/> or an <see cref="Exception"/> indicating failure.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
internal sealed class Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    /// <value>
    /// <c>true</c> if <see cref="Value"/> is not null and <see cref="Error"/> is null; otherwise, <c>false</c>.
    /// </value>
    [MemberNotNullWhen(true, nameof(Value))]
	[MemberNotNullWhen(false, nameof(Error))]
	internal bool IsSuccessful => this is { Value: not null, Error: null };

    /// <summary>
    /// Gets the successful value of the operation, if available.
    /// </summary>
    /// <value>The value of type <typeparamref name="T"/> if successful; otherwise, <c>null</c>.</value>
    internal T? Value { get; }

    /// <summary>
    /// Gets the exception that caused the operation to fail, if applicable.
    /// </summary>
    /// <value>The <see cref="Exception"/> if failed; otherwise, <c>null</c>.</value>
    internal Exception? Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class representing a successful outcome.
    /// </summary>
    /// <param name="value">The successful value.</param>
    private Result(T value)
		=> Value = value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class representing a failed outcome.
    /// </summary>
    /// <param name="error">The exception that caused the failure.</param>
    private Result(Exception error)
		=> Error = error;

    /// <summary>
    /// Attempts to retrieve the successful value of the operation.
    /// </summary>
    /// <param name="value">
    /// When this method returns, contains the successful value if the operation succeeded; 
    /// otherwise, contains the default value of <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c>.</returns>
    [MemberNotNullWhen(false, nameof(Error))]
	internal bool TryGetValue([NotNullWhen(true)] out T? value)
	{
		value = Value;
		return IsSuccessful;
	}

    /// <summary>
    /// Propagates the current failure to a new <see cref="Result{TOther}"/> instance.
    /// </summary>
    /// <typeparam name="TOther">The type of the value for the new result instance.</typeparam>
    /// <returns>A new failed <see cref="Result{TOther}"/> containing the same <see cref="Error"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the current result is successful.</exception>
    internal Result<TOther> MapFailure<TOther>()
	{
		return IsSuccessful
			? throw new InvalidOperationException("Cannot propagate an error if the result is successful.")
			: Result<TOther>.Failure(Error);
	}

    /// <summary>
    /// Creates a new successful <see cref="Result{T}"/> instance with the specified value.
    /// </summary>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    internal static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a new failed <see cref="Result{T}"/> instance with the specified exception.
    /// </summary>
    /// <param name="error">The exception representing the failure.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    internal static Result<T> Failure(Exception error) => new(error);

    /// <summary>
    /// Implicitly converts a value of type <typeparamref name="T"/> into a successful <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the specified value.</returns>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an <see cref="Exception"/> into a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="error">The exception to convert.</param>
    /// <returns>A failed <see cref="Result{T}"/> containing the specified exception.</returns>
    public static implicit operator Result<T>(Exception error) => Failure(error);
}
