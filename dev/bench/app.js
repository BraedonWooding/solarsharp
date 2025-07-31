/**
 * Main application file for the benchmark visualization application
 * Displays performance benchmark data with interactive charts and change indicators
 */

import { GRID_CONFIG, IMPLEMENTATION_COLORS } from "./config.js";
import { groupBy } from "./utils.js";
import {
  collectBenchesPerTestCase,
  filterEntriesByDateRange,
} from "./data-processing.js";
import { applyDateFilter, initializeDateFilter } from "./date-filter.js";
import { registerChartPlugins } from "./chart-plugins.js";
import { renderAllCharts } from "./chart-rendering.js";
import { renderGraph } from "./chart-rendering.js";
import {
  createSharedLegend,
  createSolarSharpToggleButton,
  createLegendItem,
  updateSharedLegendVisibility,
} from "./legend-controls.js";
import {
  createPerformanceChangeIndicators,
  updateChangeIndicatorsVisibility,
} from "./performance-indicators.js";

/**
 * List of benchmark tests to filter out from the visualization
 * These tests will be excluded from the charts and legend
 */
const FILTERED_OUT_BENCHMARKS = ["startup.lua", "regexredux.lua-2.lua"];

/**
 * Checks if a benchmark should be filtered out based on its test name
 * @param {string} benchmarkName - The full benchmark name to check
 * @returns {boolean} True if the benchmark should be filtered out
 */
function shouldFilterOutBenchmark(testName) {
  return testName && FILTERED_OUT_BENCHMARKS.includes(testName);
}

/**
 * Initializes the benchmark visualization application
 * @returns {Array} Array of datasets for rendering
 */
function init() {
  const data = window.BENCHMARK_DATA;

  // Register Chart.js plugins
  registerChartPlugins();

  // Initialize UI elements
  initializeHeader(data);
  initializeFooter(data);

  // Initialize date filter and get initial datasets
  const { filteredData, dateRange } = initializeDateFilter(data);

  // Create shared legend container
  const legendContainer = createSharedLegend(main);

  // Create tabs for switching between Time and Allocations view
  const tabsContainer = createMetricTabs(main);

  // Add "Show Only SolarSharp" button
  const showOnlyButton = createSolarSharpToggleButton(legendContainer);
  legendContainer.appendChild(showOnlyButton);

  // Create legend items for each implementation
  Object.keys(IMPLEMENTATION_COLORS).forEach((implementation) => {
    const legendItem = createLegendItem(implementation);
    legendContainer.appendChild(legendItem);
  });

  // Prepare data points for charts
  return Object.keys(filteredData).map((name) => ({
    name,
    dataSet: collectBenchesPerTestCase(filteredData[name]),
  }));
}

/**
 * Initializes the header section with repository information and machine info
 * @param {Object} data - Benchmark data object
 */
function initializeHeader(data) {
  document.getElementById("last-update").textContent = new Date(
    data.lastUpdate
  ).toString();

  const repoLink = document.getElementById("repository-link");
  repoLink.href = data.repoUrl;
  repoLink.textContent = data.repoUrl;

  // Initialize machine info section if available
  if (data.machineInfo) {
    initializeMachineInfo(data.machineInfo);
  }
}

/**
 * Initializes the footer with download functionality
 * @param {Object} data - Benchmark data object
 */
function initializeFooter(data) {
  document.getElementById("dl-button").onclick = () => {
    const dataUrl = "data:," + JSON.stringify(data, null, 2);
    const a = document.createElement("a");
    a.href = dataUrl;
    a.download = "benchmark_data.json";
    a.click();
  };
}

/**
 * Initializes the machine info section
 * @param {Object} machineInfo - Machine information object
 */
function initializeMachineInfo(machineInfo) {
  // Find or create machine info container
  let machineInfoContainer = document.getElementById("machine-info");

  if (!machineInfoContainer) {
    // Create machine info container if it doesn't exist
    machineInfoContainer = document.createElement("div");
    machineInfoContainer.id = "machine-info";
    machineInfoContainer.className = "machine-info-section";

    // Insert after header or at the beginning of main content
    const main = document.getElementById("main");
    const header =
      document.querySelector("header") || document.querySelector(".header");

    if (header && header.nextSibling) {
      header.parentNode.insertBefore(machineInfoContainer, header.nextSibling);
    } else if (main) {
      main.parentNode.insertBefore(machineInfoContainer, main);
    } else {
      document.body.insertBefore(
        machineInfoContainer,
        document.body.firstChild
      );
    }
  }

  // Style the container
  Object.assign(machineInfoContainer.style, {
    backgroundColor: "#f8f9fa",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    padding: "20px",
    margin: "20px auto",
    maxWidth: "1200px",
    fontFamily: "Arial, sans-serif",
  });

  // Create title
  const title = document.createElement("h2");
  title.textContent = "Benchmark Environment";
  Object.assign(title.style, {
    margin: "0 0 15px 0",
    fontSize: "1.4em",
    color: "#333",
    borderBottom: "2px solid #3572a5",
    paddingBottom: "8px",
  });
  machineInfoContainer.appendChild(title);

  // Create info grid
  const infoGrid = document.createElement("div");
  Object.assign(infoGrid.style, {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
    gap: "15px",
    fontSize: "14px",
  });

  // Machine info items
  const infoItems = [
    { label: "Operating System", value: machineInfo.os },
    { label: "Processor", value: machineInfo.processor },
    {
      label: "Physical/Logical Cores",
      value: machineInfo.physicalCores + " / " + machineInfo.logicalCores,
    },
    { label: "Architecture", value: machineInfo.architecture },
    { label: "Runtime Version", value: machineInfo.runtimeVersion },
    {
      label: "BenchmarkDotNet Version",
      value: machineInfo.benchmarkDotnetVersion,
    },
  ];

  infoItems.forEach((item) => {
    if (item.value !== undefined && item.value !== null) {
      const infoItem = createMachineInfoItem(item.label, item.value);
      infoGrid.appendChild(infoItem);
    }
  });

  machineInfoContainer.appendChild(infoGrid);
}

