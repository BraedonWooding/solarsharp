using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interface for the policy processing pipeline
    /// </summary>
    public interface IPolicyPipeline
    {
        /// <summary>
        /// Process directories into a complete policy store
        /// </summary>
        Task<Result<PolicyStore, PipelineError>> ProcessDirectories(
            DirectorySet directories,
            ISignatureVerifier signatureVerifier,
            ManifestScanOptions options
        );

        /// <summary>
        /// Create compiled rules from a policy store
        /// </summary>
        CompiledPolicyRules CreateCompiledRules(
            PolicyStore store,
            SignaturePolicies signaturePolicies,
            PathPolicies pathPolicies,
            SecurityPolicy fallbackPolicy
        );
    }
}
