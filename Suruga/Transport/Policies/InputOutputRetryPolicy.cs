namespace Suruga.Transport.Policies;

internal static class InputOutputRetryPolicy
{
    internal static T? Execute<T>
    (
        int maxAttempts,
        Span<byte> buffer,
        Attempt<T> attempt,
        Func<Exception, bool> shouldRetry,
        Action<Exception, int> onFailure,
        Func<Exception, int, bool> isExhausted,
        Func<Exception, Exception> exhaustedExceptionFactory
    )
    {
        for (int attemptIndex = 0; attemptIndex < maxAttempts; attemptIndex++)
        {
            try
            {
                return attempt(attemptIndex, buffer);
            }
            catch (Exception ex) when (shouldRetry(ex))
            {
                onFailure(ex, attemptIndex);

                if (isExhausted(ex, attemptIndex))
                {
                    throw exhaustedExceptionFactory(ex);
                }
            }
        }

        return default;
    }
}