/**
 * Creates a machine info item element
 * @param {string} label - The label for the info item
 * @param {string|number} value - The value for the info item
 * @returns {HTMLElement} Info item element
 */
function createMachineInfoItem(label, value) {
  const item = document.createElement("div");
  Object.assign(item.style, {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "8px 12px",
    backgroundColor: "white",
    border: "1px solid #e0e0e0",
    borderRadius: "4px",
  });

  const labelElement = document.createElement("span");
  labelElement.textContent = label + ":";
  Object.assign(labelElement.style, {
    fontWeight: "bold",
    color: "#555",
  });

  const valueElement = document.createElement("span");
  valueElement.textContent = value;
  Object.assign(valueElement.style, {
    color: "#333",
    textAlign: "right",
    wordBreak: "break-word",
  });

  item.appendChild(labelElement);
  item.appendChild(valueElement);

  return item;
}

/**
 * Renders a complete benchmark set with legend, controls, and charts
 * @param {string} name - Name of the benchmark set
 * @param {Map} benchSet - Map of benchmark data
 * @param {HTMLElement} main - Main container element
 * @param {string} metric - Current metric ("time" or "allocations")
 */
function renderBenchmarkSet(name, benchSet, main) {
  const metric = window.currentMetric || "time";

  const benchmarkRegex =
    /Benchmark\.Benchmarks\.Benchmark\(Implementation: (.*?), Test: (.*?)\)/;
  const alternateRegex =
    /Benchmark\.Benchmarks\.Benchmark\(test: (.*?), Implementation: (.*?)\)/;
  const otherRegex = /Benchmark\.Benchmarks\.(.*?)\(Implementation: (.*?)\)/;

  // Filter out benchmarks based on the filtered list
  const filteredBenchSet = groupBy(benchSet.entries(), function (kvp) {
    const benchName = kvp[0];
    const benches = kvp[1];
    const match = benchName.match(benchmarkRegex);
    const altMatch = benchName.match(alternateRegex);
    const otherMatch = benchName.match(otherRegex);
    let test = benchName;

    if (match) {
      benches[0].bench.simplifiedName = match[1];
      test = match[2];
    } else if (altMatch) {
      benches[0].bench.simplifiedName = altMatch[2];
      test = altMatch[1];
    } else if (otherMatch) {
      benches[0].bench.simplifiedName = otherMatch[2];
      test = otherMatch[1];
    }

    if (shouldFilterOutBenchmark(test)) {
      return null; // Skip this benchmark
    } else {
      return test;
    }
  });

  // Store chart references for legend control
  const charts = [];

  // Create grid container for benchmark charts
  const gridContainer = createBenchmarkGrid(main);

  // Render individual benchmark charts
  for (const [benchName, benches] of filteredBenchSet.entries()) {
    const benchmarkContainer = createBenchmarkContainer(
      gridContainer,
      benchName
    );
    let grouped_benches = groupBy(benches, (b) => {
      return b[1][0].bench.simplifiedName;
    });
    let flattened = [
      ...grouped_benches.values().map((b) => {
        return b.map((x) => x[1]).flat();
      }),
    ];

    const changesContainer = createPerformanceChangeIndicators(flattened);
    benchmarkContainer.appendChild(changesContainer);

    // Create graphs container and render chart
    const graphsElement = document.createElement("div");
    graphsElement.className = "benchmark-graphs";
    benchmarkContainer.appendChild(graphsElement);

    const chart = renderGraph(graphsElement, benchName, flattened, metric);
    charts.push(chart);
  }

  // Store the update function globally for legend handlers
  window.updateChangeIndicatorsVisibility = updateChangeIndicatorsVisibility;
}

/**
 * Creates the benchmark grid container
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Grid container element
 */
