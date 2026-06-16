namespace Microsoft.Extensions.DependencyInjection;

using System;
using CoreVitals.Resilience;

/// <summary>
/// Extension methods for registering resilience services in the dependency injection container.
/// </summary>
public static class ResilienceServiceCollectionExtensions
{
    /// <summary>
    /// Adds a retry resilience policy to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the retry options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.</exception>
    public static IServiceCollection AddRetryPolicy(this IServiceCollection services, Action<RetryOptions> configureOptions)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

        var options = new RetryOptions();
        configureOptions(options);

        // Register the specific policy instance
        services.AddSingleton<IResiliencePolicy>(new RetryPolicy(options));

        return services;
    }
}
