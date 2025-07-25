using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// A signature verifier that always returns success without performing actual verification.
    /// Used when signature verification is not needed or available.
    /// </summary>
    public sealed class NullSignatureVerifier : ISignatureVerifier
    {
        public static readonly NullSignatureVerifier Instance = new NullSignatureVerifier();

        private NullSignatureVerifier() { }

        public Task<Result<bool, string>> VerifySignatureAsync(Manifest manifest)
        {
            return Task.FromResult(Result.Success<bool, string>(true));
        }

        public Result<bool, string> VerifySignature(Manifest manifest)
        {
            return Result.Success<bool, string>(true);
        }
    }

    /// <summary>
    /// Interface for signature verification
    /// </summary>
    public interface ISignatureVerifier
    {
        Task<Result<bool, string>> VerifySignatureAsync(Manifest manifest);
        Result<bool, string> VerifySignature(Manifest manifest);
    }
}