function createBenchmarkGrid(main) {
  const gridContainer = document.createElement("div");
  gridContainer.className = "benchmark-grid";
  Object.assign(gridContainer.style, {
    display: "grid",
    gridTemplateColumns: `repeat(auto-fit, minmax(${GRID_CONFIG.MIN_COLUMN_WIDTH}, 1fr))`,
    gap: GRID_CONFIG.GAP,
    padding: GRID_CONFIG.PADDING,
  });
  main.appendChild(gridContainer);
  return gridContainer;
}

/**
 * Creates a container for an individual benchmark
 * @param {HTMLElement} gridContainer - Grid container element
 * @param {string} benchName - Name of the benchmark
 * @returns {HTMLElement} Benchmark container element
 */
function createBenchmarkContainer(gridContainer, benchName) {
  const setElement = document.createElement("div");
  setElement.className = "benchmark-set";
  Object.assign(setElement.style, {
    border: "1px solid #ddd",
    borderRadius: "8px",
    padding: "15px",
    backgroundColor: "#fafafa",
  });
  gridContainer.appendChild(setElement);

  const nameElement = document.createElement("h1");
  nameElement.className = "benchmark-title";
  nameElement.textContent = benchName;
  Object.assign(nameElement.style, {
    marginTop: "0",
    fontSize: "1.2em",
    marginBottom: "15px",
  });
  setElement.appendChild(nameElement);

  return setElement;
}

/**
 * Creates tabs for switching between Time and Allocations metrics
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Tabs container element
 */
function createMetricTabs(main) {
  const tabsContainer = document.createElement("div");
  tabsContainer.className = "metric-tabs";
  Object.assign(tabsContainer.style, {
    backgroundColor: "#fff",
    border: "1px solid #dee2e6",
    borderRadius: "8px 8px 0 0",
    margin: "20px auto 0 auto",
    maxWidth: "1200px",
    display: "flex",
    fontFamily: "Arial, sans-serif",
  });

  // Create Time tab
  const timeTab = createTab("Time", true);
  tabsContainer.appendChild(timeTab);

  // Create Allocations tab
  const allocationsTab = createTab("Allocations", false);
  tabsContainer.appendChild(allocationsTab);

  // Store current metric globally
  window.currentMetric = "time";

  // Add event listeners
  timeTab.addEventListener("click", () =>
    switchMetric("time", timeTab, [allocationsTab])
  );
  allocationsTab.addEventListener("click", () =>
    switchMetric("allocations", allocationsTab, [timeTab])
  );

  main.parentNode.insertBefore(tabsContainer, main);

  return tabsContainer;
}

/**
 * Creates a single tab element
 * @param {string} text - Tab text
 * @param {boolean} active - Whether the tab is active
 * @returns {HTMLElement} Tab element
 */
function createTab(text, active) {
  const tab = document.createElement("button");
  tab.textContent = text;
  tab.className = "metric-tab";
  Object.assign(tab.style, {
    padding: "12px 24px",
    border: "none",
    backgroundColor: active ? "#3572a5" : "#f8f9fa",
    color: active ? "white" : "#495057",
    cursor: "pointer",
    fontSize: "14px",
    fontWeight: "bold",
    borderRadius: active ? "8px 8px 0 0" : "0",
    borderBottom: active ? "3px solid #3572a5" : "3px solid transparent",
    transition: "all 0.2s ease",
  });

  // Add hover effects
  tab.addEventListener("mouseenter", () => {
    if (!tab.classList.contains("active")) {
      tab.style.backgroundColor = "#e9ecef";
    }
  });

  tab.addEventListener("mouseleave", () => {
    if (!tab.classList.contains("active")) {
      tab.style.backgroundColor = "#f8f9fa";
    }
  });

  if (active) {
    tab.classList.add("active");
  }

  return tab;
}

/**
 * Switches between Time and Allocations metrics
 * @param {string} metric - "time" or "allocations"
 * @param {HTMLElement} activeTab - The tab being activated
 * @param {HTMLElement} inactiveTab - The tab being deactivated
 */
function switchMetric(metric, activeTab, inactiveTabs) {
  // Update global metric
  window.currentMetric = metric;

  // Update tab styles
  activeTab.classList.add("active");

  Object.assign(activeTab.style, {
    backgroundColor: "#3572a5",
    color: "white",
    borderBottom: "3px solid #3572a5",
  });

  inactiveTabs.forEach((inactiveTab) => {
    inactiveTab.classList.remove("active");
    Object.assign(inactiveTab.style, {
      backgroundColor: "#f8f9fa",
      color: "#495057",
      borderBottom: "3px solid transparent",
    });
  });

  applyDateFilter();
}

// Make functions available globally for cross-module communication
window.renderAllCharts = renderAllCharts;
window.renderBenchmarkSet = renderBenchmarkSet;
window.filterEntriesByDateRange = filterEntriesByDateRange;
window.updateSharedLegendVisibility = updateSharedLegendVisibility;

// Initialize and start the application when DOM is ready
if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", () => {
    renderAllCharts(init());
  });
} else {
  renderAllCharts(init());
}
