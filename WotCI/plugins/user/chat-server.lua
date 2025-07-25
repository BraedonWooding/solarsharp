-- Chat Server Plugin
-- Demonstrates pub/sub messaging patterns
-- Acts as a message hub for chat clients

local chat_server = {
    name = "Chat Server",
    version = "1.0",
    rooms = {},      -- Chat rooms
    users = {},      -- Connected users
    messageId = 0    -- Message counter
}

api.util.log("Chat Server starting...", "info")

-- Initialize default room
chat_server.rooms["general"] = {
    name = "general",
    topic = "General discussion",
    users = {},
    messageHistory = {},
    created = os.time()
}

-- User registration
api.messages.subscribe("chat.user.register", function(msg)
    local userId = msg.data.userId
    local nickname = msg.data.nickname or userId
    
    -- Check if user already exists
    if chat_server.users[userId] then
        return {
            status = "error",
            error = "User already registered"
        }
    end
    
    -- Register user
    chat_server.users[userId] = {
        id = userId,
        nickname = nickname,
        currentRoom = nil,
        joinedAt = os.time(),
        lastActivity = os.time()
    }
    
    api.util.log("User registered: " .. nickname .. " (" .. userId .. ")", "info")
    
    -- Broadcast user joined
    api.messages.send("chat.user.joined", {
        userId = userId,
        nickname = nickname,
        timestamp = os.time()
    })
    
    return {
        status = "success",
        userId = userId,
        availableRooms = get_room_list()
    }
end)

