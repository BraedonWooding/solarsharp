-- Sound Mixer Plugin
-- By Segfault Studios
-- Advanced audio processing and 3D sound

local SoundMixer = {}

-- Audio configuration
local audio_config = {
    channels = {
        master = 0.8,
        effects = 0.7,
        music = 0.5,
        voice = 0.9,
        ambient = 0.6
    },
    effects = {
        reverb = true,
        echo_delay = 0.3,
        low_pass_filter = false,
        compressor = true
    },
    spatial = {
        enabled = true,
        listener_position = {x = 0, y = 0, z = 0},
        max_distance = 100
    }
}

function initialize()
    game.log("Sound Mixer v2.1 by Segfault Studios initializing...")
    
    -- Load audio configuration
    local config = io.open("/game/assets/config.lua", "r")
    if config then
        config:close()
        game.log("Audio configuration loaded")
    end
    
    -- Save mixer settings
    save_mixer_preset()
    
    game.log("3D audio system online")
end

function on_sound(sound_name)
    game.log("Mixer: Processing sound '" .. sound_name .. "'")
    
    -- Determine sound category
    local category = categorize_sound(sound_name)
    local volume = calculate_volume(category)
    
    -- Apply effects based on category
    if category == "combat" then
        apply_combat_effects(sound_name)
    elseif category == "ambient" then
        apply_ambient_effects(sound_name)
    elseif category == "ui" then
        -- UI sounds bypass most effects
        game.log("Mixer: UI sound - minimal processing")
    end
    
    -- Log audio event
    log_audio_event(sound_name, category, volume)
end

function categorize_sound(sound_name)
    if string.match(sound_name, "sword") or string.match(sound_name, "spell") then
        return "combat"
    elseif string.match(sound_name, "wind") or string.match(sound_name, "ambient") then
        return "ambient"
    elseif string.match(sound_name, "footstep") then
        return "movement"
    else
        return "misc"
    end
end

function calculate_volume(category)
    local base_volume = audio_config.channels[category] or audio_config.channels.effects
    local master_volume = audio_config.channels.master
    return base_volume * master_volume
end

function apply_combat_effects(sound_name)
    game.log("Mixer: Applying combat audio effects")
    
    if audio_config.effects.reverb then
        game.log("  + Reverb (arena size)")
    end
    
    if audio_config.effects.compressor then
        game.log("  + Dynamic range compression")
    end
    
    -- Add impact emphasis
    game.log("  + Low frequency boost for impact")
end

function apply_ambient_effects(sound_name)
    game.log("Mixer: Applying ambient audio effects")
    
    if audio_config.effects.reverb then
        game.log("  + Environmental reverb")
    end
    
    if audio_config.spatial.enabled then
        -- Calculate 3D position
        local distance = math.random(10, 50)
        local angle = math.random() * math.pi * 2
        game.log(string.format("  + 3D positioning (distance: %.1f, angle: %.1f)", distance, angle))
    end
end

function save_mixer_preset()
    local preset_file = io.open("/game/saves/audio_preset.sav", "w")
    if preset_file then
        preset_file:write("preset=cinematic\n")
        for channel, volume in pairs(audio_config.channels) do
            preset_file:write(string.format("%s_volume=%.2f\n", channel, volume))
        end
        preset_file:close()
    end
end

function log_audio_event(sound_name, category, volume)
    local log_file = io.open("/game/logs/audio.log", "a")
    if log_file then
        log_file:write(string.format("[%s] Sound: %s, Category: %s, Volume: %.2f\n",
            os.date("%H:%M:%S"), sound_name, category, volume))
        log_file:close()
    end
end

return SoundMixer