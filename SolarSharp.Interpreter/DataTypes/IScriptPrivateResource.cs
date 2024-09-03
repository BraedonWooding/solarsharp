﻿using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// Common interface for all resources which are uniquely bound to a script.
    /// </summary>
    public interface IScriptPrivateResource
    {
        /// <summary>
        /// Gets the script owning this resource.
        /// </summary>
        /// <value>
        /// The script owning this resource.
        /// </value>
        Script OwnerScript { get; }
    }

    internal static class ScriptPrivateResource_Extension
    {
        public static void CheckScriptOwnership(this IScriptPrivateResource containingResource, DynValue[] values)
        {
            foreach (DynValue v in values)
                containingResource.CheckScriptOwnership(v);
        }


        public static void CheckScriptOwnership(this IScriptPrivateResource containingResource, DynValue value)
        {
            if (value.IsNotNil())
            {
                var otherResource = value.GetAsPrivateResource();

                if (otherResource != null)
                {
                    containingResource.CheckScriptOwnership(otherResource);
                }
            }
        }

        public static void CheckScriptOwnership(this IScriptPrivateResource resource, Script script)
        {
            if (resource.OwnerScript != null && resource.OwnerScript != script && script != null)
            {
                throw new ScriptRuntimeException("Attempt to access a resource owned by a script, from another script");
            }
        }

        public static void CheckScriptOwnership(this IScriptPrivateResource containingResource, IScriptPrivateResource itemResource)
        {
            if (itemResource != null)
            {
                if (containingResource.OwnerScript != null && containingResource.OwnerScript != itemResource.OwnerScript && itemResource.OwnerScript != null)
                {
                    throw new ScriptRuntimeException("Attempt to perform operations with resources owned by different scripts.");
                }
                else if (containingResource.OwnerScript == null && itemResource.OwnerScript != null)
                {
                    throw new ScriptRuntimeException("Attempt to perform operations with a script private resource on a shared resource.");
                }
            }
        }
    }

}
