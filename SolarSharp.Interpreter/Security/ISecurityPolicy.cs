namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interface for security policy providers that can be converted to manifests
    /// Allows unified handling of SecurityConfiguration and Manifest objects
    /// </summary>
    public interface ISecurityPolicy
    {
        /// <summary>
        /// Converts this security policy to a manifest for Script initialization
        /// </summary>
        /// <returns>Manifest representing this security policy</returns>
        Manifests.Manifest ToManifest();
    }
}