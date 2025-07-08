using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for registering policy pipeline services with dependency injection
    /// </summary>
    public static class PolicyServiceExtensions
    {
        /// <summary>
        /// Register all policy pipeline services with the service collection
        /// </summary>
        public static IServiceCollection AddPolicyPipeline(this IServiceCollection services)
        {
            // Core infrastructure
            services.AddSingleton<IFileSystem, FileSystem>();

            // Validators and verifiers
            services.AddSingleton<IManifestValidator, ManifestValidator>();
            services.AddSingleton<ISignatureVerifier>(NullSignatureVerifier.Instance);

            // JSON configuration
            services.AddSingleton(CreateJsonSerializerOptions());

            // Pipeline
            services.AddTransient<IPolicyPipeline, PolicyPipeline>();

            return services;
        }

        /// <summary>
        /// Register policy pipeline services with a custom signature verifier
        /// </summary>
        public static IServiceCollection AddPolicyPipeline<TSignatureVerifier>(
            this IServiceCollection services
        )
            where TSignatureVerifier : class, ISignatureVerifier
        {
            services.AddPolicyPipeline();
            services.AddSingleton<ISignatureVerifier, TSignatureVerifier>();
            return services;
        }

        /// <summary>
        /// Register policy pipeline services for testing with in-memory file system
        /// </summary>
        public static IServiceCollection AddTestPolicyPipeline(this IServiceCollection services)
        {
            // Use FileSystem for testing (will be replaced with mock in tests)
            services.AddSingleton<IFileSystem, FileSystem>();

            services.AddSingleton<IManifestValidator, ManifestValidator>();
            services.AddSingleton<ISignatureVerifier>(NullSignatureVerifier.Instance);
            services.AddSingleton(CreateJsonSerializerOptions());
            services.AddTransient<IPolicyPipeline, PolicyPipeline>();

            return services;
        }

        private static JsonSerializerOptions CreateJsonSerializerOptions() =>
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = true,
            };
    }
}
