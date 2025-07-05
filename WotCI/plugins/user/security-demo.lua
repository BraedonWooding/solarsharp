-- Security Demo Plugin
-- Demonstrates message bus security features and boundaries
-- Shows what happens when security policies are enforced

local security_demo = {
    name = "Security Demo",
    version = "1.0",
    testResults = {}
}

api.util.log("Security Demo starting...", "info")
api.util.log("Trust level: " .. api.util.getTrustLevel(), "info")

-- Test 1: Check allowed message types
function test_message_types()
    api.util.log("=== Test 1: Message Type Restrictions ===", "info")
    
    -- Try to send various message types
    local messageTypes = {
        "game.event",      -- Should work for most
        "ui.update",       -- Should work for most
        "system.admin",    -- Should fail for user level
        "data.sensitive",  -- Should fail for user level
        "test.allowed"     -- Depends on policy
    }
    
    for _, msgType in ipairs(messageTypes) do
        local success, result = pcall(function()
            api.messages.send(msgType, {
                test = "security_check",
                timestamp = os.time()
            })
        end)
        
        if success then
            api.util.log("✓ Allowed to send: " .. msgType, "info")
            table.insert(security_demo.testResults, {
                test = "send_" .. msgType,
                result = "allowed"
            })
        else
            api.util.log("✗ Blocked from sending: " .. msgType .. " (" .. tostring(result) .. ")", "warning")
            table.insert(security_demo.testResults, {
                test = "send_" .. msgType,
                result = "blocked",
                error = tostring(result)
            })
        end
    end
end

-- Test 2: Subscription restrictions
function test_subscriptions()
    api.util.log("=== Test 2: Subscription Restrictions ===", "info")
    
    local subscribeTypes = {
        "game.*",          -- Wildcard subscription
        "system.config",   -- System messages
        "admin.command",   -- Admin messages
        "test.public"      -- Public test messages
    }
    
    for _, msgType in ipairs(subscribeTypes) do
        local success, result = pcall(function()
            api.messages.subscribe(msgType, function(msg)
                api.util.log("Received " .. msgType .. ": " .. tostring(msg.data), "info")
            end)
        end)
        
        if success then
            api.util.log("✓ Allowed to subscribe: " .. msgType, "info")
            table.insert(security_demo.testResults, {
                test = "subscribe_" .. msgType,
                result = "allowed"
            })
        else
            api.util.log("✗ Blocked from subscribing: " .. msgType .. " (" .. tostring(result) .. ")", "warning")
            table.insert(security_demo.testResults, {
                test = "subscribe_" .. msgType,
                result = "blocked",
                error = tostring(result)
            })
        end
    end
end

-- Test 3: Rate limiting
function test_rate_limiting()
    api.util.log("=== Test 3: Rate Limiting ===", "info")
    
    local messageCount = 0
    local blocked = false
    
    -- Try to send many messages quickly
    for i = 1, 20 do
        local success, result = pcall(function()
            api.messages.send("test.rate.limit", {
                index = i,
                timestamp = os.time()
            })
        end)
        
        if success then
            messageCount = messageCount + 1
        else
            if not blocked then
                api.util.log("✗ Rate limit hit after " .. messageCount .. " messages", "warning")
                blocked = true
            end
        end
    end
    
    api.util.log("Successfully sent " .. messageCount .. " messages before rate limit", "info")
    table.insert(security_demo.testResults, {
        test = "rate_limiting",
        result = blocked and "enforced" or "not_reached",
        messagesSent = messageCount
    })
end

-- Test 4: Message size limits
function test_message_size()
    api.util.log("=== Test 4: Message Size Limits ===", "info")
    
    -- Create increasingly large messages
    local sizes = {100, 1000, 10000, 100000, 1000000}  -- bytes
    
    for _, size in ipairs(sizes) do
        -- Create a string of approximately the target size
        local data = string.rep("x", size)
        
        local success, result = pcall(function()
            api.messages.send("test.size.limit", {
                data = data,
                size = size
            })
        end)
        
        if success then
            api.util.log("✓ Sent message of size: " .. size .. " bytes", "info")
            table.insert(security_demo.testResults, {
                test = "message_size_" .. size,
                result = "allowed"
            })
        else
            api.util.log("✗ Blocked message of size: " .. size .. " bytes", "warning")
            table.insert(security_demo.testResults, {
                test = "message_size_" .. size,
                result = "blocked",
                error = tostring(result)
            })
            break  -- No point testing larger sizes
        end
    end
