namespace Suruga.Transport.Policies;

internal delegate T Attempt<out T>(int attemptIndex, Span<byte> buffer);
