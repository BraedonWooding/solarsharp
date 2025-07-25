#nullable enable

using System;
using System.Collections.Immutable;
using SolarSharp.Interpreter.Security.Identity;

namespace SolarSharp.Interpreter.Security.Auditing
{
    /// <summary>
    /// Event raised when trust store is modified (keys added/removed)
    /// </summary>
    public sealed record TrustStoreModifiedEvent : SecurityAuditEvent
    {
        /// <summary>
        /// Type of trust store modification
        /// </summary>
        public TrustStoreModificationType ModificationType { get; init; } =
            TrustStoreModificationType.KeyAdded;

        /// <summary>
        /// Name of the trust store that was modified
        /// </summary>
        public string TrustStoreName { get; init; } = string.Empty;

        /// <summary>
        /// Public key fingerprint that was added/removed
        /// </summary>
        public string PublicKeyFingerprint { get; init; } = string.Empty;

        /// <summary>
        /// Certificate subject name (if applicable)
        /// </summary>
        public string? CertificateSubject { get; init; }

        /// <summary>
        /// Certificate issuer name (if applicable)
        /// </summary>
        public string? CertificateIssuer { get; init; }

        /// <summary>
        /// Certificate expiration date (if applicable)
        /// </summary>
        public DateTime? CertificateExpirationDate { get; init; }

        /// <summary>
        /// Trust level associated with the key
        /// </summary>
        public string TrustLevel { get; init; } = string.Empty;

        /// <summary>
        /// Reason for the modification
        /// </summary>
        public string ModificationReason { get; init; } = string.Empty;

        /// <summary>
        /// Admin or user who performed the modification
        /// </summary>
        public string ModifiedBy { get; init; } = string.Empty;

        /// <summary>
        /// Previous trust level (for updates)
        /// </summary>
        public string? PreviousTrustLevel { get; init; }

        /// <summary>
        /// Key usage restrictions
        /// </summary>
        public ImmutableArray<string> KeyUsageRestrictions { get; init; } =
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Expiration date for the trust relationship
        /// </summary>
        public DateTime? TrustExpirationDate { get; init; }

        /// <summary>
        /// Additional key metadata
        /// </summary>
        public ImmutableDictionary<string, string> KeyMetadata { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Creates a new trust store modified event
        /// </summary>
        public TrustStoreModifiedEvent()
            : base(SecurityEventType.TrustStoreModified)
        {
            Success = true;
            Operation = "trust_store_modified";
        }

        /// <summary>
        /// Creates a new trust store modified event with specified parameters
        /// </summary>
        public TrustStoreModifiedEvent(
            string scriptId,
            TrustStoreModificationType modificationType,
            string trustStoreName,
            string publicKeyFingerprint,
            string trustLevel,
            string modificationReason,
            string modifiedBy,
            string principal = "",
            ScriptIdentity? scriptIdentity = null,
            string? certificateSubject = null,
            string? certificateIssuer = null,
            DateTime? certificateExpirationDate = null,
            string? previousTrustLevel = null,
            ImmutableArray<string> keyUsageRestrictions = default,
            DateTime? trustExpirationDate = null,
            ImmutableDictionary<string, string>? keyMetadata = null
        )
            : this()
        {
            ScriptId = scriptId;
            ModificationType = modificationType;
            TrustStoreName = trustStoreName;
            PublicKeyFingerprint = publicKeyFingerprint;
            TrustLevel = trustLevel;
            ModificationReason = modificationReason;
            ModifiedBy = modifiedBy;
            Principal = principal;
            ScriptIdentity = scriptIdentity;
            CertificateSubject = certificateSubject;
            CertificateIssuer = certificateIssuer;
            CertificateExpirationDate = certificateExpirationDate;
            PreviousTrustLevel = previousTrustLevel;
            KeyUsageRestrictions = keyUsageRestrictions.IsDefault
                ? ImmutableArray<string>.Empty
                : keyUsageRestrictions;
            TrustExpirationDate = trustExpirationDate;
            KeyMetadata = keyMetadata ?? ImmutableDictionary<string, string>.Empty;
        }

        /// <summary>
        /// Creates a copy with certificate information
        /// </summary>
        public TrustStoreModifiedEvent WithCertificateInfo(
            string subject,
            string issuer,
            DateTime expirationDate
        )
        {
            return this with
            {
                CertificateSubject = subject,
                CertificateIssuer = issuer,
                CertificateExpirationDate = expirationDate,
            };
        }

        /// <summary>
        /// Creates a copy with key usage restrictions
        /// </summary>
        public TrustStoreModifiedEvent WithKeyUsageRestrictions(ImmutableArray<string> restrictions)
        {
            return this with { KeyUsageRestrictions = restrictions };
        }

        /// <summary>
        /// Creates a copy with trust expiration date
        /// </summary>
        public TrustStoreModifiedEvent WithTrustExpiration(DateTime expirationDate)
        {
            return this with { TrustExpirationDate = expirationDate };
        }

        /// <summary>
        /// Creates a copy with additional key metadata
        /// </summary>
        public TrustStoreModifiedEvent WithKeyMetadata(string key, string value)
        {
            return this with { KeyMetadata = KeyMetadata.Add(key, value) };
        }

        /// <summary>
        /// Creates a copy with previous trust level (for updates)
        /// </summary>
        public TrustStoreModifiedEvent WithPreviousTrustLevel(string previousLevel)
        {
            return this with { PreviousTrustLevel = previousLevel };
        }

        /// <summary>
        /// Gets a human-readable description of the modification
        /// </summary>
        public string GetModificationDescription()
        {
            var action = ModificationType switch
            {
                TrustStoreModificationType.KeyAdded => "added to",
                TrustStoreModificationType.KeyRemoved => "removed from",
                TrustStoreModificationType.KeyUpdated => "updated in",
                TrustStoreModificationType.TrustStoreCleared => "cleared from",
                TrustStoreModificationType.TrustStoreCreated => "created in",
                TrustStoreModificationType.TrustStoreDestroyed => "destroyed from",
                _ => "modified in",
            };

            var keyInfo = string.IsNullOrEmpty(PublicKeyFingerprint)
                ? "trust store"
                : $"key {PublicKeyFingerprint[..8]}...";

            return $"{keyInfo} {action} {TrustStoreName}";
        }
    }