-- Join room
api.messages.subscribe("chat.room.join", function(msg)
    local userId = msg.data.userId
    local roomName = msg.data.room or "general"
    
    local user = chat_server.users[userId]
    if not user then
        return { status = "error", error = "User not registered" }
    end
    
    local room = chat_server.rooms[roomName]
    if not room then
        return { status = "error", error = "Room does not exist" }
    end
    
    -- Leave current room if any
    if user.currentRoom then
        leave_room(userId, user.currentRoom)
    end
    
    -- Join new room
    user.currentRoom = roomName
    room.users[userId] = true
    
    -- Send room history to user
    local recentMessages = {}
    local historyStart = math.max(1, #room.messageHistory - 20)
    for i = historyStart, #room.messageHistory do
        table.insert(recentMessages, room.messageHistory[i])
    end
    
    -- Notify room members
    broadcast_to_room(roomName, "chat.user.entered", {
        userId = userId,
        nickname = user.nickname,
        room = roomName
    })
    
    return {
        status = "success",
        room = roomName,
        topic = room.topic,
        recentMessages = recentMessages,
        activeUsers = get_room_users(roomName)
    }
end)

-- Send message
api.messages.subscribe("chat.message.send", function(msg)
    local userId = msg.data.userId
    local content = msg.data.content
    
    local user = chat_server.users[userId]
    if not user then
        return { status = "error", error = "User not registered" }
    end
    
    if not user.currentRoom then
        return { status = "error", error = "Not in a room" }
    end
    
    local room = chat_server.rooms[user.currentRoom]
    if not room then
        return { status = "error", error = "Invalid room" }
    end
    
    -- Create message
    chat_server.messageId = chat_server.messageId + 1
    local message = {
        id = chat_server.messageId,
        userId = userId,
        nickname = user.nickname,
        content = content,
        room = user.currentRoom,
        timestamp = os.time()
    }
    
    -- Store in history
    table.insert(room.messageHistory, message)
    
    -- Limit history size
    if #room.messageHistory > 100 then
        table.remove(room.messageHistory, 1)
    end
    
    -- Update user activity
    user.lastActivity = os.time()
    
    -- Broadcast to room members
    broadcast_to_room(user.currentRoom, "chat.message.received", message)
    
    return {
        status = "success",
        messageId = message.id
    }
end)

-- Create room
api.messages.subscribe("chat.room.create", function(msg)
    local roomName = msg.data.name
    local topic = msg.data.topic or ""
    local creatorId = msg.data.userId
    
    if chat_server.rooms[roomName] then
        return { status = "error", error = "Room already exists" }
    end
    
    -- Create new room
    chat_server.rooms[roomName] = {
        name = roomName,
        topic = topic,
        users = {},
        messageHistory = {},
        created = os.time(),
        createdBy = creatorId
    }
    
    api.util.log("Room created: " .. roomName, "info")
    
    -- Broadcast room creation
    api.messages.send("chat.room.created", {
        room = roomName,
        topic = topic,
        createdBy = creatorId
    })
    
    return {
        status = "success",
        room = roomName
    }
end)

-- List rooms
api.messages.subscribe("chat.room.list", function(msg)
    return {
        status = "success",
        rooms = get_room_list()
    }
end)

-- User disconnect
api.messages.subscribe("chat.user.disconnect", function(msg)
    local userId = msg.data.userId
    local user = chat_server.users[userId]
    
    if user and user.currentRoom then
        leave_room(userId, user.currentRoom)
    end
    
    chat_server.users[userId] = nil
    
    api.util.log("User disconnected: " .. userId, "info")
    
    return { status = "success" }
end)

-- Private message
api.messages.subscribe("chat.message.private", function(msg)
    local fromId = msg.data.fromId
    local toId = msg.data.toId
    local content = msg.data.content
    
    local fromUser = chat_server.users[fromId]
    local toUser = chat_server.users[toId]
    
    if not fromUser then
        return { status = "error", error = "Sender not registered" }
    end
    
    if not toUser then
        return { status = "error", error = "Recipient not found" }
    end
    
    -- Send private message directly to recipient
    api.messages.sendTo(toId, "chat.message.private.received", {
        fromId = fromId,
        fromNickname = fromUser.nickname,
        content = content,
        timestamp = os.time()
    })
    
    return { status = "success" }
end)

-- Helper functions
function broadcast_to_room(roomName, messageType, data)
    local room = chat_server.rooms[roomName]
    if not room then return end
    
    -- Send to all users in room
    for userId, _ in pairs(room.users) do
        api.messages.send(messageType, data)
    end
end

function leave_room(userId, roomName)
    local room = chat_server.rooms[roomName]
    if room then
        room.users[userId] = nil
        
        -- Notify room members
        broadcast_to_room(roomName, "chat.user.left", {
            userId = userId,
            nickname = chat_server.users[userId].nickname,
            room = roomName
        })
    end
end

function get_room_list()
    local rooms = {}
    for name, room in pairs(chat_server.rooms) do
        local userCount = 0
        for _ in pairs(room.users) do
            userCount = userCount + 1
        end
        
        table.insert(rooms, {
            name = name,
            topic = room.topic,
            userCount = userCount,
            messageCount = #room.messageHistory
        })
    end
    return rooms
end

function get_room_users(roomName)
    local room = chat_server.rooms[roomName]
    if not room then return {} end
    
    local users = {}
    for userId, _ in pairs(room.users) do
        local user = chat_server.users[userId]
        if user then
            table.insert(users, {
                id = userId,
                nickname = user.nickname,
                lastActivity = user.lastActivity
            })
        end
    end
    return users
end

-- Periodic cleanup of inactive users
function cleanup_inactive_users()
    local now = os.time()
    local timeout = 300  -- 5 minutes
    
    for userId, user in pairs(chat_server.users) do
        if now - user.lastActivity > timeout then
            if user.currentRoom then
                leave_room(userId, user.currentRoom)
            end
            chat_server.users[userId] = nil
            api.util.log("Removed inactive user: " .. userId, "info")
        end
    end
end

-- Announce server availability
api.messages.send("chat.server.online", {
    version = chat_server.version,
    features = {
        "rooms",
        "private_messages",
        "message_history",
        "user_presence"
    }
})

-- Main loop for cleanup (if supported)
if api.util.wait then
    while true do
        cleanup_inactive_users()
        
        -- Broadcast server stats
        local userCount = 0
        for _ in pairs(chat_server.users) do
            userCount = userCount + 1
        end
        
        local roomCount = 0
        for _ in pairs(chat_server.rooms) do
            roomCount = roomCount + 1
        end
        
        api.messages.send("chat.server.stats", {
            users = userCount,
            rooms = roomCount,
            messages = chat_server.messageId
        })
        
        api.util.wait(60)  -- Check every minute
    end
end

api.util.log("Chat Server ready", "info")