/**
 * Chart.js plugins and configurations
 */

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
export const customHoverPlugin = {
  id: "customHover",
  afterEvent: (chart, event, opts) => {
    const evt = event.event;

    if (evt.type !== "click") {
      return;
    }

    const [found, labelInfo] = findLabel(getLabelHitboxes(chart.scales), evt);
  },
};

/**
 * Registers the custom hover plugin with Chart.js
 */
export function registerChartPlugins() {
  if (typeof Chart !== "undefined") {
    Chart.register(customHoverPlugin);
  }
}
