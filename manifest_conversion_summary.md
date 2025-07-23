# ManifestFileAccessTests.cs Conversion Summary

## Overview
Successfully converted all occurrences of PolicyGrant and PolicyRestrictions in ManifestFileAccessTests.cs to the new V2.0 manifest format.

## Key Changes Made

### 1. Removed Grant and Restrict Properties
- Removed all `Grant = new PolicyGrant { ... }` blocks
- Removed all `Restrict = new PolicyRestrictions { ... }` blocks

### 2. Added Direct Properties
- `MaxMemory` and `Timeout` are now direct string properties on ManifestPolicy
- Added `DenyAll` and `InheritFromFile` boolean properties

### 3. New Restriction Model
Each ManifestPolicy now includes:
- **Paths**: ManifestPathRestriction with DenyAll=true and Patterns array
- **Modules**: ManifestModuleRestriction with DenyAll=false and empty Modules array
- **Capabilities**: ManifestCapabilityRestriction with DenyAll=false and empty Capabilities array
- **Hosts**: ManifestHostRestriction with DenyAll=false and empty Patterns array

### 4. Path Pattern Consolidation
- Old: Separate FileRead and FileWrite arrays in PolicyGrant
- New: Single Patterns array in ManifestPathRestriction containing all allowed paths

### 5. Test Assertion Updates
- Changed from `policy.Grant.FileRead` to `policy.Paths.Patterns`
- Changed from `policy.Grant.FileWrite` to `policy.Paths.Patterns`
- Changed from `policy.Restrict.MaxMemory` to `policy.MaxMemory`

## Conversion Pattern Used

### Old Format:
```csharp
Grant = new PolicyGrant
{
    FileRead = new[] { "scripts/*.lua" }.ToImmutableArray(),
    FileWrite = new[] { "output/*.txt" }.ToImmutableArray(),
},
Restrict = new PolicyRestrictions 
{ 
    MaxMemory = "64MB", 
    Timeout = "30s" 
}
```

### New Format:
```csharp
MaxMemory = "64MB",
Timeout = "30s",
Paths = new ManifestPathRestriction
{
    DenyAll = true,
    Patterns = new[] { "scripts/*.lua", "output/*.txt" }.ToImmutableArray()
},
Modules = new ManifestModuleRestriction
{
    DenyAll = false,
    Modules = ImmutableArray<string>.Empty
},
Capabilities = new ManifestCapabilityRestriction
{
    DenyAll = false,
    Capabilities = ImmutableArray<string>.Empty
},
Hosts = new ManifestHostRestriction
{
    DenyAll = false,
    Patterns = ImmutableArray<string>.Empty
},
DenyAll = false,
InheritFromFile = true
```

## Total Conversions
- 11 ManifestPolicy instances converted
- All test assertions updated to match new structure
- Updated XML documentation comments to reflect new restriction-based model

## Verification
- Build verification confirms no compilation errors in ManifestFileAccessTests.cs
- Grep search confirms no remaining references to old format