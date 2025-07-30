/**
 * Benchmark Visualization Application
 * Displays performance benchmark data with interactive charts and change indicators
 */

// =============================================================================
// CONSTANTS AND CONFIGURATION
// =============================================================================

const IMPLEMENTATION_COLORS = {
  KeraImplementation: "#00add8",
  MoonSharpImplementation: "#f1e05a",
  NLuaImplementation: "#000080",
  NeoImplementation: "#dea584",
  SolarSharpImplementation: "#3572a5",
  LuaCSharpImplementation: "#b07219",
  _: "#333333",
};

const STATISTICAL_SIGNIFICANCE_CONFIG = {
  CONFIDENCE_LEVEL: 2, // 2 standard deviations (95% confidence)
  MIN_ABSOLUTE_CHANGE_MS: 1.5,
  MIN_PERCENTAGE_CHANGE: 3,
};

const GRID_CONFIG = {
  MIN_COLUMN_WIDTH: "500px",
  GAP: "40px",
  PADDING: "30px",
};

// =============================================================================
// UTILITY FUNCTIONS
// =============================================================================

/**
 * Groups items by a key generated from a keyGetter function
 * @param {Array} list - Array of items to group
 * @param {Function} keyGetter - Function that generates a key from each item
 * @returns {Map} Map with grouped items
 */
function groupBy(list, keyGetter) {
  const map = new Map();
  for (const item of list) {
    const key = keyGetter(item);
    const collection = map.get(key);
    if (!collection) {
      map.set(key, [item]);
    } else {
      collection.push(item);
    }
  }
  return map;
}

/**
 * Converts time values to milliseconds from various units
 * @param {number} value - The time value to convert
 * @param {string} unit - The current unit (s, ms, us, ns)
 * @returns {number} Value converted to milliseconds
 */
function convertToMilliseconds(value, unit) {
  switch (unit) {
    case "s":
      return value * 1000; // seconds to milliseconds
    case "ms":
      return value; // already in milliseconds
    case "us":
      return value / 1000; // microseconds to milliseconds
    case "ns":
      return value / 1000000; // nanoseconds to milliseconds
    default:
      return value;
  }
}

/**
 * Extracts clean implementation name from benchmark name
 * @param {string} implementationName - Raw implementation name
 * @returns {string} Clean implementation name
 */
function getCleanImplementationName(implementationName) {
  const match = implementationName.match(/Implementation:\s*([^,)]+)/);
  return match ? match[1] : implementationName;
}

/**
 * Collects benchmark data per test case
 * @param {Array} entries - Array of benchmark entries
 * @returns {Map} Map of test cases with their benchmark results
 */
function collectBenchesPerTestCase(entries) {
  const map = new Map();
  for (const entry of entries) {
    const { commit, date, tool, benches } = entry;
    for (const bench of benches) {
      const result = { commit, date, tool, bench };
      const arr = map.get(bench.name);
      if (arr === undefined) {
        map.set(bench.name, [result]);
      } else {
        arr.push(result);
      }
    }
  }
  return map;
}

// =============================================================================
// INITIALIZATION
// =============================================================================

/**
 * Initializes the benchmark visualization application
 * @returns {Array} Array of datasets for rendering
 */
function init() {
  const data = window.BENCHMARK_DATA;

  // Initialize UI elements
  initializeHeader(data);
  initializeFooter(data);

  // Prepare data points for charts
  return Object.keys(data.entries).map((name) => ({
    name,
    dataSet: collectBenchesPerTestCase(data.entries[name]),
  }));
}

/**
 * Initializes the header section with repository information
 * @param {Object} data - Benchmark data object
 */
