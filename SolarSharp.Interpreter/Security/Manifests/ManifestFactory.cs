using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// JSON naming policy that converts property names to kebab-case
    /// </summary>
    public class JsonKebabCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        result.Append('-');
                    result.Append(char.ToLower(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// Factory for creating manifests in V2.0 format programmatically
    /// </summary>
    public static class ManifestFactory
    {
        /// <summary>
        /// Creates a minimal valid V2.0 manifest JSON string
        /// </summary>
        public static string CreateMinimalV2Manifest()
        {
            var manifest = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        packages = new Dictionary<string, object>
                        {
                            ["test-package"] = new
                            {
                                files = new Dictionary<string, string>
                                {
                                    ["test.lua"] = "sha256:placeholder"
                                },
                                metadata = new
                                {
                                    name = "Test Package",
                                    version = "1.0.0",
                                    description = "Test package"
                                }
                            }
                        },
                        policies = new object[] { }
                    }
                }
            };

            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = new JsonKebabCaseNamingPolicy()
            });
        }

        /// <summary>
        /// Creates a V2.0 manifest with specified packages
        /// </summary>
        public static string CreateV2ManifestWithPackages(params (string packageId, string[] files)[] packages)
        {
            var packageDict = new Dictionary<string, object>();
            
            foreach (var (packageId, files) in packages)
            {
                var fileDict = new Dictionary<string, string>();
                foreach (var file in files)
                {
                    fileDict[file] = $"sha256:{Guid.NewGuid():N}";
                }
                
                packageDict[packageId] = new
                {
                    files = fileDict,
                    metadata = new
                    {
                        name = $"Package {packageId}",
                        version = "1.0.0",
                        description = $"Test package {packageId}"
                    }
                };
            }

            var manifest = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        packages = packageDict,
                        policies = new object[] { }
                    }
                }
            };

            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = new JsonKebabCaseNamingPolicy()
            });
        }

        /// <summary>
        /// Creates a V2.0 manifest with policies
        /// </summary>
        public static string CreateV2ManifestWithPolicies(string packageId = "test-package")
        {
            var manifest = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        packages = new Dictionary<string, object>
                        {
                            [packageId] = new
                            {
                                files = new Dictionary<string, string>
                                {
                                    ["test.lua"] = "sha256:placeholder"
                                },
                                metadata = new
                                {
                                    name = "Test Package",
                                    version = "1.0.0",
                                    description = "Test package"
                                }
                            }
                        },
                        policies = new[]
                        {
                            new
                            {
                                packages = new[] { packageId },
                                selector = ":file",
                                maxMemory = "10MB",
                                timeout = "30s",
                                modules = new
                                {
                                    denyAll = false,
                                    modules = new[] { "IO", "OS_System" }
                                },
                                capabilities = new
                                {
                                    denyAll = true,
                                    capabilities = new[] { "FileRead" }
                                },
                                denyAll = false
                            }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = new JsonKebabCaseNamingPolicy(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Creates a large V2.0 manifest for performance testing
        /// </summary>
        public static string CreateLargeV2Manifest(int packageCount = 100, int filesPerPackage = 10)
        {
            var packages = new Dictionary<string, object>();
            
            for (int i = 0; i < packageCount; i++)
            {
                var files = new Dictionary<string, string>();
                for (int j = 0; j < filesPerPackage; j++)
                {
                    files[$"file{j}.lua"] = $"sha256:{Guid.NewGuid():N}";
                }
                
                packages[$"package{i}"] = new
                {
                    files = files,
                    metadata = new
                    {
                        name = $"Package {i}",
                        version = "1.0.0",
                        description = $"Test package {i} with {filesPerPackage} files"
                    }
                };
            }

            var manifest = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        packages = packages,
                        policies = new object[] { }
                    }
                }
            };

            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = false, // Compact for large manifests
                PropertyNamingPolicy = new JsonKebabCaseNamingPolicy()
            });
        }
    }
}