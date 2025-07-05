-- Data Processor Plugin
-- By Segfault Studios (Partner Level)
-- Demonstrates data transformation and aggregation patterns

local data_processor = {
    name = "Data Processor",
    version = "1.0",
    processedCount = 0,
    filters = {},
    aggregators = {},
    transformers = {}
}

api.util.log("Data Processor starting...", "info")

-- Register with coordinator
api.messages.send("coordinator.register", {
    pluginId = "data-processor",
    name = data_processor.name,
    capabilities = {"data_transformation", "aggregation", "filtering", "enrichment"}
})

-- Initialize aggregators
data_processor.aggregators.temperature = {
    sum = 0,
    count = 0,
    min = nil,
    max = nil,
    window = {}
}

data_processor.aggregators.power = {
    sum = 0,
    count = 0,
    peak = 0,
    window = {}
}

-- Data transformation functions
data_processor.transformers.temperature = function(data)
    -- Convert Celsius to Fahrenheit and Kelvin
    local celsius = data.value
    return {
        celsius = celsius,
        fahrenheit = (celsius * 9/5) + 32,
        kelvin = celsius + 273.15,
        unit = "multi",
        originalUnit = data.unit
    }
end

data_processor.transformers.normalize = function(data, min, max)
    -- Normalize value to 0-1 range
    local range = max - min
    if range == 0 then return 0.5 end
    return (data.value - min) / range
end

-- Subscribe to raw data
api.messages.subscribe("data.raw", function(msg)
    local data = msg.data
    data_processor.processedCount = data_processor.processedCount + 1
    
    -- Apply filters
    if data_processor.filters[data.type] then
        local filter = data_processor.filters[data.type]
        if not filter(data) then
            return  -- Filter rejected the data
        end
    end
    
    -- Transform data
    local transformed = data
    if data_processor.transformers[data.type] then
        transformed = data_processor.transformers[data.type](data)
    end
    
    -- Enrich data
    local enriched = {
        original = data,
        transformed = transformed,
        processedAt = os.time(),
        processorId = "data-processor",
        metadata = {
            sequence = data.sequence,
            latency = os.time() - data.timestamp
        }
    }
    
    -- Send processed data
    api.messages.send("data.processed", enriched)
    
    -- Update aggregators
    if data.type == "temperature" then
        update_temperature_aggregator(data)
    elseif data.type == "power" then
        update_power_aggregator(data)
    end
    
    -- Check for patterns
    detect_patterns(data)
end)

-- Subscribe to batch data
api.messages.subscribe("data.batch", function(msg)
    local batch = msg.data
    api.util.log("Processing batch " .. batch.batchId .. " with " .. batch.count .. " items", "info")
    
    local results = {
        batchId = batch.batchId,
        processed = {},
        statistics = {},
        processingTime = 0
    }
    
    local startTime = os.time()
    
    -- Process each item in batch
    for _, item in ipairs(batch.data) do
        -- Apply transformations
        local processed = {
            original = item,
            transformed = data_processor.transformers[item.type] and 
                         data_processor.transformers[item.type](item) or item
        }
        table.insert(results.processed, processed)
    end
    
    -- Calculate batch statistics
    results.statistics = calculate_batch_statistics(batch.data)
    results.processingTime = os.time() - startTime
    
    -- Send batch results
    api.messages.send("data.batch.processed", results)
    
    api.util.log("Batch processing completed in " .. results.processingTime .. " seconds", "info")
end)

-- Pattern detection
function detect_patterns(data)
    -- Detect anomalies
    if data.type == "temperature" then
        local avg = data_processor.aggregators.temperature.sum / 
                   math.max(1, data_processor.aggregators.temperature.count)
        
        if math.abs(data.value - avg) > 5 then  -- 5 degree deviation
            api.messages.send("data.anomaly", {
                type = "temperature_spike",
                sensorId = data.sensorId,
                value = data.value,
                average = avg,
                deviation = math.abs(data.value - avg),
                timestamp = data.timestamp
            })
        end
    end
    
    -- Detect trends
    if data.type == "power" and #data_processor.aggregators.power.window >= 5 then
        local trend = calculate_trend(data_processor.aggregators.power.window)
        if math.abs(trend) > 0.1 then  -- Significant trend
            api.messages.send("data.trend", {
                type = "power_" .. (trend > 0 and "increasing" or "decreasing"),
                sensorId = data.sensorId,
                trend = trend,
                window = #data_processor.aggregators.power.window,
                timestamp = data.timestamp
            })
        end
    end
end

