using System;
using System.Collections.Immutable;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Fluent API for building manifests programmatically
    /// NOTE: This class needs to be updated for V2.0 manifest format with signed content blocks
    /// </summary>
    public class ManifestBuilder
    {
        private Manifest _manifest;

        /// <summary>
        /// Creates a new manifest builder
        /// </summary>
        public ManifestBuilder()
            : this(new Manifest()) { }

        /// <summary>
        /// Creates a manifest builder from an existing manifest
        /// </summary>
        public ManifestBuilder(Manifest baseManifest)
        {
            _manifest = baseManifest ?? new Manifest();
        }

        /// <summary>
        /// Builds the final manifest
        /// </summary>
        public Manifest Build() => _manifest;

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// Use the new V2.0 manifest structure directly or create a new builder for V2.0.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithDescription(string description)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithTimeoutSeconds(int seconds)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithTimeoutMs(int milliseconds)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithMemoryMB(int megabytes)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithCallDepth(int maxCallDepth)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AddFile(string path, ManifestFileEntry entry)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithVersion(string version)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AddFileRule(string pattern, FilePermissions permissions)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AddDirectoryRule(string pattern, DirectoryPermissions permissions)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithMemoryLimitMB(int megabytes)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithMaxInstructions(long maxInstructions)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AllowModule(CoreModules module)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AllowNetworkAccess(bool allow = true)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AllowEnvironmentAccess(bool allow = true)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder AllowCapability(ScriptCapabilities capability)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithAllowedHosts(params string[] hosts)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// DEPRECATED: V1.0 manifest builder methods are not compatible with V2.0 signed content format.
        /// </summary>
        [Obsolete("V1.0 manifest methods are not compatible with V2.0 signed content format")]
        public ManifestBuilder WithAllowedEnvironmentVariables(params string[] variables)
        {
            throw new NotSupportedException(
                "V1.0 manifest builder methods are not compatible with V2.0 signed content format. Please use the V2.0 manifest structure directly."
            );
        }

        /// <summary>
        /// Creates a new manifest builder for V2.0 format
        /// </summary>
        public static Manifest CreateV2Manifest(string manifestId)
        {
            return new Manifest
            {
                Version = "2.0",
                ManifestId = manifestId,
                SignedContent = ImmutableArray<SignedContentBlock>.Empty,
            };
        }
    }
}
