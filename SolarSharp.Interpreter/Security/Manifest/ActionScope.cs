using System;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Represents an action-based scope for manifest rules
    /// </summary>
    public class ActionScope
    {
        /// <summary>
        /// The file pattern this action applies to
        /// </summary>
        public string FilePattern { get; set; }

        /// <summary>
        /// The action type
        /// </summary>
        public ActionType Action { get; set; }

        /// <summary>
        /// Additional context for the action
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        /// <summary>
        /// Creates an action scope
        /// </summary>
        public ActionScope(string filePattern, ActionType action)
        {
            FilePattern = filePattern;
            Action = action;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Checks if this scope matches a given file and action
        /// </summary>
        public bool Matches(string filePath, ActionType requestedAction)
        {
            // Check if file matches pattern
            var scope = new ManifestScope(FilePattern);
            if (!scope.Matches(filePath))
                return false;

            // Check if action matches
            return Action == requestedAction || Action == ActionType.All;
        }

        public override string ToString() => $"{FilePattern}:{Action}";
    }

    /// <summary>
    /// Types of actions that can be controlled
    /// </summary>
    [Flags]
    public enum ActionType
    {
        /// <summary>
        /// No actions allowed
        /// </summary>
        None = 0,

        /// <summary>
        /// Read file contents
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write/modify file contents
        /// </summary>
        Write = 2,

        /// <summary>
        /// Execute as code
        /// </summary>
        Execute = 4,

        /// <summary>
        /// Delete file
        /// </summary>
        Delete = 8,

        /// <summary>
        /// Create new file
        /// </summary>
        Create = 16,

        /// <summary>
        /// List directory contents
        /// </summary>
        List = 32,

        /// <summary>
        /// Load as module
        /// </summary>
        Load = 64,

        /// <summary>
        /// Compile/transform code
        /// </summary>
        Compile = 128,

        /// <summary>
        /// All actions
        /// </summary>
        All = Read | Write | Execute | Delete | Create | List | Load | Compile
    }

    /// <summary>
    /// Manages action-based security rules
    /// </summary>
    public class ActionScopeManager
    {
        private readonly List<ActionScope> _scopes = new List<ActionScope>();
        private readonly Dictionary<string, ActionType> _cache = new Dictionary<string, ActionType>();

        /// <summary>
        /// Adds an action scope
        /// </summary>
        public void AddScope(ActionScope scope)
        {
            _scopes.Add(scope);
            _cache.Clear(); // Invalidate cache
        }

        /// <summary>
        /// Checks if an action is allowed for a file
        /// </summary>
        public bool IsActionAllowed(string filePath, ActionType action)
        {
            // Check cache first
            var cacheKey = $"{filePath}:{action}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                return cachedResult != ActionType.None;
            }

            // Find all matching scopes
            var allowedActions = ActionType.None;
            var deniedActions = ActionType.None;

            foreach (var scope in _scopes)
            {
                if (scope.Matches(filePath, action))
                {
                    // Check if this is an allow or deny rule
                    if (scope.Context.TryGetValue("deny", out var denyObj) && denyObj is bool deny && deny)
                    {
                        deniedActions |= scope.Action;
                    }
                    else
                    {
                        allowedActions |= scope.Action;
                    }
                }
            }

            // Denied actions take precedence
            var effectiveActions = allowedActions & ~deniedActions;
            _cache[cacheKey] = effectiveActions;

            return (effectiveActions & action) == action;
        }

        /// <summary>
        /// Gets all allowed actions for a file
        /// </summary>
        public ActionType GetAllowedActions(string filePath)
        {
            var allowed = ActionType.None;

            foreach (ActionType action in Enum.GetValues(typeof(ActionType)))
            {
                if (action != ActionType.None && action != ActionType.All)
                {
                    if (IsActionAllowed(filePath, action))
                    {
                        allowed |= action;
                    }
                }
            }

            return allowed;
        }

        /// <summary>
        /// Creates standard anti-polymorphism scopes
        /// </summary>
        public static ActionScopeManager CreateAntiPolymorphism()
        {
            var manager = new ActionScopeManager();

            // Lua files can be executed but not modified
            manager.AddScope(new ActionScope("*.lua", ActionType.Execute | ActionType.Read | ActionType.Load));
            manager.AddScope(new ActionScope("*.lua", ActionType.Write | ActionType.Delete | ActionType.Create)
            {
                Context = { ["deny"] = true }
            });

            // Manifest files cannot be accessed at all
            manager.AddScope(new ActionScope("manifest", ActionType.All)
            {
                Context = { ["deny"] = true }
            });

            // Digest-protected files cannot be modified
            manager.AddScope(new ActionScope("digest_target", ActionType.Write | ActionType.Delete)
            {
                Context = { ["deny"] = true }
            });

            return manager;
        }

        /// <summary>
        /// Clears all scopes
        /// </summary>
        public void Clear()
        {
            _scopes.Clear();
            _cache.Clear();
        }
    }

    /// <summary>
    /// Extension methods for integrating action scopes with manifests
    /// </summary>
    public static class ActionScopeExtensions
    {
        /// <summary>
        /// Converts a ManifestRule to ActionScopes
        /// </summary>
        public static IEnumerable<ActionScope> ToActionScopes(this ManifestRule rule)
        {
            if (rule.Target != RuleTarget.Action)
                yield break;

            if (rule is ComposableManifestRule composable)
            {
                var actions = ActionType.None;

                if (composable.CanExecute == true)
                    actions |= ActionType.Execute;
                if (composable.CanModify == true)
                    actions |= ActionType.Write | ActionType.Delete | ActionType.Create;

                if (actions != ActionType.None)
                {
                    yield return new ActionScope(rule.Scope, actions);
                }

                // Add deny rules for false values
                var denyActions = ActionType.None;
                
                if (composable.CanExecute == false)
                    denyActions |= ActionType.Execute;
                if (composable.CanModify == false)
                    denyActions |= ActionType.Write | ActionType.Delete | ActionType.Create;

                if (denyActions != ActionType.None)
                {
                    yield return new ActionScope(rule.Scope, denyActions)
                    {
                        Context = { ["deny"] = true }
                    };
                }
            }
        }

        /// <summary>
        /// Creates an ActionScopeManager from a manifest
        /// </summary>
        public static ActionScopeManager CreateActionScopeManager(this Manifest manifest)
        {
            var manager = new ActionScopeManager();

            foreach (var rule in manifest.Rules.Values)
            {
                foreach (var actionScope in rule.ToActionScopes())
                {
                    manager.AddScope(actionScope);
                }
            }

            return manager;
        }
    }
}