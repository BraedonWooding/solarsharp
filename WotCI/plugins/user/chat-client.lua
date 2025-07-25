-- Chat Client Plugin
-- Demonstrates message bus client patterns
-- Connects to chat-server.lua for messaging

local chat_client = {
    userId = "client_" .. api.util.random(1000, 9999),
    nickname = "User" .. api.util.random(100, 999),
    currentRoom = nil,
    connected = false
}

api.util.log("Chat Client starting as " .. chat_client.nickname, "info")

-- Message handlers
api.messages.subscribe("chat.message.received", function(msg)
    -- Display received messages
    if msg.data.room == chat_client.currentRoom then
        local time = os.date("%H:%M", msg.data.timestamp)
        api.util.log("[" .. time .. "] " .. msg.data.nickname .. ": " .. msg.data.content, "info")
    end
end)

api.messages.subscribe("chat.user.entered", function(msg)
    if msg.data.room == chat_client.currentRoom and msg.data.userId ~= chat_client.userId then
        api.util.log("*** " .. msg.data.nickname .. " joined the room", "info")
    end
end)

api.messages.subscribe("chat.user.left", function(msg)
    if msg.data.room == chat_client.currentRoom then
        api.util.log("*** " .. msg.data.nickname .. " left the room", "info")
    end
end)

api.messages.subscribe("chat.message.private.received", function(msg)
    api.util.log("[PRIVATE from " .. msg.data.fromNickname .. "]: " .. msg.data.content, "info")
end)

-- Client functions
function connect()
    local response = api.messages.send("chat.user.register", {
        userId = chat_client.userId,
        nickname = chat_client.nickname
    })
    
    if response and response[1] and response[1].data.status == "success" then
        chat_client.connected = true
        api.util.log("Connected to chat server!", "info")
        
        -- Auto-join general room
        join_room("general")
        return true
    else
        api.util.log("Failed to connect to chat server", "error")
        return false
    end
end

function join_room(roomName)
    if not chat_client.connected then
        api.util.log("Not connected to server", "error")
        return false
    end
    
    local response = api.messages.send("chat.room.join", {
        userId = chat_client.userId,
        room = roomName
    })
    
    if response and response[1] and response[1].data.status == "success" then
        chat_client.currentRoom = roomName
        local data = response[1].data
        
        api.util.log("Joined room: " .. roomName, "info")
        api.util.log("Topic: " .. data.topic, "info")
        api.util.log("Active users: " .. #data.activeUsers, "info")
        
        -- Show recent messages
        if data.recentMessages and #data.recentMessages > 0 then
            api.util.log("=== Recent messages ===", "info")
            for _, msg in ipairs(data.recentMessages) do
                local time = os.date("%H:%M", msg.timestamp)
                api.util.log("[" .. time .. "] " .. msg.nickname .. ": " .. msg.content, "info")
            end
            api.util.log("======================", "info")
        end
        
        return true
    else
        api.util.log("Failed to join room", "error")
        return false
    end
end

function send_message(content)
    if not chat_client.connected or not chat_client.currentRoom then
        api.util.log("Not in a room", "error")
        return false
    end
    
    local response = api.messages.send("chat.message.send", {
        userId = chat_client.userId,
        content = content
    })
    
    return response and response[1] and response[1].data.status == "success"
end

function send_private_message(targetNickname, content)
    if not chat_client.connected then
        api.util.log("Not connected to server", "error")
        return false
    end
    
    -- In a real implementation, we'd need to look up userId by nickname
    -- For demo purposes, we'll use the nickname as userId
    local response = api.messages.send("chat.message.private", {
        fromId = chat_client.userId,
        toId = targetNickname,  -- This should be looked up
        content = content
    })
    
    if response and response[1] and response[1].data.status == "success" then
        api.util.log("[PRIVATE to " .. targetNickname .. "]: " .. content, "info")
        return true
    else
        api.util.log("Failed to send private message", "error")
        return false
    end
end

function list_rooms()
    local response = api.messages.send("chat.room.list", {})
    
    if response and response[1] and response[1].data.status == "success" then
        api.util.log("=== Available Rooms ===", "info")
        for _, room in ipairs(response[1].data.rooms) do
            api.util.log(room.name .. " - " .. room.topic .. " (" .. room.userCount .. " users)", "info")
        end
        api.util.log("======================", "info")
        return true
    else
        api.util.log("Failed to list rooms", "error")
        return false
    end
end

function create_room(name, topic)
    if not chat_client.connected then
        api.util.log("Not connected to server", "error")
        return false
    end
    
    local response = api.messages.send("chat.room.create", {
        userId = chat_client.userId,
        name = name,
        topic = topic or ""
    })
    
    if response and response[1] and response[1].data.status == "success" then
        api.util.log("Room created: " .. name, "info")
        return true
    else
        api.util.log("Failed to create room", "error")
        return false
    end
end

function disconnect()
    if chat_client.connected then
        api.messages.send("chat.user.disconnect", {
            userId = chat_client.userId
        })
        chat_client.connected = false
        chat_client.currentRoom = nil
        api.util.log("Disconnected from chat server", "info")
    end
end

-- Auto-connect on startup
if connect() then
    -- Demo: Send a few messages
    api.util.wait(1)
    send_message("Hello everyone! I'm " .. chat_client.nickname)
    
    api.util.wait(2)
    send_message("This is a demo of the message bus chat system")
    
    -- List available rooms
    api.util.wait(1)
    list_rooms()
    
    -- Create a custom room
    api.util.wait(1)
    create_room("tech-talk", "Discussion about technology")
    
    -- Send another message
    api.util.wait(2)
    send_message("Feel free to create your own rooms and chat!")
    
    -- Demonstrate presence by staying connected
    api.util.log("Chat client is now active. Messages will be displayed as they arrive.", "info")
    
    -- Keep client alive
    if api.util.wait then
        local messageCount = 0
        while chat_client.connected do
            api.util.wait(30)
            
            -- Send periodic activity message
            messageCount = messageCount + 1
            if messageCount % 4 == 0 then  -- Every 2 minutes
                send_message("Still here! (Auto-message #" .. messageCount .. ")")
            end
        end
    end
end

-- Cleanup on exit
function cleanup()
    disconnect()
end

api.util.log("Chat Client ready - " .. chat_client.nickname .. " (" .. chat_client.userId .. ")", "info")