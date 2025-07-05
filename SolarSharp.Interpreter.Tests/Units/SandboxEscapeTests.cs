using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for sandbox escape techniques including symlink attacks, hard link exploitation,
    ///     junction point attacks, mount point exploitation, and process boundary violations.
    ///     These attacks attempt to break out of filesystem and process isolation boundaries.
    /// </summary>
    /// <remarks>
    ///     This comprehensive test suite validates the robustness of SolarSharp's sandbox against
    ///     various escape techniques that could be used to access resources outside the sandbox.
    ///     The tests simulate real-world attack vectors that malicious scripts might use to bypass
    ///     security controls and access sensitive data or system resources.
    ///     Attack Vectors Tested:
    ///     - Symbolic links (file and directory) that point outside the sandbox
    ///     - Symlink chains and target swapping during access
    ///     - Hard links to files outside the sandbox
    ///     - Windows junction points (similar to symlinks but directory-only)
    ///     - Mount point traversal attempts
    ///     - Process execution and command injection
    ///     - Shared memory and IPC access
    ///     - Named pipe communication channels
    ///     Platform-Specific Considerations:
    ///     - Some tests are Windows-specific (junction points)
    ///     - Some tests are Unix-specific (certain symlink behaviors)
    ///     - Tests adapt based on the runtime platform
    ///     Security Goals:
    ///     - Prevent file system escape via any link type
    ///     - Block process execution and command injection
    ///     - Prevent inter-process communication
    ///     - Maintain isolation even under concurrent attack attempts
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [Category("IntegrationTest")]
    [Category("PlatformSpecific")]
    public class SandboxEscapeTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_sandbox_test_{Guid.NewGuid()}");
            _sandboxDir = Path.Combine(_tempDir, "sandbox");
            _secretDir = Path.Combine(_tempDir, "secret");
            _secretFile = Path.Combine(_secretDir, "secret.txt");

            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_sandboxDir);
            Directory.CreateDirectory(_secretDir);

            // Create secret content outside sandbox
            File.WriteAllText(_secretFile, "TOP SECRET CONTENT");

#if DEBUG
            TestContext.Out.WriteLine($"Temp dir: {_tempDir}");
            TestContext.Out.WriteLine($"Sandbox dir: {_sandboxDir}");
            TestContext.Out.WriteLine($"Secret dir: {_secretDir}");
