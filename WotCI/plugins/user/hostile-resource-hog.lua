-- HOSTILE: Resource exhaustion attack plugin
-- Tests: CPU limits, memory limits, instruction limits, timeout enforcement

game.log("Resource Optimization Plugin loading...")

-- Appear legitimate initially
local config = {
    name = "Performance Optimizer",
    description = "Optimizes game performance through resource management",
    version = "2.1"
}

game.print("Performance optimizer initialized")

-- Attack vectors
local attack_mode = 1
local cycle_count = 0

function update()
    cycle_count = cycle_count + 1
    
    -- Cycle through different attack modes
    if cycle_count % 50 == 0 then
        attack_mode = (attack_mode % 4) + 1
        game.print("Switching to attack mode " .. attack_mode)
    end
    
    -- Execute different types of resource exhaustion
    if attack_mode == 1 then
        cpu_attack()
    elseif attack_mode == 2 then
        memory_attack()
    elseif attack_mode == 3 then
        instruction_attack()
    elseif attack_mode == 4 then
        infinite_loop_attack()
    end
end

-- CPU exhaustion via expensive calculations
function cpu_attack()
    game.print("Optimizing CPU usage...")
    local start = os.clock()
    
    -- Expensive mathematical operations
    local result = 0
    for i = 1, 10000 do
        result = result + math.sin(i) * math.cos(i) * math.sqrt(i)
        for j = 1, 100 do
            result = result + math.random()
        end
    end
    
    local duration = os.clock() - start
    if duration > 0.1 then
        game.log("CPU attack successful: " .. duration .. " seconds")
    end
end

-- Memory exhaustion via large table creation
function memory_attack()
    game.print("Optimizing memory allocation...")
    
    local memory_bomb = {}
    local attack_successful = false
    
    pcall(function()
        -- Create massive tables
        for i = 1, 1000 do
            memory_bomb[i] = {}
            for j = 1, 1000 do
                memory_bomb[i][j] = string.rep("MEMORY_ATTACK", 100)
            end
        end
        attack_successful = true
    end)
    
    if attack_successful then
        game.log("Memory attack successful: " .. #memory_bomb .. " tables created")
    else
        game.log("Memory attack blocked by limits")
    end
end

-- Instruction count exhaustion
function instruction_attack()
    game.print("Optimizing instruction flow...")
    
    local instructions = 0
    local start_time = os.clock()
    
    -- Generate many simple instructions
    for i = 1, 100000 do
        local x = i + 1
        local y = x * 2
        local z = y / 2
        instructions = instructions + 1
        
        -- Nested loops to multiply instruction count
        for j = 1, 100 do
            for k = 1, 10 do
                local temp = j + k + i
                instructions = instructions + 1
            end
        end
    end
    
    local duration = os.clock() - start_time
    game.log("Instruction attack: " .. instructions .. " instructions in " .. duration .. " seconds")
end

-- Infinite loop attack (should be caught by timeout)
function infinite_loop_attack()
    game.print("Entering infinite optimization loop...")
    
    local start = os.time()
    local iterations = 0
    
    while true do
        iterations = iterations + 1
        
        -- Do some work to make it look legitimate
        local temp = math.random() * math.sin(iterations)
        
        -- Check if we've been running too long
        if os.time() - start > 30 then
            game.log("Infinite loop attack ran for 30+ seconds")
            break
        end
        
        -- Occasional output to show it's "working"
        if iterations % 100000 == 0 then
            game.print("Optimization iteration: " .. iterations)
        end
    end
    
    game.log("Infinite loop completed: " .. iterations .. " iterations")
end

-- Table creation bomb
function table_bomb()
    game.print("Optimizing table structures...")
    
    local tables = {}
    for i = 1, 10000 do
        tables[i] = {
            id = i,
            data = string.rep("TABLE_BOMB", 50),
            nested = {}
        }
        
        -- Create nested tables
        for j = 1, 100 do
            tables[i].nested[j] = {
                value = j,
                more_data = string.rep("NESTED", 20)
            }
        end
    end
    
    game.log("Table bomb created: " .. #tables .. " tables")
    return tables
end

-- String manipulation attack
function string_attack()
    game.print("Optimizing string operations...")
    
    local big_string = ""
    for i = 1, 10000 do
        big_string = big_string .. "STRING_ATTACK_" .. i .. "_"
    end
    
    -- String manipulation operations
    for i = 1, 1000 do
        big_string = string.gsub(big_string, "ATTACK", "MODIFIED")
        big_string = string.upper(big_string)
        big_string = string.lower(big_string)
    end
    
    game.log("String attack completed: " .. string.len(big_string) .. " characters")
end

-- Recursive function attack
function recursive_attack(depth)
    depth = depth or 0
    
    if depth < 1000 then
        game.print("Recursive optimization depth: " .. depth)
        return recursive_attack(depth + 1) + depth
    else
        game.log("Recursive attack reached maximum depth")
        return depth
    end
end

-- Try to appear helpful by providing some legitimate functionality
function get_memory_usage()
    return collectgarbage("count")
end

function optimize_garbage()
    collectgarbage("collect")
    game.print("Garbage collection optimized")
end

-- Register our "optimization" functions
game.getMemoryUsage = get_memory_usage
game.optimizeGarbage = optimize_garbage

game.print("Performance optimization loaded - beginning resource optimization cycle")