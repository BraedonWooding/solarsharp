using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Scans directories recursively for manifest files and loads them
    /// </summary>
    public class ManifestScanner
    {
        private readonly IFileSystem _fileSystem;
        private readonly ManifestValidator _validator;

        /// <summary>
        /// Default manifest filenames to search for
        /// </summary>
        public static readonly string[] DefaultManifestNames =
        {
            "Manifest.json",
            "manifest.json",
            ".luamanifest.json",
            "solarsharp.manifest.json",
        };

        public ManifestScanner(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _validator = new ManifestValidator();
        }

        /// <summary>
        /// Recursively scans a directory for manifest files
        /// </summary>
        /// <param name="rootPath">Root directory to start scanning from</param>
        /// <param name="options">Scanning options</param>
        /// <returns>Collection of loaded manifests with their locations</returns>
        public async Task<IReadOnlyList<ManifestScanResult>> ScanDirectoryAsync(
            string rootPath,
            ManifestScanOptions options = null
        )
        {
            options ??= ManifestScanOptions.Default;
            var results = new List<ManifestScanResult>();

            await ScanDirectoryRecursiveAsync(rootPath, results, options, 0);

            return results;
        }

        /// <summary>
        /// Synchronous version of directory scanning
        /// </summary>
        public IReadOnlyList<ManifestScanResult> ScanDirectory(
            string rootPath,
            ManifestScanOptions options = null
        )
        {
            return ScanDirectoryAsync(rootPath, options).GetAwaiter().GetResult();
        }

        private async Task ScanDirectoryRecursiveAsync(
            string directoryPath,
            List<ManifestScanResult> results,
            ManifestScanOptions options,
            int depth
        )
        {
            // Check depth limit
            if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
                return;

            // Check if directory should be excluded
            if (ShouldExcludeDirectory(directoryPath, options))
                return;

            try
            {
                // Look for manifest files in current directory
                foreach (var manifestName in options.ManifestFileNames)
                {
                    var manifestPath = Path.Combine(directoryPath, manifestName);

                    if (_fileSystem.File.Exists(manifestPath))
                    {
                        var result = await TryLoadManifestAsync(manifestPath, options);
                        if (result != null)
                        {
                            results.Add(result);

                            // If StopOnFirstMatch is true, don't look for more manifests in this directory
                            if (options.StopOnFirstMatch)
                                break;
                        }
                    }
                }

                // Recursively scan subdirectories
                if (
                    !options.StopAtManifest
                    || !results.Any(r => Path.GetDirectoryName(r.FilePath) == directoryPath)
                )
                {
                    var subdirectories = _fileSystem.Directory.GetDirectories(directoryPath);

                    foreach (var subdirectory in subdirectories)
                    {
                        await ScanDirectoryRecursiveAsync(
                            subdirectory,
                            results,
                            options,
                            depth + 1
                        );
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (options.IncludeErrors)
                {
                    results.Add(
                        new ManifestScanResult
                        {
                            FilePath = directoryPath,
                            Error = new ManifestLoadError
                            {
                                ErrorType = ManifestErrorType.AccessDenied,
                                Message = $"Access denied to directory: {ex.Message}",
                            },
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                if (options.IncludeErrors)
                {
                    results.Add(
                        new ManifestScanResult
                        {
                            FilePath = directoryPath,
                            Error = new ManifestLoadError
                            {
                                ErrorType = ManifestErrorType.Unknown,
                                Message = $"Error scanning directory: {ex.Message}",
                            },
                        }
                    );
                }
            }
        }

        private async Task<ManifestScanResult> TryLoadManifestAsync(
            string manifestPath,
            ManifestScanOptions options
        )
        {
            try
            {
                // Read manifest file
                var content = await _fileSystem.File.ReadAllTextAsync(manifestPath);

                // Parse JSON
                var manifest = JsonSerializer.Deserialize<Manifest>(
                    content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    }
                );

                if (manifest == null)
                {
                    return new ManifestScanResult
                    {
                        FilePath = manifestPath,
                        Error = new ManifestLoadError
                        {
                            ErrorType = ManifestErrorType.InvalidFormat,
                            Message = "Manifest deserialized to null",
                        },
                    };
                }

                // Note: Directory path is tracked separately in the scanning result

                // Check signature requirement
                if (options.RequireSignature)
                {
                    if (!manifest.IsSigned())
                    {
                        return new ManifestScanResult
                        {
                            FilePath = manifestPath,
                            // Don't set Manifest when there's an error
                            Error = new ManifestLoadError
                            {
                                ErrorType = ManifestErrorType.NotSigned,
                                Message = "Manifest is not signed but signature is required",
                            },
                        };
                    }
                }

                // Validate if requested
                if (options.ValidateManifests)
                {
                    var validationResult = _validator.Validate(manifest);
                    if (!validationResult.IsValid)
                    {
                        return new ManifestScanResult
                        {
                            FilePath = manifestPath,
                            // Don't set Manifest when there's an error
                            ValidationErrors = validationResult.Errors,
                            Error = new ManifestLoadError
                            {
                                ErrorType = ManifestErrorType.ValidationFailed,
                                Message = string.Join("; ", validationResult.Errors),
                            },
                        };
                    }
                }

                // Success
                return new ManifestScanResult
                {
                    FilePath = manifestPath,
                    Manifest = manifest,
                    DirectoryPath = Path.GetDirectoryName(manifestPath),
                    RelativePath = string.IsNullOrEmpty(options.RootPath)
                        ? manifestPath
                        : Path.GetRelativePath(options.RootPath, manifestPath),
                };
            }
            catch (JsonException ex)
            {
                return new ManifestScanResult
                {
                    FilePath = manifestPath,
                    Error = new ManifestLoadError
                    {
                        ErrorType = ManifestErrorType.InvalidFormat,
                        Message = $"JSON parsing error: {ex.Message}",
                        InnerException = ex,
                    },
                };
            }
            catch (Exception ex)
            {
                return new ManifestScanResult
                {
                    FilePath = manifestPath,
                    Error = new ManifestLoadError
                    {
                        ErrorType = ManifestErrorType.Unknown,
                        Message = $"Error loading manifest: {ex.Message}",
                        InnerException = ex,
                    },
                };
            }
        }

        private bool ShouldExcludeDirectory(string directoryPath, ManifestScanOptions options)
        {
            var dirName = Path.GetFileName(directoryPath);

            // Check excluded directory names
            if (options.ExcludedDirectories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                return true;

            // Check excluded patterns
            foreach (var pattern in options.ExcludedPatterns)
            {
                if (MatchesPattern(directoryPath, pattern))
                    return true;
            }

            return false;
        }

        private bool MatchesPattern(string path, string pattern)
        {
            // Simple pattern matching - in production use Microsoft.Extensions.FileSystemGlobbing
            if (pattern.StartsWith("**/"))
            {
                return path.Contains(pattern.Substring(3).Replace("*", ""));
            }

            if (pattern.Contains("*"))
            {
                var parts = pattern.Split('*');
                return path.StartsWith(parts[0]) && path.EndsWith(parts[1]);
            }

            return path.Contains(pattern);
        }
    }

    /// <summary>
    /// Options for manifest scanning
    /// </summary>
    public class ManifestScanOptions
    {
        /// <summary>
        /// Manifest filenames to search for
        /// </summary>
        public string[] ManifestFileNames { get; set; } = ManifestScanner.DefaultManifestNames;

        /// <summary>
        /// Maximum directory depth to scan (null = unlimited)
        /// </summary>
        public int? MaxDepth { get; set; }

        /// <summary>
        /// Stop scanning subdirectories when a manifest is found
        /// </summary>
        public bool StopAtManifest { get; set; }

        /// <summary>
        /// Stop looking for more manifests in a directory after finding one
        /// </summary>
        public bool StopOnFirstMatch { get; set; } = true;

        /// <summary>
        /// Directory names to exclude from scanning
        /// </summary>
        public HashSet<string> ExcludedDirectories { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                ".svn",
                ".hg",
                "node_modules",
                "bin",
                "obj",
                ".vs",
                ".idea",
            };

        /// <summary>
        /// Path patterns to exclude
        /// </summary>
        public List<string> ExcludedPatterns { get; set; } =
            new List<string> { "**/node_modules/**", "**/bin/**", "**/obj/**" };

        /// <summary>
        /// Validate manifests after loading
        /// </summary>
        public bool ValidateManifests { get; set; } = true;

        /// <summary>
        /// Require manifests to be signed
        /// </summary>
        public bool RequireSignature { get; set; }

        /// <summary>
        /// Verify manifest signatures
        /// </summary>
        public bool VerifySignatures { get; set; }

        /// <summary>
        /// Include error results in output
        /// </summary>
        public bool IncludeErrors { get; set; } = true;

        /// <summary>
        /// Root path for calculating relative paths
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// Default options for typical use
        /// </summary>
        public static ManifestScanOptions Default
        {
            get { return new ManifestScanOptions(); }
        }

        /// <summary>
        /// Strict options requiring signed manifests
        /// </summary>
        public static ManifestScanOptions Strict
        {
            get
            {
                return new ManifestScanOptions
                {
                    RequireSignature = true,
                    VerifySignatures = true,
                    ValidateManifests = true,
                };
            }
        }

        /// <summary>
        /// Fast options for quick scanning
        /// </summary>
        public static ManifestScanOptions Fast
        {
            get
            {
                return new ManifestScanOptions
                {
                    ValidateManifests = false,
                    VerifySignatures = false,
                    StopOnFirstMatch = true,
                    StopAtManifest = true,
                };
            }
        }
    }

    /// <summary>
    /// Result of scanning for a manifest
    /// </summary>
    public class ManifestScanResult
    {
        /// <summary>
        /// Full path to the manifest file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Directory containing the manifest
        /// </summary>
        public string DirectoryPath { get; set; }

        /// <summary>
        /// Relative path from scan root
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Loaded manifest (null if error)
        /// </summary>
        public Manifest Manifest { get; set; }

        /// <summary>
        /// Error information if loading failed
        /// </summary>
        public ManifestLoadError Error { get; set; }

        /// <summary>
        /// Validation errors if validation was performed
        /// </summary>
        public List<string> ValidationErrors { get; set; }

        /// <summary>
        /// Whether the manifest loaded successfully
        /// </summary>
        public bool IsSuccess
        {
            get { return Manifest != null && Error == null; }
        }

        /// <summary>
        /// Get a descriptive string for this result
        /// </summary>
        public override string ToString()
        {
            if (IsSuccess)
            {
                var firstPackage = Manifest.GetAllPackages().FirstOrDefault();
                var name = firstPackage.Package?.Metadata?.Name ?? "Unknown";
                var version = firstPackage.Package?.Metadata?.Version ?? "1.0";
                return $"Success: {RelativePath ?? FilePath} [{name} v{version}]";
            }
            return $"Failed: {RelativePath ?? FilePath} - {Error?.Message}";
        }
    }

    /// <summary>
    /// Error information for manifest loading
    /// </summary>
    public class ManifestLoadError
    {
        public ManifestErrorType ErrorType { get; set; }
        public string Message { get; set; }
        public Exception InnerException { get; set; }
    }

    /// <summary>
    /// Types of manifest loading errors
    /// </summary>
    public enum ManifestErrorType
    {
        None,
        FileNotFound,
        AccessDenied,
        InvalidFormat,
        ValidationFailed,
        NotSigned,
        InvalidSignature,
        Unknown,
    }
}