function initializeHeader(data) {
  document.getElementById("last-update").textContent = new Date(
    data.lastUpdate
  ).toString();

  const repoLink = document.getElementById("repository-link");
  repoLink.href = data.repoUrl;
  repoLink.textContent = data.repoUrl;
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

// =============================================================================
// CHART.JS PLUGIN CONFIGURATION
// =============================================================================

/**
 * Finds a label at the given event coordinates
 * @param {Array} labels - Array of label objects with hitboxes
 * @param {Object} evt - Mouse event object
 * @returns {Array} [found, labelInfo] - Whether a label was found and its info
 */
const findLabel = (labels, evt) => {
  let found = false;
  let res = null;

  labels.forEach((l) => {
    l.labels.forEach((label, index) => {
      if (
        evt.x > label.x &&
        evt.x < label.x2 &&
        evt.y > label.y &&
        evt.y < label.y2
      ) {
        res = {
          label: label.label,
          index,
        };
        found = true;
      }
    });
  });

  return [found, res];
};

/**
 * Gets label hitboxes from chart scales
 * @param {Object} scales - Chart.js scales object
 * @returns {Array} Array of label hitbox objects
 */
const getLabelHitboxes = (scales) =>
  Object.values(scales)
    .filter((s) => !!s._labelItems)
    .map((s) => ({
      scaleId: s.id,
      labels: s._labelItems.map((e, i) => ({
        x: e.translation[0] - s._labelSizes.widths[i],
        x2: e.translation[0] + s._labelSizes.widths[i] / 2,
        y: e.translation[1] - s._labelSizes.heights[i] / 2,
        y2: e.translation[1] + s._labelSizes.heights[i] / 2,
        label: e.label,
        index: i,
      })),
    }));

/**
 * Custom Chart.js plugin for enhanced hover interactions
 */
const customHoverPlugin = {
  id: "customHover",
  afterEvent: (chart, event, opts) => {
    const evt = event.event;

    if (evt.type !== "click") {
      return;
    }

    const [found, labelInfo] = findLabel(getLabelHitboxes(chart.scales), evt);

    if (found) {
      console.log(labelInfo);
    }
  },
};

Chart.register(customHoverPlugin);

// =============================================================================
// CHART RENDERING
// =============================================================================

/**
 * Renders all benchmark charts
 * @param {Array} dataSets - Array of datasets to render
 */
function renderAllCharts(dataSets) {
  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    renderBenchmarkSet(name, dataSet, main);
  }
}

/**
 * Normalizes benchmark data units to milliseconds
 * @param {Array} dataset - Array of benchmark data
 */
function normalizeBenchmarkUnits(dataset) {
  for (const set of dataset) {
    for (const subset of set) {
      const currentUnit = subset.bench.unit;

      // Store original unit before conversion
      subset.bench.originalUnit = currentUnit;
      subset.bench.value = convertToMilliseconds(
        subset.bench.value,
        currentUnit
      );
      subset.bench.unit = "ms";
    }
  }
}

/**
 * Creates chart configuration options
 * @param {Array} dataset - Benchmark dataset
 * @returns {Object} Chart.js options object
 */
function createChartOptions(dataset) {
  return {
    responsive: true,
    plugins: {
      legend: {
        display: false, // Disable individual chart legends
      },
      tooltip: createTooltipConfig(dataset),
    },
    scales: {
      x: {
        display: true,
        title: {
          display: true,
          text: "Commit",
        },
      },
      y: {
        display: true,
        title: {
          display: true,
          text: "Time (ms)",
        },
        ticks: {
          beginAtZero: true,
        },
      },
    },
    onClick: (mouseEvent, activeElems) => {
      if (activeElems.length === 0) return;

      // Note: This uses undocumented Chart.js behavior
      const index = activeElems[0].index;
      const url = dataset[activeElems[0].datasetIndex][index].commit.url;
      window.open(url, "_blank");
    },
  };
}

/**
 * Creates tooltip configuration for charts
 * @param {Array} dataset - Benchmark dataset
 * @returns {Object} Tooltip configuration object
 */
