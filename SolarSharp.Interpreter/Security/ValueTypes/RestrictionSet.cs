using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.ValueTypes
{
    /// <summary>
    /// Represents a restriction pattern that can be either "deny specific items" or "deny all except specific items".
    /// This is a core domain type for expressing security restrictions in a functional, immutable way.
    /// </summary>
    public abstract record RestrictionSet<T>
    {
        /// <summary>
        /// Creates a restriction that denies specific items.
        /// </summary>
        public static RestrictionSet<T> DenySpecific(params T[] items) =>
            new DenySpecificRestriction<T>(items.ToImmutableHashSet());

        /// <summary>
        /// Creates a restriction that denies specific items.
        /// </summary>
        public static RestrictionSet<T> DenySpecific(IEnumerable<T> items) =>
            new DenySpecificRestriction<T>(items.ToImmutableHashSet());

        /// <summary>
        /// Creates a restriction that denies all items except those specified.
        /// </summary>
        public static RestrictionSet<T> DenyAllExcept(params T[] exceptions) =>
            new DenyAllExceptRestriction<T>(exceptions.ToImmutableHashSet());

        /// <summary>
        /// Creates a restriction that denies all items except those specified.
        /// </summary>
        public static RestrictionSet<T> DenyAllExcept(IEnumerable<T> exceptions) =>
            new DenyAllExceptRestriction<T>(exceptions.ToImmutableHashSet());

        /// <summary>
        /// Creates an empty restriction (denies nothing).
        /// </summary>
        public static RestrictionSet<T> None { get; } = new DenySpecificRestriction<T>(ImmutableHashSet<T>.Empty);

        /// <summary>
        /// Creates a restriction that denies everything.
        /// </summary>
        public static RestrictionSet<T> All { get; } = new DenyAllExceptRestriction<T>(ImmutableHashSet<T>.Empty);

        /// <summary>
        /// Checks if an item is denied by this restriction.
        /// </summary>
        public abstract bool IsDenied(T item);

        /// <summary>
        /// Checks if an item is allowed by this restriction.
        /// </summary>
        public bool IsAllowed(T item) => !IsDenied(item);

        /// <summary>
        /// Combines two restrictions, taking the most restrictive combination.
        /// </summary>
        public abstract RestrictionSet<T> CombineWith(RestrictionSet<T> other);

        /// <summary>
        /// Converts this restriction to a set of allowed items, if possible.
        /// Returns None if the allowed set would be infinite or cannot be enumerated.
        /// </summary>
        public abstract Maybe<ImmutableHashSet<T>> ToAllowedSet();

        /// <summary>
        /// Converts this restriction to a set of denied items, if possible.
        /// Returns None if the denied set would be infinite or cannot be enumerated.
        /// </summary>
        public abstract Maybe<ImmutableHashSet<T>> ToDeniedSet();

        /// <summary>
        /// Gets whether this restriction denies everything.
        /// </summary>
        public abstract bool DeniesEverything { get; }

        /// <summary>
        /// Gets whether this restriction denies nothing.
        /// </summary>
        public abstract bool DeniesNothing { get; }
    }

    /// <summary>
    /// A restriction that denies specific items.
    /// </summary>
    internal sealed record DenySpecificRestriction<T>(ImmutableHashSet<T> DeniedItems) : RestrictionSet<T>
    {
        public override bool IsDenied(T item) => DeniedItems.Contains(item);

        public override RestrictionSet<T> CombineWith(RestrictionSet<T> other)
        {
            return other switch
            {
                DenySpecificRestriction<T> specific => 
                    DenySpecific(DeniedItems.Union(specific.DeniedItems)),
                
                DenyAllExceptRestriction<T> allExcept =>
                    // When combining "deny specific" with "deny all except",
                    // we need to deny all except items that are:
                    // 1. In the exception list of "deny all except"
                    // 2. NOT in our denied items list
                    DenyAllExcept(allExcept.AllowedItems.Except(DeniedItems)),
                
                _ => throw new InvalidOperationException($"Unknown restriction type: {other.GetType()}")
            };
        }

        public override Maybe<ImmutableHashSet<T>> ToAllowedSet() => Maybe<ImmutableHashSet<T>>.None;

        public override Maybe<ImmutableHashSet<T>> ToDeniedSet() => Maybe<ImmutableHashSet<T>>.From(DeniedItems);

        public override bool DeniesEverything => false;

        public override bool DeniesNothing => DeniedItems.IsEmpty;

        public override string ToString() =>
            DeniedItems.IsEmpty ? "Deny: None" : $"Deny: {string.Join(", ", DeniedItems)}";
    }

    /// <summary>
    /// A restriction that denies all items except those explicitly allowed.
    /// </summary>
    internal sealed record DenyAllExceptRestriction<T>(ImmutableHashSet<T> AllowedItems) : RestrictionSet<T>
    {
        public override bool IsDenied(T item) => !AllowedItems.Contains(item);

        public override RestrictionSet<T> CombineWith(RestrictionSet<T> other)
        {
            return other switch
            {
                DenySpecificRestriction<T> specific =>
                    // When combining "deny all except" with "deny specific",
                    // we keep "deny all except" but remove any specifically denied items
                    DenyAllExcept(AllowedItems.Except(specific.DeniedItems)),
                
                DenyAllExceptRestriction<T> allExcept =>
                    // When combining two "deny all except", only items in BOTH allow lists are allowed
                    DenyAllExcept(AllowedItems.Intersect(allExcept.AllowedItems)),
                
                _ => throw new InvalidOperationException($"Unknown restriction type: {other.GetType()}")
            };
        }

        public override Maybe<ImmutableHashSet<T>> ToAllowedSet() => Maybe<ImmutableHashSet<T>>.From(AllowedItems);

        public override Maybe<ImmutableHashSet<T>> ToDeniedSet() => Maybe<ImmutableHashSet<T>>.None;

        public override bool DeniesEverything => AllowedItems.IsEmpty;

        public override bool DeniesNothing => false; // Always denies something (everything outside the allowed set)

        public override string ToString() =>
            AllowedItems.IsEmpty ? "Deny: All" : $"Deny: All except {string.Join(", ", AllowedItems)}";
    }

    /// <summary>
    /// Extension methods for creating RestrictionSet from common patterns.
    /// </summary>
    public static class RestrictionSetExtensions
    {
        /// <summary>
        /// Creates a RestrictionSet from a JSON-like representation.
        /// Supports: { "deny": ["item1", "item2"] } or { "denyAllExcept": ["item1", "item2"] }
        /// </summary>
        public static Result<RestrictionSet<T>, string> FromJsonPattern<T>(
            bool denyAll,
            IEnumerable<T> items,
            Func<string, Result<T, string>> parser)
        {
            if (items == null)
                return Result.Success<RestrictionSet<T>, string>(RestrictionSet<T>.None);

            var itemList = items.ToList();
            if (!itemList.Any())
            {
                return Result.Success<RestrictionSet<T>, string>(
                    denyAll ? RestrictionSet<T>.All : RestrictionSet<T>.None);
            }

            return denyAll
                ? Result.Success<RestrictionSet<T>, string>(RestrictionSet<T>.DenyAllExcept(itemList))
                : Result.Success<RestrictionSet<T>, string>(RestrictionSet<T>.DenySpecific(itemList));
        }

        /// <summary>
        /// Converts a RestrictionSet to a JSON-like representation.
        /// </summary>
        public static (bool denyAll, ImmutableArray<T> items) ToJsonPattern<T>(this RestrictionSet<T> restriction)
        {
            return restriction switch
            {
                DenySpecificRestriction<T> specific => (false, specific.DeniedItems.ToImmutableArray()),
                DenyAllExceptRestriction<T> allExcept => (true, allExcept.AllowedItems.ToImmutableArray()),
                _ => (false, ImmutableArray<T>.Empty)
            };
        }
    }
}