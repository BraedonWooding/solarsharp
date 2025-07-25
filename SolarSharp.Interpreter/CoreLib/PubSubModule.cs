using System;
using System.Collections.Generic;
using System.Text.Json;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Communication;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Lua module for publish/subscribe messaging
    /// </summary>
    [SolarSharpModule(Namespace = "pubsub")]
    public class PubSubModule
    {
        /// <summary>
        /// Retrieves the message bus associated with the given script, if available.
        /// </summary>
        /// <param name="script">The script object that provides access to the message bus service.</param>
        /// <returns>
        /// A result containing the message bus instance if successful, or an error message if the message bus is not available.
        /// </returns>
        private static Result<IMessageBus, string> GetMessageBus(Script script)
        {
            var messageBus = script.GetService<IMessageBus>();
            return messageBus != null
                ? Result.Success<IMessageBus, string>(messageBus)
                : Result.Failure<IMessageBus, string>(
                    "Message bus not available - script must have pubsub permissions in manifest"
                );
        }

        private static Result<ScriptIdentity, string> GetScriptIdentity()
        {
            return ExecutionContextManager.Current.Match(
                ctx =>
                    ctx.Identity.Match(
                        identity => Result.Success<ScriptIdentity, string>(identity),
                        () =>
                            Result.Failure<ScriptIdentity, string>(
                                "No script identity - script must be loaded with manifest"
                            )
                    ),
                () =>
                    Result.Failure<ScriptIdentity, string>(
                        "No execution context - script must be loaded with manifest"
                    )
            );
        }

        /// <summary>
        /// Publishes a message to a topic
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="args">Arguments: topic (string), data (table or string)</param>
        /// <returns>Success (boolean) or error message</returns>
        [MoonSharpModuleMethod]
        public static DynValue publish(ScriptExecutionContext context, CallbackArguments args)
        {
            // Functional pipeline
            var result = GetScript(context)
                .Bind(script =>
                    GetMessageBus(script)
                        .Bind(messageBus =>
                            GetScriptIdentity()
                                .Bind(identity =>
                                    ParseTopic(args)
                                        .Bind(topic =>
                                            ParseData(args)
                                                .Bind(jsonData =>
                                                {
                                                    var message = PubSubMessage.Create(
                                                        topic,
                                                        identity,
                                                        jsonData
                                                    );
                                                    return PublishMessage(messageBus, message);
                                                })
                                        )
                                )
                        )
                );

            return result.Match(
                _ => DynValue.True,
                err => DynValue.NewTuple(DynValue.False, DynValue.NewString(err))
            );
        }

        private static Result<Script, string> GetScript(ScriptExecutionContext context)
        {
            try
            {
                var script = context.GetScript();
                return script != null
                    ? Result.Success<Script, string>(script)
                    : Result.Failure<Script, string>("Script context not available");
            }
            catch (Exception ex)
            {
                return Result.Failure<Script, string>($"Failed to get script: {ex.Message}");
            }
        }

        private static Result<string, string> ParseTopic(CallbackArguments args)
        {
            if (args.Count < 1 || args[0].Type != DataType.String)
                return Result.Failure<string, string>(
                    "publish: first argument must be topic (string)"
                );

            var topic = args[0].String;
            if (string.IsNullOrWhiteSpace(topic))
                return Result.Failure<string, string>("publish: topic cannot be empty");

            return Result.Success<string, string>(topic);
        }

        private static Result<string, string> ParseData(CallbackArguments args)
        {
            if (args.Count < 2)
                return Result.Success<string, string>("{}");

            return args[1].Type switch
            {
                DataType.String => Result.Success<string, string>(args[1].String),
                DataType.Table => TryConvertTableToJson(args[1].Table),
                _ => Result.Failure<string, string>("publish: data must be string or table"),
            };
        }

        private static Result<string, string> PublishMessage(
            IMessageBus messageBus,
            PubSubMessage message
        )
        {
            try
            {
                var task = messageBus.PublishAsync(message);
                task.Wait(); // Lua is synchronous

                return task.Result.Match(
                    () => Result.Success<string, string>("Message published successfully"),
                    err => Result.Failure<string, string>(err.Message)
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<string, string>($"Publish failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a request and waits for a reply
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="args">Arguments: topic (string), data (table or string), timeout (number, optional)</param>
        /// <returns>Reply data (string) or (nil, error message)</returns>
        [MoonSharpModuleMethod]
        public static DynValue request(ScriptExecutionContext context, CallbackArguments args)
        {
            // Functional pipeline
            var result = GetScript(context)
                .Bind(script =>
                    GetMessageBus(script)
                        .Bind(messageBus =>
                            GetScriptIdentity()
                                .Bind(identity =>
                                    ParseTopic(args)
                                        .Bind(topic =>
                                            ParseData(args)
                                                .Bind(jsonData =>
                                                    ParseTimeout(args)
                                                        .Bind(timeout =>
                                                        {
                                                            var message = PubSubMessage.Create(
                                                                topic,
                                                                identity,
                                                                jsonData
                                                            );
                                                            return SendRequest(
                                                                messageBus,
                                                                message,
                                                                timeout
                                                            );
                                                        })
                                                )
                                        )
                                )
                        )
                );

            return result.Match(
                reply => DynValue.NewString(reply),
                err => DynValue.NewTuple(DynValue.Nil, DynValue.NewString(err))
            );
        }

        private static Result<Maybe<TimeSpan>, string> ParseTimeout(CallbackArguments args)
        {
            if (args.Count < 3)
                return Result.Success<Maybe<TimeSpan>, string>(Maybe<TimeSpan>.None);

            if (args[2].Type != DataType.Number)
                return Result.Failure<Maybe<TimeSpan>, string>("request: timeout must be a number");

            var seconds = args[2].Number;
            if (seconds <= 0)
                return Result.Failure<Maybe<TimeSpan>, string>("request: timeout must be positive");

            return Result.Success<Maybe<TimeSpan>, string>(
                Maybe<TimeSpan>.From(TimeSpan.FromSeconds(seconds))
            );
        }

        private static Result<string, string> SendRequest(
            IMessageBus messageBus,
            PubSubMessage message,
            Maybe<TimeSpan> timeout
        )
        {
            try
            {
                var task = messageBus.RequestAsync(message, timeout.GetValueOrDefault());
                task.Wait(); // Lua is synchronous

                return task.Result.Match(
                    reply => Result.Success<string, string>(reply),
                    err => Result.Failure<string, string>(err.Message)
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<string, string>($"Request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the script's identity information
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="args">No arguments</param>
        /// <returns>Table with identity info</returns>
        [MoonSharpModuleMethod]
        public static DynValue identity(ScriptExecutionContext context, CallbackArguments args)
        {
            // Functional pipeline
            var result =
                from script in GetScript(context)
                from identity in GetScriptIdentity()
                from table in CreateIdentityTable(script, identity)
                select DynValue.NewTable(table);

            return result.Match(
                value => value,
                err => DynValue.NewTuple(DynValue.Nil, DynValue.NewString(err))
            );
        }

        /// <summary>
        /// Creates a Lua-compatible table containing identity information from the specified script and script identity.
        /// </summary>
        /// <param name="script">The script associated with the identity for which the table will be created.</param>
        /// <param name="identity">The script identity object containing identity details to populate the table.</param>
        /// <returns>
        /// A result containing the populated identity table if successful, or an error message if table creation fails.
        /// </returns>
        private static Result<Table, string> CreateIdentityTable(
            Script script,
            ScriptIdentity identity
        )
        {
            try
            {
                var table = new Table();
                table["name"] = identity.Name;
                table["version"] = identity.Version.ToString();
                table["token"] = CertificateManager.TokenToHex(identity.PublicKeyToken);

                if (identity.MinVersion != null)
                    table["minVersion"] = identity.MinVersion.ToString();

                if (identity.MaxVersion != null)
                    table["maxVersion"] = identity.MaxVersion.ToString();

                return Result.Success<Table, string>(table);
            }
            catch (Exception ex)
            {
                return Result.Failure<Table, string>(
                    $"Failed to create identity table: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Module initialization - called when module is loaded
        /// </summary>
        /// <param name="globalTable">The global table</param>
        /// <param name="moduleTable">The module table</param>
        public static void MoonSharpInit(Table globalTable, Table moduleTable)
        {
            // Register handler functions that will be called from manifest subscriptions
            // These are registered in the module table but called by the message bus

            // Example subscription handler registration
            // The actual handlers are defined in the manifest and wired during load
        }

        /// <summary>
        /// Tries to convert a Lua table to JSON string
        /// </summary>
        private static Result<string, string> TryConvertTableToJson(Table table)
        {
            try
            {
                return Result.Success<string, string>(ConvertTableToJson(table));
            }
            catch (Exception ex)
            {
                return Result.Failure<string, string>(
                    $"Failed to convert table to JSON: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Converts a Lua table to JSON string
        /// </summary>
        private static string ConvertTableToJson(Table table)
        {
            var jsonObject = ConvertTableToJsonObject(table);
            return JsonSerializer.Serialize(jsonObject);
        }

        /// <summary>
        /// Recursively converts a Lua table to a JSON-serializable object
        /// </summary>
        private static object ConvertTableToJsonObject(Table table)
        {
            // Check if this is an array (sequential numeric keys starting at 1)
            var isArray = true;
            var expectedIndex = 1;

            foreach (var pair in table)
            {
                if (
                    pair.Key.Type != DataType.Number
                    || Math.Abs(pair.Key.Number - expectedIndex) > 0.001
                )
                {
                    isArray = false;
                    break;
                }
                expectedIndex++;
            }

            if (isArray && table.Length > 0)
            {
                // Convert as array
                var array = new object[table.Length];
                for (var i = 0; i < table.Length; i++)
                {
                    array[i] = ConvertDynValueToJsonObject(table.Get(i + 1));
                }
                return array;
            }

            // Convert as object/dictionary
            var dict = new Dictionary<string, object>();

            foreach (var pair in table)
            {
                var key = pair.Key.Type switch
                {
                    DataType.String => pair.Key.String,
                    DataType.Number => pair.Key.Number.ToString(),
                    _ => pair.Key.ToString(),
                };

                dict[key] = ConvertDynValueToJsonObject(pair.Value);
            }

            return dict;
        }

        /// <summary>
        /// Converts a DynValue to a JSON-serializable object
        /// </summary>
        private static object ConvertDynValueToJsonObject(DynValue value)
        {
            return value.Type switch
            {
                DataType.Nil => null,
                DataType.Boolean => value.Boolean,
                DataType.Number => value.Number,
                DataType.String => value.String,
                DataType.Table => ConvertTableToJsonObject(value.Table),
                _ => value.ToString(),
            };
        }
    }
}
