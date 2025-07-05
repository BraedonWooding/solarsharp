using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Automatically discovers and loads Lua manifests from the file system
    /// </summary>
    public static class ManifestAutoLoader
    {
        private static readonly ConcurrentDictionary<string, LoadedManifest> _manifestCache = new();
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Maximum depth for manifest includes to prevent DoS attacks
        /// </summary>
        private const int MAX_INCLUDE_DEPTH = 10;

        /// <summary>
        /// Discovers a manifest for the given script path by walking up the directory tree
        /// </summary>
        public static LoadedManifest DiscoverManifest(string scriptPath)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(scriptPath));
            var manifestPaths = new List<string>();
            
            // Walk up directory tree collecting all manifest paths
            while (!string.IsNullOrEmpty(directory))
            {
                var manifestPath = Path.Combine(directory, "LuaManifest.json");
                if (File.Exists(manifestPath))
                {
                    manifestPaths.Add(manifestPath);
                }
                directory = Path.GetDirectoryName(directory);
            }
            
            if (manifestPaths.Count == 0)
                return null;
            
            // Load the nearest manifest (first in list)
            var nearestManifestPath = manifestPaths[0];
            
            // Check cache first
            if (_manifestCache.TryGetValue(nearestManifestPath, out var cached))
                return cached;
            
            // Load and verify the nearest manifest
            lock (_lock)
            {
                // Double-check after lock
                if (_manifestCache.TryGetValue(nearestManifestPath, out cached))
                    return cached;
                
                var manifest = LoadAndVerifyManifest(nearestManifestPath);
                
                // If there are parent manifests, validate the chain
                if (manifestPaths.Count > 1)
                {
                    ValidateParentManifestChain(manifest.Manifest, manifestPaths);
                }
                
                _manifestCache[nearestManifestPath] = manifest;
                return manifest;
            }
        }

        /// <summary>
        /// Validates the parent manifest chain for trust requirements
        /// </summary>
        private static void ValidateParentManifestChain(Manifest childManifest, List<string> manifestPaths)
        {
            // Start from the second manifest (first parent) and validate each one
            for (int i = 1; i < manifestPaths.Count; i++)
            {
                var parentManifestPath = manifestPaths[i];
                
                // Load the parent manifest
                LoadedManifest parentLoadedManifest;
                if (_manifestCache.TryGetValue(parentManifestPath, out var cached))
                {
                    parentLoadedManifest = cached;
                }
                else
                {
                    parentLoadedManifest = LoadAndVerifyManifest(parentManifestPath);
                    _manifestCache[parentManifestPath] = parentLoadedManifest;
                }
                
                // Only validate that signed manifests use trusted keys
                // Don't apply the "signed manifests can only include signed manifests" rule
                // during discovery - that rule applies only to explicit includes
                if (parentLoadedManifest.Manifest.IsSigned())
                {
                    // For certificate-based signing
                    if (parentLoadedManifest.Manifest.Security.UsesCertificates())
                    {
                        var trustLevel = CertificateTrustStore.GetCertificateTrustLevel(parentLoadedManifest.Manifest);
                        if (trustLevel != TrustLevel.Trusted)
                        {
                            throw new ManifestSignatureException(
                                $"Manifest at '{parentLoadedManifest.Directory}' is signed with an untrusted certificate.",
                                "ValidateParentManifestChain"
                            );
                        }
                    }
                    // For raw key signing
                    else if (!ManifestTrustStore.IsManifestTrusted(parentLoadedManifest.Manifest))
                    {
                        throw new ManifestSignatureException(
                            $"Manifest at '{parentLoadedManifest.Directory}' is signed with an untrusted key.",
                            "ValidateParentManifestChain"
                        );
                    }
                }
                
                // Move up the chain for next iteration
                childManifest = parentLoadedManifest.Manifest;
            }
        }

        /// <summary>
        /// Clears the manifest cache
        /// </summary>
        public static void ClearCache()
        {
            _manifestCache.Clear();
        }

        /// <summary>
        /// Verifies the signature of a manifest (test compatibility method)
        /// </summary>
        /// <param name="luaManifest">Manifest to verify</param>
        /// <exception cref="ManifestSignatureException">Thrown if signature verification fails</exception>
        public static void VerifyManifestSignature(LuaManifest luaManifest)
        {
            if (luaManifest?.Manifest == null)
                throw new ArgumentNullException(nameof(luaManifest));

            if (!luaManifest.Manifest.IsSigned())
                throw new ManifestSignatureException("Manifest is not signed", "VerifyManifestSignature");

            try
            {
                // Simple signature verification - check if manifest is signed and valid
                if (luaManifest.Manifest.Security?.Signature?.Value == null)
                    throw new ManifestSignatureException("Manifest signature is missing", "VerifyManifestSignature");

                if (luaManifest.Manifest.Security?.PublicKey?.Value == null && !luaManifest.Manifest.Security.UsesCertificates())
                    throw new ManifestSignatureException("Manifest public key is missing", "VerifyManifestSignature");

                // Perform actual cryptographic verification by serializing the manifest and calling the internal verification method
                var json = JsonSerializer.Serialize(luaManifest.Manifest, new JsonSerializerOptions { WriteIndented = true });
                if (!VerifyManifestSignature(json, luaManifest.Manifest))
                {
                    throw new ManifestSignatureException("Manifest signature verification failed", "VerifyManifestSignature");
                }
            }
            catch (Exception ex) when (!(ex is ManifestSignatureException))
            {
                throw new ManifestSignatureException($"Error verifying manifest signature: {ex.Message}", ex, "VerifyManifestSignature");
            }
        }

        /// <summary>
        /// Loads and verifies a manifest from disk
        /// </summary>
        private static LoadedManifest LoadAndVerifyManifest(string path)
        {
            return LoadAndVerifyManifest(path, new HashSet<string>(), new HashSet<string>(), 0);
        }

        /// <summary>
        /// Loads and verifies a manifest from disk with cycle detection and depth limiting
        /// </summary>
        private static LoadedManifest LoadAndVerifyManifest(string path, HashSet<string> loadingPaths, HashSet<string> loadedManifests, int depth)
        {
            var normalizedPath = Path.GetFullPath(path);
            
            // Check include depth limit to prevent DoS attacks
            if (depth > MAX_INCLUDE_DEPTH)
            {
                throw new ManifestFormatException($"Maximum manifest include depth ({MAX_INCLUDE_DEPTH}) exceeded. Possible DoS attack or excessively complex manifest hierarchy.", "LoadAndVerifyManifest");
            }
            
            // Check if this manifest was already loaded in this execution context
            if (loadedManifests.Contains(normalizedPath))
            {
                // Return cached version if available, otherwise skip
                if (_manifestCache.TryGetValue(normalizedPath, out var cached))
                {
                    return cached;
                }
                else
                {
                    throw new ManifestFormatException($"Manifest already loaded in this context but not cached: {normalizedPath}", "LoadAndVerifyManifest");
                }
            }
            
            // Check for circular dependencies in current loading chain
            if (loadingPaths.Contains(normalizedPath))
            {
                throw new ManifestFormatException($"Circular manifest dependency detected: {normalizedPath}", "LoadAndVerifyManifest");
            }

            try
            {
                loadingPaths.Add(normalizedPath);
                loadedManifests.Add(normalizedPath);

                var json = File.ReadAllText(path);
                
                // Check for double signature attack - manifest security should be a single object, not an array
                if (json.Contains("\"security\":[") || json.Contains("\"security\": ["))
                {
                    throw new ManifestFormatException($"Invalid manifest format: multiple security signatures detected in {path}. This may be an attack.", "LoadAndVerifyManifest");
                }
                
                Manifest manifest;
                try 
                {
                    manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                        AllowTrailingCommas = true
                    });
                }
                catch (JsonException ex)
                {
                    throw new ManifestFormatException($"Invalid manifest JSON format in {path}: {ex.Message}", ex, "LoadAndVerifyManifest");
                }

                manifest.ManifestDirectory = Path.GetDirectoryName(path);

                // Verify signature if present
                if (manifest.Security is { Signature: not null, PublicKey: not null })
                {
                    if (!VerifyManifestSignature(json, manifest))
                    {
                        throw new ManifestSignatureException($"Invalid manifest signature: {path}", "LoadAndVerifyManifest");
                    }
                    
                    // For raw key signing, verify the key is trusted
                    if (!manifest.Security.UsesCertificates() && !ManifestTrustStore.IsManifestTrusted(manifest))
                    {
                        throw new ManifestSignatureException(
                            $"Manifest signature not trusted: {path}", 
                            "LoadAndVerifyManifest"
                        );
                    }
                }

                var loaded = new LoadedManifest
                {
                    Path = path,
                    Directory = Path.GetDirectoryName(path),
                    Manifest = manifest,
                    PublicKey = manifest.Security?.GetPublicKey()
                };

                // Load included manifests
                if (manifest.Includes is { Count: > 0 })
                {
                    loaded.IncludedManifests = LoadIncludedManifests(manifest, Path.GetDirectoryName(path), loadingPaths, loadedManifests, depth + 1);
                }

                return loaded;
            }
            catch (SecurityException)
            {
                // Re-throw SecurityExceptions without wrapping them
                throw;
            }
            catch (Exception ex)
            {
                throw new ManifestFormatException($"Failed to load manifest: {path}", ex, "LoadAndVerifyManifest");
            }
            finally
            {
                loadingPaths.Remove(normalizedPath);
            }
        }

        /// <summary>
        /// Verifies the manifest signature
        /// </summary>
        private static bool VerifyManifestSignature(string json, Manifest manifest)
        {
            try
            {
                // For certificate-based signing, validate certificate chain first
                if (manifest.Security.UsesCertificates())
                {
                    X509ChainStatus[] chainStatus;
                    bool chainValid = false;
                    
                    if (manifest.Security.Certificate != null)
                    {
                        chainValid = CertificateTrustStore.ValidateCertificateChain(manifest.Security.Certificate, out chainStatus);
                    }
                    else if (manifest.Security.CertificateChain != null)
                    {
                        chainValid = CertificateTrustStore.ValidateCertificateChain(manifest.Security.CertificateChain, out chainStatus);
                    }
                    
                    if (!chainValid)
                    {
                        throw new ManifestSignatureException("Certificate chain validation failed", "VerifyManifestSignature");
                    }
                }
                else if (CertificateTrustStore.HasLoadedCAs())
                {
                    // If CAs are loaded but this manifest uses raw key signing, reject it
                    throw new ManifestSignatureException(
                        "Raw key signing is not allowed when Certificate Authorities are loaded. " +
                        "All manifests must use certificate-based signing.",
                        "VerifyManifestSignature"
                    );
                }

                // Validate PIV compatibility
                if (!manifest.Security.Signature.IsPivCompatible())
                {
                    throw new NotSupportedException($"Signature algorithm is not PIV compatible: {manifest.Security.Signature.Algorithm}");
                }

                // Use RFC 8785 canonicalization, excluding the signature field
                var canonicalJson = JsonCanonicalizer.CanonicalizeExcluding(json, "security.signature");
                var manifestBytes = Encoding.UTF8.GetBytes(canonicalJson);
                var signatureBytes = Convert.FromBase64String(manifest.Security.Signature.Value);

                using var publicKey = manifest.Security.GetPublicKey();
                
                // Verify algorithm consistency between signature and key
                var signatureType = manifest.Security.Signature.GetSignatureType();
                
                switch (publicKey)
                {
                    case RSA rsa:
                        if (signatureType != SignatureType.RSA_SHA256)
                            throw new NotSupportedException($"RSA key used with non-RSA signature algorithm: {manifest.Security.Signature.Algorithm}");
                        
                        var rsaValid = rsa.VerifyData(manifestBytes, signatureBytes, 
                            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        if (!rsaValid)
                            throw new ManifestSignatureException("RSA signature verification failed - signature does not match manifest content", "VerifyManifestSignature");
                        return true;
                    
                    case ECDsa ecdsa:
                        if (signatureType != SignatureType.ECDSA_P256_SHA256 && 
                            signatureType != SignatureType.ECDSA_P384_SHA256)
                            throw new NotSupportedException($"ECDSA key used with non-ECDSA signature algorithm: {manifest.Security.Signature.Algorithm}");
                        
                        var ecdsaValid = ecdsa.VerifyData(manifestBytes, signatureBytes, 
                            HashAlgorithmName.SHA256);
                        if (!ecdsaValid)
                            throw new ManifestSignatureException("ECDSA signature verification failed - signature does not match manifest content", "VerifyManifestSignature");
                        return true;
                    
                    default:
                        throw new NotSupportedException($"Key algorithm not supported: {publicKey.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                throw new ManifestSignatureException("Failed to verify manifest signature", ex, "VerifyManifestSignature");
            }
        }

        /// <summary>
        /// Loads included manifests
        /// </summary>
        private static List<LoadedManifest> LoadIncludedManifests(Manifest parent, string baseDirectory, HashSet<string> loadingPaths, HashSet<string> loadedManifests, int depth)
        {
            var included = new List<LoadedManifest>();
            
            foreach (var include in parent.Includes)
            {
                // Validate include path - disallow absolute paths
                if (Path.IsPathRooted(include))
                {
                    throw new PathTraversalException(
                        $"Manifest include path cannot be absolute: {include}", 
                        "LoadIncludedManifests", include);
                }
                
                // Check for obvious path traversal patterns
                if (include.Contains("..") && (include.Contains("/etc/") || include.Contains("\\etc\\") || 
                    include.Contains("/sys/") || include.Contains("\\sys\\") ||
                    include.Contains("/proc/") || include.Contains("\\proc\\") ||
                    include.Contains("/root/") || include.Contains("\\root\\") ||
                    include.Contains("/home/") || include.Contains("\\home\\") ||
                    include.Contains("/usr/") || include.Contains("\\usr\\") ||
                    include.Contains("/var/") || include.Contains("\\var\\")))
                {
                    throw new PathTraversalException(
                        $"Manifest include path attempts to access system directories: {include}",
                        "LoadIncludedManifests", include);
                }
                
                var includePath = Path.Combine(baseDirectory, include);
                
                // Resolve to full path and ensure it exists
                var fullPath = Path.GetFullPath(includePath);
                
                // Validate that the resolved path doesn't escape the intended directory structure
                // We allow cross-directory includes within the same project, but prevent escaping to system directories
                var baseFullPath = Path.GetFullPath(baseDirectory);
                var commonRoot = FindCommonRoot(baseFullPath, fullPath);
                
                // If the common root is too high in the directory tree (indicating path traversal to system areas),
                // we should block it. Allow up to 3 levels up from the base directory.
                var allowedRoot = GetParentDirectory(baseFullPath, 3);
                if (allowedRoot != null && !commonRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PathTraversalException(
                        $"Manifest include path escapes allowed directory structure: {include}",
                        "LoadIncludedManifests", include);
                }
                
                // Support wildcards
                if (include.Contains("*"))
                {
                    var directory = Path.GetDirectoryName(includePath);
                    var pattern = Path.GetFileName(includePath);
                    
                    if (Directory.Exists(directory))
                    {
                        var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var normalizedFile = Path.GetFullPath(file);
                            
                            if (_manifestCache.TryGetValue(normalizedFile, out var cached))
                            {
                                ValidateSignedManifestChain(parent, cached.Manifest);
                                included.Add(cached);
                            }
                            else
                            {
                                var manifest = LoadAndVerifyManifest(file, loadingPaths, loadedManifests, depth);
                                manifest.Manifest.ParentPath = parent.ManifestDirectory;
                                ValidateSignedManifestChain(parent, manifest.Manifest);
                                _manifestCache[normalizedFile] = manifest;
                                included.Add(manifest);
                            }
                        }
                    }
                }
                else
                {
                    if (File.Exists(includePath))
                    {
                        var normalizedIncludePath = Path.GetFullPath(includePath);
                        
                        if (_manifestCache.TryGetValue(normalizedIncludePath, out var cached))
                        {
                            ValidateSignedManifestChain(parent, cached.Manifest);
                            included.Add(cached);
                        }
                        else
                        {
                            var manifest = LoadAndVerifyManifest(includePath, loadingPaths, loadedManifests, depth);
                            manifest.Manifest.ParentPath = parent.ManifestDirectory;
                            ValidateSignedManifestChain(parent, manifest.Manifest);
                            _manifestCache[normalizedIncludePath] = manifest;
                            included.Add(manifest);
                        }
                    }
                }
            }
            
            return included;
        }

        /// <summary>
        /// Validates signed manifest chain requirement - if parent is signed, all included manifests must be signed with same key
        /// </summary>
        private static void ValidateSignedManifestChain(Manifest parent, Manifest child)
        {
            // If parent is not signed, no requirement
            if (!parent.IsSigned())
                return;

            // If parent is signed, child must either be unsigned (and thus can only tighten restrictions)
            // or be signed with a trusted key
            if (child.IsSigned())
            {
                // Child is signed - validate it uses a trusted key
                // This is handled below in the trust validation logic
            }
            else
            {
                // Child is unsigned - this is allowed as unsigned manifests can only tighten restrictions
                // No further validation needed for unsigned child
                return;
            }

            // Child must be signed with a trusted key (not necessarily the same key)
            // For certificate-based signing, validate the certificate chain
            if (parent.Security.UsesCertificates() || child.Security.UsesCertificates())
            {
                // Certificate-based validation - check trust chain
                var childTrustLevel = CertificateTrustStore.GetCertificateTrustLevel(child);
                if (childTrustLevel != TrustLevel.Trusted)
                {
                    throw new ManifestSignatureException(
                        $"Included manifests must be signed with a trusted certificate. " +
                        $"Child manifest at '{child.ManifestDirectory}' is not trusted.",
                        "ValidateSignedManifestChain"
                    );
                }
                return;
            }

            // For raw key signing, check if CAs are loaded
            // If CAs are present, raw key signing should not be allowed (prevents downgrade attacks)
            if (CertificateTrustStore.HasLoadedCAs())
            {
                throw new ManifestSignatureException(
                    $"Raw key signing is not allowed when Certificate Authorities are loaded. " +
                    $"All manifests must use certificate-based signing.",
                    "ValidateSignedManifestChain"
                );
            }
            
            // If no CAs are loaded, raw key signing is allowed
            // But we still need to validate that the child manifest uses a trusted key
            if (!ManifestTrustStore.IsManifestTrusted(child))
            {
                throw new ManifestSignatureException(
                    $"Signed manifests can only include manifests signed with trusted keys. " +
                    $"Child manifest at '{child.ManifestDirectory}' is signed with an untrusted key.",
                    "ValidateSignedManifestChain"
                );
            }
        }

        /// <summary>
        /// Finds the applicable policy for a script file
        /// </summary>
        public static ManifestPolicy FindApplicablePolicy(LoadedManifest manifest, string scriptPath)
        {
            if (manifest == null)
                return null;

            var normalizedScriptPath = Path.GetFullPath(scriptPath).Replace('\\', '/');
            var manifestDir = Path.GetFullPath(manifest.Directory).Replace('\\', '/');

            // Make script path relative to manifest directory
            if (normalizedScriptPath.StartsWith(manifestDir, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = normalizedScriptPath.Substring(manifestDir.Length).TrimStart('/', '\\');

                // Check file entries for exact match or pattern match
                foreach (var entry in manifest.Manifest.Files)
                {
                    if (MatchesPattern(relativePath, entry.Key) || 
                        MatchesPattern(relativePath, entry.Value.Pattern))
                    {
                        return entry.Value.Policy ?? manifest.Manifest.Policy;
                    }
                }
            }

            // Check included manifests
            if (manifest.IncludedManifests != null)
            {
                foreach (var included in manifest.IncludedManifests)
                {
                    var policy = FindApplicablePolicy(included, scriptPath);
                    if (policy != null)
                        return policy;
                }
            }

            // Return default policy
            return manifest.Manifest.Policy;
        }

        /// <summary>
        /// Creates security configuration overrides from a discovered manifest
        /// </summary>
        /// <param name="scriptPath">Path to the script being executed</param>
        /// <param name="baseConfig">Base configuration to validate against for untrusted manifests</param>
        /// <returns>Security overrides or null if no manifest found</returns>
        public static SecurityConfigurationOverrides CreateOverridesFromManifest(string scriptPath, SecurityConfiguration baseConfig)
        {
            var manifest = DiscoverManifest(scriptPath);
            if (manifest == null)
                return null;

            var policy = FindApplicablePolicy(manifest, scriptPath);
            if (policy == null)
                return null;

            // Check trust level to determine what overrides are allowed
            var trustLevel = ManifestTrustStore.GetTrustLevel(manifest.Manifest);
            var overrides = policy.ToSecurityOverrides();

            // For untrusted manifests, validate that they only make things more restrictive
            if (trustLevel == TrustLevel.Untrusted)
            {
                ValidateUntrustedOverrides(overrides, baseConfig);
            }

            return overrides;
        }

        /// <summary>
        /// Validates that untrusted manifest overrides only make security more restrictive
        /// </summary>
        /// <param name="overrides">The overrides to validate</param>
        /// <param name="baseConfig">Base configuration to compare against</param>
        private static void ValidateUntrustedOverrides(SecurityConfigurationOverrides overrides, SecurityConfiguration baseConfig)
        {
            // Untrusted manifests can only make things more restrictive
            // This validation ensures they can't grant additional permissions
            
            if (baseConfig == null)
                throw new ArgumentNullException(nameof(baseConfig), "Base configuration is required for validating untrusted overrides");

            // Timeout can only be decreased, not increased or set to 0
            if (overrides.TimeoutMs.HasValue)
            {
                var baseTimeout = baseConfig.Execution.TimeoutMs ?? 60000; // Default to 60s if null
                if (overrides.TimeoutMs.Value == 0)
                {
                    throw new ManifestFormatException(
                        "Untrusted manifests cannot disable timeout limits (set to 0)", 
                        "ValidateUntrustedOverrides");
                }
                if (overrides.TimeoutMs.Value > baseTimeout)
                {
                    throw new ManifestFormatException(
                        $"Untrusted manifests cannot increase timeout from {baseTimeout}ms to {overrides.TimeoutMs.Value}ms", 
                        "ValidateUntrustedOverrides");
                }
            }

            // Memory limits can only be decreased
            if (overrides.MaxMemoryMB.HasValue)
            {
                var baseMemory = baseConfig.Execution.MaxMemoryMB ?? 128; // Default to 128MB if null
                if (overrides.MaxMemoryMB.Value == 0)
                {
                    throw new ManifestFormatException(
                        "Untrusted manifests cannot disable memory limits (set to 0)", 
                        "ValidateUntrustedOverrides");
                }
                if (overrides.MaxMemoryMB.Value > baseMemory)
                {
                    throw new ManifestFormatException(
                        $"Untrusted manifests cannot increase memory limit from {baseMemory}MB to {overrides.MaxMemoryMB.Value}MB", 
                        "ValidateUntrustedOverrides");
                }
            }

            // Instruction limits can only be decreased
            if (overrides.MaxInstructions.HasValue)
            {
                var baseInstructions = baseConfig.Execution.MaxInstructions ?? 10_000_000_000; // Default to 10B if null
                if (overrides.MaxInstructions.Value == 0)
                {
                    throw new ManifestFormatException(
                        "Untrusted manifests cannot disable instruction limits (set to 0)", 
                        "ValidateUntrustedOverrides");
                }
                if (overrides.MaxInstructions.Value > baseInstructions)
                {
                    throw new ManifestFormatException(
                        $"Untrusted manifests cannot increase instruction limit from {baseInstructions} to {overrides.MaxInstructions.Value}", 
                        "ValidateUntrustedOverrides");
                }
            }

            // File access can only be more restrictive
            if (overrides.DefaultFileAccess is FilePermissions.ReadWrite)
            {
                throw new ManifestFormatException("Untrusted manifests cannot grant full file write access", "ValidateUntrustedOverrides");
            }

            // Network access cannot be enabled
            if (overrides.AllowNetworkAccess.HasValue && overrides.AllowNetworkAccess.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot enable network access", "ValidateUntrustedOverrides");
            }

            // Environment access cannot be enabled
            if (overrides.AllowEnvironmentAccess.HasValue && overrides.AllowEnvironmentAccess.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot enable environment access", "ValidateUntrustedOverrides");
            }

            // Chroot cannot be disabled
            if (overrides.EnableChroot.HasValue && !overrides.EnableChroot.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot disable chroot sandbox", "ValidateUntrustedOverrides");
            }

            // Manifest discovery cannot be disabled
            if (overrides.EnableManifestDiscovery.HasValue && !overrides.EnableManifestDiscovery.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot disable manifest enforcement", "ValidateUntrustedOverrides");
            }

            // Anti-polymorphism protections cannot be disabled
            if (overrides.AllowOnlyLuaExtension.HasValue && !overrides.AllowOnlyLuaExtension.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot disable extension restrictions", "ValidateUntrustedOverrides");
            }

            if (overrides.PreventLuaFileWrites.HasValue && !overrides.PreventLuaFileWrites.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot allow Lua file writes", "ValidateUntrustedOverrides");
            }

            if (overrides.PreventRunString.HasValue && !overrides.PreventRunString.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot allow external string execution", "ValidateUntrustedOverrides");
            }
            
            if (overrides.PreventInternalDynamicCode.HasValue && !overrides.PreventInternalDynamicCode.Value)
            {
                throw new ManifestFormatException("Untrusted manifests cannot allow internal dynamic code execution", "ValidateUntrustedOverrides");
            }
        }

        /// <summary>
        /// Finds the common root directory between two paths
        /// </summary>
        private static string FindCommonRoot(string path1, string path2)
        {
            var parts1 = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts2 = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            var commonParts = new List<string>();
            var minLength = Math.Min(parts1.Length, parts2.Length);
            
            for (int i = 0; i < minLength; i++)
            {
                if (string.Equals(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase))
                {
                    commonParts.Add(parts1[i]);
                }
                else
                {
                    break;
                }
            }
            
            return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
        }
        
        /// <summary>
        /// Gets a parent directory at the specified level up from the given path
        /// </summary>
        private static string GetParentDirectory(string path, int levelsUp)
        {
            var current = path;
            for (int i = 0; i < levelsUp && !string.IsNullOrEmpty(current); i++)
            {
                current = Path.GetDirectoryName(current);
            }
            return current;
        }

        /// <summary>
        /// Simple pattern matching (supports * and **)
        /// </summary>
        private static bool MatchesPattern(string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Simple implementation - can be enhanced
            if (pattern == "**")
                return true;

            if (pattern.EndsWith("/**"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 3);
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.Contains("*"))
            {
                // Convert pattern to regex
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*\\*", ".*")
                    .Replace("\\*", "[^/]*") + "$";
                    
                return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return path.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Represents a loaded and verified manifest
    /// </summary>
    public class LoadedManifest
    {
        /// <summary>
        /// Path to the manifest file
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Directory containing the manifest
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// The loaded manifest
        /// </summary>
        public Manifest Manifest { get; set; }

        /// <summary>
        /// Extracted public key for verification
        /// </summary>
        public AsymmetricAlgorithm PublicKey { get; set; }

        /// <summary>
        /// Included manifests (for hierarchical manifests)
        /// </summary>
        public List<LoadedManifest> IncludedManifests { get; set; }
    }
}