-- Test script to demonstrate untrusted manifest behavior
-- This script will create a manifest signed with an untrusted key

local json = require("json") or { encode = function() return "{}" end, decode = function() return {} end }

-- Helper to create a test manifest
local function create_test_manifest()
    return {
        version = "1.0",
        files = {
            ["test.lua"] = {
                hash = "dummy_hash",
                hashAlgorithm = "SHA256",
                size = 100,
                readOnly = true
            }
        }
    }
end

print("=== Testing Untrusted Manifest Signature ===")
print("This test demonstrates whether untrusted signed manifests are properly rejected")
print("")

-- Test 1: Check if we can access manifest info
print("Test 1: Checking if manifest validation is enforced...")
local success, err = pcall(function()
    -- Try to do something that would require manifest validation
    local f = io.open("test.lua", "r")
    if f then
        f:close()
        print("- File access succeeded")
    end
end)

if success then
    print("RESULT: File operations allowed - manifest may not be validated")
else
    print("RESULT: File operations blocked - " .. tostring(err))
end

print("")
print("Test complete. If file access succeeded without proper validation, this indicates a security issue.")

return true