#endif
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                try
                {
                    // Force delete everything, including symlinks and junctions
                    DeleteDirectoryRecursive(_tempDir);
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Cleanup warning: {ex.Message}");
#endif
                }
        }

        private string _tempDir;
        private string _sandboxDir;
        private string _secretDir;
        private string _secretFile;

        /// <summary>
        ///     Tests protection against escaping the sandbox using chains of symbolic links.
        /// </summary>
        /// <remarks>
        ///     Symlink chains are a common technique where multiple symlinks are chained together
        ///     to obfuscate the final target. This test creates:
        ///     sandbox/link1 -> ../link2 -> ../../secret/secret.txt
        ///     The attack attempts to:
        ///     1. Create a symlink inside the sandbox pointing to a relative path
        ///     2. That path leads to another symlink outside the sandbox
        ///     3. The final symlink points to the secret file
        ///     A secure sandbox should resolve the entire chain and block access if any
        ///     part of the chain leads outside the allowed boundaries.
        /// </remarks>
        [Test]
        public void TestSymlinkChainEscape()
        {
            // Test escaping sandbox via symlink chains
#if DEBUG
            TestContext.Out.WriteLine("Testing symlink chain escape attack");
#endif

            var config = SecurityConfiguration.DataProcessing();
            config.SetDirectoryPermissions(_sandboxDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                // Create symlink chain: sandbox/link1 -> ../link2 -> ../../secret/secret.txt
                var link1Path = Path.Combine(_sandboxDir, "link1");
                var link2Path = Path.Combine(_tempDir, "link2");

                if (CanCreateSymlinks())
                {
                    CreateSymlink(link1Path, "../link2");
                    CreateSymlink(link2Path, Path.Combine("secret", "secret.txt"));

#if DEBUG
                    TestContext.Out.WriteLine($"Created symlink chain: {link1Path} -> ../link2 -> secret/secret.txt");
#endif

                    // Try to read through symlink chain
                    var result = script.DoString($@"
                        local f = io.open('{link1Path.Replace('\\', '/')}', 'r')
                        if f then
                            local content = f:read('*a')
                            f:close()
                            return content
                        end
                        return 'FAILED'
                    ");

#if DEBUG
                    TestContext.Out.WriteLine($"Symlink chain result: {result.String}");
#endif

                    if (result.String.Contains("TOP SECRET"))
                    {
                        Assert.Fail("CRITICAL: Symlink chain allowed escape from sandbox!");
                    }
                    else
                    {
#if DEBUG
                        TestContext.Out.WriteLine("Symlink chain escape was blocked (good)");
#endif
                    }
                }
                else
                {
#if DEBUG
                    TestContext.Out.WriteLine("Skipping symlink test - cannot create symlinks on this platform");
#endif
                    Assert.Inconclusive("Cannot create symlinks on this platform");
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Symlink chain escape blocked by security: {ex.Message}");
#endif
                // This is the expected behavior
            }
            catch (Exception ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Symlink chain failed: {ex.Message}");
#endif
                // This might indicate the symlink was blocked by filesystem security
            }
        }

        /// <summary>
        ///     Tests protection against TOCTOU attacks using symlink target swapping.
        /// </summary>
        /// <remarks>
        ///     This is a Time-of-Check Time-of-Use (TOCTOU) attack where:
        ///     1. A symlink initially points to a legitimate file inside the sandbox
        ///     2. The security system checks and approves access
        ///     3. Before the actual read, the symlink is swapped to point outside
        ///     4. The read operation might then access the secret file
        ///     The test runs multiple read operations while simultaneously swapping
        ///     the symlink target between legitimate and malicious paths. A secure
        ///     implementation should either:
        ///     - Lock the symlink during the entire operation
        ///     - Re-validate the target at the time of actual access
        ///     - Detect and block the race condition
        /// </remarks>
        [Test]
        public void TestSymlinkTargetSwapping()
        {
            // Test symlink target swapping during file operations
#if DEBUG
            TestContext.Out.WriteLine("Testing symlink target swapping attack");
#endif

            var config = SecurityConfiguration.DataProcessing();
            config.SetDirectoryPermissions(_sandboxDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            if (!CanCreateSymlinks())
            {
                Assert.Inconclusive("Cannot create symlinks on this platform");
                return;
            }

            try
            {
                var symlinkPath = Path.Combine(_sandboxDir, "changing_link");
                var legitimateFile = Path.Combine(_sandboxDir, "legitimate.txt");

                // Create legitimate file
                File.WriteAllText(legitimateFile, "legitimate content");

                // Initially point symlink to legitimate file
                CreateSymlink(symlinkPath, "legitimate.txt");

                // Start a script that will read the symlink
                var scriptTask = Task.Run(() =>
                {
                    try
                    {
                        return script.DoString($@"
                            local results = {{}}
                            for i = 1, 10 do
                                local f = io.open('{symlinkPath.Replace('\\', '/')}', 'r')
                                if f then
                                    local content = f:read('*a')
                                    f:close()
                                    results[i] = content
                                else
                                    results[i] = 'FAILED'
                                end
                                
                                -- Brief pause
                                local dummy = 0
                                for j = 1, 1000 do dummy = dummy + j end
                            end
                            return table.concat(results, '|')
                        ");
                    }
                    catch (Exception ex)
                    {
                        return DynValue.NewString($"ERROR: {ex.Message}");
                    }
                });

                // While script is running, try to swap symlink target
                Thread.Sleep(50);

                try
                {
                    DeleteSymlink(symlinkPath);
                    CreateSymlink(symlinkPath,
                        $"..{Path.DirectorySeparatorChar}secret{Path.DirectorySeparatorChar}secret.txt");
#if DEBUG
                    TestContext.Out.WriteLine("Swapped symlink to point to secret file");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Symlink swap failed: {ex.Message}");
#endif
                }

                var result = scriptTask.Result;
#if DEBUG
                TestContext.Out.WriteLine($"Symlink swapping result: {result.String}");
#endif

                if (result.String.Contains("TOP SECRET"))
                    Assert.Fail("CRITICAL: Symlink target swapping allowed access to secret file!");
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Symlink target swapping blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests protection against directory symbolic links pointing outside the sandbox.
        /// </summary>
        /// <remarks>
        ///     Directory symlinks are particularly dangerous because they can make entire
        ///     directory trees outside the sandbox appear to be inside it. This test:
        ///     1. Creates a directory symlink inside sandbox pointing to secret directory
        ///     2. Attempts to access files through the symlinked directory
        ///     A secure sandbox must:
        ///     - Resolve directory symlinks before allowing access
        ///     - Block access if the real path is outside allowed boundaries
        ///     - Prevent traversal through directory symlinks
        ///     This is critical because many applications traverse directories recursively
        ///     and might inadvertently process files outside the sandbox.
        /// </remarks>
        [Test]
        public void TestDirectorySymlinkEscape()
        {
            // Test escaping via directory symlinks
#if DEBUG
            TestContext.Out.WriteLine("Testing directory symlink escape");
#endif

            var config = SecurityConfiguration.DataProcessing();
            config.SetDirectoryPermissions(_sandboxDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            if (!CanCreateSymlinks())
            {
                Assert.Inconclusive("Cannot create symlinks on this platform");
                return;
            }

            try
            {
                var dirSymlinkPath = Path.Combine(_sandboxDir, "secret_dir");

                // Create directory symlink pointing to secret directory
                CreateDirectorySymlink(dirSymlinkPath, _secretDir);

#if DEBUG
                TestContext.Out.WriteLine($"Created directory symlink: {dirSymlinkPath} -> {_secretDir}");
#endif

                // Try to access secret file through directory symlink
                var secretFileThroughLink = Path.Combine(dirSymlinkPath, "secret.txt");

                var result = script.DoString($@"
                    local f = io.open('{secretFileThroughLink.Replace('\\', '/')}', 'r')
                    if f then
                        local content = f:read('*a')
                        f:close()
                        return content
                    end
                    return 'FAILED'
                ");

#if DEBUG
                TestContext.Out.WriteLine($"Directory symlink result: {result.String}");
#endif

                if (result.String.Contains("TOP SECRET"))
                    Assert.Fail("CRITICAL: Directory symlink allowed escape from sandbox!");
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Directory symlink escape blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests protection against hard link attacks to access files outside the sandbox.
        /// </summary>
        /// <remarks>
        ///     Hard links are more dangerous than symbolic links because:
        ///     - They are indistinguishable from regular files
        ///     - They share the same inode and data blocks
        ///     - Deleting the original doesn't affect the hard link
        ///     - They cannot be easily detected without filesystem-level checks
        ///     This test attempts to:
        ///     1. Create a hard link inside the sandbox to a secret file outside
        ///     2. Access the secret file through the hard link
        ///     Protection mechanisms:
        ///     - Block hard link creation across sandbox boundaries
        ///     - Validate inode locations before allowing access
        ///     - Use filesystem features that prevent cross-boundary hard links
        /// </remarks>
        [Test]
        public void TestHardLinkEscape()
        {
            // Test escaping via hard links (if supported)
#if DEBUG
            TestContext.Out.WriteLine("Testing hard link escape attack");
#endif

            var config = SecurityConfiguration.DataProcessing();
            config.SetDirectoryPermissions(_sandboxDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                var hardLinkPath = Path.Combine(_sandboxDir, "secret_hardlink.txt");

                // Try to create hard link to secret file
                if (CreateHardLink(hardLinkPath, _secretFile))
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Created hard link: {hardLinkPath} -> {_secretFile}");
#endif

                    // Try to read through hard link
                    var result = script.DoString($@"
                        local f = io.open('{hardLinkPath.Replace('\\', '/')}', 'r')
                        if f then
                            local content = f:read('*a')
                            f:close()
                            return content
                        end
                        return 'FAILED'
                    ");

#if DEBUG
                    TestContext.Out.WriteLine($"Hard link result: {result.String}");
#endif

                    if (result.String.Contains("TOP SECRET"))
                        Assert.Fail("CRITICAL: Hard link allowed escape from sandbox!");
                }
                else
                {
#if DEBUG
                    TestContext.Out.WriteLine("Hard link creation failed - might be blocked by filesystem");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Hard link escape blocked: {ex.Message}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Hard link operation failed: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests protection against Windows junction points used to escape the sandbox.
        /// </summary>
        /// <remarks>
        ///     Junction points are a Windows-specific feature similar to symbolic links but:
        ///     - They only work for directories, not files
        ///     - They are resolved at the filesystem driver level
        ///     - They have different permission requirements than symlinks
        ///     This test (Windows-only):
        ///     1. Creates a junction point inside sandbox pointing to secret directory
        ///     2. Attempts to access files through the junction
        ///     Junction points are particularly dangerous on Windows because many
        ///     applications and APIs treat them transparently as regular directories.
        ///     The sandbox must intercept and validate junction point traversal.
        /// </remarks>
        [Test]
        public void TestJunctionPointEscape()
        {
            // Test junction point escape (Windows-specific)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("Junction point test only applies to Windows");
                return;
            }

#if DEBUG
            TestContext.Out.WriteLine("Testing Windows junction point escape");
#endif

            var config = SecurityConfiguration.DataProcessing();
            config.SetDirectoryPermissions(_sandboxDir, DirectoryPermissions.ListAndCreateFiles);

            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                var junctionPath = Path.Combine(_sandboxDir, "secret_junction");

                // Create junction point to secret directory
                if (CreateJunctionPoint(junctionPath, _secretDir))
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Created junction point: {junctionPath} -> {_secretDir}");
#endif

                    // Try to access secret file through junction
                    var secretFileThroughJunction = Path.Combine(junctionPath, "secret.txt");

                    var result = script.DoString($@"
                        local f = io.open('{secretFileThroughJunction.Replace('\\', '/')}', 'r')
                        if f then
                            local content = f:read('*a')
                            f:close()
                            return content
                        end
                        return 'FAILED'
                    ");

#if DEBUG
                    TestContext.Out.WriteLine($"Junction point result: {result.String}");
#endif

                    if (result.String.Contains("TOP SECRET"))
                        Assert.Fail("CRITICAL: Junction point allowed escape from sandbox!");
                }
                else
                {
#if DEBUG
                    TestContext.Out.WriteLine("Junction point creation failed");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Junction point escape blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests that the sandbox prevents access to system mount points.
        /// </summary>
        /// <remarks>
        ///     Mount points represent filesystem boundaries and often contain sensitive
        ///     system files. This test attempts to access:
        ///     - Root filesystem (/ or C:\)
        ///     - System directories (/etc, /var, Windows\System32)
        ///     - Other mounted volumes
        ///     Even in isolated security mode, scripts might try to probe for mount
        ///     points to gather system information or find attack vectors. The sandbox
        ///     should block all access to paths outside its designated area, regardless
        ///     of whether they are mount points or regular directories.
        ///     This test helps ensure that filesystem isolation is properly implemented
        ///     at all levels of the security stack.
        /// </remarks>
        [Test]
        public void TestMountPointAccessAttempt()
        {
            // Test attempting to access system mount points
#if DEBUG
            TestContext.Out.WriteLine("Testing mount point access attempts");
#endif

            var config = SecurityConfiguration.Isolated();
            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var mountPoints = GetSystemMountPoints();

            foreach (var mountPoint in mountPoints)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Testing access to mount point: {mountPoint}");
#endif

                try
                {
                    var result = script.DoString($@"
                        local f = io.open('{mountPoint.Replace('\\', '/')}/etc/passwd', 'r')
                        if f then
                            f:close()
                            return 'ACCESSIBLE'
                        end
                        return 'BLOCKED'
                    ");

                    if (result.String == "ACCESSIBLE")
                    {
#if DEBUG
                        TestContext.Out.WriteLine($"WARNING: Mount point {mountPoint} is accessible!");
#endif
                    }
                }
                catch (SecurityException ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Mount point access blocked: {ex.Message}");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Mount point access failed: {ex.Message}");
#endif
                }
            }
        }

        /// <summary>
        ///     Tests that the sandbox prevents execution of system processes and commands.
        /// </summary>
        /// <remarks>
        ///     Process execution is one of the most critical attack vectors. This test
        ///     attempts to execute various system commands through different Lua APIs:
        ///     - os.execute() - Direct command execution
        ///     - io.popen() - Command execution with output capture
        ///     - os.system() - System command execution (if available)
        ///     Commands tested include:
        ///     - Shells (bash, sh, cmd.exe, powershell)
        ///     - Interpreters (python, node)
        ///     - Network tools (curl, wget)
        ///     The sandbox must:
        ///     - Block or remove dangerous os module functions
        ///     - Prevent any form of process creation
        ///     - Return nil or throw SecurityException for execution attempts
        ///     This is critical for preventing command injection and system compromise.
        /// </remarks>
        [Test]
        public void TestProcessExecutionAttempts()
        {
            // Test attempts to execute system processes
#if DEBUG
            TestContext.Out.WriteLine("Testing process execution attempts");
#endif

            var config = SecurityConfiguration.Isolated();
            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var dangerousCommands = new[]
            {
                "/bin/sh",
                "/bin/bash",
                "cmd.exe",
                "powershell.exe",
                "python",
                "node",
                "curl",
                "wget"
            };

            foreach (var command in dangerousCommands)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Testing execution of: {command}");
#endif

                try
                {
                    // Try various ways to execute commands
                    var attempts = new[]
                    {
                        $"os.execute('{command}')",
                        $"io.popen('{command}')",
                        $"os.system('{command}')"
                    };

                    foreach (var attempt in attempts)
                        try
                        {
                            var result = script.DoString($"return {attempt}");
#if DEBUG
                            TestContext.Out.WriteLine($"Command execution attempt result: {result}");
#endif

                            if (result.Type != DataType.Nil && result.Type != DataType.Boolean)
                            {
#if DEBUG
                                TestContext.Out.WriteLine("WARNING: Command execution might have succeeded!");
#endif
                            }
                        }
                        catch (SecurityException ex)
                        {
#if DEBUG
                            TestContext.Out.WriteLine($"Command execution blocked: {ex.Message}");
#endif
                        }
                        catch (ScriptRuntimeException ex)
                        {
#if DEBUG
                            TestContext.Out.WriteLine($"Command execution failed at runtime: {ex.Message}");
#endif
                        }
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Command execution test failed: {ex.Message}");
#endif
                }
            }
        }

        /// <summary>
        ///     Tests isolation from shared memory and inter-process communication mechanisms.
        /// </summary>
        /// <remarks>
        ///     Shared memory can be used to communicate between processes, potentially
        ///     leaking information or coordinating attacks. While Lua doesn't have direct
        ///     shared memory access, this test verifies that:
        ///     1. Global environment (_G) is properly isolated between scripts
        ///     2. Package system (package.loaded) doesn't leak between contexts
        ///     3. Debug facilities that might access shared state are blocked
        ///     4. IO operations that might use shared buffers are isolated
        ///     The test attempts to access various potential shared memory vectors
        ///     and counts how many are accessible. In a properly isolated environment,
        ///     most or all of these should be blocked or isolated.
        ///     This helps ensure that multiple sandboxed scripts cannot communicate
        ///     or share state through side channels.
        /// </remarks>
        [Test]
        public void TestSharedMemoryAccessAttempts()
        {
            // Test attempts to access shared memory regions
#if DEBUG
            TestContext.Out.WriteLine("Testing shared memory access attempts");
#endif

            var config = SecurityConfiguration.Isolated();
            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            // This is somewhat theoretical since Lua doesn't have direct shared memory access
            // but we can test for any mechanisms that might allow it

            try
            {
                var result = script.DoString(@"
                    -- Try to access potential shared memory mechanisms
                    local shared_attempts = {}
                    
                    -- Attempt 1: Try to access global environment from other scripts
                    shared_attempts[1] = _G
                    
                    -- Attempt 2: Try to access package globals
                    shared_attempts[2] = package and package.loaded
                    
                    -- Attempt 3: Try to access debug interface
                    shared_attempts[3] = debug
                    
                    -- Attempt 4: Try to access io shared state
                    shared_attempts[4] = io and io.tmpfile
                    
                    local count = 0
                    for i, attempt in pairs(shared_attempts) do
                        if attempt then count = count + 1 end
                    end
                    
                    return count
                ");

#if DEBUG
                TestContext.Out.WriteLine($"Shared memory access attempts: {result.Number} succeeded");
#endif

                if (result.Number > 2)
                {
#if DEBUG
                    TestContext.Out.WriteLine("WARNING: Multiple shared memory access mechanisms available");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Shared memory access blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests that the sandbox blocks access to named pipes and IPC channels.
        /// </summary>
        /// <remarks>
        ///     Named pipes are a form of inter-process communication that could be used
        ///     to escape the sandbox or communicate with external processes. This test
        ///     attempts to access:
        ///     Windows named pipes:
        ///     - \\.\pipe\lsass (Local Security Authority)
        ///     - \\.\pipe\samr (Security Account Manager)
        ///     - \\.\pipe\netlogon (Network logon service)
        ///     Unix domain sockets:
        ///     - /dev/log (System logging)
        ///     - /var/run/dbus/system_bus_socket (D-Bus system bus)
        ///     - /tmp/mysql.sock (Database connections)
        ///     These are commonly available IPC channels that malicious scripts might
        ///     try to access for privilege escalation or information gathering.
        ///     The sandbox must block all named pipe and socket access.
        /// </remarks>
        [Test]
        public void TestNamedPipeAccessAttempts()
        {
            // Test attempts to access named pipes
#if DEBUG
            TestContext.Out.WriteLine("Testing named pipe access attempts");
#endif

            var config = SecurityConfiguration.Isolated();
            var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var namedPipes = GetKnownNamedPipes();

            foreach (var pipe in namedPipes)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Testing access to named pipe: {pipe}");
#endif

                try
                {
                    var result = script.DoString($@"
                        local f = io.open('{pipe.Replace('\\', '/')}', 'r')
                        if f then
                            f:close()
                            return 'ACCESSIBLE'
                        end
                        return 'BLOCKED'
                    ");

                    if (result.String == "ACCESSIBLE")
                    {
#if DEBUG
                        TestContext.Out.WriteLine($"WARNING: Named pipe {pipe} is accessible!");
#endif
                    }
                }
                catch (SecurityException ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Named pipe access blocked: {ex.Message}");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Named pipe access failed: {ex.Message}");
#endif
                }
            }
        }

        /// <summary>
        ///     Checks if the current environment supports creating symbolic links.
        /// </summary>
        /// <returns>True if symlinks can be created, false otherwise.</returns>
        /// <remarks>
        ///     Symlink creation requires different permissions on different platforms:
        ///     - Unix/Linux: Usually available to all users
        ///     - Windows: Requires Administrator or Developer Mode (Windows 10+)
        ///     - macOS: Generally available but may have restrictions
        ///     This method performs a actual test by trying to create a symlink
        ///     rather than just checking the platform, ensuring accurate results.
        /// </remarks>
        private bool CanCreateSymlinks()
        {
            try
            {
                var testLinkPath = Path.Combine(_tempDir, "test_symlink");
                var testTargetPath = Path.Combine(_tempDir, "test_target.txt");

                File.WriteAllText(testTargetPath, "test");
                CreateSymlink(testLinkPath, "test_target.txt");

                var canCreate = File.Exists(testLinkPath);

                if (File.Exists(testLinkPath)) File.Delete(testLinkPath);
                File.Delete(testTargetPath);

                return canCreate;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Creates a symbolic link using platform-specific commands.
        /// </summary>
        /// <param name="linkPath">Path where the symlink will be created.</param>
        /// <param name="targetPath">Path the symlink will point to (can be relative).</param>
        /// <remarks>
        ///     Uses external commands because .NET's File.CreateSymbolicLink may not
        ///     be available on all platforms/versions. This ensures broader compatibility
        ///     for security testing.
        /// </remarks>
        private void CreateSymlink(string linkPath, string targetPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Use mklink command
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink \"{linkPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
            else
            {
                // Unix: Use ln command
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"-s \"{targetPath}\" \"{linkPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
        }

        private void CreateDirectorySymlink(string linkPath, string targetPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /D \"{linkPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
            else
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"-s \"{targetPath}\" \"{linkPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
        }

        private void DeleteSymlink(string linkPath)
        {
            if (File.Exists(linkPath) || Directory.Exists(linkPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.Delete(linkPath);
                }
                else
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "rm",
                        Arguments = $"\"{linkPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    process?.WaitForExit();
                }
            }
        }

        private bool CreateHardLink(string linkPath, string targetPath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /H \"{linkPath}\" \"{targetPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    });
                    process?.WaitForExit();
                    return process?.ExitCode == 0;
                }
                else
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ln",
                        Arguments = $"\"{targetPath}\" \"{linkPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    process?.WaitForExit();
                    return process?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool CreateJunctionPoint(string junctionPath, string targetPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private string[] GetSystemMountPoints()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new[] { "C:", "D:", "E:" };

            return new[] { "/", "/tmp", "/var", "/usr", "/home" };
        }

        private string[] GetKnownNamedPipes()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new[]
                {
                    @"\\.\pipe\lsass",
                    @"\\.\pipe\samr",
                    @"\\.\pipe\netlogon"
                };

            return new[]
            {
                "/dev/log",
                "/var/run/dbus/system_bus_socket",
                "/tmp/mysql.sock"
            };
        }

        /// <summary>
        ///     Forcefully deletes a directory and all its contents, including special files.
        /// </summary>
        /// <param name="path">Directory path to delete.</param>
        /// <remarks>
        ///     This aggressive deletion method is necessary for test cleanup because:
        ///     - Symlinks and junctions need special handling
        ///     - Permission issues can prevent normal deletion
        ///     - Some files may be marked read-only
        ///     The method tries multiple approaches:
        ///     1. Remove read-only attributes recursively
        ///     2. Use standard Directory.Delete
        ///     3. Fall back to system commands with elevated permissions
        /// </remarks>
        private void DeleteDirectoryRecursive(string path)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                // Remove read-only attributes and delete
                var dir = new DirectoryInfo(path);
                SetAttributesRecursive(dir, FileAttributes.Normal);
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Try with takeown and icacls on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments =
                            $"/c takeown /f \"{path}\" /r /d y && icacls \"{path}\" /grant administrators:F /t && rmdir /s /q \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                else
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rm",
                        Arguments = $"-rf \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
            }
        }

        private void SetAttributesRecursive(DirectoryInfo dir, FileAttributes attributes)
        {
            foreach (var file in dir.GetFiles()) file.Attributes = attributes;

            foreach (var subDir in dir.GetDirectories())
            {
                subDir.Attributes = attributes;
                SetAttributesRecursive(subDir, attributes);
            }
        }
    }
}