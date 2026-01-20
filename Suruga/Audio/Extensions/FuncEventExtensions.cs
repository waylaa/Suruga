namespace Suruga.Audio.Extensions;

internal static class FuncEventExtensions
{
    extension<T>(Func<T, Task>? handler)
    {
        internal async Task InvokeAsync(T args)
        {
            if (handler is null)
            {
                return;
            }

            foreach (Delegate del in handler.GetInvocationList())
            {
                try
                {
                    await ((Func<T, Task>)del)(args);
                }
                catch
                {
                    // Swallow exceptions from individual handlers.
                }
            }
        }
    }

    extension<T1, T2>(Func<T1, T2, Task>? handler)
    {
        internal async Task InvokeAsync(T1 arg1, T2 arg2)
        {
            if (handler is null)
            {
                return;
            }

            foreach (Delegate del in handler.GetInvocationList())
            {
                try
                {
                    await ((Func<T1, T2, Task>)del)(arg1, arg2);
                }
                catch
                {
                    // Swallow exceptions from individual handlers.
                }
            }
        }
    }
}