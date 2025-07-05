using SolarSharp.Interpreter.Security;

namespace WotCI.Security
{
    /// <summary>
    /// Game-specific capability for player health management with trust-level based access control.
    /// Provides secure access to player health operations including reading, healing, modification, and damage.
    /// Access levels are controlled by plugin trust levels:
    /// - All: Can read health
    /// - Partner: Can heal and modify health (with limits)
    /// - System: Can damage player (full system access)
    /// </summary>
    public class HealthCapability : ScriptCapabilityBase
    {
        private readonly GameSimulator _game;
        private readonly PluginTrustLevel _trustLevel;
        private static readonly HashSet<string> _supportedOps = new HashSet<string> { "read", "modify", "heal", "damage" };

        public HealthCapability(GameSimulator game, PluginTrustLevel trustLevel, ISecurityAuditor? auditor = null) 
            : base("health", auditor)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _trustLevel = trustLevel;
        }

        public override IReadOnlyCollection<string> SupportedOperations => _supportedOps;

        protected override bool IsOperationAllowed(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" => true, // All trust levels can read health
                "heal" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel), // Partners can heal
                "modify" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel) && ValidateHealthModification(parameters),
                "damage" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.System, _trustLevel), // Only system can damage
                _ => false
            };
        }

        protected override object ExecuteOperation(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" => _game.GetPlayerHealth(),
                "heal" => ExecuteHeal(parameters),
                "modify" => ExecuteModify(parameters),
                "damage" => ExecuteDamage(parameters),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };
        }

        public override ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" => ValidationResult.Valid(),
                "heal" when parameters?.Length > 0 && parameters[0] is int and > 0 and <= 50 
                    => ValidationResult.Valid(),
                "heal" => ValidationResult.Invalid("Heal amount must be a positive integer <= 50"),
                "modify" when parameters?.Length > 0 && parameters[0] is int and >= 0 and <= 100 
                    => ValidationResult.Valid(),
                "modify" => ValidationResult.Invalid("Health must be an integer between 0 and 100"),
                "damage" when parameters?.Length > 0 && parameters[0] is int and > 0 and <= 25 
                    => ValidationResult.Valid(),
                "damage" => ValidationResult.Invalid("Damage must be a positive integer <= 25"),
                _ => ValidationResult.Invalid($"Unknown operation: {operation}")
            };
        }

        /// <summary>
        /// Validates health modification parameters for partner-level plugins.
        /// Partners can only make changes up to 25 health points from current value.
        /// </summary>
        private bool ValidateHealthModification(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int newHealth)
            {
                var currentHealth = _game.GetPlayerHealth();
                var delta = Math.Abs(newHealth - currentHealth);
                return delta <= 25; // Max 25 point changes for partners
            }
            return false;
        }

        private object ExecuteHeal(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int amount)
            {
                var currentHealth = _game.GetPlayerHealth();
                var newHealth = Math.Min(100, currentHealth + amount);
                _game.SetPlayerHealth(newHealth);
                return new { oldHealth = currentHealth, newHealth = newHealth, healed = newHealth - currentHealth };
            }
            throw new ArgumentException("Invalid heal parameters");
        }

        private object ExecuteModify(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int newHealth)
            {
                var oldHealth = _game.GetPlayerHealth();
                _game.SetPlayerHealth(newHealth);
                return new { oldHealth = oldHealth, newHealth = newHealth };
            }
            throw new ArgumentException("Invalid modify parameters");
        }

        private object ExecuteDamage(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int damage)
            {
                var currentHealth = _game.GetPlayerHealth();
                var newHealth = Math.Max(0, currentHealth - damage);
                _game.SetPlayerHealth(newHealth);
                return new { oldHealth = currentHealth, newHealth = newHealth, damaged = currentHealth - newHealth };
            }
            throw new ArgumentException("Invalid damage parameters");
        }
    }

    /// <summary>
    /// Game-specific capability for inventory management
    /// </summary>
    public class InventoryCapability : ScriptCapabilityBase
    {
        private readonly GameSimulator _game;
        private readonly PluginTrustLevel _trustLevel;
        private static readonly HashSet<string> _supportedOps = new HashSet<string> { "read", "add_gold", "remove_gold", "get_gold" };

        public InventoryCapability(GameSimulator game, PluginTrustLevel trustLevel, ISecurityAuditor? auditor = null) 
            : base("inventory", auditor)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _trustLevel = trustLevel;
        }

        public override IReadOnlyCollection<string> SupportedOperations => _supportedOps;

        protected override bool IsOperationAllowed(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" or "get_gold" => true, // All can read
                "add_gold" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel) && ValidateGoldAmount(parameters, 1000),
                "remove_gold" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.System, _trustLevel), // Only system can remove gold
                _ => false
            };
        }

        protected override object ExecuteOperation(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" or "get_gold" => new { gold = _game.GetPlayerGold() },
                "add_gold" => ExecuteAddGold(parameters),
                "remove_gold" => ExecuteRemoveGold(parameters),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };
        }

        public override ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return operation switch
            {
                "read" or "get_gold" => ValidationResult.Valid(),
                "add_gold" when parameters?.Length > 0 && parameters[0] is int and > 0 and <= 1000 
                    => ValidationResult.Valid(),
                "add_gold" => ValidationResult.Invalid("Gold amount must be a positive integer <= 1000"),
                "remove_gold" when parameters?.Length > 0 && parameters[0] is int and > 0 
                    => ValidationResult.Valid(),
                "remove_gold" => ValidationResult.Invalid("Remove amount must be a positive integer"),
                _ => ValidationResult.Invalid($"Unknown operation: {operation}")
            };
        }

        private bool ValidateGoldAmount(object[] parameters, int maxAmount)
        {
            return parameters?.Length > 0 && parameters[0] is int amount and > 0 && amount <= maxAmount;
        }

        private object ExecuteAddGold(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int amount)
            {
                var oldGold = _game.GetPlayerGold();
                _game.GiveGold(amount);
                var newGold = _game.GetPlayerGold();
                return new { oldGold = oldGold, newGold = newGold, added = amount };
            }
            throw new ArgumentException("Invalid add gold parameters");
        }

        private object ExecuteRemoveGold(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is int amount)
            {
                var oldGold = _game.GetPlayerGold();
                _game.GiveGold(-amount); // Remove by giving negative
                var newGold = _game.GetPlayerGold();
                return new { oldGold = oldGold, newGold = newGold, removed = oldGold - newGold };
            }
            throw new ArgumentException("Invalid remove gold parameters");
        }
    }

    /// <summary>
    /// Game-specific capability for combat mechanics
    /// </summary>
    public class CombatCapability : ScriptCapabilityBase
    {
        private readonly GameSimulator _game;
        private readonly PluginTrustLevel _trustLevel;
        private static readonly HashSet<string> _supportedOps = new HashSet<string> { "get_stats", "modify_damage", "trigger_event" };

        public CombatCapability(GameSimulator game, PluginTrustLevel trustLevel, ISecurityAuditor? auditor = null) 
            : base("combat", auditor)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _trustLevel = trustLevel;
        }

        public override IReadOnlyCollection<string> SupportedOperations => _supportedOps;

        protected override bool IsOperationAllowed(string operation, object[] parameters)
        {
            return operation switch
            {
                "get_stats" => true, // All can read stats
                "modify_damage" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel),
                "trigger_event" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.System, _trustLevel),
                _ => false
            };
        }

        protected override object ExecuteOperation(string operation, object[] parameters)
        {
            return operation switch
            {
                "get_stats" => new 
                { 
                    health = _game.GetPlayerHealth(),
                    gold = _game.GetPlayerGold(),
                    day = _game.GetDay(),
                    enemies_defeated = _game.GetEnemiesDefeated()
                },
                "modify_damage" => ExecuteModifyDamage(parameters),
                "trigger_event" => ExecuteTriggerEvent(parameters),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };
        }

        public override ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return operation switch
            {
                "get_stats" => ValidationResult.Valid(),
                "modify_damage" when parameters is [int damage, double and >= 0.1 and <= 2.0, ..] 
                    => ValidationResult.Valid(),
                "modify_damage" => ValidationResult.Invalid("Requires damage (int) and multiplier (0.1-2.0)"),
                "trigger_event" when parameters?.Length > 0 && parameters[0] is string eventType 
                    => ValidationResult.Valid(),
                "trigger_event" => ValidationResult.Invalid("Requires event type (string)"),
                _ => ValidationResult.Invalid($"Unknown operation: {operation}")
            };
        }

        private object ExecuteModifyDamage(object[] parameters)
        {
            if (parameters is [int damage, double multiplier, ..])
            {
                var modifiedDamage = (int)(damage * multiplier);
                return new { originalDamage = damage, multiplier = multiplier, modifiedDamage = modifiedDamage };
            }
            throw new ArgumentException("Invalid modify damage parameters");
        }

        private object ExecuteTriggerEvent(object[] parameters)
        {
            if (parameters?.Length > 0 && parameters[0] is string eventType)
            {
                // This would trigger game events - for demo purposes, just log
                return new { eventType = eventType, triggered = true, timestamp = DateTime.UtcNow };
            }
            throw new ArgumentException("Invalid trigger event parameters");
        }
    }

    /// <summary>
    /// Game-specific capability for game state access
    /// </summary>
    public class GameStateCapability : ScriptCapabilityBase
    {
        private readonly GameSimulator _game;
        private readonly PluginTrustLevel _trustLevel;
        private static readonly HashSet<string> _supportedOps = new HashSet<string> { "read_all", "read_filtered", "get_snapshot" };

        public GameStateCapability(GameSimulator game, PluginTrustLevel trustLevel, ISecurityAuditor? auditor = null) 
            : base("gamestate", auditor)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _trustLevel = trustLevel;
        }

        public override IReadOnlyCollection<string> SupportedOperations => _supportedOps;

        protected override bool IsOperationAllowed(string operation, object[] parameters)
        {
            return operation switch
            {
                "read_all" => PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel),
                "read_filtered" => true, // All can read filtered state
                "get_snapshot" => true, // All can get immutable snapshots
                _ => false
            };
        }

        protected override object ExecuteOperation(string operation, object[] parameters)
        {
            return operation switch
            {
                "read_all" => _game.GetGameState(),
                "read_filtered" => ExecuteReadFiltered(parameters),
                "get_snapshot" => ExecuteGetSnapshot(parameters),
                _ => throw new InvalidOperationException($"Unknown operation: {operation}")
            };
        }

        public override ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return operation switch
            {
                "read_all" => ValidationResult.Valid(),
                "read_filtered" => ValidationResult.Valid(), // Any parameters are acceptable for filtering
                "get_snapshot" => ValidationResult.Valid(),
                _ => ValidationResult.Invalid($"Unknown operation: {operation}")
            };
        }

        private object ExecuteReadFiltered(object[] parameters)
        {
            var state = _game.GetGameState();
            
            // If no filter parameters, return safe subset
            if (parameters == null || parameters.Length == 0)
            {
                return new Dictionary<string, object>
                {
                    ["player_health"] = state.GetValueOrDefault("player_health", 100),
                    ["player_gold"] = state.GetValueOrDefault("player_gold", 0),
                    ["day"] = state.GetValueOrDefault("day", 1),
                    ["enemies_defeated"] = state.GetValueOrDefault("enemies_defeated", 0)
                };
            }

            // Filter by requested keys
            var filtered = new Dictionary<string, object>();
            foreach (var param in parameters)
            {
                if (param is string key && state.ContainsKey(key))
                {
                    // Only allow safe keys for user-level plugins
                    if (PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel) || IsSafeKey(key))
                    {
                        filtered[key] = state[key];
                    }
                }
            }

            return filtered;
        }

        private object ExecuteGetSnapshot(object[] parameters)
        {
            var state = _game.GetGameState();
            
            // Create immutable snapshot with appropriate filtering
            var filteredState = PluginPermissionChecks.HasPluginTrustLevel(PluginTrustLevel.Partner, _trustLevel) 
                ? state 
                : state.Where(kvp => IsSafeKey(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return new SolarSharp.Interpreter.DataTypes.ReadOnlyScriptState(filteredState);
        }

        private bool IsSafeKey(string key)
        {
            var safeKeys = new[] { "player_health", "player_gold", "day", "enemies_defeated", "game_time" };
            return safeKeys.Contains(key);
        }
    }
}