function createTooltipConfig(dataset) {
  return {
    callbacks: {
      afterTitle: (items) => {
        const { dataIndex, datasetIndex } = items[0];
        const data = dataset[datasetIndex][dataIndex];
        return (
          "\n" +
          data.commit.message.split("\n")[0] +
          "\n\n" +
          data.commit.timestamp +
          " committed by @" +
          data.commit.committer.username +
          "\n"
        );
      },
      label: (item) => {
        let label = item.formattedValue;
        const { range, unit } =
          dataset[item.datasetIndex][item.dataIndex].bench;
        label += " " + unit;

        if (range) {
          const rangeValue = Number(range.replace("±", ""));
          const originalUnit =
            dataset[item.datasetIndex][item.dataIndex].bench.originalUnit ||
            unit;
          const convertedRange = convertToMilliseconds(
            rangeValue,
            originalUnit
          );
          label += " (± " + convertedRange.toFixed(2) + ")";
        }

        return label;
      },
      afterLabel: (item) => {
        const { name } = dataset[item.datasetIndex][item.dataIndex].bench;
        return name ? "\n" + name : "";
      },
    },
  };
}

/**
 * Renders a single benchmark graph
 * @param {HTMLElement} parent - Parent element to append the chart to
 * @param {string} name - Name of the benchmark
 * @param {Array} dataset - Benchmark dataset
 * @returns {Chart} Chart.js chart instance
 */
function renderGraph(parent, name, dataset) {
  const canvas = document.createElement("canvas");
  canvas.className = "benchmark-chart";
  parent.appendChild(canvas);

  normalizeBenchmarkUnits(dataset);

  const data = {
    labels: dataset[0].map((d) => d.commit.id.slice(0, 7)),
    datasets: dataset.map((d) => ({
      label: d[0].bench.name,
      data: d.map((d) => ({
        y: d.bench.value,
        x: d.commit.id.slice(0, 7),
      })),
      borderColor: IMPLEMENTATION_COLORS[d[0].bench.name],
      backgroundColor: IMPLEMENTATION_COLORS[d[0].bench.name] + "60", // Add alpha
    })),
  };

  const chart = new Chart(canvas, {
    type: "line",
    data,
    options: createChartOptions(dataset),
  });

  // Store chart reference for legend control
  if (typeof window.globalCharts === "undefined") {
    window.globalCharts = [];
  }
  window.globalCharts.push(chart);

  return chart;
}

// =============================================================================
// LEGEND AND CONTROL COMPONENTS
// =============================================================================

/**
 * Creates and configures the shared legend container
 * @param {HTMLElement} main - Main container element
 * @returns {HTMLElement} Legend container element
 */
function createSharedLegend(main) {
  const legendContainer = document.createElement("div");
  legendContainer.className = "shared-legend";
  Object.assign(legendContainer.style, {
    display: "flex",
    justifyContent: "center",
    flexWrap: "wrap",
    gap: "20px",
    padding: "20px",
    backgroundColor: "#f8f9fa",
    border: "1px solid #dee2e6",
    borderRadius: "8px",
    marginBottom: "20px",
  });
  main.appendChild(legendContainer);
  return legendContainer;
}

/**
 * Creates the "Show Only SolarSharp" toggle button
 * @param {Array} charts - Array of chart instances
 * @param {HTMLElement} legendContainer - Legend container element
 * @returns {HTMLElement} Button element
 */
function createSolarSharpToggleButton(charts, legendContainer) {
  const showOnlyButton = document.createElement("button");
  showOnlyButton.textContent = "Show Only SolarSharp";
  Object.assign(showOnlyButton.style, {
    padding: "8px 16px",
    backgroundColor: "#3572a5",
    color: "white",
    border: "none",
    borderRadius: "4px",
    cursor: "pointer",
    fontSize: "14px",
    fontWeight: "bold",
    marginRight: "20px",
  });

  showOnlyButton.addEventListener("click", () => {
    const { onlySolarSharpVisible, hasSolarSharp } =
      checkSolarSharpVisibility(charts);
    const showAll = onlySolarSharpVisible && hasSolarSharp;

    toggleChartVisibility(charts, showAll);
    updateLegendAppearance(legendContainer, showAll);
    updateButtonText(showOnlyButton, showAll);
    updateChangeIndicatorsVisibility();
  });

  return showOnlyButton;
}

