using System;
using System.IO;
using System.IO.Abstractions;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Default manifest discovery service that checks only the same directory as the script
    /// </summary>
    /// <remarks>
    /// Architecture principle: Each directory can have at most one manifest (LuaManifest.json).
    /// Manifests are only loaded from the same directory as the script file.
    /// This prevents circular references and simplifies the security model.
    /// </remarks>
    public sealed class DefaultManifestDiscoveryService : IManifestDiscoveryService
    {
        private readonly IFileSystem _fileSystem;

        public DefaultManifestDiscoveryService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public Maybe<string> DiscoverManifestPath(string scriptPath)
        {
            var scriptDir = _fileSystem.Path.GetDirectoryName(scriptPath);

            if (string.IsNullOrEmpty(scriptDir))
            {
                Console.WriteLine($"Invalid script path: {scriptPath}");
                return Maybe<string>.None;
            }

            var manifestPath = Path.Combine(scriptDir, "LuaManifest.json");
            if (_fileSystem.File.Exists(manifestPath))
            {
                Console.WriteLine($"Found manifest at: {manifestPath}");
                return Maybe<string>.From(manifestPath);
            }

            Console.WriteLine($"No manifest found in script directory: {scriptDir}");
            return Maybe<string>.None;
        }
    }
}
