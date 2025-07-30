/**
 * Chart rendering functions
 */

import { IMPLEMENTATION_COLORS } from "./config.js";
import { convertToMilliseconds } from "./utils.js";

/**
 * Renders all benchmark charts
 * @param {Array} dataSets - Array of datasets to render
 * @param {string} metric - Metric to display ("time" or "allocations")
 */
export function renderAllCharts(dataSets) {
  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    window.renderBenchmarkSet(name, dataSet, main);
  }
}

/**
 * Normalizes benchmark data units to milliseconds or KB based on metric
 * @param {Array} dataset - Array of benchmark data
 * @param {string} metric - Current metric ("time" or "allocations")
 */
export function normalizeBenchmarkUnits(dataset, metric = "time") {
  for (const set of dataset) {
    for (const subset of set) {
      const currentUnit = subset.bench.unit;

      if (metric === "time") {
        // Store original unit before conversion
        subset.bench.originalUnit = currentUnit;
        subset.bench.value = convertToMilliseconds(
          subset.bench.value,
          currentUnit
        );
        subset.bench.unit = "ms";
      } else if (metric === "allocations") {
        // Handle allocations metric
        const allocValue = subset.bench.allocations;
        if (allocValue !== undefined) {
          subset.bench.originalUnit = "B"; // Assume bytes
          // Convert bytes to KB
          subset.bench.memValue = allocValue / 1024;
          subset.bench.memUnit = "KB";
        }
      }
    }
  }
}

/**
 * Creates chart configuration options
 * @param {Array} dataset - Benchmark dataset
 * @param {string} metric - Current metric ("time" or "allocations")
 * @returns {Object} Chart.js options object
 */
function createChartOptions(dataset, metric = "time") {
  const yAxisTitle = metric === "time" ? "Time (ms)" : "Memory (KB)";

  return {
    responsive: true,
    plugins: {
      legend: {
        display: true, // Disable individual chart legends
      },
      tooltip: createTooltipConfig(dataset, metric),
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
          text: yAxisTitle,
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
 * @param {string} metric - Current metric ("time" or "allocations")
 * @returns {Object} Tooltip configuration object
 */
function createTooltipConfig(dataset, metric = "time") {
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

        if (range && metric === "time") {
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
        const bench = dataset[item.datasetIndex][item.dataIndex].bench;
        return bench.name ? "\n" + bench.name : "";
      },
    },
  };
}

/**
 * Renders a single benchmark graph
 * @param {HTMLElement} parent - Parent element to append the chart to
 * @param {string} name - Name of the benchmark
 * @param {Array} dataset - Benchmark dataset
 * @param {string} metric - Current metric ("time" or "allocations")
 * @returns {Chart} Chart.js chart instance
 */
export function renderGraph(parent, name, dataset, metric = "time") {
  const canvas = document.createElement("canvas");
  canvas.className = "benchmark-chart";
  parent.appendChild(canvas);

  const data = {
    labels: dataset[0].map((d) => d.commit.id.slice(0, 7)),
    datasets: dataset.map((d) => {
      return {
        label: d[0].bench.simplifiedName,
        data: d.map((d) => ({
          y: metric == "time" ? d.bench.value : d.bench.allocations,
          x: d.commit.id.slice(0, 7),
        })),
        borderColor: IMPLEMENTATION_COLORS[d[0].bench.simplifiedName],
        backgroundColor:
          IMPLEMENTATION_COLORS[d[0].bench.simplifiedName] + "60", // Add alpha
      };
    }),
  };

  const chart = new Chart(canvas, {
    type: "line",
    data,
    options: createChartOptions(dataset, metric),
  });

  // Store chart reference for legend control
  if (typeof window.globalCharts === "undefined") {
    window.globalCharts = [];
  }
  window.globalCharts.push(chart);

  return chart;
}
