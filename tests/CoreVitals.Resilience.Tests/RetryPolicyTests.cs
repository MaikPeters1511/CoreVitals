namespace CoreVitals.Resilience.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsResultWithoutRetrying()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            UseJitter = false
        };
        var policy = new RetryPolicy(options);
        int callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            callCount++;
            return Task.FromResult("Success");
        });

        // Assert
        result.Should().Be("Success");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailures_RetriesAndEventuallySucceeds()
    {
        // Arrange
        int retriesRegistered = 0;
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            UseJitter = false,
            OnRetry = (ex, attempt, delay) =>
            {
                retriesRegistered++;
                attempt.Should().Be(retriesRegistered);
            }
        };
        var policy = new RetryPolicy(options);
        int callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new InvalidOperationException("Transient error");
            }
            return Task.FromResult("Succeeded after errors");
        });

        // Assert
        result.Should().Be("Succeeded after errors");
        callCount.Should().Be(3);
        retriesRegistered.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxRetries_ThrowsLastException()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            UseJitter = false
        };
        var policy = new RetryPolicy(options);
        int callCount = 0;

        // Act
        Func<Task<string>> action = () => policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            throw new InvalidOperationException($"Failure {callCount}");
        });

        // Assert
        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("Failure 3"); // 1 initial try + 2 retries = 3 calls
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryReturnsFalse_DoesNotRetry()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            UseJitter = false,
            ShouldRetry = ex => ex is ArgumentNullException
        };
        var policy = new RetryPolicy(options);
        int callCount = 0;

        // Act
        Func<Task> action = () => policy.ExecuteAsync(ct =>
        {
            callCount++;
            throw new InvalidOperationException("Not an ArgumentNullException");
        });

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1); // Fails immediately, no retries
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledExceptionWithoutRetrying()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            UseJitter = false
        };
        var policy = new RetryPolicy(options);
        using var cts = new CancellationTokenSource();
        int callCount = 0;

        // Act
        Func<Task> action = () => policy.ExecuteAsync(ct =>
        {
            callCount++;
            if (callCount == 1)
            {
                cts.Cancel(); // Request cancellation during the first call
                throw new InvalidOperationException("Transient error");
            }
            return Task.CompletedTask;
        }, cts.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        callCount.Should().Be(1); // Should not proceed to retry since cancellation is requested
    }
}
