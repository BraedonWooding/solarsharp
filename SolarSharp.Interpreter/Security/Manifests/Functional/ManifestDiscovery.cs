using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests.Functional
{
    /// <summary>
    /// Composable manifest discovery using chain of responsibility pattern.
    /// Pure functional approach with no side effects.
    /// </summary>
    public static class ManifestDiscovery
    {
        /// <summary>
        /// Default discovery chain for most use cases.
        /// </summary>
        private static readonly ImmutableArray<IManifestDiscoveryStrategy> DefaultChain =
            ImmutableArray.Create<IManifestDiscoveryStrategy>(
                new SameDirectoryDiscovery(),      // Look in same directory as script
                new ProjectRootDiscovery(),        // Look in project root (git/hg markers)
                new ConventionBasedDiscovery()     // Look for package.json → LuaManifest.json
            );

        /// <summary>
        /// Discover manifest using the default discovery chain.
        /// Pure function - no side effects.
        /// </summary>
        public static Maybe<string> Discover(string scriptPath) =>
            DiscoverWithChain(scriptPath, DefaultChain);

        /// <summary>
        /// Discover manifest using a custom discovery chain.
        /// Pure function - no side effects.
        /// </summary>
        public static Maybe<string> DiscoverWithChain(
            string scriptPath,
            ImmutableArray<IManifestDiscoveryStrategy> chain)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                return Maybe<string>.None;

            // Try each strategy in order until one succeeds
            foreach (var strategy in chain)
            {
                var result = strategy.Discover(scriptPath);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return Maybe<string>.None;
        }

        /// <summary>
        /// Create a custom discovery chain.
        /// </summary>
        public static ImmutableArray<IManifestDiscoveryStrategy> CreateChain(
            params IManifestDiscoveryStrategy[] strategies) =>
            strategies.ToImmutableArray();

        /// <summary>
        /// Create the default discovery service implementation.
        /// </summary>
        public static IManifestDiscoveryService CreateDefault() =>
            new FunctionalManifestDiscoveryService();
    }

    /// <summary>
    /// Interface for manifest discovery strategies.
    /// Pure functional interface - no side effects.
    /// </summary>
    public interface IManifestDiscoveryStrategy
    {
        /// <summary>
        /// Discover manifest path for the given script path.
        /// Pure function - no side effects.
        /// </summary>
        Maybe<string> Discover(string scriptPath);
    }

    /// <summary>
    /// Discovery strategy that looks in the same directory as the script.
    /// </summary>
    public class SameDirectoryDiscovery : IManifestDiscoveryStrategy
    {
        private const string ManifestFileName = "LuaManifest.json";

        public Maybe<string> Discover(string scriptPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(scriptPath);
                if (string.IsNullOrEmpty(directory))
                    return Maybe<string>.None;

                var manifestPath = Path.Combine(directory, ManifestFileName);
                return File.Exists(manifestPath) 
                    ? Maybe<string>.From(manifestPath)
                    : Maybe<string>.None;
            }
            catch
            {
                return Maybe<string>.None;
            }
        }
    }

    /// <summary>
    /// Discovery strategy that looks in the project root directory.
    /// Identifies project root by looking for git/hg markers.
    /// </summary>
    public class ProjectRootDiscovery : IManifestDiscoveryStrategy
    {
        private const string ManifestFileName = "LuaManifest.json";
        
        private static readonly ImmutableArray<string> ProjectMarkers = 
            ImmutableArray.Create(".git", ".hg", ".svn", "package.json", "*.sln");

        public Maybe<string> Discover(string scriptPath)
        {
            try
            {
                var projectRoot = FindProjectRoot(scriptPath);
                if (!projectRoot.HasValue)
                    return Maybe<string>.None;

                var manifestPath = Path.Combine(projectRoot.Value, ManifestFileName);
                return File.Exists(manifestPath)
                    ? Maybe<string>.From(manifestPath)
                    : Maybe<string>.None;
            }
            catch
            {
                return Maybe<string>.None;
            }
        }

        /// <summary>
        /// Find project root by looking for project markers.
        /// </summary>
        private static Maybe<string> FindProjectRoot(string startPath)
        {
            var directory = Path.GetDirectoryName(startPath);
            
            while (!string.IsNullOrEmpty(directory))
            {
                // Check for project markers
                if (ProjectMarkers.Any(marker => 
                    marker.Contains("*") 
                        ? Directory.GetFiles(directory, marker).Any()
                        : Directory.Exists(Path.Combine(directory, marker)) || 
                          File.Exists(Path.Combine(directory, marker))))
                {
                    return Maybe<string>.From(directory);
                }

                var parent = Directory.GetParent(directory);
                if (parent == null)
                    break;
                    
                directory = parent.FullName;
            }

            return Maybe<string>.None;
        }
    }

    /// <summary>
    /// Convention-based discovery that follows common patterns.
    /// Looks for package.json → LuaManifest.json in the same directory.
    /// </summary>
    public class ConventionBasedDiscovery : IManifestDiscoveryStrategy
    {
        private const string ManifestFileName = "LuaManifest.json";
        private const string PackageJsonFileName = "package.json";

        public Maybe<string> Discover(string scriptPath)
        {
            try
            {
                // Look for package.json in script directory or parent directories
                var packageJsonPath = FindPackageJson(scriptPath);
                if (!packageJsonPath.HasValue)
                    return Maybe<string>.None;

                // Look for LuaManifest.json in the same directory as package.json
                var directory = Path.GetDirectoryName(packageJsonPath.Value);
                var manifestPath = Path.Combine(directory, ManifestFileName);
                
                return File.Exists(manifestPath)
                    ? Maybe<string>.From(manifestPath)
                    : Maybe<string>.None;
            }
            catch
            {
                return Maybe<string>.None;
            }
        }

        /// <summary>
        /// Find package.json by walking up the directory tree.
        /// </summary>
        private static Maybe<string> FindPackageJson(string startPath)
        {
            var directory = Path.GetDirectoryName(startPath);
            
            while (!string.IsNullOrEmpty(directory))
            {
                var packageJsonPath = Path.Combine(directory, PackageJsonFileName);
                if (File.Exists(packageJsonPath))
                {
                    return Maybe<string>.From(packageJsonPath);
                }

                var parent = Directory.GetParent(directory);
                if (parent == null)
                    break;
                    
                directory = parent.FullName;
            }

            return Maybe<string>.None;
        }
    }

    /// <summary>
    /// Functional implementation of IManifestDiscoveryService.
    /// Uses the functional discovery chain internally.
    /// </summary>
    public class FunctionalManifestDiscoveryService : IManifestDiscoveryService
    {
        /// <summary>
        /// Discover manifest path using functional discovery chain.
        /// </summary>
        public Maybe<string> DiscoverManifestPath(string scriptPath) =>
            ManifestDiscovery.Discover(scriptPath);
    }

    /// <summary>
    /// Extension methods for working with discovery chains functionally.
    /// </summary>
    public static class DiscoveryExtensions
    {
        /// <summary>
        /// Add a strategy to a discovery chain.
        /// </summary>
        public static ImmutableArray<IManifestDiscoveryStrategy> Add(
            this ImmutableArray<IManifestDiscoveryStrategy> chain,
            IManifestDiscoveryStrategy strategy) =>
            chain.Add(strategy);

        /// <summary>
        /// Insert a strategy at the beginning of a discovery chain.
        /// </summary>
        public static ImmutableArray<IManifestDiscoveryStrategy> Prepend(
            this ImmutableArray<IManifestDiscoveryStrategy> chain,
            IManifestDiscoveryStrategy strategy) =>
            ImmutableArray.Create(strategy).AddRange(chain);

        /// <summary>
        /// Create a discovery chain from strategies.
        /// </summary>
        public static ImmutableArray<IManifestDiscoveryStrategy> ToDiscoveryChain(
            this IManifestDiscoveryStrategy[] strategies) =>
            strategies.ToImmutableArray();
    }
}

