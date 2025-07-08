using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for FileSystemSecurity's key-based access control functionality
    /// including GetFilePermissionsWithKey and DirectoryAccessRules integration.
    /// </summary>
    [TestFixture]
    [Category("Security.FileSystemKeys")]
    public class FileSystemSecurityKeyTests
    {
        private FileSystemSecurity _fileSystem;

        [SetUp]
        public void Setup()
        {
            _fileSystem = new FileSystemSecurity
            {
                DefaultFilePermissions = FilePermissions.None,
                DirectoryAccessRules = ImmutableArray<DirectoryAccessRule>.Empty,
            };
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_NoRules_ReturnsBasePermissions()
        {
            // Arrange
            _fileSystem.DefaultFilePermissions = FilePermissions.Read;

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey("/test/file.txt", "anykey");

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.Read));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_MatchingKey_ReturnsRulePermissions()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.ReadWrite, "key123");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey("/secure/file.txt", "key123");

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.ReadWrite));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_NonMatchingKey_ReturnsNone()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.ReadWrite, "key123");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);
            _fileSystem.DefaultFilePermissions = FilePermissions.Read;

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey("/secure/file.txt", "wrongkey");

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.None));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_EmptyKeySet_AllowsAnyKey()
        {
            // Arrange
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/public",
                Permissions = FilePermissions.Read,
                RequiredSigningKeys = ImmutableHashSet<string>.Empty,
            };
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions1 = _fileSystem.GetFilePermissionsWithKey("/public/file.txt", "key1");
            var permissions2 = _fileSystem.GetFilePermissionsWithKey("/public/file.txt", "key2");
            var permissions3 = _fileSystem.GetFilePermissionsWithKey("/public/file.txt", "");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(permissions1, Is.EqualTo(FilePermissions.Read));
                Assert.That(permissions2, Is.EqualTo(FilePermissions.Read));
                Assert.That(permissions3, Is.EqualTo(FilePermissions.Read));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_MultipleRules_TakesMostPermissive()
        {
            // Arrange
            var rule1 = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            var rule2 = DirectoryAccessRule.Create("/data", FilePermissions.ReadWrite, "key1");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule1, rule2);

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey("/data/file.txt", "key1");

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.ReadWrite));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_MultipleKeysInRule_MatchesAny()
        {
            // Arrange
            var rule = new DirectoryAccessRule
            {
                DirectoryPattern = "/multi",
                Permissions = FilePermissions.Read,
                RequiredSigningKeys = ImmutableHashSet.Create("key1", "key2", "key3"),
            };
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions1 = _fileSystem.GetFilePermissionsWithKey("/multi/file.txt", "key2");
            var permissions2 = _fileSystem.GetFilePermissionsWithKey("/multi/file.txt", "key4");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(permissions1, Is.EqualTo(FilePermissions.Read));
                Assert.That(permissions2, Is.EqualTo(FilePermissions.None));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_WildcardPattern_MatchesCorrectly()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create(
                "/data/*/secure",
                FilePermissions.Read,
                "wildkey"
            );
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions1 = _fileSystem.GetFilePermissionsWithKey(
                "/data/user1/secure/file.txt",
                "wildkey"
            );
            var permissions2 = _fileSystem.GetFilePermissionsWithKey(
                "/data/user2/secure/doc.pdf",
                "wildkey"
            );
            var permissions3 = _fileSystem.GetFilePermissionsWithKey(
                "/data/public/file.txt",
                "wildkey"
            );

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(permissions1, Is.EqualTo(FilePermissions.Read));
                Assert.That(permissions2, Is.EqualTo(FilePermissions.Read));
                Assert.That(permissions3, Is.EqualTo(FilePermissions.None));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_SubdirectoryAccess_IsAllowed()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/root", FilePermissions.ReadWrite, "rootkey");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey(
                "/root/sub/deep/file.txt",
                "rootkey"
            );

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.ReadWrite));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_ExactPathMatch_TakesPrecedence()
        {
            // Arrange
            var generalRule = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            var specificRule = DirectoryAccessRule.Create(
                "/data/sensitive",
                FilePermissions.None,
                "key1"
            );
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(generalRule, specificRule);

            // Act
            var generalPermissions = _fileSystem.GetFilePermissionsWithKey(
                "/data/file.txt",
                "key1"
            );
            var specificPermissions = _fileSystem.GetFilePermissionsWithKey(
                "/data/sensitive/secret.txt",
                "key1"
            );

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(generalPermissions, Is.EqualTo(FilePermissions.Read));
                Assert.That(specificPermissions, Is.EqualTo(FilePermissions.None));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_NullOrEmptyKey_UsesBasePermissions()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/secure", FilePermissions.ReadWrite, "required");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);
            _fileSystem.DefaultFilePermissions = FilePermissions.Read;

            // Act
            var permissionsNull = _fileSystem.GetFilePermissionsWithKey("/secure/file.txt", null);
            var permissionsEmpty = _fileSystem.GetFilePermissionsWithKey("/secure/file.txt", "");

            // Assert - null/empty key means no signing, so DirectoryAccessRules requiring keys deny access
            Assert.Multiple(() =>
            {
                Assert.That(permissionsNull, Is.EqualTo(FilePermissions.None));
                Assert.That(permissionsEmpty, Is.EqualTo(FilePermissions.None));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_CombinesWithExplicitFilePermissions()
        {
            // Arrange
            _fileSystem.SetFilePermissions("/data/specific.txt", FilePermissions.ReadWrite);
            var rule = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey("/data/specific.txt", "key1");

            // Assert - explicit file permission should take precedence
            Assert.That(permissions, Is.EqualTo(FilePermissions.ReadWrite));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_CrossPlatformPaths_WorkCorrectly()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("/data", FilePermissions.Read, "key1");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act - test with backslashes (Windows-style)
            var permissions = _fileSystem.GetFilePermissionsWithKey(@"\data\file.txt", "key1");

            // Assert - path normalization should handle this
            Assert.That(permissions, Is.EqualTo(FilePermissions.Read));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_RelativePaths_AreHandled()
        {
            // Arrange
            var rule = DirectoryAccessRule.Create("relative/path", FilePermissions.Read, "key1");
            _fileSystem.DirectoryAccessRules = ImmutableArray.Create(rule);

            // Act
            var permissions = _fileSystem.GetFilePermissionsWithKey(
                "relative/path/file.txt",
                "key1"
            );

            // Assert
            Assert.That(permissions, Is.EqualTo(FilePermissions.Read));
        }

        [Category("Security.Unit")]
        [Test]
        public void GetFilePermissionsWithKey_PerformanceWithManyRules_IsReasonable()
        {
            // Arrange - create many rules
            var rules = new List<DirectoryAccessRule>();
            for (int i = 0; i < 1000; i++)
            {
                rules.Add(DirectoryAccessRule.Create($"/path{i}", FilePermissions.Read, $"key{i}"));
            }
            rules.Add(
                DirectoryAccessRule.Create("/target", FilePermissions.ReadWrite, "targetkey")
            );
            _fileSystem.DirectoryAccessRules = rules.ToImmutableArray();

            // Act
            var start = DateTime.UtcNow;
            var permissions = _fileSystem.GetFilePermissionsWithKey(
                "/target/file.txt",
                "targetkey"
            );
            var duration = DateTime.UtcNow - start;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(permissions, Is.EqualTo(FilePermissions.ReadWrite));
                Assert.That(
                    duration.TotalMilliseconds,
                    Is.LessThan(100),
                    "Should complete quickly even with many rules"
                );
            });
        }
    }
}
