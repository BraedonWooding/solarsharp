using System;
using System.Collections.Immutable;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// V2.0-compatible manifest builder that works with signed-content structure
    /// </summary>
    public static class V2ManifestBuilder
    {
        /// <summary>
        /// Creates a new V2.0 manifest with a single unsigned content block
        /// </summary>
        public static Manifest CreateUnsigned(string manifestId, string packageName)
        {
            var packageMetadata = new PackageMetadata
            {
                Name = packageName,
                Version = "1.0.0",
                Description = "",
            };

            var package = new ManifestPackage
            {
                Files = ImmutableDictionary<string, string>.Empty,
                Metadata = packageMetadata,
            };

            var signedContentBlock = new SignedContentBlock
            {
                KeyId = "",
                Signature = "",
                PublicKey = "",
                Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                    packageName,
                    package
                ),
                Policies = ImmutableArray<ManifestPolicy>.Empty,
            };

            return new Manifest
            {
                Version = "2.0",
                ManifestId = manifestId,
                SignedContent = ImmutableArray.Create(signedContentBlock),
            };
        }

        /// <summary>
        /// Adds a file to a package in the manifest
        /// </summary>
        public static Manifest WithFile(
            this Manifest manifest,
            string packageName,
            string filePath,
            string hash
        )
        {
            if (manifest.Version != "2.0" || !manifest.HasSignedContent)
                throw new InvalidOperationException(
                    "Only V2.0 manifests with signed content are supported"
                );

            var updatedBlocks = manifest
                .SignedContent.Select(block =>
                {
                    if (block.Packages.TryGetValue(packageName, out var package))
                    {
                        var updatedFiles = package.Files.SetItem(filePath, hash);
                        var updatedPackage = package with { Files = updatedFiles };
                        var updatedPackages = block.Packages.SetItem(packageName, updatedPackage);
                        return block with { Packages = updatedPackages };
                    }
                    return block;
                })
                .ToImmutableArray();

            return manifest with
            {
                SignedContent = updatedBlocks,
            };
        }

        /// <summary>
        /// Adds a policy to the manifest
        /// </summary>
        public static Manifest WithPolicy(
            this Manifest manifest,
            ManifestPolicy policy
        )
        {
            if (manifest.Version != "2.0" || !manifest.HasSignedContent)
                throw new InvalidOperationException(
                    "Only V2.0 manifests with signed content are supported"
                );

            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            var updatedBlocks = manifest
                .SignedContent.Select(block =>
                {
                    var updatedPolicies = block.Policies.Add(policy);
                    return block with { Policies = updatedPolicies };
                })
                .ToImmutableArray();

            return manifest with
            {
                SignedContent = updatedBlocks,
            };
        }

        /// <summary>
        /// Adds a policy to the manifest using builder pattern
        /// </summary>
        public static Manifest WithPolicy(
            this Manifest manifest,
            Action<ManifestPolicyBuilder> configurePolicy
        )
        {
            if (configurePolicy == null)
                throw new ArgumentNullException(nameof(configurePolicy));

            var builder = ManifestPolicyBuilder.Create();
            configurePolicy(builder);
            return manifest.WithPolicy(builder.Build());
        }

        /// <summary>
        /// Sets package metadata
        /// </summary>
        public static Manifest WithPackageMetadata(
            this Manifest manifest,
            string packageName,
            string version = null,
            string description = null
        )
        {
            if (manifest.Version != "2.0" || !manifest.HasSignedContent)
                throw new InvalidOperationException(
                    "Only V2.0 manifests with signed content are supported"
                );

            var updatedBlocks = manifest
                .SignedContent.Select(block =>
                {
                    if (block.Packages.TryGetValue(packageName, out var package))
                    {
                        var updatedMetadata = package.Metadata with
                        {
                            Version = version ?? package.Metadata.Version,
                            Description = description ?? package.Metadata.Description,
                        };
                        var updatedPackage = package with { Metadata = updatedMetadata };
                        var updatedPackages = block.Packages.SetItem(packageName, updatedPackage);
                        return block with { Packages = updatedPackages };
                    }
                    return block;
                })
                .ToImmutableArray();

            return manifest with
            {
                SignedContent = updatedBlocks,
            };
        }
    }
}
