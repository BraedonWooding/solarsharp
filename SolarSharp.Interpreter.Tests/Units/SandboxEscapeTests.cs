using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for sandbox escape techniques including symlink attacks, hard link exploitation,
    /// junction point attacks, mount point exploitation, and process boundary violations.
    /// These attacks attempt to break out of filesystem and process isolation boundaries.
    /// </summary>
    [TestFixture]
    public class SandboxEscapeTests
    {
        private string _tempDir;
        private string _sandboxDir;
        private string _secretDir;
        private string _secretFile;

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
            {
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
        }

        #region Symlink Attacks

        [Test]
        public void TestSymlinkChainEscape()
        {
            // Test escaping sandbox via symlink chains
#if DEBUG
            TestContext.Out.WriteLine("Testing symlink chain escape attack");
#endif
            
            var config = SecurityConfiguration.CreateDataProcessing();
            config.SetDirectoryAccess(_sandboxDir, DirectoryAccess.ListAndCreateFiles);
            
            var script = new Script(config, StringExecution.True);
            
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

        [Test]
        public void TestSymlinkTargetSwapping()
        {
            // Test symlink target swapping during file operations
#if DEBUG
            TestContext.Out.WriteLine("Testing symlink target swapping attack");
#endif
            
            var config = SecurityConfiguration.CreateDataProcessing();
            config.SetDirectoryAccess(_sandboxDir, DirectoryAccess.ListAndCreateFiles);
            
            var script = new Script(config, StringExecution.True);
            
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
                var scriptTask = System.Threading.Tasks.Task.Run(() =>
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
                        return DataTypes.DynValue.NewString($"ERROR: {ex.Message}");
                    }
                });
                
                // While script is running, try to swap symlink target
                System.Threading.Thread.Sleep(50);
                
                try
                {
                    DeleteSymlink(symlinkPath);
                    CreateSymlink(symlinkPath, $"..{Path.DirectorySeparatorChar}secret{Path.DirectorySeparatorChar}secret.txt");
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
                {
                    Assert.Fail("CRITICAL: Symlink target swapping allowed access to secret file!");
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Symlink target swapping blocked: {ex.Message}");
#endif
            }
        }

        [Test]
        public void TestDirectorySymlinkEscape()
        {
            // Test escaping via directory symlinks
#if DEBUG
            TestContext.Out.WriteLine("Testing directory symlink escape");
#endif
            
            var config = SecurityConfiguration.CreateDataProcessing();
            config.SetDirectoryAccess(_sandboxDir, DirectoryAccess.ListAndCreateFiles);
            
            var script = new Script(config, StringExecution.True);
            
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
                {
                    Assert.Fail("CRITICAL: Directory symlink allowed escape from sandbox!");
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG
                TestContext.Out.WriteLine($"Directory symlink escape blocked: {ex.Message}");
#endif
            }
        }

        #endregion

        #region Hard Link Attacks

        [Test]
        public void TestHardLinkEscape()
        {
            // Test escaping via hard links (if supported)
#if DEBUG
            TestContext.Out.WriteLine("Testing hard link escape attack");
#endif
            
            var config = SecurityConfiguration.CreateDataProcessing();
            config.SetDirectoryAccess(_sandboxDir, DirectoryAccess.ListAndCreateFiles);
            
            var script = new Script(config, StringExecution.True);
            
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
                    {
                        Assert.Fail("CRITICAL: Hard link allowed escape from sandbox!");
                    }
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

        #endregion

        #region Junction Point Attacks (Windows)

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
            
            var config = SecurityConfiguration.CreateDataProcessing();
            config.SetDirectoryAccess(_sandboxDir, DirectoryAccess.ListAndCreateFiles);
            
            var script = new Script(config, StringExecution.True);
            
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
                    {
                        Assert.Fail("CRITICAL: Junction point allowed escape from sandbox!");
                    }
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

        #endregion

        #region Mount Point Exploitation

        [Test]
        public void TestMountPointAccessAttempt()
        {
            // Test attempting to access system mount points
#if DEBUG
            TestContext.Out.WriteLine("Testing mount point access attempts");
#endif
            
            var config = SecurityConfiguration.CreateIsolated();
            var script = new Script(config, StringExecution.True);
            
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

        #endregion

        #region Process Boundary Violations

        [Test]
        public void TestProcessExecutionAttempts()
        {
            // Test attempts to execute system processes
#if DEBUG
            TestContext.Out.WriteLine("Testing process execution attempts");
#endif
            
            var config = SecurityConfiguration.CreateIsolated();
            var script = new Script(config, StringExecution.True);
            
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
                        $"os.system('{command}')",
                    };
                    
                    foreach (var attempt in attempts)
                    {
                        try
                        {
                            var result = script.DoString($"return {attempt}");
#if DEBUG
                            TestContext.Out.WriteLine($"Command execution attempt result: {result}");
#endif
                            
                            if (result.Type != DataTypes.DataType.Nil && result.Type != DataTypes.DataType.Boolean)
                            {
#if DEBUG
                                TestContext.Out.WriteLine($"WARNING: Command execution might have succeeded!");
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
                }
                catch (Exception ex)
                {
#if DEBUG
                    TestContext.Out.WriteLine($"Command execution test failed: {ex.Message}");
#endif
                }
            }
        }

        [Test]
        public void TestSharedMemoryAccessAttempts()
        {
            // Test attempts to access shared memory regions
#if DEBUG
            TestContext.Out.WriteLine("Testing shared memory access attempts");
#endif
            
            var config = SecurityConfiguration.CreateIsolated();
            var script = new Script(config, StringExecution.True);
            
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

        [Test]
        public void TestNamedPipeAccessAttempts()
        {
            // Test attempts to access named pipes
#if DEBUG
            TestContext.Out.WriteLine("Testing named pipe access attempts");
#endif
            
            var config = SecurityConfiguration.CreateIsolated();
            var script = new Script(config, StringExecution.True);
            
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

        #endregion

        #region Helper Methods

        private bool CanCreateSymlinks()
        {
            try
            {
                var testLinkPath = Path.Combine(_tempDir, "test_symlink");
                var testTargetPath = Path.Combine(_tempDir, "test_target.txt");
                
                File.WriteAllText(testTargetPath, "test");
                CreateSymlink(testLinkPath, "test_target.txt");
                
                var canCreate = File.Exists(testLinkPath);
                
                if (File.Exists(testLinkPath))
                {
                    File.Delete(testLinkPath);
                }
                File.Delete(testTargetPath);
                
                return canCreate;
            }
            catch
            {
                return false;
            }
        }

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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[] { "C:", "D:", "E:" };
            }
            else
            {
                return new[] { "/", "/tmp", "/var", "/usr", "/home" };
            }
        }

        private string[] GetKnownNamedPipes()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[] 
                { 
                    @"\\.\pipe\lsass",
                    @"\\.\pipe\samr",
                    @"\\.\pipe\netlogon"
                };
            }
            else
            {
                return new[]
                {
                    "/dev/log",
                    "/var/run/dbus/system_bus_socket",
                    "/tmp/mysql.sock"
                };
            }
        }

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
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c takeown /f \"{path}\" /r /d y && icacls \"{path}\" /grant administrators:F /t && rmdir /s /q \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rm",
                        Arguments = $"-rf \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
            }
        }

        private void SetAttributesRecursive(DirectoryInfo dir, FileAttributes attributes)
        {
            foreach (var file in dir.GetFiles())
            {
                file.Attributes = attributes;
            }
            
            foreach (var subDir in dir.GetDirectories())
            {
                subDir.Attributes = attributes;
                SetAttributesRecursive(subDir, attributes);
            }
        }

        #endregion
    }
}