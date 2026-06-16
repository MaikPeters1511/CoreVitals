namespace CoreVitals.Resilience;

using System;

/// <summary>
/// Provides helper methods for calculating backoff durations with exponential backoff and jitter.
/// </summary>
internal static class BackoffHelper
{
#if NET8_0_OR_GREATER
    private static double GetRandomDouble() => Random.Shared.NextDouble();
#else
    [ThreadStatic]
    private static Random? _random;
    private static double GetRandomDouble()
    {
        _random ??= new Random();
        return _random.NextDouble();
    }
#endif

    /// <summary>
    /// Calculates the delay duration for a specific retry attempt based on options.
    /// </summary>
    /// <param name="attempt">The 1-based attempt count.</param>
    /// <param name="options">The retry options specifying delay parameters.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the calculated delay.</returns>
    public static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        if (attempt <= 0) return TimeSpan.Zero;

        // Calculate exponential backoff: InitialDelay * (BackoffFactor ^ (attempt - 1))
        double delayMs = options.InitialDelay.TotalMilliseconds * Math.Pow(options.BackoffFactor, attempt - 1);
        
        // Clamp to MaxDelay
        double maxDelayMs = options.MaxDelay.TotalMilliseconds;
        if (delayMs > maxDelayMs)
        {
            delayMs = maxDelayMs;
        }

        if (options.UseJitter)
        {
            // Full Jitter algorithm: random value between 0 and the current backoff limit
            delayMs = GetRandomDouble() * delayMs;
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
