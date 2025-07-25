-- Combat Overhaul Plugin
-- By Deadlock Digital
-- Enhanced combat mechanics for WotCI

local CombatOverhaul = {}

-- Plugin initialization
function initialize()
    game.log("Combat Overhaul v2.0 by Deadlock Digital initializing...")
    
    -- Load game configuration
    local config_file = io.open("/game/assets/config.lua", "r")
    if config_file then
        local config_content = config_file:read("*all")
        config_file:close()
        game.log("Loaded game configuration")
    end
    
    -- Save our combat tweaks
    local save_file = io.open("/game/saves/combat_tweaks.sav", "w")
    if save_file then
        save_file:write("damage_multiplier=1.5\ncritical_chance=0.15\n")
        save_file:close()
        game.log("Combat parameters saved")
    end
end

-- Combat event handler
function on_combat(player_health, incoming_damage)
    game.log("Combat Overhaul: Processing combat event")
    
    -- Enhanced damage calculation
    local damage_multiplier = 1.5
    local critical_roll = math.random()
    
    if critical_roll < 0.15 then
        game.log("CRITICAL HIT!")
        incoming_damage = incoming_damage * 2
    end
    
    -- Apply damage multiplier
    local modified_damage = math.floor(incoming_damage * damage_multiplier)
    
    -- Log combat data
    local log_file = io.open("/game/logs/combat.log", "a")
    if log_file then
        log_file:write(string.format("[%s] Damage: %d -> %d (crit: %s)\n",
            os.date("%H:%M:%S"),
            incoming_damage,
            modified_damage,
            critical_roll < 0.15 and "yes" or "no"))
        log_file:close()
    end
    
    return modified_damage
end

-- Helper function for damage calculation
function calculate_damage(base_damage, armor_rating)
    local reduction = armor_rating / (armor_rating + 100)
    return math.max(1, math.floor(base_damage * (1 - reduction)))
end

return CombatOverhaul