-- Aggregator updates
function update_temperature_aggregator(data)
    local agg = data_processor.aggregators.temperature
    
    agg.sum = agg.sum + data.value
    agg.count = agg.count + 1
    agg.min = agg.min and math.min(agg.min, data.value) or data.value
    agg.max = agg.max and math.max(agg.max, data.value) or data.value
    
    -- Sliding window (keep last 100 values)
    table.insert(agg.window, data.value)
    if #agg.window > 100 then
        table.remove(agg.window, 1)
    end
    
    -- Publish aggregated stats every 10 readings
    if agg.count % 10 == 0 then
        api.messages.send("data.aggregate.temperature", {
            average = agg.sum / agg.count,
            min = agg.min,
            max = agg.max,
            count = agg.count,
            standardDeviation = calculate_std_dev(agg.window)
        })
    end
end

function update_power_aggregator(data)
    local agg = data_processor.aggregators.power
    
    agg.sum = agg.sum + data.value
    agg.count = agg.count + 1
    agg.peak = math.max(agg.peak, data.value)
    
    -- Sliding window
    table.insert(agg.window, {value = data.value, timestamp = data.timestamp})
    if #agg.window > 60 then  -- Keep 1 minute of data at 1Hz
        table.remove(agg.window, 1)
    end
    
    -- Calculate moving average
    if #agg.window >= 10 then
        local movingAvg = 0
        for i = #agg.window - 9, #agg.window do
            movingAvg = movingAvg + agg.window[i].value
        end
        movingAvg = movingAvg / 10
        
        api.messages.send("data.aggregate.power", {
            current = data.value,
            movingAverage = movingAvg,
            peak = agg.peak,
            totalEnergy = agg.sum,  -- Watt-seconds
            timestamp = data.timestamp
        })
    end
end

-- Statistical functions
function calculate_batch_statistics(dataPoints)
    local stats = {
        byType = {}
    }
    
    -- Group by type
    for _, point in ipairs(dataPoints) do
        if not stats.byType[point.type] then
            stats.byType[point.type] = {
                values = {},
                count = 0,
                sum = 0
            }
        end
        
        local typeStats = stats.byType[point.type]
        table.insert(typeStats.values, point.value)
        typeStats.count = typeStats.count + 1
        typeStats.sum = typeStats.sum + point.value
    end
    
    -- Calculate statistics for each type
    for dataType, typeStats in pairs(stats.byType) do
        typeStats.average = typeStats.sum / typeStats.count
        typeStats.min = math.min(table.unpack(typeStats.values))
        typeStats.max = math.max(table.unpack(typeStats.values))
        typeStats.stdDev = calculate_std_dev(typeStats.values)
        
        -- Remove raw values to save memory
        typeStats.values = nil
    end
    
    return stats
end

function calculate_std_dev(values)
    if #values < 2 then return 0 end
    
    local sum = 0
    for _, v in ipairs(values) do
        sum = sum + v
    end
    local mean = sum / #values
    
    local variance = 0
    for _, v in ipairs(values) do
        variance = variance + (v - mean) ^ 2
    end
    variance = variance / (#values - 1)
    
    return math.sqrt(variance)
end

function calculate_trend(window)
    if #window < 2 then return 0 end
    
    -- Simple linear regression
    local n = #window
    local sumX, sumY, sumXY, sumX2 = 0, 0, 0, 0
    
    for i, point in ipairs(window) do
        sumX = sumX + i
        sumY = sumY + point.value
        sumXY = sumXY + (i * point.value)
        sumX2 = sumX2 + (i * i)
    end
    
    local slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX)
    return slope
end

-- Configuration handlers
api.messages.subscribe("processor.filter.add", function(msg)
    local dataType = msg.data.type
    local condition = msg.data.condition  -- e.g., {min = 10, max = 30}
    
    data_processor.filters[dataType] = function(data)
        if condition.min and data.value < condition.min then return false end
        if condition.max and data.value > condition.max then return false end
        return true
    end
    
    api.util.log("Added filter for " .. dataType, "info")
    return { status = "filter_added", type = dataType }
end)

-- Status reporting
api.messages.subscribe("processor.status.request", function(msg)
    return {
        processedCount = data_processor.processedCount,
        activeFilters = #data_processor.filters,
        aggregators = {
            temperature = {
                count = data_processor.aggregators.temperature.count,
                average = data_processor.aggregators.temperature.sum / 
                         math.max(1, data_processor.aggregators.temperature.count)
            },
            power = {
                count = data_processor.aggregators.power.count,
                peak = data_processor.aggregators.power.peak
            }
        }
    }
end)

-- Announce availability
api.messages.send("data.processor.online", {
    version = data_processor.version,
    capabilities = {
        "temperature_conversion",
        "power_aggregation",
        "anomaly_detection",
        "trend_analysis",
        "batch_processing"
    }
})

api.util.log("Data Processor ready", "info")