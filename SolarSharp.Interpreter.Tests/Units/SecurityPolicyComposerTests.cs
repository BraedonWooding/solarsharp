using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Unit tests for the <see cref="SecurityPolicyComposer"/> class that validate the
    /// functionality of composing security policies under various conditions.
    /// </summary>
    [TestFixture]
    [Category("Security.Policy")]
    public class SecurityPolicyComposerTests
    {
        /// <summary>
        /// Tests the behavior of the <see cref="SecurityPolicyComposer.ComposeSecurityPolicies"/> method
        /// when provided with an empty array of security policies.
        /// </summary>
        /// <remarks>
        /// Verifies that the resulting <see cref="SecurityPolicy"/>:
        /// - Has <see cref="SecurityPolicy.AllowExecution"/> set to false.
        /// - Has <see cref="SecurityPolicy.TimeoutMs"/> equal to 0.
        /// - Has <see cref="SecurityPolicy.MaxMemoryMB"/> equal to 0.
        /// - Has <see cref="SecurityPolicy.MaxInstructions"/> equal to 0.
        /// - Has <see cref="SecurityPolicy.AllowedModules"/> set to <see cref="CoreModules.None"/>.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_EmptyArray_ReturnsEmptyPolicy()
        {
            var result = SecurityPolicyComposer.ComposeSecurityPolicies();
            Assert.Multiple(() =>
            {
                Assert.That(result.AllowExecution, Is.False);
                Assert.That(result.TimeoutMs, Is.EqualTo(0));
                Assert.That(result.MaxMemoryMB, Is.EqualTo(0));
                Assert.That(result.MaxInstructions, Is.EqualTo(0));
                Assert.That(result.AllowedModules, Is.EqualTo(CoreModules.None));
            });
        }

        /// <summary>
        /// Validates that when a null array is provided as input to the ComposeSecurityPolicies method,
        /// an empty security policy is returned with default restrictive values.
        /// </summary>
        /// <remarks>
        /// This unit test ensures the method handles null input gracefully by returning a policy
        /// without allowing any execution rights. It verifies robustness of the implementation
        /// against invalid or unexpected input.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_NullArray_ReturnsEmptyPolicy()
        {
            var result = SecurityPolicyComposer.ComposeSecurityPolicies(null);

            Assert.That(result.AllowExecution, Is.False);
        }

        /// <summary>
        /// Verifies that null security policies in the input are filtered out when composing security policies.
        /// </summary>
        /// <remarks>
        /// Ensures that the resulting composed policy excludes all null entries, processing only valid non-null policies
        /// and preserving their combined configurations.
        /// </remarks>
        /// <seealso cref="SecurityPolicy" />
        /// <seealso cref="SecurityPolicyComposer.ComposeSecurityPolicies" />    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_WithNullPolicies_FiltersThemOut()
        {
            var policy = new SecurityPolicy { TimeoutMs = 5000, AllowExecution = true };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(null, policy, null);
            Assert.Multiple(() =>
            {
                Assert.That(result.TimeoutMs, Is.EqualTo(5000));
                Assert.That(result.AllowExecution, Is.True);
            });
        }

        /// <summary>
        /// Validates that the <see cref="SecurityPolicyComposer.ComposeSecurityPolicies"/> method returns the same security policy
        /// when a single policy is provided as an input.
        /// </summary>
        /// <remarks>
        /// This test ensures that no composition logic is applied unnecessarily in cases where only one
        /// policy is present, and the method simply returns the input policy as-is. It verifies that all
        /// properties of the policy, such as timeout, memory limits, and execution permissions, remain intact.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if any of the expected properties of the returned policy do not match the original input policy.
        /// </exception>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_SinglePolicy_ReturnsSamePolicy()
        {
            var policy = new SecurityPolicy
            {
                Name = Maybe<string>.From("TestPolicy"),
                TimeoutMs = 5000,
                MaxMemoryMB = 100,
                AllowExecution = true,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy);
            Assert.Multiple(() =>
            {
                Assert.That(result.TimeoutMs, Is.EqualTo(5000));
                Assert.That(result.MaxMemoryMB, Is.EqualTo(100));
                Assert.That(result.AllowExecution, Is.True);
            });
        }

        /// <summary>
        /// Validates the composition of two security policies by ensuring the resulting policy takes the minimum
        /// numeric limits among the provided policies. The method checks that properties such as timeout, memory,
        /// instructions, and call depth in the resulting policy reflect the smallest values from the input policies.
        /// </summary>
        /// <remarks>
        /// This method is a unit test for the <c>SecurityPolicyComposer.ComposeSecurityPolicies</c> function.
        /// It ensures that when multiple policies are combined, the resulting policy correctly adopts the most
        /// restrictive numeric limits for timeout, memory usage, instruction count, and call depth.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_TakesMinimumNumericLimits()
        {
            var policy1 = new SecurityPolicy
            {
                TimeoutMs = 5000,
                MaxMemoryMB = 100,
                MaxInstructions = 10000,
                MaxCallDepth = 50,
            };

            var policy2 = new SecurityPolicy
            {
                TimeoutMs = 3000,
                MaxMemoryMB = 50,
                MaxInstructions = 5000,
                MaxCallDepth = 30,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                Assert.That(result.TimeoutMs, Is.EqualTo(3000));
                Assert.That(result.MaxMemoryMB, Is.EqualTo(50));
                Assert.That(result.MaxInstructions, Is.EqualTo(5000));
                Assert.That(result.MaxCallDepth, Is.EqualTo(30));
            });
        }

        /// <summary>
        /// Validates that the policy composition selects the minimum non-zero values
        /// among multiple security policies for numeric fields, while filtering out
        /// zero values.
        /// </summary>
        /// <remarks>
        /// This test ensures that the composed security policy adheres to the principle
        /// of selecting the smallest non-zero numeric value for policy parameters such as
        /// TimeoutMs, MaxMemoryMB, and MaxInstructions. Policies with zero values for these
        /// parameters are ignored in favor of non-zero values from other policies.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when any of the numeric fields in the composed policy do not match
        /// the expected minimum non-zero value across all input policies.
        /// </exception>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_TakesMinimumNonZeroValues()
        {
            var policy1 = new SecurityPolicy
            {
                TimeoutMs = 5000,
                MaxMemoryMB = 0, // Zero
                MaxInstructions = 10000,
            };

            var policy2 = new SecurityPolicy
            {
                TimeoutMs = 0, // Zero
                MaxMemoryMB = 50,
                MaxInstructions = 5000,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                Assert.That(result.TimeoutMs, Is.EqualTo(5000)); // Non-zero from policy1
                Assert.That(result.MaxMemoryMB, Is.EqualTo(50)); // Non-zero from policy2
                Assert.That(result.MaxInstructions, Is.EqualTo(5000)); // Minimum non-zero
            });
        }

        /// <summary>
        /// Verifies that when composing multiple security policies, boolean permission attributes
        /// follow a "false wins" strategy. In cases where one policy specifies <c>false</c> for a
        /// boolean attribute, and another specifies <c>true</c>, the resulting composed policy
        /// ensures the attribute is set to <c>false</c>.
        /// This test ensures that restrictive settings take precedence to enforce more secure behavior.
        /// </summary>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_BooleanPermissions_FalseWins()
        {
            var policy1 = new SecurityPolicy
            {
                AllowExecution = true,
                AllowNetworkAccess = true,
                AllowEnvironmentAccess = false,
                EnableChroot = false,
                PreventSignedModification = false,
            };

            var policy2 = new SecurityPolicy
            {
                AllowExecution = false,
                AllowNetworkAccess = true,
                AllowEnvironmentAccess = true,
                EnableChroot = true,
                PreventSignedModification = true,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                Assert.That(result.AllowExecution, Is.False);
                Assert.That(result.AllowNetworkAccess, Is.True);
                Assert.That(result.AllowEnvironmentAccess, Is.False);
                Assert.That(result.EnableChroot, Is.True); // More restrictive
                Assert.That(result.PreventSignedModification, Is.True); // More restrictive
            });
        }

        /// <summary>
        /// Composes security policies by calculating the intersection of the module flags
        /// allowed by two provided security policies.
        /// </summary>
        /// <returns>
        /// A new <see cref="SecurityPolicy"/> containing only the module flags that are common
        /// between both input security policies.
        /// </returns>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_ModuleFlagsIntersection()
        {
            var policy1 = new SecurityPolicy
            {
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
            };

            var policy2 = new SecurityPolicy
            {
                AllowedModules = CoreModules.Basic | CoreModules.Table | CoreModules.Math,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);

            // Should only have Basic and Math (intersection)
            Assert.That(result.AllowedModules, Is.EqualTo(CoreModules.Basic | CoreModules.Math));
        }

        /// <summary>
        /// Verifies that the intersection of capabilities between two or more provided security policies
        /// is correctly calculated by the <c>ComposeSecurityPolicies</c> method.
        /// </summary>
        /// <remarks>
        /// This method tests the capability intersection logic by combining two security policies
        /// with overlapping capabilities and ensures that the resulting policy contains only the
        /// capabilities common to both input policies.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_CapabilitiesIntersection()
        {
            var policy1 = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
            };

            var policy2 = new SecurityPolicy
            {
                Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.EnvironmentAccess,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);

            // Should only have FileRead (intersection)
            Assert.That(result.Capabilities, Is.EqualTo(ScriptCapabilities.FileRead));
        }

        /// <summary>
        /// Validates that the composition of security policies through intersecting lists (e.g., allowed hosts or environment variables)
        /// results in a composed policy containing only items present in all provided policies.
        /// </summary>
        /// <remarks>
        /// This method is used to test the correct functionality of the <see cref="SecurityPolicyComposer.ComposeSecurityPolicies"/> method
        /// when dealing with intersecting lists within security policies. For example, if multiple policies specify allowed hosts or
        /// environment variables, the composed policy should only include those that are shared across all input policies.
        /// </remarks>
        /// <test>
        /// Tests scenarios where multiple input policies contain overlapping and non-overlapping entries for allowed host lists
        /// or environment variable lists, ensuring the resulting lists in the composed policy are accurate intersections of the inputs.
        /// </test>
        /// <assertions>
        /// Validates that the intersected lists in the resulting policy only include common elements from the input policies:
        /// - Allowed hosts are correctly intersected.
        /// - Allowed environment variables are correctly intersected.
        /// </assertions>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_ListsIntersection()
        {
            var policy1 = new SecurityPolicy
            {
                AllowedHosts = ImmutableArray.Create("host1.com", "host2.com", "host3.com"),
                AllowedEnvironmentVariables = ImmutableArray.Create("PATH", "HOME", "USER"),
            };

            var policy2 = new SecurityPolicy
            {
                AllowedHosts = ImmutableArray.Create("host2.com", "host3.com", "host4.com"),
                AllowedEnvironmentVariables = ImmutableArray.Create("HOME", "USER", "TEMP"),
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                // Should only have common elements
                Assert.That(
                    result.AllowedHosts,
                    Is.EquivalentTo(new[] { "host2.com", "host3.com" })
                );
                Assert.That(
                    result.AllowedEnvironmentVariables,
                    Is.EquivalentTo(new[] { "HOME", "USER" })
                );
            });
        }

        /// <summary>
        /// Composes file permissions from multiple security policies, resolving conflicts
        /// by applying the most restrictive set of permissions for each file path.
        /// Only considers paths present in all provided policies, assigning them the most
        /// restrictive access (e.g., `FilePermissions.None` when paths are absent in some policies).
        /// </summary>
        /// <returns>
        /// A new <see cref="SecurityPolicy"/> instance containing the resolved file permissions.
        /// File paths not common to all provided policies will default to <see cref="FilePermissions.None"/>.
        /// </returns>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_FilePermissionsComposition()
        {
            var policy1 = new SecurityPolicy
            {
                FilePermissions = ImmutableDictionary<string, FilePermissions>
                    .Empty.Add("/path1", FilePermissions.ReadWrite)
                    .Add("/path2", FilePermissions.Read)
                    .Add("/path3", FilePermissions.ReadWrite),
            };

            var policy2 = new SecurityPolicy
            {
                FilePermissions = ImmutableDictionary<string, FilePermissions>
                    .Empty.Add("/path1", FilePermissions.Read) // More restrictive
                    .Add("/path2", FilePermissions.ReadWrite) // Less restrictive
                    .Add("/path4", FilePermissions.Read), // New path
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                // Should take most restrictive for each path, including paths from both
                Assert.That(result.FilePermissions["/path1"], Is.EqualTo(FilePermissions.Read));
                Assert.That(result.FilePermissions["/path2"], Is.EqualTo(FilePermissions.Read));
                Assert.That(result.FilePermissions["/path3"], Is.EqualTo(FilePermissions.None));
                Assert.That(result.FilePermissions["/path4"], Is.EqualTo(FilePermissions.None));
            });
        }

        /// <summary>
        /// Tests the intersection of token-based access permissions between security policies
        /// during composition.
        /// </summary>
        /// <remarks>
        /// This test ensures that the composed security policy contains only the common tokens
        /// for both reading and writing from the provided security policies.
        /// The intersection involves:
        /// - For "AllowReadByToken": Tokens that exist in all input policies' read permissions.
        /// - For "AllowWriteByToken": Tokens that exist in all input policies' write permissions.
        /// Expected result:
        /// - The composed policy will have "AllowReadByToken" and "AllowWriteByToken" fields
        /// representing the intersection of the tokens from the input policies.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_TokenAccessIntersection()
        {
            var policy1 = new SecurityPolicy
            {
                AllowReadByToken = ImmutableHashSet.Create("token1", "token2", "token3"),
                AllowWriteByToken = ImmutableHashSet.Create("token1", "token2"),
            };

            var policy2 = new SecurityPolicy
            {
                AllowReadByToken = ImmutableHashSet.Create("token2", "token3", "token4"),
                AllowWriteByToken = ImmutableHashSet.Create("token2", "token3"),
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                // Should only have common tokens
                Assert.That(result.AllowReadByToken, Is.EquivalentTo(new[] { "token2", "token3" }));
                Assert.That(result.AllowWriteByToken, Is.EquivalentTo(new[] { "token2" }));
            });
        }

        /// <summary>
        /// Verifies that when two security policies are composed, the resulting policy's name
        /// includes the names of both input policies.
        /// </summary>
        /// <remarks>
        /// This test ensures that the composed policy correctly combines the names
        /// of the input policies, maintaining visibility of the input components in
        /// the resulting policy. It checks that the resulting policy's name is non-null
        /// and contains substrings matching the names of the input policies.
        /// </remarks>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_ComposedNameIncludesBothNames()
        {
            var policy1 = new SecurityPolicy { Name = Maybe<string>.From("Policy1") };
            var policy2 = new SecurityPolicy { Name = Maybe<string>.From("Policy2") };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2);
            Assert.Multiple(() =>
            {
                Assert.That(result.Name.HasValue, Is.True);
                Assert.That(result.Name.Value, Does.Contain("Policy1"));
            });
            Assert.That(result.Name.Value, Does.Contain("Policy2"));
        }

        /// <summary>
        /// Validates that the method for composing three or more security policies produces
        /// the correct combined result based on the minimum timeout, execution allowance,
        /// and intersection of allowed modules from the provided policies.
        /// </summary>
        /// <remarks>
        /// This unit test evaluates the behavior of the <c>SecurityPolicyComposer.ComposeSecurityPolicies</c>
        /// method when given three policies. The test ensures:
        /// - The resulting timeout is the lowest timeout among the provided policies.
        /// - The resulting execution permission is set to <c>false</c> if any policy disallows execution.
        /// - The combined set of allowed modules is the intersection of the allowed modules from all policies.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown when the resulting combined policy does not meet expectations, including incorrect timeout,
        /// execution permission, or module intersection.
        /// </exception>    [Category("Policy.Security")]
        [Category("Security.Unit")]
        [Test]
        public void ComposeSecurityPolicies_ThreeOrMorePolicies_ComposesCorrectly()
        {
            var policy1 = new SecurityPolicy
            {
                TimeoutMs = 5000,
                AllowExecution = true,
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
            };

            var policy2 = new SecurityPolicy
            {
                TimeoutMs = 3000,
                AllowExecution = true,
                AllowedModules = CoreModules.Basic | CoreModules.String,
            };

            var policy3 = new SecurityPolicy
            {
                TimeoutMs = 1000,
                AllowExecution = false, // This should make the result false
                AllowedModules = CoreModules.Basic,
            };

            var result = SecurityPolicyComposer.ComposeSecurityPolicies(policy1, policy2, policy3);
            Assert.Multiple(() =>
            {
                Assert.That(result.TimeoutMs, Is.EqualTo(1000));
                Assert.That(result.AllowExecution, Is.False);
                Assert.That(result.AllowedModules, Is.EqualTo(CoreModules.Basic));
            });
        }
    }
}
