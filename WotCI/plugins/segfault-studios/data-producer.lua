-- Data Producer Plugin
-- By Segfault Studios (Partner Level)
-- Demonstrates data streaming patterns via message bus

local data_producer = {
    name = "Data Producer",
    version = "1.0",
    sensorId = "sensor_" .. api.util.random(1000, 9999),
    productionRate = 1,  -- Messages per second
    totalProduced = 0
}

api.util.log("Data Producer starting - Sensor ID: " .. data_producer.sensorId, "info")

-- Register with coordinator if available
api.messages.send("coordinator.register", {
    pluginId = "data-producer-" .. data_producer.sensorId,
    name = data_producer.name,
    capabilities = {"data_generation", "streaming", "telemetry"}
})

-- Configuration handler
api.messages.subscribe("producer.config.update", function(msg)
    if msg.data.sensorId == data_producer.sensorId or msg.data.sensorId == "*" then
        if msg.data.productionRate then
            data_producer.productionRate = math.max(0.1, math.min(10, msg.data.productionRate))
            api.util.log("Production rate updated to: " .. data_producer.productionRate .. " msg/sec", "info")
        end
        
        return {
            status = "configured",
            sensorId = data_producer.sensorId,
            productionRate = data_producer.productionRate
        }
    end
end)

-- Generate sensor data
function generate_sensor_data()
    -- Simulate various sensor readings
    local data_types = {
        temperature = function()
            return {
                value = 20 + api.util.random() * 10,  -- 20-30°C
                unit = "celsius"
            }
        end,
        humidity = function()
            return {
                value = 40 + api.util.random() * 40,  -- 40-80%
                unit = "percent"
            }
        end,
        pressure = function()
            return {
                value = 1000 + api.util.random() * 50,  -- 1000-1050 hPa
                unit = "hPa"
            }
        end,
        power = function()
            return {
                value = 100 + api.util.random() * 900,  -- 100-1000W
                unit = "watts"
            }
        end,
        cpu_usage = function()
            return {
                value = api.util.random() * 100,  -- 0-100%
                unit = "percent"
            }
        end
    }
    
    -- Pick a random data type
    local types = {}
    for k, _ in pairs(data_types) do
        table.insert(types, k)
    end
    
    local dataType = types[api.util.random(1, #types)]
    local reading = data_types[dataType]()
    
    return {
        type = dataType,
        sensorId = data_producer.sensorId,
        value = reading.value,
        unit = reading.unit,
        timestamp = os.time(),
        sequence = data_producer.totalProduced + 1
    }
end

-- Batch data generation
function generate_batch(size)
    local batch = {
        batchId = "batch_" .. os.time() .. "_" .. api.util.random(1000, 9999),
        sensorId = data_producer.sensorId,
        data = {},
        startTime = os.time()
    }
    
    for i = 1, size do
        table.insert(batch.data, generate_sensor_data())
        data_producer.totalProduced = data_producer.totalProduced + 1
    end
    
    batch.endTime = os.time()
    batch.count = size
    
    return batch
end

-- Stream control handlers
api.messages.subscribe("producer.stream.start", function(msg)
    if msg.data.sensorId == data_producer.sensorId or msg.data.sensorId == "*" then
        data_producer.streaming = true
        api.util.log("Streaming started", "info")
        return { status = "streaming", sensorId = data_producer.sensorId }
    end
end)

api.messages.subscribe("producer.stream.stop", function(msg)
    if msg.data.sensorId == data_producer.sensorId or msg.data.sensorId == "*" then
        data_producer.streaming = false
        api.util.log("Streaming stopped", "info")
        return { status = "stopped", sensorId = data_producer.sensorId }
    end
end)

-- Status reporting
api.messages.subscribe("producer.status.request", function(msg)
    return {
        sensorId = data_producer.sensorId,
        status = data_producer.streaming and "streaming" or "idle",
        totalProduced = data_producer.totalProduced,
        productionRate = data_producer.productionRate,
        uptime = os.time()
    }
end)

-- Main production loop
data_producer.streaming = true  -- Start streaming by default

if api.util.wait then
    api.util.log("Starting data production loop", "info")
    
    local lastBatchTime = os.time()
    local batchInterval = 10  -- Send batch every 10 seconds
    
    while true do
        if data_producer.streaming then
            -- Generate and send individual data points
            local data = generate_sensor_data()
            
            -- Send raw data
            api.messages.send("data.raw", data)
            
            -- Send typed data for specific processors
            api.messages.send("data." .. data.type, {
                sensorId = data.sensorId,
                value = data.value,
                unit = data.unit,
                timestamp = data.timestamp
            })
            
            data_producer.totalProduced = data_producer.totalProduced + 1
            
            -- Check if it's time for a batch
            if os.time() - lastBatchTime >= batchInterval then
                -- Generate and send batch
                local batch = generate_batch(10)
                api.messages.send("data.batch", batch)
                
                api.util.log("Sent batch " .. batch.batchId .. " with " .. batch.count .. " items", "info")
                lastBatchTime = os.time()
            end
            
            -- Send high-priority alerts for anomalies
            if data.type == "temperature" and data.value > 28 then
                api.messages.send("data.alert", {
                    level = "warning",
                    sensorId = data.sensorId,
                    message = "High temperature detected",
                    value = data.value,
                    threshold = 28,
                    timestamp = data.timestamp
                })
            elseif data.type == "cpu_usage" and data.value > 90 then
                api.messages.send("data.alert", {
                    level = "critical",
                    sensorId = data.sensorId,
                    message = "Critical CPU usage",
                    value = data.value,
                    threshold = 90,
                    timestamp = data.timestamp
                })
            end
        end
        
        -- Heartbeat
        if data_producer.totalProduced % 100 == 0 then
            api.messages.send("coordinator.heartbeat", {
                pluginId = "data-producer-" .. data_producer.sensorId,
                status = data_producer.streaming and "active" or "idle"
            })
        end
        
        -- Wait based on production rate
        api.util.wait(1.0 / data_producer.productionRate)
    end
else
    -- If no wait support, just produce a batch
    local batch = generate_batch(100)
    api.messages.send("data.batch", batch)
    api.util.log("Produced batch of 100 data points", "info")
end

api.util.log("Data Producer ready", "info")