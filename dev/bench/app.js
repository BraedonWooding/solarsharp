/**
 * Main application file for the benchmark visualization application
 * Displays performance benchmark data with interactive charts and change indicators
 */

import { GRID_CONFIG } from './config.js';
import { groupBy } from './utils.js';
import { collectBenchesPerTestCase } from './data-processing.js';
import { initializeDateFilter } from './date-filter.js';
import { registerChartPlugins } from './chart-plugins.js';
import { renderAllCharts } from './chart-rendering.js';
import { renderGraph } from './chart-rendering.js';
import { createSharedLegend, createSolarSharpToggleButton, createLegendItem } from './legend-controls.js';
import { createPerformanceChangeIndicators, updateChangeIndicatorsVisibility } from './performance-indicators.js';

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
    { label: "Physical Cores", value: machineInfo.physicalCores },
    { label: "Logical Cores", value: machineInfo.logicalCores },
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
 */
function renderBenchmarkSet(name, benchSet, main) {
  // Create shared legend container
  const legendContainer = createSharedLegend(main);

  // Collect all unique implementations across all benchmark sets
  const allImplementations = new Set();
  const benchmarkRegex =
    /Benchmark\.Benchmarks\.Benchmark\(Implementation: (.*?), Test: (.*?)\)/;

  for (const [benchName, benches] of groupBy(benchSet.entries(), function (k) {
    const match = k[0].match(benchmarkRegex);
    if (match) {
      k[1][0].bench.simplifiedName = match[1];
      allImplementations.add(match[1]);
      return match[2];
    }
    return k[0]; // fallback
  }).entries()) {
    // Implementation collection happens in this loop
  }

  // Store chart references for legend control
  const charts = [];

  // Add "Show Only SolarSharp" button
  const showOnlyButton = createSolarSharpToggleButton(charts, legendContainer);
  legendContainer.appendChild(showOnlyButton);

  // Create legend items for each implementation
  allImplementations.forEach((implementation) => {
    const legendItem = createLegendItem(implementation, charts);
    legendContainer.appendChild(legendItem);
  });

  // Create grid container for benchmark charts
  const gridContainer = createBenchmarkGrid(main);

  // Render individual benchmark charts
  for (const [benchName, benches] of groupBy(benchSet.entries(), function (k) {
    const match = k[0].match(benchmarkRegex);
    if (match) {
      k[1][0].bench.simplifiedName = match[1];
      return match[2];
    }
    return k[0]; // fallback
  }).entries()) {
    const benchmarkContainer = createBenchmarkContainer(
      gridContainer,
      benchName
    );
    const changesContainer = createPerformanceChangeIndicators(benches);
    benchmarkContainer.appendChild(changesContainer);

    // Create graphs container and render chart
    const graphsElement = document.createElement("div");
    graphsElement.className = "benchmark-graphs";
    benchmarkContainer.appendChild(graphsElement);

    const chart = renderGraph(
      graphsElement,
      benchName,
      benches.map((b) => b[1])
    );
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

// Make functions available globally for cross-module communication
window.renderAllCharts = renderAllCharts;
window.renderBenchmarkSet = renderBenchmarkSet;

// Initialize and start the application when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    renderAllCharts(init());
  });
} else {
  renderAllCharts(init());
}