/**
 * Checks if only SolarSharp implementation is currently visible
 * @param {Array} charts - Array of chart instances
 * @returns {Object} Object with visibility status
 */
function checkSolarSharpVisibility(charts) {
  let onlySolarSharpVisible = true;
  let hasSolarSharp = false;

  if (charts.length > 0) {
    charts[0].data.datasets.forEach((dataset, index) => {
      const isVisible = charts[0].isDatasetVisible(index) !== false;
      if (dataset.label === "SolarSharpImplementation") {
        hasSolarSharp = true;
        if (!isVisible) {
          onlySolarSharpVisible = false;
        }
      } else if (isVisible) {
        onlySolarSharpVisible = false;
      }
    });
  }

  return { onlySolarSharpVisible, hasSolarSharp };
}

/**
 * Toggles chart dataset visibility
 * @param {Array} charts - Array of chart instances
 * @param {boolean} showAll - Whether to show all or only SolarSharp
 */
function toggleChartVisibility(charts, showAll) {
  charts.forEach((chart) => {
    chart.data.datasets.forEach((dataset, index) => {
      if (showAll) {
        chart.setDatasetVisibility(index, true);
      } else {
        const shouldShow = dataset.label === "SolarSharpImplementation";
        chart.setDatasetVisibility(index, shouldShow);
      }
    });
    chart.update();
  });
}

/**
 * Updates legend item appearances
 * @param {HTMLElement} legendContainer - Legend container element
 * @param {boolean} showAll - Whether all items should be shown
 */
function updateLegendAppearance(legendContainer, showAll) {
  const legendItems = legendContainer.querySelectorAll("div:not(:first-child)");
  legendItems.forEach((item) => {
    const label = item.querySelector("span").textContent;
    item.style.opacity =
      showAll || label === "SolarSharpImplementation" ? "1" : "0.5";
  });
}

/**
 * Updates the toggle button text
 * @param {HTMLElement} button - Button element
 * @param {boolean} showAll - Current visibility state
 */
function updateButtonText(button, showAll) {
  button.textContent = showAll ? "Show Only SolarSharp" : "Show All";
}

/**
 * Creates a legend item for an implementation
 * @param {string} implementation - Implementation name
 * @param {Array} charts - Array of chart instances
 * @returns {HTMLElement} Legend item element
 */
function createLegendItem(implementation, charts) {
  const legendItem = document.createElement("div");
  Object.assign(legendItem.style, {
    display: "flex",
    alignItems: "center",
    cursor: "pointer",
    userSelect: "none",
  });

  const colorBox = document.createElement("div");
  Object.assign(colorBox.style, {
    width: "20px",
    height: "20px",
    backgroundColor: IMPLEMENTATION_COLORS[implementation] || "#333333",
    marginRight: "8px",
    border: "1px solid #ccc",
  });

  const label = document.createElement("span");
  label.textContent = implementation;
  label.style.fontSize = "14px";

  legendItem.appendChild(colorBox);
  legendItem.appendChild(label);

  // Add click handler for show/hide functionality
  legendItem.addEventListener("click", () => {
    toggleImplementationVisibility(charts, implementation);
    updateLegendItemAppearance(legendItem, charts, implementation);
    updateChangeIndicatorsVisibility();
  });

  return legendItem;
}

/**
 * Toggles visibility for a specific implementation
 * @param {Array} charts - Array of chart instances
 * @param {string} implementation - Implementation name to toggle
 */
function toggleImplementationVisibility(charts, implementation) {
  charts.forEach((chart) => {
    const datasetIndex = chart.data.datasets.findIndex(
      (dataset) => dataset.label === implementation
    );
    if (datasetIndex !== -1) {
      const isHidden = chart.isDatasetVisible(datasetIndex) === false;
      chart.setDatasetVisibility(datasetIndex, isHidden);
      chart.update();
    }
  });
}

