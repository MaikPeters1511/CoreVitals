namespace CoreVitals.Resilience;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A resilience policy that retries failed operations with exponential backoff and jitter.
/// </summary>
public sealed class RetryPolicy : IResiliencePolicy
{
    private readonly RetryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicy"/> class with the specified options.
    /// </summary>
    /// <param name="options">The retry configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when option values are out of bounds.</exception>
    public RetryPolicy(RetryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        if (options.MaxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxRetries, "MaxRetries must be greater than or equal to 0.");
        if (options.InitialDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), options.InitialDelay, "InitialDelay must be non-negative.");
        if (options.MaxDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxDelay, "MaxDelay must be non-negative.");
        if (options.BackoffFactor < 1.0)
            throw new ArgumentOutOfRangeException(nameof(options), options.BackoffFactor, "BackoffFactor must be greater than or equal to 1.0.");
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        int attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt >= _options.MaxRetries || !_options.ShouldRetry(ex))
                {
                    throw;
                }

                attempt++;
                TimeSpan delay = BackoffHelper.CalculateDelay(attempt, _options);

                _options.OnRetry?.Invoke(ex, attempt, delay);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        int attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt >= _options.MaxRetries || !_options.ShouldRetry(ex))
                {
                    throw;
                }

                attempt++;
                TimeSpan delay = BackoffHelper.CalculateDelay(attempt, _options);

                _options.OnRetry?.Invoke(ex, attempt, delay);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
