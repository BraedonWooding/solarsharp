-- Data Consumer Plugin
-- By Segfault Studios (Partner Level)
-- Demonstrates data consumption, storage, and reporting patterns

local data_consumer = {
    name = "Data Consumer",
    version = "1.0",
    storage = {
        raw = {},
        processed = {},
        aggregates = {},
        alerts = {}
    },
    reports = {},
    consumedCount = 0,
    startTime = os.time()
}

api.util.log("Data Consumer starting...", "info")

-- Register with coordinator
api.messages.send("coordinator.register", {
    pluginId = "data-consumer",
    name = data_consumer.name,
    capabilities = {"data_storage", "reporting", "visualization", "alerting"}
})

-- Storage limits
local MAX_RAW_STORAGE = 1000
local MAX_PROCESSED_STORAGE = 500
local MAX_ALERTS = 100

-- Subscribe to processed data
api.messages.subscribe("data.processed", function(msg)
    local data = msg.data
    data_consumer.consumedCount = data_consumer.consumedCount + 1
    
    -- Store processed data
    table.insert(data_consumer.storage.processed, {
        data = data,
        receivedAt = os.time()
    })
    
    -- Maintain storage limits
    if #data_consumer.storage.processed > MAX_PROCESSED_STORAGE then
        table.remove(data_consumer.storage.processed, 1)
    end
    
    -- Update real-time statistics
    update_statistics(data)
end)

-- Subscribe to aggregated data
api.messages.subscribe("data.aggregate.*", function(msg)
    local aggregateType = msg.type:match("data%.aggregate%.(.+)")
    
    if not data_consumer.storage.aggregates[aggregateType] then
        data_consumer.storage.aggregates[aggregateType] = {}
    end
    
    table.insert(data_consumer.storage.aggregates[aggregateType], {
        data = msg.data,
        timestamp = os.time()
    })
    
    -- Keep only recent aggregates (last hour)
    local cutoff = os.time() - 3600
    local filtered = {}
    for _, agg in ipairs(data_consumer.storage.aggregates[aggregateType]) do
        if agg.timestamp > cutoff then
            table.insert(filtered, agg)
        end
    end
    data_consumer.storage.aggregates[aggregateType] = filtered
end)

-- Subscribe to alerts
api.messages.subscribe("data.alert", function(msg)
    local alert = msg.data
    alert.receivedAt = os.time()
    
    table.insert(data_consumer.storage.alerts, alert)
    
    -- Maintain alert limit
    if #data_consumer.storage.alerts > MAX_ALERTS then
        table.remove(data_consumer.storage.alerts, 1)
    end
    
    -- Log critical alerts
    if alert.level == "critical" then
        api.util.log("CRITICAL ALERT: " .. alert.message .. " (value: " .. alert.value .. ")", "error")
    end
    
    -- Generate alert summary
    generate_alert_summary()
end)

-- Subscribe to anomalies
api.messages.subscribe("data.anomaly", function(msg)
    api.util.log("Anomaly detected: " .. msg.data.type .. " - deviation: " .. msg.data.deviation, "warning")
    
    -- Store as special alert
    table.insert(data_consumer.storage.alerts, {
        level = "warning",
        type = "anomaly",
        message = msg.data.type,
        data = msg.data,
        timestamp = os.time()
    })
end)

-- Subscribe to trends
api.messages.subscribe("data.trend", function(msg)
    api.util.log("Trend detected: " .. msg.data.type .. " (slope: " .. string.format("%.4f", msg.data.trend) .. ")", "info")
end)