/**
 * Updates the appearance of a legend item based on visibility
 * @param {HTMLElement} legendItem - Legend item element
 * @param {Array} charts - Array of chart instances
 * @param {string} implementation - Implementation name
 */
function updateLegendItemAppearance(legendItem, charts, implementation) {
  const isHidden =
    charts.length > 0 &&
    charts[0].data.datasets.some(
      (dataset) =>
        dataset.label === implementation &&
        charts[0].isDatasetVisible(charts[0].data.datasets.indexOf(dataset)) ===
          false
    );
  legendItem.style.opacity = isHidden ? "0.5" : "1";
}
// =============================================================================
// PERFORMANCE CHANGE INDICATORS
// =============================================================================

/**
 * Determines if a performance change is statistically significant
 * @param {Object} latest - Latest benchmark result
 * @param {Object} previous - Previous benchmark result
 * @param {number} change - Percentage change
 * @param {number} latestValueMs - Latest value in milliseconds
 * @param {number} previousValueMs - Previous value in milliseconds
 * @returns {boolean} Whether the change is significant
 */
function isStatisticallySignificant(
  latest,
  previous,
  change,
  latestValueMs,
  previousValueMs
) {
  let isSignificant = false;

  if (latest.bench.range && previous.bench.range) {
    // Extract standard deviation from range (format: "± 64768.29586634039")
    const latestStdevRaw = parseFloat(latest.bench.range.replace(/[±\s]/g, ""));
    const previousStdevRaw = parseFloat(
      previous.bench.range.replace(/[±\s]/g, "")
    );

    // Convert standard deviations to milliseconds
    const latestStdev = convertToMilliseconds(
      latestStdevRaw,
      latest.bench.unit
    );
    const previousStdev = convertToMilliseconds(
      previousStdevRaw,
      previous.bench.unit
    );

    // Calculate the absolute difference between values (in ms)
    const absoluteChange = Math.abs(latestValueMs - previousValueMs);

    // Use combined standard error (sqrt of sum of variances)
    const combinedStdev = Math.sqrt(
      latestStdev * latestStdev + previousStdev * previousStdev
    );

    // Consider significant if change meets all criteria
    isSignificant =
      absoluteChange >
        STATISTICAL_SIGNIFICANCE_CONFIG.CONFIDENCE_LEVEL * combinedStdev &&
      absoluteChange >=
        STATISTICAL_SIGNIFICANCE_CONFIG.MIN_ABSOLUTE_CHANGE_MS &&
      Math.abs(change) >= STATISTICAL_SIGNIFICANCE_CONFIG.MIN_PERCENTAGE_CHANGE;
  } else {
    // Fallback: consider significant if change meets basic criteria
    const absoluteChange = Math.abs(latestValueMs - previousValueMs);
    isSignificant =
      Math.abs(change) >=
        STATISTICAL_SIGNIFICANCE_CONFIG.MIN_PERCENTAGE_CHANGE &&
      absoluteChange >= STATISTICAL_SIGNIFICANCE_CONFIG.MIN_ABSOLUTE_CHANGE_MS;
  }

  return isSignificant;
}

/**
 * Creates a performance change indicator element
 * @param {Object} latest - Latest benchmark result
 * @param {Object} previous - Previous benchmark result
 * @param {number} change - Percentage change
 * @param {number} latestValueMs - Latest value in milliseconds
 * @param {number} previousValueMs - Previous value in milliseconds
 * @returns {HTMLElement} Change indicator element
 */
