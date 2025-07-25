#nullable enable

using System;
using System.IO;
using System.Text.Json;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Loads and parses signed content manifests from disk
    /// </summary>
    public class SignedContentLoader
    {
        private readonly SignedContentValidator _validator;

        public SignedContentLoader(SignedContentValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>
        /// Loads a signed content manifest from a file path
        /// </summary>
        public Result<ValidatedSignedContentManifest, ManifestValidationError> LoadManifest(
            string manifestPath,
            ITrustStore trustStore
        )
        {
            if (!File.Exists(manifestPath))
            {
                return ManifestValidationError.NotFound(manifestPath);
            }

            try
            {
                var manifestJson = File.ReadAllText(manifestPath);
                return LoadManifestFromJson(manifestJson, manifestPath, trustStore);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ManifestValidationError.UnexpectedError(
                    $"Access denied reading manifest: {ex.Message}",
                    ex,
                    manifestPath
                );
            }
            catch (IOException ex)
            {
                return ManifestValidationError.UnexpectedError(
                    $"IO error reading manifest: {ex.Message}",
                    ex,
                    manifestPath
                );
            }
        }

        /// <summary>
        /// Loads a signed content manifest from JSON content
        /// </summary>
        public Result<ValidatedSignedContentManifest, ManifestValidationError> LoadManifestFromJson(
            string manifestJson,
            string manifestPath,
            ITrustStore trustStore
        )
        {
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                return ManifestValidationError.InvalidFormat(
                    "Manifest content is empty",
                    manifestPath
                );
            }

            try
            {
                // Parse the JSON into a signed content manifest
                var manifest = JsonSerializer.Deserialize<Manifest>(
                    manifestJson,
                    ManifestJsonOptions.Default
                );

                if (manifest == null)
                {
                    return ManifestValidationError.InvalidFormat(
                        "Failed to parse manifest JSON",
                        manifestPath
                    );
                }

                // Validate version
                if (manifest.Version != "2.0")
                {
                    return ManifestValidationError.InvalidFormat(
                        $"Unsupported manifest version: {manifest.Version}. Expected 2.0",
                        manifestPath
                    );
                }

                // Validate the manifest structure and signatures
                return _validator.ValidateManifest(manifest, trustStore, manifestPath);
            }
            catch (JsonException ex)
            {
                return ManifestValidationError.InvalidFormat(
                    $"Invalid JSON format: {ex.Message}",
                    manifestPath
                );
            }
            catch (Exception ex)
            {
                return ManifestValidationError.UnexpectedError(
                    $"Unexpected error parsing manifest: {ex.Message}",
                    ex,
                    manifestPath
                );
            }
        }

        /// <summary>
        /// Discovers and loads a manifest for a script file
        /// </summary>
        public Result<
            ValidatedSignedContentManifest,
            ManifestValidationError
        > DiscoverAndLoadManifest(string scriptPath, ITrustStore trustStore)
        {
            var scriptDirectory = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(scriptDirectory))
            {
                return ManifestValidationError.NotFound(
                    "LuaManifest.json",
                    $"Cannot determine directory for script: {scriptPath}"
                );
            }

            var manifestPath = Path.Combine(scriptDirectory, "LuaManifest.json");
            return LoadManifest(manifestPath, trustStore);
        }

        /// <summary>
        /// Checks if a manifest exists for a given script path
        /// </summary>
        public bool ManifestExistsForScript(string scriptPath)
        {
            var scriptDirectory = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(scriptDirectory))
            {
                return false;
            }

            var manifestPath = Path.Combine(scriptDirectory, "LuaManifest.json");
            return File.Exists(manifestPath);
        }

        /// <summary>
        /// Gets the expected manifest path for a script
        /// </summary>
        public string GetManifestPathForScript(string scriptPath)
        {
            var scriptDirectory = Path.GetDirectoryName(scriptPath) ?? "";
            return Path.Combine(scriptDirectory, "LuaManifest.json");
        }
    }
}
