const implementationColors = {
  KeraImplementation: "#00add8",
  MoonSharpImplementation: "#f1e05a",
  NLuaImplementation: "#000080",
  NeoImplementation: "#dea584",
  SolarSharpImplementation: "#3572a5",
  _: "#333333",
};

function init() {
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

  const data = window.BENCHMARK_DATA;

  // Render header
  document.getElementById("last-update").textContent = new Date(
    data.lastUpdate
  ).toString();
  const repoLink = document.getElementById("repository-link");
  repoLink.href = data.repoUrl;
  repoLink.textContent = data.repoUrl;

  // Render footer
  document.getElementById("dl-button").onclick = () => {
    const dataUrl = "data:," + JSON.stringify(data, null, 2);
    const a = document.createElement("a");
    a.href = dataUrl;
    a.download = "benchmark_data.json";
    a.click();
  };

  // Prepare data points for charts
  return Object.keys(data.entries).map((name) => ({
    name,
    dataSet: collectBenchesPerTestCase(data.entries[name]),
  }));
}

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

const plugin = {
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
Chart.register(plugin);

function renderAllChars(dataSets) {
  function renderGraph(parent, name, dataset) {
    const canvas = document.createElement("canvas");
    canvas.className = "benchmark-chart";
    parent.appendChild(canvas);

    for (const set of dataset) {
      for (const subset of set) {
        const currentUnit = subset.bench.unit;

        // Store original unit before conversion
        subset.bench.originalUnit = currentUnit;

        switch (currentUnit) {
          case "s":
            subset.bench.value *= 1000; // seconds to milliseconds
            break;
          case "ms":
            // already in milliseconds, no conversion needed
            break;
          case "us":
            subset.bench.value /= 1000; // microseconds to milliseconds
            break;
          case "ns":
            subset.bench.value /= 1000000; // nanoseconds to milliseconds
            break;
        }

        subset.bench.unit = "ms";
      }
    }

    const data = {
      labels: dataset[0].map((d) => d.commit.id.slice(0, 7)),
      datasets: dataset.map((d) => ({
        label: d[0].bench.name,
        data: d.map((d) => ({
          y: d.bench.value,
          x: d.commit.id.slice(0, 7),
        })),
        borderColor: implementationColors[d[0].bench.name],
        backgroundColor: implementationColors[d[0].bench.name] + "60", // Add alpha for #rrggbbaa
      })),
    };
    const options = {
      responsive: true,
      plugins: {
        legend: {
          display: false, // Disable individual chart legends
        },
        tooltip: {
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
                // Convert range to milliseconds if needed
                const rangeValue = Number(range.replace("±", ""));
                let convertedRange = rangeValue;

                // Convert range to milliseconds based on original unit
                const originalUnit =
                  dataset[item.datasetIndex][item.dataIndex].bench
                    .originalUnit || unit;
                switch (originalUnit) {
                  case "s":
                    convertedRange = rangeValue * 1000; // seconds to milliseconds
                    break;
                  case "ms":
                    convertedRange = rangeValue; // already in milliseconds
                    break;
                  case "us":
                    convertedRange = rangeValue / 1000; // microseconds to milliseconds
                    break;
                  case "ns":
                    convertedRange = rangeValue / 1000000; // nanoseconds to milliseconds
                    break;
                }

                label += " (± " + convertedRange.toFixed(2) + ")";
              }
              return label;
            },
            afterLabel: (item) => {
              const { name } = dataset[item.datasetIndex][item.dataIndex].bench;
              return name ? "\n" + name : "";
            },
          },
        },
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
      onClick: (_mouseEvent, activeElems) => {
        if (activeElems.length === 0) {
          return;
        }
        // XXX: Undocumented. How can we know the index?
        const index = activeElems[0].index;
        const url = dataset[activeElems[0].datasetIndex][index].commit.url;
        window.open(url, "_blank");
      },
    };

    var chart = new Chart(canvas, {
      type: "line",
      data,
      options,
    });

    // Store chart reference for legend control
    if (typeof window.globalCharts === "undefined") {
      window.globalCharts = [];
    }
    window.globalCharts.push(chart);

    return chart;
  }

  function groupBy(list, keyGetter) {
    let map = new Map();
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

  function renderBenchSet(name, benchSet, main) {
    // Create shared legend container
    const legendContainer = document.createElement("div");
    legendContainer.className = "shared-legend";
    legendContainer.style.display = "flex";
    legendContainer.style.justifyContent = "center";
    legendContainer.style.flexWrap = "wrap";
    legendContainer.style.gap = "20px";
    legendContainer.style.padding = "20px";
    legendContainer.style.backgroundColor = "#f8f9fa";
    legendContainer.style.border = "1px solid #dee2e6";
    legendContainer.style.borderRadius = "8px";
    legendContainer.style.marginBottom = "20px";
    main.appendChild(legendContainer);

    // Collect all unique implementations across all benchmark sets
    const allImplementations = new Set();
    for (const [benchName, benches] of groupBy(
      benchSet.entries(),
      function (k) {
        const match = k[0].match(
          /Benchmark.Benchmarks.Benchmark\(Implementation: (.*?), Test: (.*?)\)/
        );
        k[1][0].bench.name = match[1];
        allImplementations.add(match[1]);
        return match[2];
      }
    ).entries()) {
      // Just collect implementations in this pass
    }

    // Create legend items
    const charts = []; // Store chart references for legend control

    // Add "Show Only SolarSharp" button
    const showOnlyButton = document.createElement("button");
    showOnlyButton.textContent = "Show Only SolarSharp";
    showOnlyButton.style.padding = "8px 16px";
    showOnlyButton.style.backgroundColor = "#3572a5";
    showOnlyButton.style.color = "white";
    showOnlyButton.style.border = "none";
    showOnlyButton.style.borderRadius = "4px";
    showOnlyButton.style.cursor = "pointer";
    showOnlyButton.style.fontSize = "14px";
    showOnlyButton.style.fontWeight = "bold";
    showOnlyButton.style.marginRight = "20px";

    showOnlyButton.addEventListener("click", () => {
      // Check if only SolarSharp is currently visible
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

      // If only SolarSharp is visible, show all. Otherwise, show only SolarSharp
      const showAll = onlySolarSharpVisible && hasSolarSharp;

      charts.forEach((chart) => {
        chart.data.datasets.forEach((dataset, index) => {
          if (showAll) {
            // Show all implementations
            chart.setDatasetVisibility(index, true);
          } else {
            // Show only SolarSharp
            const shouldShow = dataset.label === "SolarSharpImplementation";
            chart.setDatasetVisibility(index, shouldShow);
          }
        });
        chart.update();
      });

      // Update legend item appearances and button text
      const legendItems = legendContainer.querySelectorAll(
        "div:not(:first-child)"
      );
      legendItems.forEach((item) => {
        const label = item.querySelector("span").textContent;
        if (showAll) {
          item.style.opacity = "1";
        } else {
          item.style.opacity =
            label === "SolarSharpImplementation" ? "1" : "0.5";
        }
      });

      // Update button text
      showOnlyButton.textContent = showAll
        ? "Show Only SolarSharp"
        : "Show All";

      // Update change indicators visibility
      updateChangeIndicatorsVisibility();
    });

    legendContainer.appendChild(showOnlyButton);

    allImplementations.forEach((implementation) => {
      const legendItem = document.createElement("div");
      legendItem.style.display = "flex";
      legendItem.style.alignItems = "center";
      legendItem.style.cursor = "pointer";
      legendItem.style.userSelect = "none";

      const colorBox = document.createElement("div");
      colorBox.style.width = "20px";
      colorBox.style.height = "20px";
      colorBox.style.backgroundColor =
        implementationColors[implementation] || "#333333";
      colorBox.style.marginRight = "8px";
      colorBox.style.border = "1px solid #ccc";

      const label = document.createElement("span");
      label.textContent = implementation;
      label.style.fontSize = "14px";

      legendItem.appendChild(colorBox);
      legendItem.appendChild(label);

      // Add click handler for show/hide functionality
      legendItem.addEventListener("click", () => {
        charts.forEach((chart) => {
          const datasetIndex = chart.data.datasets.findIndex(
            (dataset) => dataset.label === implementation
          );
          if (datasetIndex !== -1) {
            const dataset = chart.data.datasets[datasetIndex];
            const isHidden = chart.isDatasetVisible(datasetIndex) === false;
            chart.setDatasetVisibility(datasetIndex, isHidden);
            chart.update();
          }
        });

        // Update legend item appearance
        const isHidden =
          charts.length > 0 &&
          charts[0].data.datasets.some(
            (dataset) =>
              dataset.label === implementation &&
              charts[0].isDatasetVisible(
                charts[0].data.datasets.indexOf(dataset)
              ) === false
          );
        legendItem.style.opacity = isHidden ? "0.5" : "1";

        // Update change indicators visibility
        updateChangeIndicatorsVisibility();
      });

      legendContainer.appendChild(legendItem);
    });

    // Create a grid container for all benchmark sets
    const gridContainer = document.createElement("div");
    gridContainer.className = "benchmark-grid";
    gridContainer.style.display = "grid";
    gridContainer.style.gridTemplateColumns =
      "repeat(auto-fit, minmax(500px, 1fr))";
    gridContainer.style.gap = "40px";
    gridContainer.style.padding = "30px";
    main.appendChild(gridContainer);

    for (const [benchName, benches] of groupBy(
      benchSet.entries(),
      function (k) {
        const match = k[0].match(
          /Benchmark.Benchmarks.Benchmark\(Implementation: (.*?), Test: (.*?)\)/
        );
        k[1][0].bench.name = match[1];
        return match[2];
      }
    ).entries()) {
      const setElem = document.createElement("div");
      setElem.className = "benchmark-set";
      setElem.style.border = "1px solid #ddd";
      setElem.style.borderRadius = "8px";
      setElem.style.padding = "15px";
      setElem.style.backgroundColor = "#fafafa";
      gridContainer.appendChild(setElem);

      const nameElem = document.createElement("h1");
      nameElem.className = "benchmark-title";
      nameElem.textContent = benchName;
      nameElem.style.marginTop = "0";
      nameElem.style.fontSize = "1.2em";
      nameElem.style.marginBottom = "15px";
      setElem.appendChild(nameElem);

      // Add performance change indicators
      const changesContainer = document.createElement("div");
      changesContainer.className = "performance-changes";
      changesContainer.style.display = "flex";
      changesContainer.style.flexWrap = "wrap";
      changesContainer.style.gap = "8px";
      changesContainer.style.marginBottom = "15px";

      // Calculate percentage changes for each implementation
      benches
        .map(function (b) {
          return b[1];
        })
        .forEach((implementationData) => {
          if (implementationData.length >= 2) {
            const latest = implementationData[implementationData.length - 1];
            const previous = implementationData[implementationData.length - 2];

            // Convert both values to milliseconds before comparison
            const convertToMs = (value, unit) => {
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
            };

            const latestValueMs = convertToMs(
              latest.bench.value,
              latest.bench.unit
            );
            const previousValueMs = convertToMs(
              previous.bench.value,
              previous.bench.unit
            );

            const change =
              ((latestValueMs - previousValueMs) / previousValueMs) * 100;

            // Check if change is statistically significant
            let isSignificant = false;
            if (latest.bench.range && previous.bench.range) {
              // Extract standard deviation from range (format: "± 64768.29586634039")
              const latestStdevRaw = parseFloat(
                latest.bench.range.replace(/[±\s]/g, "")
              );
              const previousStdevRaw = parseFloat(
                previous.bench.range.replace(/[±\s]/g, "")
              );

              // Convert standard deviations to milliseconds as well
              const latestStdev = convertToMs(
                latestStdevRaw,
                latest.bench.unit
              );
              const previousStdev = convertToMs(
                previousStdevRaw,
                previous.bench.unit
              );

              // Calculate the absolute difference between values (in ms)
              const absoluteChange = Math.abs(latestValueMs - previousValueMs);

              // Use combined standard error (sqrt of sum of variances)
              const combinedStdev = Math.sqrt(
                latestStdev * latestStdev + previousStdev * previousStdev
              );

              // Consider significant if change is greater than 2 standard deviations (95% confidence)
              // AND the absolute change is at least 1.5ms (practical significance)
              // AND the percentage change is at least 3%
              isSignificant =
                absoluteChange > 2 * combinedStdev &&
                absoluteChange >= 1.5 &&
                Math.abs(change) >= 3;
            } else {
              // Fallback: consider significant if change is greater than 3% AND at least 1.5ms
              const absoluteChange = Math.abs(latestValueMs - previousValueMs);
              isSignificant = Math.abs(change) >= 3 && absoluteChange >= 1.5;
            }

            // Only show indicator if change is statistically significant
            if (isSignificant) {
              const changeIndicator = document.createElement("div");
              changeIndicator.style.display = "inline-flex";
              changeIndicator.style.alignItems = "center";
              changeIndicator.style.padding = "4px 8px";
              changeIndicator.style.borderRadius = "12px";
              changeIndicator.style.fontSize = "12px";
              changeIndicator.style.fontWeight = "bold";
              changeIndicator.style.border = "1px solid";

              const implementation = latest.bench.name;
              const color = implementationColors[implementation] || "#333333";

              // Extract clean implementation name using regex
              let cleanImplementationName = implementation;
              const implementationMatch = implementation.match(
                /Implementation:\s*([^,)]+)/
              );
              if (implementationMatch) {
                cleanImplementationName = implementationMatch[1];
              }

              if (change > 0) {
                // Performance regression (slower = bad)
                changeIndicator.style.backgroundColor = "#fff5f5";
                changeIndicator.style.color = "#dc3545";
                changeIndicator.style.borderColor = "#dc3545";
                const deltaMs = (latestValueMs - previousValueMs).toFixed(2);
                changeIndicator.textContent = `${cleanImplementationName}: +${change.toFixed(
                  1
                )}% (+${deltaMs}ms)`;
              } else {
                // Performance improvement (faster = good)
                changeIndicator.style.backgroundColor = "#f0fff4";
                changeIndicator.style.color = "#28a745";
                changeIndicator.style.borderColor = "#28a745";
                const deltaMs = Math.abs(
                  latestValueMs - previousValueMs
                ).toFixed(2);
                changeIndicator.textContent = `${cleanImplementationName}: ${change.toFixed(
                  1
                )}% (-${deltaMs}ms)`;
              }

              // Add implementation color dot
              const colorDot = document.createElement("span");
              colorDot.style.display = "inline-block";
              colorDot.style.width = "8px";
              colorDot.style.height = "8px";
              colorDot.style.borderRadius = "50%";
              colorDot.style.backgroundColor = color;
              colorDot.style.marginRight = "6px";
              changeIndicator.insertBefore(
                colorDot,
                changeIndicator.firstChild
              );

              // Add tooltip showing statistical significance
              changeIndicator.title = `Statistically significant change (>2σ confidence)`;

              // Add data attribute for filtering
              changeIndicator.setAttribute(
                "data-implementation",
                cleanImplementationName
              );

              changesContainer.appendChild(changeIndicator);
            }
          }
        });

      setElem.appendChild(changesContainer);

      // Function to update change indicators visibility based on chart visibility
      const updateChangeIndicatorsVisibility = () => {
        const changeIndicators = document.querySelectorAll(
          "[data-implementation]"
        );
        changeIndicators.forEach((indicator) => {
          const implementation = indicator.getAttribute("data-implementation");
          let isVisible = true;

          if (charts.length > 0) {
            const datasetIndex = charts[0].data.datasets.findIndex(
              (dataset) => dataset.label === implementation
            );
            if (datasetIndex !== -1) {
              isVisible = charts[0].isDatasetVisible(datasetIndex) !== false;
            }
          }

          indicator.style.display = isVisible ? "inline-flex" : "none";
        });
      };

      // Store the update function for use in legend handlers
      window.updateChangeIndicatorsVisibility =
        updateChangeIndicatorsVisibility;

      const graphsElem = document.createElement("div");
      graphsElem.className = "benchmark-graphs";
      setElem.appendChild(graphsElem);

      const chart = renderGraph(
        graphsElem,
        benchName,
        benches.map(function (b) {
          return b[1];
        })
      );

      // Add chart to our collection for legend control
      charts.push(chart);
    }
  }

  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    renderBenchSet(name, dataSet, main);
  }
}

renderAllChars(init()); // Start
