using System;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Operations
{
    /// <summary>
    /// Factory for creating validated BasePolicySet instances
    /// </summary>
    public static class BasePolicySetFactory
    {
        private static readonly BasePolicySetValidator Validator = new();

        /// <summary>
        /// Creates a validated BasePolicySet from a PolicySet
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> Create(PolicySet policySet)
        {
            if (policySet == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("PolicySet cannot be null")
                );

            var validationResult = Validator.Validate(policySet);
            if (validationResult.IsValid)
            {
                return Result.Success<BasePolicySet, PolicyValidationError>(
                    new BasePolicySet(policySet)
                );
            }

            var errorMessage = string.Join(
                "; ",
                validationResult.Errors.Select(e => e.ErrorMessage)
            );
            return Result.Failure<BasePolicySet, PolicyValidationError>(
                PolicyValidationError.Create(errorMessage)
            );
        }

        /// <summary>
        /// Creates a BasePolicySet using a builder function for complex configurations
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> CreateFromBuilder(
            Action<PolicySetBuilder> builderAction
        )
        {
            if (builderAction == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Builder action cannot be null")
                );

            try
            {
                var builder = new PolicySetBuilder();
                builderAction(builder);
                var policySet = builder.Build();
                return Create(policySet);
            }
            catch (Exception ex)
            {
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create($"Builder configuration failed: {ex.Message}")
                );
            }
        }
    }
}
