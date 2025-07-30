/**
 * Configuration constants for the benchmark visualization application
 */

export const IMPLEMENTATION_COLORS = {
  KeraImplementation: "#00add8",
  MoonSharpImplementation: "#f1e05a",
  NLuaImplementation: "#000080",
  NeoImplementation: "#dea584",
  SolarSharpImplementation: "#3572a5",
  LuaCSharpImplementation: "#b07219",
  _: "#333333",
};

export const STATISTICAL_SIGNIFICANCE_CONFIG = {
  CONFIDENCE_LEVEL: 2, // 2 standard deviations (95% confidence)
  MIN_ABSOLUTE_CHANGE_MS: 1.5,
  MIN_PERCENTAGE_CHANGE: 3,
};

export const GRID_CONFIG = {
  MIN_COLUMN_WIDTH: "500px",
  GAP: "40px",
  PADDING: "30px",
};

export const DATE_FILTER_CONFIG = {
  DEFAULT_RANGE_DAYS: 30, // Default to last 30 days
  SCRUBBER_HEIGHT: 60, // Height of the scrubber in pixels
  HANDLE_WIDTH: 24, // Width of the resize handles
  MIN_SELECTION_WIDTH: 150, // Minimum width of selection in pixels
};
