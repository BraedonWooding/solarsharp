using System.Reflection;
using SolarSharp.Interpreter.Security.Manifests.Domain;

namespace SolarSharp.Interpreter.Tests.TestHelpers
{
    public static class ManifestTestHelpers
    {
        /// <summary>
        /// Helper method to access the internal trust store of a Script instance for testing
        /// </summary>
        public static ITrustStore GetTrustStore(this Script script)
        {
            var trustStoreField = typeof(Script).GetField("_trustStore", BindingFlags.NonPublic | BindingFlags.Instance);
            return trustStoreField?.GetValue(script) as ITrustStore;
        }
    }
}