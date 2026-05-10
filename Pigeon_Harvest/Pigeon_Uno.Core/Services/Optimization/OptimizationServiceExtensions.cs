using Microsoft.Extensions.DependencyInjection;

namespace Pigeon_Uno.Core.Services.Optimization;

/// <summary>
/// Extension methods for registering optimization services with dependency injection.
/// </summary>
public static class OptimizationServiceExtensions
{
    /// <summary>
    /// Registers all optimization services with the service collection.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddOptimizationServices(this IServiceCollection services)
    {
        // Note: Actual implementations are registered in the main app project (App.xaml.cs)
        // This is just a placeholder extension method for consistency
        // The platform-specific registration happens in the main project
        
        return services;
    }
}
