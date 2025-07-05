namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for AntiPolymorphismPolicy to provide fluent configuration API
    /// </summary>
    public static class AntiPolymorphismPolicyExtensions
    {
        /// <summary>
        /// Configures the policy to only allow .lua file execution
        /// </summary>
        /// <param name="policy">Anti-polymorphism policy</param>
        /// <returns>The same policy instance for fluent chaining</returns>
        public static AntiPolymorphismPolicy AllowOnlyLuaExtension(this AntiPolymorphismPolicy policy)
        {
            policy.AllowOnlyLuaExtension = true;
            return policy;
        }

        /// <summary>
        /// Configures the policy to prevent writing to .lua files
        /// </summary>
        /// <param name="policy">Anti-polymorphism policy</param>
        /// <returns>The same policy instance for fluent chaining</returns>
        public static AntiPolymorphismPolicy PreventLuaFileWrites(this AntiPolymorphismPolicy policy)
        {
            policy.PreventLuaFileWrites = true;
            return policy;
        }

        /// <summary>
        /// Configures the policy to block access to manifest files
        /// </summary>
        /// <param name="policy">Anti-polymorphism policy</param>
        /// <returns>The same policy instance for fluent chaining</returns>
        public static AntiPolymorphismPolicy BlockManifestAccess(this AntiPolymorphismPolicy policy)
        {
            policy.BlockManifestAccess = true;
            return policy;
        }

        /// <summary>
        /// Configures the policy to prevent dynamic code execution (both internal and external)
        /// </summary>
        /// <param name="policy">Anti-polymorphism policy</param>
        /// <returns>The same policy instance for fluent chaining</returns>
        public static AntiPolymorphismPolicy PreventDynamicCode(this AntiPolymorphismPolicy policy)
        {
            policy.PreventRunString = true;
            policy.PreventInternalDynamicCode = true;
            return policy;
        }

        /// <summary>
        /// Configures the policy to require signed scripts
        /// </summary>
        /// <param name="policy">Anti-polymorphism policy</param>
        /// <returns>The same policy instance for fluent chaining</returns>
        public static AntiPolymorphismPolicy RequireSignedScripts(this AntiPolymorphismPolicy policy)
        {
            policy.RequireSignedScripts = true;
            return policy;
        }
    }
}