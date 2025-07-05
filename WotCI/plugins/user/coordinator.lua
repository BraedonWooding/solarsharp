-- Plugin Coordinator
-- Demonstrates message bus orchestration patterns
-- Shows how to coordinate multiple plugins via messaging

local coordinator = {
    name = "Plugin Coordinator",
    version = "1.0",
    plugins = {},  -- Track registered plugins
    tasks = {}     -- Active tasks
}

-- Initialize coordinator
api.util.log("Plugin Coordinator starting...", "info")

-- Subscribe to plugin registration messages
api.messages.subscribe("coordinator.register", function(msg)
    local pluginId = msg.data.pluginId
    local capabilities = msg.data.capabilities or {}
    
    -- Register the plugin
    coordinator.plugins[pluginId] = {
        id = pluginId,
        name = msg.data.name or pluginId,
        capabilities = capabilities,
        status = "active",
        lastSeen = os.time()
    }
    
    api.util.log("Registered plugin: " .. pluginId .. " with " .. #capabilities .. " capabilities", "info")
    
    -- Acknowledge registration
    return {
        status = "registered",
        coordinatorVersion = coordinator.version,
        assignedId = pluginId
    }
end)

-- Subscribe to plugin heartbeats
api.messages.subscribe("coordinator.heartbeat", function(msg)
    local pluginId = msg.data.pluginId
    
    if coordinator.plugins[pluginId] then
        coordinator.plugins[pluginId].lastSeen = os.time()
        coordinator.plugins[pluginId].status = msg.data.status or "active"
    end
    
    return { acknowledged = true }
end)

-- Task orchestration
api.messages.subscribe("coordinator.task.create", function(msg)
    local taskId = msg.data.taskId or ("task_" .. os.time())
    local taskType = msg.data.type
    local requirements = msg.data.requirements or {}
    
    -- Find suitable plugins for the task
    local candidates = {}
    for id, plugin in pairs(coordinator.plugins) do
        if plugin.status == "active" then
            local suitable = true
            for _, req in ipairs(requirements) do
                local hasCapability = false
                for _, cap in ipairs(plugin.capabilities) do
                    if cap == req then
                        hasCapability = true
                        break
                    end
                end
                if not hasCapability then
                    suitable = false
                    break
                end
            end
            
            if suitable then
                table.insert(candidates, id)
            end
        end
    end
    
    if #candidates == 0 then
        return {
            status = "failed",
            error = "No suitable plugins found for requirements"
        }
    end
    
    -- Create task record
    coordinator.tasks[taskId] = {
        id = taskId,
        type = taskType,
        requirements = requirements,
        assignedTo = candidates[1],  -- Simple assignment to first candidate
        status = "pending",
        createdAt = os.time()
    }
    
    -- Dispatch task to selected plugin
    api.messages.sendTo(candidates[1], "task.execute", {
        taskId = taskId,
        type = taskType,
        data = msg.data.taskData or {},
        deadline = msg.data.deadline
    })
    
    api.util.log("Task " .. taskId .. " assigned to " .. candidates[1], "info")
    
    return {
        status = "created",
        taskId = taskId,
        assignedTo = candidates[1]
    }
end)

-- Task status updates
api.messages.subscribe("coordinator.task.update", function(msg)
    local taskId = msg.data.taskId
    local task = coordinator.tasks[taskId]
    
    if not task then
        return { error = "Unknown task ID" }
    end
    
    -- Update task status
    task.status = msg.data.status
    task.lastUpdate = os.time()
    
    if msg.data.progress then
        task.progress = msg.data.progress
    end
    
    -- Broadcast status update to interested parties
    api.messages.send("task.status.changed", {
        taskId = taskId,
        status = task.status,
        progress = task.progress,
        assignedTo = task.assignedTo
    })
    
    -- Handle task completion
    if task.status == "completed" then
        api.util.log("Task " .. taskId .. " completed successfully", "info")
        
        -- Clean up completed tasks after some time
        api.util.wait(5)
        coordinator.tasks[taskId] = nil
    elseif task.status == "failed" then
        api.util.log("Task " .. taskId .. " failed: " .. (msg.data.error or "Unknown error"), "error")
        
        -- Could implement retry logic here
    end
    
    return { acknowledged = true }
end)

-- Query registered plugins
api.messages.subscribe("coordinator.plugins.list", function(msg)
    local pluginList = {}
    local now = os.time()
    
    for id, plugin in pairs(coordinator.plugins) do
        -- Mark plugins as inactive if no heartbeat for 60 seconds
        if now - plugin.lastSeen > 60 then
            plugin.status = "inactive"
        end
        
        table.insert(pluginList, {
            id = plugin.id,
            name = plugin.name,
            status = plugin.status,
            capabilities = plugin.capabilities,
            lastSeen = plugin.lastSeen
        })
    end
    
    return {
        plugins = pluginList,
        count = #pluginList
    }
end)

-- Query active tasks
api.messages.subscribe("coordinator.tasks.list", function(msg)
    local taskList = {}
    
    for id, task in pairs(coordinator.tasks) do
        table.insert(taskList, {
            id = task.id,
            type = task.type,
            status = task.status,
            assignedTo = task.assignedTo,
            progress = task.progress,
            createdAt = task.createdAt
        })
    end
    
    return {
        tasks = taskList,
        count = #taskList
    }
end)

-- Broadcast coordinator availability
api.messages.send("coordinator.online", {
    version = coordinator.version,
    capabilities = {
        "plugin_registration",
        "task_orchestration",
        "status_monitoring"
    }
})

-- Periodic cleanup of inactive plugins
function cleanup_inactive_plugins()
    local now = os.time()
    local removed = 0
    
    for id, plugin in pairs(coordinator.plugins) do
        if now - plugin.lastSeen > 300 then  -- 5 minutes
            coordinator.plugins[id] = nil
            removed = removed + 1
        end
    end
    
    if removed > 0 then
        api.util.log("Removed " .. removed .. " inactive plugins", "info")
    end
end

-- Main loop (if supported)
if api.util.wait then
    while true do
        cleanup_inactive_plugins()
        
        -- Broadcast coordinator status
        api.messages.send("coordinator.status", {
            activePlugins = 0,  -- Count active plugins
            activeTasks = 0,    -- Count active tasks
            uptime = os.time()
        })
        
        -- Count active plugins
        local activeCount = 0
        for _, plugin in pairs(coordinator.plugins) do
            if plugin.status == "active" then
                activeCount = activeCount + 1
            end
        end
        
        -- Count active tasks
        local taskCount = 0
        for _ in pairs(coordinator.tasks) do
            taskCount = taskCount + 1
        end
        
        -- Update broadcast with actual counts
        api.messages.send("coordinator.status", {
            activePlugins = activeCount,
            activeTasks = taskCount,
            uptime = os.time()
        })
        
        api.util.wait(30)  -- Wait 30 seconds between cleanups
    end
end

api.util.log("Plugin Coordinator ready", "info")