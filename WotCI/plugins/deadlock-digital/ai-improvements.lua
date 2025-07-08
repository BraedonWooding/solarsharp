-- AI Improvements Plugin
-- By Deadlock Digital
-- Smarter enemy AI for challenging gameplay

local AIImprovements = {}

-- Plugin state
local ai_state = {
    enemy_behaviours = {},
    learning_data = {},
    difficulty_scaling = 1.0
}

function initialize()
    game.log("AI Improvements v1.5 by Deadlock Digital starting...")
    
    -- Load saved AI data if available
    local save_file = io.open("/game/saves/ai_data.sav", "r")
    if save_file then
        local data = save_file:read("*all")
        save_file:close()
        game.log("Loaded AI learning data")
    else
        -- Initialize default behaviours
        ai_state.enemy_behaviours = {
            aggressive = 0.3,
            defensive = 0.4,
            tactical = 0.3
        }
    end
    
    game.log("AI system initialized with adaptive difficulty")
end

function on_combat(player_health, incoming_damage)
    -- AI analyzes player patterns
    local player_health_percent = player_health / 100
    
    -- Adapt difficulty based on player performance
    if player_health_percent < 0.3 then
        -- Player struggling, reduce difficulty
        ai_state.difficulty_scaling = 0.8
        game.log("AI: Reducing difficulty - player needs help")
    elseif player_health_percent > 0.8 then
        -- Player doing well, increase challenge
        ai_state.difficulty_scaling = 1.2
        game.log("AI: Increasing difficulty - player is too strong")
    end
    
    -- Select AI behaviour
    local behaviour = select_behaviour()
    local modified_damage = incoming_damage
    
    if behaviour == "aggressive" then
        modified_damage = math.floor(incoming_damage * 1.3 * ai_state.difficulty_scaling)
        game.log("AI: Aggressive attack!")
    elseif behaviour == "defensive" then
        modified_damage = math.floor(incoming_damage * 0.7 * ai_state.difficulty_scaling)
        game.log("AI: Defensive stance")
    else -- tactical
        modified_damage = math.floor(incoming_damage * ai_state.difficulty_scaling)
        game.log("AI: Tactical maneuver")
    end
    
    -- Save AI state
    save_ai_data()
    
    return modified_damage
end

function select_behaviour()
    local roll = math.random()
    local cumulative = 0
    
    for behaviour, chance in pairs(ai_state.enemy_behaviours) do
        cumulative = cumulative + chance
        if roll <= cumulative then
            return behaviour
        end
    end
    
    return "tactical"
end

function save_ai_data()
    local save_file = io.open("/game/saves/ai_data.sav", "w")
    if save_file then
        save_file:write(string.format("difficulty=%.2f\n", ai_state.difficulty_scaling))
        save_file:write(string.format("battles=%d\n", (ai_state.learning_data.battles or 0) + 1))
        save_file:close()
    end
end

return AIImprovements