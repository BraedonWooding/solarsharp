using System;
using System.IO;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Execution
{
    /// <summary>
    /// Internal functional script execution engine
    /// Uses railway-oriented programming for robust error handling
    /// </summary>
    internal static class ScriptEngine
    {
        /// <summary>
        /// Executes Lua code string using functional patterns
        /// </summary>
        internal static Result<DynValue, ScriptError> ExecuteString(
            string code,
            ScriptConfiguration config,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            return ValidateConfiguration(config)
                .Bind(() => ValidateCode(code))
                .Bind(() => CreateScriptContext(config, globalContext))
                .Bind(context => CompileCode(context, code, codeFriendlyName))
                .Bind(compiledScript => ExecuteCompiledScript(compiledScript));
        }

        /// <summary>
        /// Executes Lua code from stream using functional patterns
        /// </summary>
        internal static Result<DynValue, ScriptError> ExecuteStream(
            Stream stream,
            ScriptConfiguration config,
            Table globalContext = null,
            string codeFriendlyName = null
        )
        {
            return ValidateConfiguration(config)
                .Bind(() => ReadStreamContent(stream))
                .Bind(code => ExecuteString(code, config, globalContext, codeFriendlyName));
        }

        /// <summary>
        /// Executes Lua code from file using functional patterns
        /// </summary>
        internal static Result<DynValue, ScriptError> ExecuteFile(
            string filename,
            ScriptConfiguration config,
            Table globalContext = null
        )
        {
            return ValidateConfiguration(config)
                .Bind(() => ValidateFileAccess(filename, config))
                .Bind(() => ReadFileContent(filename))
                .Bind(code => ExecuteString(code, config, globalContext, filename));
        }

        /// <summary>
        /// Validates script configuration
        /// </summary>
        private static UnitResult<ScriptError> ValidateConfiguration(ScriptConfiguration config)
        {
            try
            {
                config.Validate();
                return UnitResult.Success<ScriptError>();
            }
            catch (ArgumentException ex)
            {
                return UnitResult.Failure(ScriptError.Configuration(ex.Message, ex));
            }
        }

        /// <summary>
        /// Validates Lua code input
        /// </summary>
        private static UnitResult<ScriptError> ValidateCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return UnitResult.Failure(
                    ScriptError.Configuration("Code cannot be null or empty")
                );

            // Basic security checks
            if (code.Length > 10_000_000) // 10MB limit for code size
                return UnitResult.Failure(
                    ScriptError.SecurityViolation("Code size exceeds maximum allowed limit")
                );

            return UnitResult.Success<ScriptError>();
        }

        /// <summary>
        /// Creates script execution context
        /// </summary>
        private static Result<ScriptContext, ScriptError> CreateScriptContext(
            ScriptConfiguration config,
            Table globalContext
        )
        {
            try
            {
                var context = new ScriptContext
                {
                    Configuration = config,
                    GlobalTable = globalContext ?? new Table(),
                    StartTime = DateTime.UtcNow,
                };

                return Result.Success<ScriptContext, ScriptError>(context);
            }
            catch (Exception ex)
            {
                return Result.Failure<ScriptContext, ScriptError>(
                    ScriptError.RuntimeError("Failed to create script context", ex)
                );
            }
        }

        /// <summary>
        /// Compiles Lua code into executable form
        /// </summary>
        private static Result<CompiledScript, ScriptError> CompileCode(
            ScriptContext context,
            string code,
            string codeFriendlyName
        )
        {
            try
            {
                // This would integrate with the existing Script compilation logic
                // For now, this is a placeholder that shows the functional structure
                var compiled = new CompiledScript
                {
                    Context = context,
                    Code = code,
                    FriendlyName = codeFriendlyName ?? "chunk",
                    CompileTime = DateTime.UtcNow,
                };

                return Result.Success<CompiledScript, ScriptError>(compiled);
            }
            catch (Exception ex)
            {
                return Result.Failure<CompiledScript, ScriptError>(
                    ScriptError.SyntaxError($"Compilation failed: {ex.Message}", ex)
                );
            }
        }

        /// <summary>
        /// Executes compiled script
        /// </summary>
        private static Result<DynValue, ScriptError> ExecuteCompiledScript(CompiledScript compiled)
        {
            try
            {
                // Check timeout
                var elapsed = DateTime.UtcNow - compiled.Context.StartTime;
                if (elapsed.TotalMilliseconds > compiled.Context.Configuration.TimeoutMs)
                {
                    return Result.Failure<DynValue, ScriptError>(
                        ScriptError.ResourceExhausted("Script execution timed out")
                    );
                }

                // This would integrate with the existing Script execution logic
                // For now, return a placeholder result
                var result = DynValue.NewString("Functional execution result");

                return Result.Success<DynValue, ScriptError>(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<DynValue, ScriptError>(
                    ScriptError.RuntimeError($"Execution failed: {ex.Message}", ex)
                );
            }
        }

        /// <summary>
        /// Validates file access permissions
        /// </summary>
        private static UnitResult<ScriptError> ValidateFileAccess(
            string filename,
            ScriptConfiguration config
        )
        {
            if (!config.AllowFileSystemAccess)
                return UnitResult.Failure(
                    ScriptError.SecurityViolation("File system access is not allowed")
                );

            if (!File.Exists(filename))
                return UnitResult.Failure(ScriptError.RuntimeError($"File not found: {filename}"));

            // Additional security checks could go here
            return UnitResult.Success<ScriptError>();
        }

        /// <summary>
        /// Reads file content safely
        /// </summary>
        private static Result<string, ScriptError> ReadFileContent(string filename)
        {
            try
            {
                var content = File.ReadAllText(filename);
                return Result.Success<string, ScriptError>(content);
            }
            catch (Exception ex)
            {
                return Result.Failure<string, ScriptError>(
                    ScriptError.RuntimeError($"Failed to read file {filename}: {ex.Message}", ex)
                );
            }
        }

        /// <summary>
        /// Reads stream content safely
        /// </summary>
        private static Result<string, ScriptError> ReadStreamContent(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                return Result.Success<string, ScriptError>(content);
            }
            catch (Exception ex)
            {
                return Result.Failure<string, ScriptError>(
                    ScriptError.RuntimeError($"Failed to read stream: {ex.Message}", ex)
                );
            }
        }
    }

    /// <summary>
    /// Internal script execution context
    /// </summary>
    internal sealed record ScriptContext
    {
        public ScriptConfiguration Configuration { get; init; } = new ScriptConfiguration();
        public Table GlobalTable { get; init; }
        public DateTime StartTime { get; init; }
    }

    /// <summary>
    /// Internal compiled script representation
    /// </summary>
    internal sealed record CompiledScript
    {
        public ScriptContext Context { get; init; }
        public string Code { get; init; } = "";
        public string FriendlyName { get; init; } = "";
        public DateTime CompileTime { get; init; }
    }
}
