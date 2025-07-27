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
                label +=
                  " (" + "± " + Number(range.replace("±", "")) / 1000 + ")";
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
          text: "commit",
        },
        y: {
          display: true,
          text: dataset.length > 0 ? dataset[0][0].bench.unit : "",
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
    // Create a grid container for all benchmark sets
    const gridContainer = document.createElement("div");
    gridContainer.className = "benchmark-grid";
    gridContainer.style.display = "grid";
    gridContainer.style.gridTemplateColumns =
      "repeat(auto-fit, minmax(500px, 1fr))";
    gridContainer.style.gap = "20px";
    gridContainer.style.padding = "20px";
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

      const graphsElem = document.createElement("div");
      graphsElem.className = "benchmark-graphs";
      setElem.appendChild(graphsElem);

      renderGraph(
        graphsElem,
        benchName,
        benches.map(function (b) {
          return b[1];
        })
      );
    }
  }

  const main = document.getElementById("main");
  for (const { name, dataSet } of dataSets) {
    renderBenchSet(name, dataSet, main);
  }
}

renderAllChars(init()); // Start