-- Generate reports
function generate_report(reportType)
    local report = {
        id = "report_" .. os.time() .. "_" .. api.util.random(1000, 9999),
        type = reportType,
        generatedAt = os.time(),
        period = {
            start = data_consumer.startTime,
            ["end"] = os.time()
        }
    }
    
    if reportType == "summary" then
        report.data = {
            totalConsumed = data_consumer.consumedCount,
            uptime = os.time() - data_consumer.startTime,
            storageUsage = {
                raw = #data_consumer.storage.raw,
                processed = #data_consumer.storage.processed,
                alerts = #data_consumer.storage.alerts
            },
            aggregateTypes = {}
        }
        
        for aggType, _ in pairs(data_consumer.storage.aggregates) do
            table.insert(report.data.aggregateTypes, aggType)
        end
        
    elseif reportType == "statistics" then
        report.data = calculate_comprehensive_statistics()
        
    elseif reportType == "alerts" then
        report.data = {
            total = #data_consumer.storage.alerts,
            byLevel = {},
            recent = {}
        }
        
        -- Count by level
        for _, alert in ipairs(data_consumer.storage.alerts) do
            local level = alert.level or "unknown"
            report.data.byLevel[level] = (report.data.byLevel[level] or 0) + 1
        end
        
        -- Get recent alerts
        local recentCount = math.min(10, #data_consumer.storage.alerts)
        for i = #data_consumer.storage.alerts - recentCount + 1, #data_consumer.storage.alerts do
            table.insert(report.data.recent, data_consumer.storage.alerts[i])
        end
    end
    
    -- Store report
    table.insert(data_consumer.reports, report)
    
    -- Publish report
    api.messages.send("consumer.report." .. reportType, report)
    
    return report
end

-- Calculate comprehensive statistics
function calculate_comprehensive_statistics()
    local stats = {
        byType = {},
        overall = {
            count = 0,
            processingLatency = {
                sum = 0,
                count = 0,
                min = nil,
                max = nil
            }
        }
    }
    
    -- Analyze processed data
    for _, item in ipairs(data_consumer.storage.processed) do
        local originalData = item.data.original
        local dataType = originalData.type
        
        -- Initialize type statistics if needed
        if not stats.byType[dataType] then
            stats.byType[dataType] = {
                count = 0,
                values = {},
                latencies = []
            }
        end
        
        -- Update type statistics
        local typeStats = stats.byType[dataType]
        typeStats.count = typeStats.count + 1
        table.insert(typeStats.values, originalData.value)
        
        -- Calculate latency
        local latency = item.data.metadata and item.data.metadata.latency or 0
        table.insert(typeStats.latencies, latency)
        
        -- Update overall statistics
        stats.overall.count = stats.overall.count + 1
        stats.overall.processingLatency.sum = stats.overall.processingLatency.sum + latency
        stats.overall.processingLatency.count = stats.overall.processingLatency.count + 1
        
        if not stats.overall.processingLatency.min or latency < stats.overall.processingLatency.min then
            stats.overall.processingLatency.min = latency
        end
        if not stats.overall.processingLatency.max or latency > stats.overall.processingLatency.max then
            stats.overall.processingLatency.max = latency
        end
    end
    
    -- Calculate final statistics for each type
    for dataType, typeStats in pairs(stats.byType) do
        if #typeStats.values > 0 then
            -- Calculate value statistics
            local sum = 0
            for _, v in ipairs(typeStats.values) do
                sum = sum + v
            end
            typeStats.average = sum / #typeStats.values
            typeStats.min = math.min(table.unpack(typeStats.values))
            typeStats.max = math.max(table.unpack(typeStats.values))
            
            -- Calculate latency statistics
            local latencySum = 0
            for _, l in ipairs(typeStats.latencies) do
                latencySum = latencySum + l
            end
            typeStats.avgLatency = latencySum / #typeStats.latencies
        end
        
        -- Remove raw arrays to save memory
        typeStats.values = nil
        typeStats.latencies = nil
    end
    
    -- Calculate overall average latency
    if stats.overall.processingLatency.count > 0 then
        stats.overall.processingLatency.average = 
            stats.overall.processingLatency.sum / stats.overall.processingLatency.count
    end
    
    return stats
end

-- Update real-time statistics
function update_statistics(data)
    -- This could update a dashboard or real-time display
    if data_consumer.consumedCount % 100 == 0 then
        api.util.log("Consumed " .. data_consumer.consumedCount .. " messages", "info")
    end
end

-- Generate alert summary
function generate_alert_summary()
    local summary = {
        total = #data_consumer.storage.alerts,
        byLevel = {},
        last24h = 0
    }
    
    local cutoff = os.time() - 86400  -- 24 hours ago
    
    for _, alert in ipairs(data_consumer.storage.alerts) do
        -- Count by level
        local level = alert.level or "unknown"
        summary.byLevel[level] = (summary.byLevel[level] or 0) + 1
        
        -- Count recent alerts
        if (alert.timestamp or alert.receivedAt) > cutoff then
            summary.last24h = summary.last24h + 1
        end
    end
    
    -- Send alert summary
    api.messages.send("consumer.alert.summary", summary)
end

-- Query handlers
api.messages.subscribe("consumer.query", function(msg)
    local query = msg.data
    local results = {}
    
    if query.type == "recent_data" then
        -- Get recent processed data
        local count = math.min(query.count or 10, #data_consumer.storage.processed)
        for i = #data_consumer.storage.processed - count + 1, #data_consumer.storage.processed do
            table.insert(results, data_consumer.storage.processed[i])
        end
        
    elseif query.type == "alerts" then
        -- Get alerts by level
        for _, alert in ipairs(data_consumer.storage.alerts) do
            if not query.level or alert.level == query.level then
                table.insert(results, alert)
            end
        end
        
    elseif query.type == "aggregates" then
        -- Get specific aggregate data
        if query.aggregateType and data_consumer.storage.aggregates[query.aggregateType] then
            results = data_consumer.storage.aggregates[query.aggregateType]
        else
            results = data_consumer.storage.aggregates
        end
    end
    
    return {
        query = query,
        results = results,
        count = #results,
        timestamp = os.time()
    }
end)

-- Report generation commands
api.messages.subscribe("consumer.report.generate", function(msg)
    local reportType = msg.data.type or "summary"
    local report = generate_report(reportType)
    
    api.util.log("Generated " .. reportType .. " report: " .. report.id, "info")
    
    return {
        status = "generated",
        reportId = report.id,
        type = reportType
    }
end)

-- Periodic report generation
if api.util.wait then
    -- Generate reports periodically
    local reportInterval = 60  -- Generate reports every minute
    local lastReportTime = os.time()
    
    while true do
        api.util.wait(10)  -- Check every 10 seconds
        
        if os.time() - lastReportTime >= reportInterval then
            -- Generate periodic summary
            generate_report("summary")
            
            -- Generate statistics report every 5 minutes
            if data_consumer.consumedCount > 0 and data_consumer.consumedCount % 5 == 0 then
                generate_report("statistics")
            end
            
            lastReportTime = os.time()
        end
        
        -- Send heartbeat
        if data_consumer.consumedCount % 10 == 0 then
            api.messages.send("coordinator.heartbeat", {
                pluginId = "data-consumer",
                status = "active",
                metrics = {
                    consumed = data_consumer.consumedCount,
                    alerts = #data_consumer.storage.alerts
                }
            })
        end
    end
end

-- Announce availability
api.messages.send("data.consumer.online", {
    version = data_consumer.version,
    capabilities = {
        "data_storage",
        "statistical_analysis",
        "alert_monitoring",
        "report_generation",
        "query_interface"
    }
})

api.util.log("Data Consumer ready", "info")