namespace CoreVitals.Resilience;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a resilience policy that can execute synchronous or asynchronous actions.
/// </summary>
public interface IResiliencePolicy
{
    /// <summary>
    /// Executes the specified asynchronous action within the resilience policy.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution and contains the result of the action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when execution is canceled via the <paramref name="cancellationToken"/>.</exception>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the specified asynchronous action within the resilience policy.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when execution is canceled via the <paramref name="cancellationToken"/>.</exception>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
