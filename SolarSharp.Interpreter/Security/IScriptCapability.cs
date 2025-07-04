using System;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Represents a fine-grained capability that can perform specific operations
    /// </summary>
    public interface IScriptCapability
    {
        /// <summary>
        /// Unique name for this capability
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Checks if a specific operation is allowed with the given parameters
        /// </summary>
        /// <param name="operation">The operation being attempted</param>
        /// <param name="parameters">Parameters for the operation</param>
        /// <returns>True if allowed, false if denied</returns>
        bool IsAllowed(string operation, object[] parameters);

        /// <summary>
        /// Executes an operation if allowed
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="parameters">Parameters for the operation</param>
        /// <returns>Result of the operation</returns>
        object Execute(string operation, object[] parameters);

        /// <summary>
        /// Gets the set of operations this capability supports
        /// </summary>
        IReadOnlyCollection<string> SupportedOperations { get; }

        /// <summary>
        /// Validates operation parameters before execution
        /// </summary>
        /// <param name="operation">The operation being validated</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Validation result with any error messages</returns>
        ValidationResult ValidateParameters(string operation, object[] parameters);

        /// <summary>
        /// Gets usage statistics for this capability
        /// </summary>
        CapabilityUsageStats GetUsageStats();
    }

    /// <summary>
    /// Result of parameter validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();

        public static ValidationResult Valid() => new() { IsValid = true };
        public static ValidationResult Invalid(string error) => new() { IsValid = false, ErrorMessage = error };
        public static ValidationResult ValidWithWarning(string warning) => new() 
        { 
            IsValid = true, 
            Warnings = new List<string> { warning } 
        };
    }

    /// <summary>
    /// Usage statistics for a capability
    /// </summary>
    public class CapabilityUsageStats
    {
        public string CapabilityName { get; set; } = string.Empty;
        public Dictionary<string, int> OperationCounts { get; set; } = new();
        public DateTime LastUsed { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalOperations => OperationCounts.Values.Sum();
        public int FailedOperations { get; set; }
        public int BlockedOperations { get; set; }
    }

    /// <summary>
    /// Base implementation of script capability providing common functionality
    /// </summary>
    public abstract class ScriptCapabilityBase : IScriptCapability
    {
        private readonly CapabilityUsageStats _stats;
        private readonly ISecurityAuditor _auditor;

        protected ScriptCapabilityBase(string name, ISecurityAuditor auditor = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _auditor = auditor;
            _stats = new CapabilityUsageStats { CapabilityName = name };
        }

        public string Name { get; }

        public abstract IReadOnlyCollection<string> SupportedOperations { get; }

        public virtual bool IsAllowed(string operation, object[] parameters)
        {
            if (!SupportedOperations.Contains(operation))
                return false;

            var validation = ValidateParameters(operation, parameters);
            if (!validation.IsValid)
            {
                RecordBlockedOperation(operation, validation.ErrorMessage);
                return false;
            }

            return IsOperationAllowed(operation, parameters);
        }

        public virtual object Execute(string operation, object[] parameters)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                if (!IsAllowed(operation, parameters))
                {
                    var ex = new MissingCapabilityException(
                        $"Operation '{operation}' not allowed for capability '{Name}'",
                        operation, parameters);
                    
                    RecordFailedOperation(operation, ex.Message);
                    throw ex;
                }

                var result = ExecuteOperation(operation, parameters);
                RecordSuccessfulOperation(operation, DateTime.UtcNow - startTime);
                
                _auditor?.LogCapabilityUsage(Name, operation, parameters, result, true);
                
                return result;
            }
            catch (Exception ex)
            {
                RecordFailedOperation(operation, ex.Message);
                _auditor?.LogCapabilityUsage(Name, operation, parameters, null, false);
                throw;
            }
        }

        public virtual ValidationResult ValidateParameters(string operation, object[] parameters)
        {
            return ValidationResult.Valid();
        }

        public CapabilityUsageStats GetUsageStats()
        {
            return new CapabilityUsageStats
            {
                CapabilityName = _stats.CapabilityName,
                OperationCounts = new Dictionary<string, int>(_stats.OperationCounts),
                LastUsed = _stats.LastUsed,
                TotalExecutionTime = _stats.TotalExecutionTime,
                FailedOperations = _stats.FailedOperations,
                BlockedOperations = _stats.BlockedOperations
            };
        }

        /// <summary>
        /// Override to implement operation-specific permission checking
        /// </summary>
        protected abstract bool IsOperationAllowed(string operation, object[] parameters);

        /// <summary>
        /// Override to implement the actual operation execution
        /// </summary>
        protected abstract object ExecuteOperation(string operation, object[] parameters);

        private void RecordSuccessfulOperation(string operation, TimeSpan executionTime)
        {
            _stats.OperationCounts[operation] = _stats.OperationCounts.GetValueOrDefault(operation, 0) + 1;
            _stats.LastUsed = DateTime.UtcNow;
            _stats.TotalExecutionTime += executionTime;
        }

        private void RecordFailedOperation(string operation, string error)
        {
            _stats.FailedOperations++;
            _stats.LastUsed = DateTime.UtcNow;
        }

        private void RecordBlockedOperation(string operation, string reason)
        {
            _stats.BlockedOperations++;
            _auditor?.LogSecurityViolation($"Capability '{Name}' blocked operation '{operation}': {reason}");
        }
    }
}