-- User Configuration Script
-- Personal settings and preferences

local config = {}

-- Gameplay preferences
config.difficulty = "normal"
config.auto_save = true
config.show_hints = true

-- Control settings
config.controls = {
    attack = "left_click",
    block = "right_click",
    jump = "space",
    interact = "e"
}

-- Display preferences
config.display = {
    brightness = 0.8,
    ui_scale = 1.0,
    show_fps = false,
    colorblind_mode = false
}

-- Audio preferences
config.audio = {
    master_volume = 0.7,
    music_enabled = true,
    sound_effects = true,
    voice_volume = 0.9
}

-- Helper function to display config
function show_config()
    print("=== User Configuration ===")
    print("Difficulty: " .. config.difficulty)
    print("Auto-save: " .. tostring(config.auto_save))
    print("UI Scale: " .. config.display.ui_scale)
    print("Master Volume: " .. config.audio.master_volume)
    print("========================")
end

-- Apply configuration
function apply_config()
    print("Applying user configuration...")
    
    -- In a real game, this would actually apply the settings
    if config.auto_save then
        print("Auto-save enabled")
    end
    
    if config.display.colorblind_mode then
        print("Colorblind mode activated")
    end
    
    print("Configuration applied!")
end

-- Initialize on load
print("User configuration loaded")
apply_config()

return config