using DataControls.Core.Abstractions;
using DataControls.Core.Ai;
using DataControls.Infrastructure.Ai;
using DataControls.Infrastructure.Controls;
using DataControls.Infrastructure.Data;
using DataControls.Infrastructure.Metadata;
using DataControls.Infrastructure.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataControls.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers every SQL Server implementation of the Core interfaces. The only place that "knows" there is SQL Server.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.Section))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Database:ConnectionString is required.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<IMetadataProvider, SqlMetadataProvider>();
        services.AddScoped<IControlRepository, SqlControlRepository>();
        services.AddScoped<IControlRunRepository, SqlControlRunRepository>();
        services.AddScoped<IControlQueryExecutor, SqlControlQueryExecutor>();

        // AI: a typed HttpClient to the Python service. To replace the model - change the AI service, not this code.
        var ai = configuration.GetSection(AiServiceOptions.Section).Get<AiServiceOptions>() ?? new AiServiceOptions();
        services.AddHttpClient<IAiServiceClient, HttpAiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(ai.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });
        return services;
    }
}
