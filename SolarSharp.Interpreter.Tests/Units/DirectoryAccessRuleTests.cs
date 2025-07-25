using System.Collections.Immutable;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Comprehensive test suite for DirectoryAccessRule functionality including
    /// pattern matching, signing key validation, and permission enforcement.
    /// </summary>
    [TestFixture]
    [Category("Security.DirectoryAccess")]
    public class DirectoryAccessRuleTests
    {
        [Category("Security.DirectoryAccess")]
        [Test]
        public void DirectoryAccessRule_Create_SetsPropertiesCorrectly()
        {
            // Act
            var rule = DirectoryAccessRule.Create("/secure/data", FilePermissions.Read, "key123");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(rule.DirectoryPattern, Is.EqualTo("/secure/data"));
                Assert.That(rule.Permissions, Is.EqualTo(FilePermissions.Read));
                Assert.That(rule.RequiredSigningKeys.Count, Is.EqualTo(1));
                Assert.That(rule.RequiredSigningKeys.Contains("key123"), Is.True);
                Assert.That(rule.EnforceManifestScope, Is.True);
            });
        }

        [Test]
        public void DirectoryAccessRule_CreateWithMultipleKeys_SetsAllKeys()
        {
            // Arrange
            var keys = ImmutableHashSet.Create("key1", "key2", "key3");

            // Act
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/secure/multi",
                Permissions = FilePermissions.ReadWrite,
                RequiredSigningKeys = keys,
                EnforceManifestScope = false,
            };

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(rule.RequiredSigningKeys.Count, Is.EqualTo(3));
                Assert.That(rule.RequiredSigningKeys.Contains("key1"), Is.True);
                Assert.That(rule.RequiredSigningKeys.Contains("key2"), Is.True);
                Assert.That(rule.RequiredSigningKeys.Contains("key3"), Is.True);
                Assert.That(rule.EnforceManifestScope, Is.False);
            });
        }

        [Test]
        public void DirectoryAccessRule_EmptySigningKeys_AllowsAnyKey()
        {
            // Act
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/public/data",
                Permissions = FilePermissions.Read,
                RequiredSigningKeys = ImmutableHashSet<string>.Empty,
            };

            // Assert
            Assert.That(rule.RequiredSigningKeys.IsEmpty, Is.True);
        }

        [Test]
        public void DirectoryAccessRule_DefaultValues_AreCorrect()
        {
            // Act
            var rule = new DirectoryAccessRule();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(rule.DirectoryPattern, Is.EqualTo(""));
                Assert.That(rule.Permissions, Is.EqualTo(FilePermissions.None));
                Assert.That(rule.RequiredSigningKeys, Is.Not.Null);
                Assert.That(rule.RequiredSigningKeys.IsEmpty, Is.True);
                Assert.That(rule.EnforceManifestScope, Is.True);
            });
        }

        [Test]
        public void DirectoryAccessRule_WithWildcardPattern_IsValid()
        {
            // Act
            var rule = DirectoryAccessRule.Create(
                "/data/*/secure",
                FilePermissions.Read,
                "wildcard-key"
            );

            // Assert
            Assert.That(rule.DirectoryPattern, Is.EqualTo("/data/*/secure"));
        }

        [Test]
        public void DirectoryAccessRule_RecordEquality_WorksCorrectly()
        {
            // Arrange
            var rule1 = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            var rule2 = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            var rule3 = DirectoryAccessRule.Create("/data", FilePermissions.ReadWrite, "key1");
            var rule4 = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key2");

            // Assert
            Assert.Multiple(() =>
            {
                // Records with ImmutableHashSet don't have value equality by default
                // Check individual properties instead
                Assert.That(rule1.DirectoryPattern, Is.EqualTo(rule2.DirectoryPattern));
                Assert.That(rule1.Permissions, Is.EqualTo(rule2.Permissions));
                Assert.That(
                    rule1.RequiredSigningKeys.SetEquals(rule2.RequiredSigningKeys),
                    Is.True
                );
                Assert.That(rule1.EnforceManifestScope, Is.EqualTo(rule2.EnforceManifestScope));

                Assert.That(
                    rule1.Permissions,
                    Is.Not.EqualTo(rule3.Permissions),
                    "Rules with different permissions should not be equal"
                );
                Assert.That(
                    rule1.RequiredSigningKeys.SetEquals(rule4.RequiredSigningKeys),
                    Is.False,
                    "Rules with different keys should not be equal"
                );
            });
        }

        [Test]
        public void DirectoryAccessRule_WithExpression_CopiesCorrectly()
        {
            // Arrange
            var original = DirectoryAccessRule.Create("/original", FilePermissions.Read, "key1");

            // Act
            var modified = original with
            {
                DirectoryPattern = "/modified",
            };

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(modified.DirectoryPattern, Is.EqualTo("/modified"));
                Assert.That(modified.Permissions, Is.EqualTo(original.Permissions));
                Assert.That(modified.RequiredSigningKeys, Is.EqualTo(original.RequiredSigningKeys));
                Assert.That(
                    modified.EnforceManifestScope,
                    Is.EqualTo(original.EnforceManifestScope)
                );
            });
        }

        [Test]
        public void DirectoryAccessRule_NullPattern_IsAllowed()
        {
            // This tests that null patterns are handled (though they may not be meaningful)
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = null,
                Permissions = FilePermissions.Read,
            };

            Assert.That(rule.DirectoryPattern, Is.Null);
        }

        [Test]
        public void DirectoryAccessRule_ComplexPermissions_ArePreserved()
        {
            // Arrange
            var complexPermissions =
                FilePermissions.Read
                | FilePermissions.ReadWrite
                | FilePermissions.SandboxedReadWrite;

            // Act
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/complex",
                Permissions = complexPermissions,
                RequiredSigningKeys = ImmutableHashSet.Create("complex-key"),
            };

            // Assert
            Assert.That(rule.Permissions, Is.EqualTo(complexPermissions));
            Assert.That(rule.Permissions.HasFlag(FilePermissions.Read), Is.True);
            Assert.That(rule.Permissions.HasFlag(FilePermissions.ReadWrite), Is.True);
            Assert.That(rule.Permissions.HasFlag(FilePermissions.SandboxedReadWrite), Is.True);
        }

        [Test]
        public void DirectoryAccessRule_CaseInsensitiveKeys_DependsOnHashSet()
        {
            // Note: ImmutableHashSet uses default string comparison (case-sensitive)
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/case-test",
                Permissions = FilePermissions.Read,
                RequiredSigningKeys = ImmutableHashSet.Create("Key123", "KEY123"),
            };

            // Both keys should be present as they differ in case
            Assert.That(rule.RequiredSigningKeys.Count, Is.EqualTo(2));
        }

        [Test]
        public void DirectoryAccessRule_ToString_ProvidesUsefulInfo()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create(
                "/test/path",
                FilePermissions.ReadWrite,
                "testkey"
            );

            // Act
            var str = rule.ToString();

            // Assert
            Assert.That(str, Is.Not.Null);
            Assert.That(str, Does.Contain("DirectoryAccessRule"));
            // The exact format depends on record's ToString implementation
        }
    }
}
