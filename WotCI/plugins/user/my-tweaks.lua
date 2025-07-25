-- My Personal Game Tweaks
-- Just some quality of life improvements

print("Loading my personal tweaks...")

-- Simple helper functions
function quick_heal()
    if game then
        game.set_player_health(100)
        print("Player healed to full!")
    end
end

function show_stats()
    print("=== Game Stats ===")
    print("Health: " .. (game and game.get_player_health() or "unknown"))
    print("Time: " .. os.date("%H:%M:%S"))
    print("==================")
end

-- Auto-save reminder
local last_reminder = 0
function reminder_check()
    local current_time = os.time()
    if current_time - last_reminder > 300 then  -- Every 5 minutes
        print("Remember to save your game!")
        last_reminder = current_time
    end
end

-- Initialize
print("Tweaks loaded! Commands available:")
print("  quick_heal() - Restore full health")
print("  show_stats() - Display game stats")

-- Try to set up a simple config
local my_config = {
    auto_save_reminder = true,
    debug_mode = false,
    favorite_weapon = "sword"
}

print("My configuration loaded successfully!")