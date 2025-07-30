/**
 * Chart rendering functions
 */

import { IMPLEMENTATION_COLORS } from './config.js';
import { convertToMilliseconds } from './utils.js';

/**
 * Renders all benchmark charts
 * @param {Array} dataSets - Array of datasets to render
 */
export function renderAllCharts(dataSets) {
  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    window.renderBenchmarkSet(name, dataSet, main);
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
export function renderGraph(parent, name, dataset) {
  const canvas = document.createElement("canvas");
  canvas.className = "benchmark-chart";
  parent.appendChild(canvas);

  normalizeBenchmarkUnits(dataset);

  const data = {
    labels: dataset[0].map((d) => d.commit.id.slice(0, 7)),
    datasets: dataset.map((d) => ({
      label: d[0].bench.simplifiedName,
      data: d.map((d) => ({
        y: d.bench.value,
        x: d.commit.id.slice(0, 7),
      })),
      borderColor: IMPLEMENTATION_COLORS[d[0].bench.simplifiedName],
      backgroundColor: IMPLEMENTATION_COLORS[d[0].bench.simplifiedName] + "60", // Add alpha
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
