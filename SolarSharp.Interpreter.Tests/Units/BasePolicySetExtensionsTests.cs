using System;
using System.Linq;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    [Category("Security.Policy")]
    public class BasePolicySetExtensionsTests
    {
        /// <summary>
        /// Validates that the `WithEvalAllowed` method correctly adds the `:eval` pattern with execution permissions
        /// to all policies within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that calling the `WithEvalAllowed` extension method on a `BasePolicySet` instance produces a non-null
        /// updated `BasePolicySet` and verifies that the derived `PolicySet` includes the `:eval` pattern mapped to a policy
        /// allowing execution. The method guarantees that the specified execution permission is properly configured in the resulting policy definitions.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the resulting `BasePolicySet` is null, if the `:eval` pattern is not present in the file policies,
        /// or if the corresponding security policy does not permit execution.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithEvalAllowed_AddsEvalPattern()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;

            var result = basePolicySet.WithEvalAllowed();

            Assert.That(result, Is.Not.Null);

            // Check that :eval pattern is mapped
            var policySet = result.PolicySet;
            Assert.That(policySet.FilePolicies.ContainsKey(":eval"), Is.True);

            // Verify the eval policy allows execution
            var evalPolicyName = policySet.FilePolicies[":eval"];
            var evalPolicy = policySet.PolicyDefinitions[evalPolicyName];
            Assert.That(evalPolicy.AllowExecution, Is.True);
        }

        /// <summary>
        /// Validates that the `WithFileRead` method correctly adds read permissions for a specified file pattern
        /// to all policies within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that invoking `WithFileRead` on a `BasePolicySet` instance results in a non-null updated
        /// `BasePolicySet` and confirms that every policy in the derived `PolicySet` includes the specified file pattern
        /// with read permissions. The method thereby guarantees that read access is consistently applied across all policies.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Raised if the resultant `BasePolicySet` is null or if any policy in the derived `PolicySet` fails to include
        /// the specified read permission for the provided file pattern.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithFileRead_AddsReadPermission()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            const string pattern = "*.txt";

            var result = basePolicySet.WithFileRead(pattern);

            Assert.That(result, Is.Not.Null);

            // Verify all policies have read permission for the pattern
            foreach (var policy in result.PolicySet.PolicyDefinitions.Values)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(policy.FilePermissions.ContainsKey(pattern), Is.True);
                    Assert.That(policy.FilePermissions[pattern], Is.EqualTo(FilePermissions.Read));
                });
            }
        }

        /// <summary>
        /// Validates that the `WithFileWrite` method correctly adds write permissions for a specified file pattern
        /// to all policies within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that invoking `WithFileWrite` on a `BasePolicySet` instance produces a non-null result
        /// and that every policy in the resultant `PolicySet` includes the specified file pattern with write permissions.
        /// It verifies that the write access is consistently enabled across all policies.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the resultant `BasePolicySet` is null or if any policy fails to have the specified write
        /// permission associated with the provided file pattern.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithFileWrite_AddsWritePermission()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            const string pattern = "*.log";

            var result = basePolicySet.WithFileWrite(pattern);

            Assert.That(result, Is.Not.Null);

            // Verify all policies have write permission for the pattern
            foreach (var policy in result.PolicySet.PolicyDefinitions.Values)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(policy.FilePermissions.ContainsKey(pattern), Is.True);
                    Assert.That(
                        policy.FilePermissions[pattern],
                        Is.EqualTo(FilePermissions.ReadWrite)
                    );
                });
            }
        }

        /// <summary>
        /// Verifies that the `WithModule` method correctly incorporates the specified module into all policies
        /// within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that calling `WithModule` on a `BasePolicySet` instance results in a non-null return value
        /// and that all policy definitions within the updated `PolicySet` include the specified module
        /// in their allowed modules set. It validates that the module is successfully enabled for every policy
        /// within the set.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the resultant `BasePolicySet` is null or if any policy fails to have the specified module
        /// marked as enabled in its allowed modules set.
        /// </exception>
        [Category("Module.Unit")]
        [Test]
        public void WithModule_AddsModule()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            const CoreModules module = CoreModules.IO;

            var result = basePolicySet.WithModule(module);

            Assert.That(result, Is.Not.Null);

            // Verify all policies have the module enabled
            foreach (var policy in result.PolicySet.PolicyDefinitions.Values)
            {
                Assert.That(policy.AllowedModules.HasFlag(module), Is.True);
            }
        }

        /// <summary>
        /// Validates that the `WithTimeout` method correctly applies the specified timeout value
        /// to all policies within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that calling `WithTimeout` on a `BasePolicySet` instance updates the timeout
        /// value in all associated policy definitions. It verifies that the resultant `BasePolicySet` is not null
        /// and that all individual policy definitions within the `PolicySet` have the expected timeout value set.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the resultant `BasePolicySet` is null or if any policy within the `PolicySet` does not
        /// have the expected timeout value correctly applied.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithTimeout_SetsTimeout()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            const int timeout = 10000;

            var result = basePolicySet.WithTimeout(timeout);

            Assert.That(result, Is.Not.Null);

            // Verify all policies have the timeout set
            foreach (var policy in result.PolicySet.PolicyDefinitions.Values)
            {
                Assert.That(policy.TimeoutMs, Is.EqualTo(timeout));
            }
        }

        /// <summary>
        /// Validates that the `WithMemoryLimit` method correctly applies the specified memory limit
        /// to all policies within the `BasePolicySet`.
        /// </summary>
        /// <remarks>
        /// This test ensures that calling `WithMemoryLimit` on a `BasePolicySet` instance updates the memory
        /// limit in all associated policy definitions. It verifies that the resultant `BasePolicySet` is not null
        /// and that all individual policy definitions within the `PolicySet` have the provided memory limit set.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the resultant `BasePolicySet` is null or if any policy within the `PolicySet` does not
        /// have the expected memory limit applied.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithMemoryLimit_SetsMemoryLimit()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            const int memoryLimit = 64;

            var result = basePolicySet.WithMemoryLimit(memoryLimit);

            Assert.That(result, Is.Not.Null);

            // Verify all policies have the memory limit set
            foreach (var policy in result.PolicySet.PolicyDefinitions.Values)
            {
                Assert.That(policy.MaxMemoryMB, Is.EqualTo(memoryLimit));
            }
        }

        /// <summary>
        /// Validates the fluent API chaining functionality of `BasePolicySetExtensions` methods.
        /// </summary>
        /// <remarks>
        /// This test ensures that multiple method calls in the `BasePolicySetExtensions` fluent API
        /// properly configure the policy set. It verifies the functionality of chained operations, such as
        /// enabling eval permissions, setting file read/write patterns, specifying allowed modules, and
        /// configuring memory and timeout limits. The resultant configuration is verified for correctness
        /// through assertions.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the constructed `BasePolicySet` is null or if any of the expected policy settings
        /// are not properly applied or validated.
        /// </exception>
        [Test]
        [Category("Security.Policy")]
        public void FluentAPI_ChainingWorks()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;

            var result = basePolicySet
                .WithEvalAllowed()
                .WithFileRead("*.txt")
                .WithFileWrite("*.log")
                .WithModule(CoreModules.IO)
                .WithTimeout(15000)
                .WithMemoryLimit(128);

            Assert.That(result, Is.Not.Null);

            // Verify eval is allowed
            Assert.That(result.PolicySet.FilePolicies.ContainsKey(":eval"), Is.True);

            // Verify file permissions
            var policies = result.PolicySet.PolicyDefinitions.Values.ToList();
            Assert.Multiple(() =>
            {
                Assert.That(
                    policies.All(static p => p.FilePermissions.ContainsKey("*.txt")),
                    Is.True
                );
                Assert.That(
                    policies.All(static p => p.FilePermissions.ContainsKey("*.log")),
                    Is.True
                );

                // Verify module
                Assert.That(
                    policies.All(static p => p.AllowedModules.HasFlag(CoreModules.IO)),
                    Is.True
                );

                // Verify timeout and memory
                Assert.That(policies.All(static p => p.TimeoutMs == 15000), Is.True);
                Assert.That(policies.All(static p => p.MaxMemoryMB == 128), Is.True);
            });
        }

        /// <summary>
        /// Validates the integration of the `WithEvalAllowed` extension method within a policy set.
        /// </summary>
        /// <remarks>
        /// This test ensures that the `WithEvalAllowed` method correctly adds evaluation permissions to
        /// the base policy set and verifies the resulting policy set behaviour through assertions.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the `BasePolicySet` is null or the expected policy configurations are not present.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithEvalAllowed_IntegrationTest()
        {
            // This test verifies the usage pattern from the requirement
            var script = new Script(
                Examples
                    .IsolatedBasePolicySet.WithEvalAllowed()
                    .WithFileRead("*.txt")
                    .WithModule(CoreModules.IO)
            );

            // Verify the script has the expected policy set
            Assert.That(script.BasePolicySet, Is.Not.Null);
            Assert.That(script.BasePolicySet.PolicySet.FilePolicies.ContainsKey(":eval"), Is.True);
        }

        /// <summary>
        /// Verifies that methods in the `BasePolicySetExtensions` class throw the correct exceptions
        /// when the `BasePolicySet` parameter is null.
        /// </summary>
        /// <param name="basePolicySet">
        /// An instance of the `BasePolicySet` class. This parameter is expected to be null during the test.
        /// </param>
        /// <param name="expectedExceptionType">
        /// The type of the exception that is expected to be thrown when the `BasePolicySet` is null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the `BasePolicySet` is null while invoking any extension method on it.
        /// </exception>
        [Test]
        [TestCase(null, typeof(ArgumentNullException))]
        [Category("Security.Policy")]
        public void Extensions_ThrowOnNullBasePolicySet(
            BasePolicySet basePolicySet,
            Type expectedExceptionType
        )
        {
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithEvalAllowed());
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithFileRead("*.txt"));
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithFileWrite("*.txt"));
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithModule(CoreModules.IO));
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithTimeout(1000));
            Assert.Throws(expectedExceptionType, () => basePolicySet.WithMemoryLimit(64));
        }

        /// <summary>
        /// Verifies that the `WithFileRead` extension method throws an exception when provided with an invalid pattern.
        /// </summary>
        /// <remarks>
        /// This test ensures robust validation within the `WithFileRead` method by checking for exceptions when null,
        /// empty, or whitespace patterns are supplied as input.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the input pattern is null, an empty string, or consists only of whitespace characters.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithFileRead_ThrowsOnInvalidPattern()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileRead(null));
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileRead(""));
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileRead("   "));
        }

        /// <summary>
        /// Validates that the `WithFileWrite` method throws appropriate exceptions when provided with invalid patterns.
        /// </summary>
        /// <remarks>
        /// This test ensures that the `WithFileWrite` extension method enforces input validation by throwing
        /// an <see cref="ArgumentException"/> when null, empty, or whitespace strings are passed as the pattern.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown if the provided pattern is null, empty, or consists only of whitespace.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithFileWrite_ThrowsOnInvalidPattern()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileWrite(null));
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileWrite(""));
            Assert.Throws<ArgumentException>(() => basePolicySet.WithFileWrite("   "));
        }

        /// <summary>
        /// Verifies that the `WithTimeout` extension method throws an exception when provided with a negative timeout value.
        /// </summary>
        /// <remarks>
        /// This test ensures that the `WithTimeout` method enforces valid input by rejecting negative timeout values,
        /// which would be invalid for a policy configuration.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown if a negative timeout value is passed to the `WithTimeout` method.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithTimeout_ThrowsOnNegativeValue()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            Assert.Throws<ArgumentException>(() => basePolicySet.WithTimeout(-2));
        }

        /// <summary>
        /// Ensures that the <see cref="BasePolicySetExtensions.WithMemoryLimit"/> method throws an exception when provided with a negative memory limit value.
        /// </summary>
        /// <remarks>
        /// This test verifies the validation logic in <see cref="BasePolicySetExtensions.WithMemoryLimit"/>, ensuring that
        /// invalid memory limit values do not lead to undefined behaviour or application errors.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the memory limit value provided to <see cref="BasePolicySetExtensions.WithMemoryLimit"/> is negative.
        /// </exception>
        [Category("Security.Policy")]
        [Test]
        public void WithMemoryLimit_ThrowsOnNegativeValue()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            Assert.Throws<ArgumentException>(() => basePolicySet.WithMemoryLimit(-2));
        }
    }
}
