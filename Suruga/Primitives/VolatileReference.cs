using System.Diagnostics.CodeAnalysis;

namespace Suruga.Primitives;

/// <summary>
/// Holds a reference-type value that can be safely read and written from multiple threads.
/// </summary>
/// <typeparam name="T">The specified type of value being stored.</typeparam>
internal sealed class VolatileReference<T> where T : class
{
    /// <summary>
    /// Gets a value indicating whether the stored value is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
	internal bool IsValid => Volatile.Read(ref _value) is not null;

    /// <summary>
    /// Gets or sets the stored value.
    /// </summary>
    public T? Value
	{
		get => Volatile.Read(ref _value);
		set => Volatile.Write(ref _value, value);
	}

	private T? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileReference{T}"/> class.
    /// </summary>
    /// <param name="initialValue">
    /// The initial value to store, or <see langword="null"/> if no value should be stored initially.
    /// </param>
    internal VolatileReference(T? initialValue = null)
		=> _value = initialValue;

    /// <summary>
    /// Attempts to atomically capture the current value.
    /// </summary>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, contains the captured value.
    /// Otherwise, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the current value is not <see langword="null"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal bool TryCapture([NotNullWhen(true)] out T? value)
	{
		value = Volatile.Read(ref _value);
		return value is not null;
	}

    /// <summary>
    /// Atomically replaces the stored value and returns the previous value.
    /// </summary>
    /// <param name="newValue">The value to store.</param>
    /// <returns>The value that was stored before the replacement occurred.</returns>
    internal T? Replace(T? newValue)
		=> Interlocked.Exchange(ref _value, newValue);
}
