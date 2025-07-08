using System;
using System.IO;
using System.Linq;
using NuGet.Versioning;

namespace SolarSharp.Interpreter.Security.Identity
{
    /// <summary>
    /// Representation of a script's identity including name, version, and public key token
    /// </summary>
    public readonly struct ScriptIdentity : IEquatable<ScriptIdentity>
    {
        /// <summary>
        /// Name of the script (from manifest)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Semantic version of the script
        /// </summary>
        public NuGetVersion Version { get; }

        /// <summary>
        /// Minimum version for matching (optional)
        /// </summary>
        public NuGetVersion MinVersion { get; }

        /// <summary>
        /// Maximum version for matching (optional)
        /// </summary>
        public NuGetVersion MaxVersion { get; }

        /// <summary>
        /// Public key token (16 bytes from SHA256 hash)
        /// </summary>
        public byte[] PublicKeyToken { get; }

        /// <summary>
        /// Creates a new script identity
        /// </summary>
        /// <param name="name">Script name from manifest</param>
        /// <param name="version">Script version</param>
        /// <param name="publicKeyToken">16-byte public key token</param>
        /// <param name="minVersion">Minimum version for matching (optional)</param>
        /// <param name="maxVersion">Maximum version for matching (optional)</param>
        public ScriptIdentity(
            string name,
            NuGetVersion version,
            byte[] publicKeyToken,
            NuGetVersion minVersion = null,
            NuGetVersion maxVersion = null
        )
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PublicKeyToken =
                publicKeyToken ?? throw new ArgumentNullException(nameof(publicKeyToken));

            if (publicKeyToken.Length != 16)
                throw new ArgumentException(
                    "Public key token must be exactly 16 bytes",
                    nameof(publicKeyToken)
                );

            MinVersion = minVersion;
            MaxVersion = maxVersion;
        }

        /// <summary>
        /// Default identity for unsigned scripts (uses filename without extension)
        /// </summary>
        public static ScriptIdentity CreateDefault(string filename) =>
            new ScriptIdentity(
                Path.GetFileNameWithoutExtension(filename) ?? "unknown",
                new NuGetVersion(0, 0, 0),
                new byte[16]
            );

        /// <summary>
        /// Checks if this identity matches the given selector criteria
        /// </summary>
        public bool Matches(ScriptSelector selector)
        {
            if (selector == null)
                return true;

            if (selector.Name != null && Name != selector.Name)
                return false;

            if (
                selector.PublicKeyToken != null
                && !PublicKeyToken.SequenceEqual(selector.PublicKeyToken)
            )
                return false;

            if (selector.MinVersion != null && Version < selector.MinVersion)
                return false;

            if (selector.MaxVersion != null && Version > selector.MaxVersion)
                return false;

            return true;
        }

        public override string ToString()
        {
            var tokenHex =
                PublicKeyToken == null || PublicKeyToken.All(b => b == 0)
                    ? "0000000000000000"
                    : BitConverter.ToString(PublicKeyToken).Replace("-", "").ToLowerInvariant();

            var versionRange = "";
            if (MinVersion != null || MaxVersion != null)
            {
                var min = MinVersion?.ToString() ?? "*";
                var max = MaxVersion?.ToString() ?? "*";
                versionRange = $", VersionRange=[{min},{max}]";
            }

            return $"{Name}, Version={Version}, PublicKeyToken={tokenHex}{versionRange}";
        }

        public bool Equals(ScriptIdentity other)
        {
            return Name == other.Name
                && Version.Equals(other.Version)
                && PublicKeyToken.SequenceEqual(other.PublicKeyToken)
                && Equals(MinVersion, other.MinVersion)
                && Equals(MaxVersion, other.MaxVersion);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (Version?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (MinVersion?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (MaxVersion?.GetHashCode() ?? 0);

                if (PublicKeyToken != null)
                {
                    foreach (var b in PublicKeyToken)
                    {
                        hash = (hash * 397) ^ b;
                    }
                }

                return hash;
            }
        }

        public static bool operator ==(ScriptIdentity left, ScriptIdentity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScriptIdentity left, ScriptIdentity right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Selector for matching script identities
    /// </summary>
    public class ScriptSelector
    {
        /// <summary>
        /// Optional name to match
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional minimum version
        /// </summary>
        public NuGetVersion MinVersion { get; set; }

        /// <summary>
        /// Optional maximum version
        /// </summary>
        public NuGetVersion MaxVersion { get; set; }

        /// <summary>
        /// Optional public key token to match
        /// </summary>
        public byte[] PublicKeyToken { get; set; }
    }
}
