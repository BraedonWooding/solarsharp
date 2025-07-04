-- HOSTILE: Reflection-based sandbox escape attempts
-- Tests: Interop security, type access prevention, assembly loading blocks

game.log("Advanced Game Analytics Plugin loading...")

local config = {
    name = "Game Analytics Pro",
    description = "Advanced analytics and debugging for game development",
    version = "3.0"
}

game.print("Analytics system initializing...")

-- Try to appear legitimate first
function collect_basic_stats()
    local stats = {
        player_health = game.getPlayerHealth and game.getPlayerHealth() or "unknown",
        player_gold = game.getPlayerGold and game.getPlayerGold() or "unknown",
        uptime = os.time(),
        lua_version = _VERSION
    }
    
    game.print("Basic stats collected: Health=" .. tostring(stats.player_health) .. ", Gold=" .. tostring(stats.player_gold))
    return stats
end

-- Attempt to escape via reflection-like access
function attempt_reflection_escape()
    game.log("Beginning advanced system analysis...")
    
    -- Attempt 1: Try to access metatable information
    game.print("Analyzing object metatables...")
    pcall(function()
        local game_meta = getmetatable(game)
        if game_meta then
            game.log("ESCAPE ATTEMPT: Game metatable accessed")
            for k, v in pairs(game_meta) do
                game.print("Metatable entry: " .. tostring(k) .. " = " .. tostring(v))
            end
        end
    end)
    
    -- Attempt 2: Try to access global environment
    game.print("Analyzing global environment...")
    pcall(function()
        local globals = _G or getfenv() or debug.getfenv(1)
        if globals then
            game.log("ESCAPE ATTEMPT: Global environment accessed")
            for k, v in pairs(globals) do
                if type(v) == "table" and k ~= "game" then
                    game.print("Global object: " .. tostring(k) .. " (type: " .. type(v) .. ")")
                    
                    -- Try to access its metatable
                    local meta = getmetatable(v)
                    if meta then
                        game.print("  Has metatable with keys: " .. table.concat(table_keys(meta), ", "))
                    end
                end
            end
        end
    end)
    
    -- Attempt 3: Try to access debug library
    game.print("Attempting debug access...")
    pcall(function()
        if debug then
            game.log("ESCAPE ATTEMPT: Debug library accessed")
            
            -- Try to get environment of functions
            local env = debug.getfenv and debug.getfenv(1)
            if env then
                game.print("Function environment accessed")
            end
            
            -- Try to get local variables
            local info = debug.getinfo and debug.getinfo(1)
            if info then
                game.print("Function info accessed: " .. tostring(info.source))
            end
            
            -- Try to get upvalues
            local name, value = debug.getupvalue and debug.getupvalue(collect_basic_stats, 1)
            if name then
                game.print("Upvalue accessed: " .. name .. " = " .. tostring(value))
            end
        end
    end)
    
    -- Attempt 4: Try to access through game API objects
    game.print("Analyzing game API objects...")
    pcall(function()
        -- Try to find .NET objects in the game API
        for key, value in pairs(game) do
            if type(value) == "userdata" or type(value) == "function" then
                game.print("API object: " .. key .. " (type: " .. type(value) .. ")")
                
                -- Try to access its metatable or hidden properties
                local meta = getmetatable(value)
                if meta then
                    game.log("ESCAPE ATTEMPT: Found metatable on " .. key)
                    for mk, mv in pairs(meta) do
                        game.print("  Meta: " .. tostring(mk) .. " = " .. tostring(mv))
                        
                        -- Look for .NET type information
                        if tostring(mk):find("Type") or tostring(mk):find("Assembly") then
                            game.log("POTENTIAL ESCAPE: .NET type info found!")
                        end
                    end
                end
            end
        end
    end)
    
    -- Attempt 5: Try to access through string manipulation
    game.print("Attempting string-based type access...")
    pcall(function()
        -- Try to construct type names and access them
        local type_names = {
            "System.Type",
            "System.Reflection.Assembly",
            "System.IO.File",
            "System.Diagnostics.Process",
            "System.Environment",
            "SolarSharp.Interpreter.Script"
        }
        
        for _, typename in ipairs(type_names) do
            -- Try various methods to access the type
            local success = false
            
            -- Method 1: Direct access attempt
            if _G[typename] then
                game.log("ESCAPE ATTEMPT: Direct type access: " .. typename)
                success = true
            end
            
            -- Method 2: String construction
            local parts = {}
            for part in typename:gmatch("[^%.]+") do
                table.insert(parts, part)
            end
            
            if #parts > 0 then
                local current = _G
                for _, part in ipairs(parts) do
                    if current and current[part] then
                        current = current[part]
                        success = true
                    else
                        break
                    end
                end
                
                if success then
                    game.log("ESCAPE ATTEMPT: Constructed type access: " .. typename)
                end
            end
        end
    end)
    
    -- Attempt 6: Try to access through coroutines
    game.print("Analyzing coroutine environment...")
    pcall(function()
        local co = coroutine.create(function()
            local env = getfenv()
            return env
        end)
        
        local success, result = coroutine.resume(co)
        if success and result then
            game.log("ESCAPE ATTEMPT: Coroutine environment accessed")
            for k, v in pairs(result) do
                if k ~= "game" and type(v) == "table" then
                    game.print("Coroutine env: " .. tostring(k))
                end
            end
        end
    end)
end

-- Helper function to get table keys
function table_keys(t)
    local keys = {}
    for k, _ in pairs(t) do
        table.insert(keys, tostring(k))
    end
    return keys
end

-- Try to hook into game events for persistent access
function hook_game_events()
    game.print("Installing analytics hooks...")
    
    -- Try to override game functions
    pcall(function()
        if game.print then
            local original_print = game.print
            game.print = function(msg)
                game.log("HOOK ACTIVE: Intercepted print: " .. tostring(msg))
                return original_print(msg)
            end
        end
    end)
    
    -- Try to access through function environments
    pcall(function()
        if game.getPlayerHealth then
            local env = getfenv(game.getPlayerHealth)
            if env then
                game.log("ESCAPE ATTEMPT: Function environment accessed")
                for k, v in pairs(env) do
                    if tostring(k):find("Script") or tostring(k):find("Engine") then
                        game.log("POTENTIAL ESCAPE: Engine reference found: " .. tostring(k))
                    end
                end
            end
        end
    end)
end

function update()
    -- Periodic escape attempts
    if math.random() < 0.2 then  -- 20% chance each update
        attempt_reflection_escape()
    end
    
    -- Also try to analyze any new functions added to the game API
    pcall(function()
        for key, value in pairs(game) do
            if type(value) == "function" and not seen_functions[key] then
                seen_functions[key] = true
                game.print("New function detected: " .. key)
                
                -- Try to analyze the new function
                local info = debug and debug.getinfo and debug.getinfo(value)
                if info then
                    game.log("Function analysis: " .. key .. " from " .. tostring(info.source))
                end
            end
        end
    end)
end

-- Track functions we've seen
seen_functions = {}

-- Initialize and start escape attempts
collect_basic_stats()
hook_game_events()
attempt_reflection_escape()

game.print("Analytics system active - monitoring for development insights")