function createChangeIndicator(
  latest,
  previous,
  change,
  latestValueMs,
  previousValueMs
) {
  const changeIndicator = document.createElement("div");
  Object.assign(changeIndicator.style, {
    display: "inline-flex",
    alignItems: "center",
    padding: "4px 8px",
    borderRadius: "12px",
    fontSize: "12px",
    fontWeight: "bold",
    border: "1px solid",
  });

  const implementation = latest.bench.name;
  const color = IMPLEMENTATION_COLORS[implementation] || "#333333";
  const cleanImplementationName = getCleanImplementationName(implementation);

  if (change > 0) {
    // Performance regression (slower = bad)
    Object.assign(changeIndicator.style, {
      backgroundColor: "#fff5f5",
      color: "#dc3545",
      borderColor: "#dc3545",
    });
    const deltaMs = (latestValueMs - previousValueMs).toFixed(2);
    changeIndicator.textContent = `${cleanImplementationName}: +${change.toFixed(
      1
    )}% (+${deltaMs}ms)`;
  } else {
    // Performance improvement (faster = good)
    Object.assign(changeIndicator.style, {
      backgroundColor: "#f0fff4",
      color: "#28a745",
      borderColor: "#28a745",
    });
    const deltaMs = Math.abs(latestValueMs - previousValueMs).toFixed(2);
    changeIndicator.textContent = `${cleanImplementationName}: ${change.toFixed(
      1
    )}% (-${deltaMs}ms)`;
  }

  // Add implementation color dot
  const colorDot = document.createElement("span");
  Object.assign(colorDot.style, {
    display: "inline-block",
    width: "8px",
    height: "8px",
    borderRadius: "50%",
    backgroundColor: color,
    marginRight: "6px",
  });
  changeIndicator.insertBefore(colorDot, changeIndicator.firstChild);

  // Add tooltip and data attribute
  changeIndicator.title = `Statistically significant change (>2σ confidence)`;
  changeIndicator.setAttribute("data-implementation", cleanImplementationName);

  return changeIndicator;
}

/**
 * Calculates and creates performance change indicators
 * @param {Array} benches - Array of benchmark data
 * @returns {HTMLElement} Container with change indicators
 */
function createPerformanceChangeIndicators(benches) {
  const changesContainer = document.createElement("div");
  changesContainer.className = "performance-changes";
  Object.assign(changesContainer.style, {
    display: "flex",
    flexWrap: "wrap",
    gap: "8px",
    marginBottom: "15px",
  });

  benches
    .map((b) => b[1])
    .forEach((implementationData) => {
      if (implementationData.length >= 2) {
        const latest = implementationData[implementationData.length - 1];
        const previous = implementationData[implementationData.length - 2];

        const latestValueMs = convertToMilliseconds(
          latest.bench.value,
          latest.bench.unit
        );
        const previousValueMs = convertToMilliseconds(
          previous.bench.value,
          previous.bench.unit
        );
        const change =
          ((latestValueMs - previousValueMs) / previousValueMs) * 100;

        if (
          isStatisticallySignificant(
            latest,
            previous,
            change,
            latestValueMs,
            previousValueMs
          )
        ) {
          const changeIndicator = createChangeIndicator(
            latest,
            previous,
            change,
            latestValueMs,
            previousValueMs
          );
          changesContainer.appendChild(changeIndicator);
        }
      }
    });

  return changesContainer;
}

/**
 * Updates change indicators visibility based on chart visibility
 */
function updateChangeIndicatorsVisibility() {
  const changeIndicators = document.querySelectorAll("[data-implementation]");
  changeIndicators.forEach((indicator) => {
    const implementation = indicator.getAttribute("data-implementation");
    let isVisible = true;

    if (window.globalCharts && window.globalCharts.length > 0) {
      const datasetIndex = window.globalCharts[0].data.datasets.findIndex(
        (dataset) => dataset.label === implementation
      );
      if (datasetIndex !== -1) {
        isVisible =
          window.globalCharts[0].isDatasetVisible(datasetIndex) !== false;
      }
    }

    indicator.style.display = isVisible ? "inline-flex" : "none";
  });
}

// =============================================================================
// MAIN RENDERING FUNCTION
// =============================================================================

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
      k[1][0].bench.name = match[1];
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
      k[1][0].bench.name = match[1];
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

// =============================================================================
// APPLICATION ENTRY POINT
// =============================================================================

// Initialize and start the application
renderAllCharts(init());
