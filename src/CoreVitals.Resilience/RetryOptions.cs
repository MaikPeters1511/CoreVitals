namespace CoreVitals.Resilience;

using System;

/// <summary>
/// Configuration options for the retry resilience policy.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Defaults to 3.
    /// </summary>
    /// <value>The maximum retry attempts. Must be greater than or equal to 0.</value>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before the first retry attempt. Defaults to 100 milliseconds.
    /// </summary>
    /// <value>The initial delay duration. Must be non-negative.</value>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts. Defaults to 30 seconds.
    /// </summary>
    /// <value>The maximum delay duration. Must be non-negative.</value>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the backoff exponent factor. Defaults to 2.0.
    /// </summary>
    /// <value>The backoff factor. Must be greater than or equal to 1.0.</value>
    public double BackoffFactor { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether jitter (randomized delay variation) should be applied. Defaults to true.
    /// </summary>
    /// <value><c>true</c> if jitter is enabled; otherwise, <c>false</c>.</value>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Gets or sets a predicate to determine if a specific exception should trigger a retry.
    /// Defaults to retrying all exceptions (except <see cref="OperationCanceledException"/>).
    /// </summary>
    /// <value>The exception predicate.</value>
    public Func<Exception, bool> ShouldRetry { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets an optional callback invoked before a retry delay is initiated.
    /// </summary>
    /// <value>The on-retry callback containing the exception, current attempt index (1-based), and computed delay.</value>
    public Action<Exception, int, TimeSpan>? OnRetry { get; set; }
}