end

-- Test 5: Direct messaging restrictions
function test_direct_messaging()
    api.util.log("=== Test 5: Direct Messaging Restrictions ===", "info")
    
    local targets = {
        "coordinator",           -- Should depend on policy
        "chat-server",          -- Should depend on policy
        "system-admin",         -- Should be blocked for user level
        "trusted-service"       -- Should be blocked for user level
    }
    
    for _, target in ipairs(targets) do
        local success, result = pcall(function()
            return api.messages.sendTo(target, "test.direct", {
                message = "Testing direct communication",
                from = security_demo.name
            })
        end)
        
        if success then
            api.util.log("✓ Allowed to send to: " .. target, "info")
            table.insert(security_demo.testResults, {
                test = "direct_message_" .. target,
                result = "allowed"
            })
        else
            api.util.log("✗ Blocked from sending to: " .. target .. " (" .. tostring(result) .. ")", "warning")
            table.insert(security_demo.testResults, {
                test = "direct_message_" .. target,
                result = "blocked",
                error = tostring(result)
            })
        end
    end
end

-- Test 6: Logging rate limits
function test_logging_limits()
    api.util.log("=== Test 6: Logging Rate Limits ===", "info")
    
    local logCount = 0
    local blocked = false
    
    -- Try to log many messages quickly
    for i = 1, 50 do
        local success = api.util.log("Test log message #" .. i, "debug")
        
        if success then
            logCount = logCount + 1
        else
            if not blocked then
                api.util.log("✗ Logging rate limit hit after " .. logCount .. " messages", "warning")
                blocked = true
                break
            end
        end
    end
    
    api.util.log("Successfully logged " .. logCount .. " messages", "info")
    table.insert(security_demo.testResults, {
        test = "logging_rate_limit",
        result = blocked and "enforced" or "not_reached",
        logsSent = logCount
    })
end

-- Run all tests
function run_security_tests()
    api.util.log("Starting security boundary tests...", "info")
    
    test_message_types()
    api.util.wait(1)
    
    test_subscriptions()
    api.util.wait(1)
    
    test_rate_limiting()
    api.util.wait(2)  -- Wait for rate limit to reset
    
    test_message_size()
    api.util.wait(1)
    
    test_direct_messaging()
    api.util.wait(1)
    
    test_logging_limits()
    
    -- Generate summary report
    generate_summary_report()
end

-- Generate summary report
function generate_summary_report()
    api.util.log("=== Security Test Summary ===", "info")
    
    local summary = {
        plugin = security_demo.name,
        trustLevel = api.util.getTrustLevel(),
        timestamp = os.time(),
        results = security_demo.testResults,
        statistics = {
            total = #security_demo.testResults,
            allowed = 0,
            blocked = 0,
            enforced = 0
        }
    }
    
    -- Count results
    for _, result in ipairs(security_demo.testResults) do
        if result.result == "allowed" then
            summary.statistics.allowed = summary.statistics.allowed + 1
        elseif result.result == "blocked" then
            summary.statistics.blocked = summary.statistics.blocked + 1
        elseif result.result == "enforced" then
            summary.statistics.enforced = summary.statistics.enforced + 1
        end
    end
    
    api.util.log("Total tests: " .. summary.statistics.total, "info")
    api.util.log("Allowed: " .. summary.statistics.allowed, "info")
    api.util.log("Blocked: " .. summary.statistics.blocked, "info")
    api.util.log("Enforced: " .. summary.statistics.enforced, "info")
    
    -- Try to publish results (may be blocked)
    local success, result = pcall(function()
        api.messages.send("security.test.results", summary)
    end)
    
    if success then
        api.util.log("Published test results", "info")
    else
        api.util.log("Could not publish results (expected for user level)", "info")
    end
end

-- Subscribe to security events (if allowed)
local subSuccess = pcall(function()
    api.messages.subscribe("security.violation", function(msg)
        api.util.log("Security violation detected: " .. tostring(msg.data.message), "warning")
    end)
end)

if subSuccess then
    api.util.log("Subscribed to security violations (unexpected for user level!)", "warning")
else
    api.util.log("Cannot subscribe to security violations (expected for user level)", "info")
end

-- Run the tests
run_security_tests()

api.util.log("Security Demo completed", "info")