    /// <summary>
    /// Types of trust store modifications
    /// </summary>
    public enum TrustStoreModificationType
    {
        /// <summary>
        /// A new key was added to the trust store
        /// </summary>
        KeyAdded,

        /// <summary>
        /// A key was removed from the trust store
        /// </summary>
        KeyRemoved,

        /// <summary>
        /// An existing key's trust level or metadata was updated
        /// </summary>
        KeyUpdated,

        /// <summary>
        /// All keys were cleared from the trust store
        /// </summary>
        TrustStoreCleared,

        /// <summary>
        /// A new trust store was created
        /// </summary>
        TrustStoreCreated,

        /// <summary>
        /// A trust store was destroyed
        /// </summary>
        TrustStoreDestroyed,

        /// <summary>
        /// Trust store backup was created
        /// </summary>
        TrustStoreBackedUp,

        /// <summary>
        /// Trust store was restored from backup
        /// </summary>
        TrustStoreRestored,
    }

    /// <summary>
    /// Factory methods for creating trust store modification events
    /// </summary>
    public static class TrustStoreEventFactory
    {
        /// <summary>
        /// Creates a key added event
        /// </summary>
        public static TrustStoreModifiedEvent CreateKeyAdded(
            string scriptId,
            string trustStoreName,
            string publicKeyFingerprint,
            string trustLevel,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.KeyAdded,
                trustStoreName,
                publicKeyFingerprint,
                trustLevel,
                reason,
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a key removed event
        /// </summary>
        public static TrustStoreModifiedEvent CreateKeyRemoved(
            string scriptId,
            string trustStoreName,
            string publicKeyFingerprint,
            string trustLevel,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.KeyRemoved,
                trustStoreName,
                publicKeyFingerprint,
                trustLevel,
                reason,
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a key updated event
        /// </summary>
        public static TrustStoreModifiedEvent CreateKeyUpdated(
            string scriptId,
            string trustStoreName,
            string publicKeyFingerprint,
            string newTrustLevel,
            string previousTrustLevel,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.KeyUpdated,
                trustStoreName,
                publicKeyFingerprint,
                newTrustLevel,
                reason,
                modifiedBy,
                principal,
                previousTrustLevel: previousTrustLevel
            );
        }

        /// <summary>
        /// Creates a trust store cleared event
        /// </summary>
        public static TrustStoreModifiedEvent CreateTrustStoreCleared(
            string scriptId,
            string trustStoreName,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.TrustStoreCleared,
                trustStoreName,
                "",
                "",
                reason,
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a trust store created event
        /// </summary>
        public static TrustStoreModifiedEvent CreateTrustStoreCreated(
            string scriptId,
            string trustStoreName,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.TrustStoreCreated,
                trustStoreName,
                "",
                "",
                reason,
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a trust store destroyed event
        /// </summary>
        public static TrustStoreModifiedEvent CreateTrustStoreDestroyed(
            string scriptId,
            string trustStoreName,
            string modifiedBy,
            string reason,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.TrustStoreDestroyed,
                trustStoreName,
                "",
                "",
                reason,
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a certificate-based key added event
        /// </summary>
        public static TrustStoreModifiedEvent CreateCertificateKeyAdded(
            string scriptId,
            string trustStoreName,
            string publicKeyFingerprint,
            string trustLevel,
            string modifiedBy,
            string reason,
            string certificateSubject,
            string certificateIssuer,
            DateTime certificateExpirationDate,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.KeyAdded,
                trustStoreName,
                publicKeyFingerprint,
                trustLevel,
                reason,
                modifiedBy,
                principal,
                certificateSubject: certificateSubject,
                certificateIssuer: certificateIssuer,
                certificateExpirationDate: certificateExpirationDate
            );
        }

        /// <summary>
        /// Creates a backup event
        /// </summary>
        public static TrustStoreModifiedEvent CreateBackup(
            string scriptId,
            string trustStoreName,
            string modifiedBy,
            string backupLocation,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.TrustStoreBackedUp,
                trustStoreName,
                "",
                "",
                $"Backup created at {backupLocation}",
                modifiedBy,
                principal
            );
        }

        /// <summary>
        /// Creates a restore event
        /// </summary>
        public static TrustStoreModifiedEvent CreateRestore(
            string scriptId,
            string trustStoreName,
            string modifiedBy,
            string backupLocation,
            string principal = ""
        )
        {
            return new TrustStoreModifiedEvent(
                scriptId,
                TrustStoreModificationType.TrustStoreRestored,
                trustStoreName,
                "",
                "",
                $"Restored from backup at {backupLocation}",
                modifiedBy,
                principal
            );
        }
    }
}
