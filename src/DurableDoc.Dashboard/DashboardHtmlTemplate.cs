namespace DurableDoc.Dashboard;

internal static class DashboardHtmlTemplate
{
    public static string Render(
        string payload,
        string mermaidBundleFileName,
        string dashboardCssFileName,
        string dashboardScriptFileName)
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>durable-doc dashboard</title>
  <link rel="stylesheet" href="__DASHBOARD_CSS__">
</head>
<body>
  <div class="app-shell">
    <aside class="sidebar">
      <section class="panel brand">
        <p class="eyebrow">Workflow Explorer</p>
        <h1>durable-doc</h1>
        <p class="lede">Read the flow in order, switch views quickly, and keep your place while localhost refreshes.</p>
      </section>

      <section class="panel controls">
        <label class="field" for="orchestrator-filter">
          <span>Filter orchestrators</span>
          <input id="orchestrator-filter" type="search" placeholder="Search by orchestrator name">
        </label>
        <label class="field" for="mode-filter">
          <span>Availability</span>
          <select id="mode-filter">
            <option value="">All mode availability</option>
            <option value="developer">Has developer view</option>
            <option value="business">Has business view</option>
            <option value="both">Has both views</option>
          </select>
        </label>
        <p class="hint">Use Up and Down to move between orchestrators, Left and Right to switch modes, and <code>/</code> to focus step search.</p>
      </section>

      <section class="panel results-panel">
        <div class="section-heading">
          <h2>Orchestrators</h2>
          <span id="result-count" class="count">0 total</span>
        </div>
        <div id="results" class="results"></div>
      </section>
    </aside>

    <main id="workspace" class="workspace">
      <section id="stage" class="panel stage">
        <div class="stage-header">
          <div>
            <div id="selected-mode" class="badge">No selection</div>
            <h2 id="selected-title">Select a generated diagram</h2>
          </div>
          <div class="stage-actions">
            <div id="mode-switcher" class="mode-switcher"></div>
            <button id="toggle-stage" class="panel-toggle" type="button" aria-pressed="false">Collapse stage</button>
            <button id="compare-toggle" class="compare-toggle" type="button">Compare views</button>
            <div id="refresh-indicator" class="refresh-indicator">Static snapshot</div>
          </div>
        </div>

        <p class="hint">The diagram view prioritizes execution order. Click a step to trace what comes before and after it. Localhost preview keeps polling for regenerated artifacts.</p>

        <div id="summary-cards" class="summary-grid"></div>

        <div class="toolbar">
          <label class="field field-grow" for="node-search">
            <span>Jump to step</span>
            <input id="node-search" type="search" placeholder="Find step, event, timer, or branch label">
          </label>
          <div class="toolbar-actions">
            <button id="find-step" type="button">Find</button>
            <button id="start-step" type="button">Start step</button>
            <button id="clear-step" type="button">Clear highlight</button>
          </div>
        </div>

        <div id="node-search-status" class="meta"></div>
        <div id="legend" class="legend"></div>
        <div id="diagram-grid" class="diagram-grid">
          <div class="empty">Choose an orchestrator to inspect its generated diagrams.</div>
        </div>
      </section>

      <aside class="panel inspector">
        <section class="inspector-section">
          <h3>Workflow details</h3>
          <div id="details" class="details-grid"></div>
        </section>

        <section class="inspector-section">
          <h3>Selected step</h3>
          <div id="node-details" class="node-details empty">Select a step to inspect its incoming and outgoing flow.</div>
        </section>

        <section class="inspector-section">
          <h3>Warnings</h3>
          <ul id="warnings" class="warnings"></ul>
        </section>

        <details class="source-panel">
          <summary>Mermaid source</summary>
          <a id="open-rendered-diagram" class="viewer-link disabled" href="diagram.html" target="_blank" rel="noopener noreferrer" aria-disabled="true">Open rendered diagram</a>
          <pre id="source" class="source"></pre>
        </details>
      </aside>
    </main>
  </div>

  <script id="dashboard-bootstrap" type="application/json">__PAYLOAD__</script>
  <script src="__MERMAID_BUNDLE__"></script>
  <script src="__DASHBOARD_SCRIPT__"></script>
</body>
</html>
"""
            .Replace("__PAYLOAD__", payload, StringComparison.Ordinal)
            .Replace("__MERMAID_BUNDLE__", mermaidBundleFileName, StringComparison.Ordinal)
            .Replace("__DASHBOARD_CSS__", dashboardCssFileName, StringComparison.Ordinal)
            .Replace("__DASHBOARD_SCRIPT__", dashboardScriptFileName, StringComparison.Ordinal);
    }
}

internal static class DiagramViewerHtmlTemplate
{
    public static string Render(
        string payload,
        string mermaidBundleFileName,
        string dashboardCssFileName,
        string diagramScriptFileName)
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>durable-doc diagram viewer</title>
  <link rel="stylesheet" href="__DASHBOARD_CSS__">
</head>
<body class="diagram-viewer-body">
  <main class="diagram-viewer-shell">
    <section class="panel diagram-viewer-panel">
      <div class="diagram-viewer-header">
        <div>
          <a id="viewer-back-link" class="viewer-back-link" href="index.html">Back to dashboard</a>
          <div id="viewer-mode" class="badge">No selection</div>
          <h1 id="viewer-title">Rendered diagram</h1>
        </div>
        <div id="viewer-refresh" class="refresh-indicator">Static snapshot</div>
      </div>
      <p id="viewer-meta" class="meta">Select an orchestrator from the dashboard to open a rendered diagram.</p>
      <div id="viewer-stage" class="diagram-viewer-stage">
        <div class="empty">Rendered diagram will appear here.</div>
      </div>
    </section>
  </main>

  <script id="dashboard-bootstrap" type="application/json">__PAYLOAD__</script>
  <script src="__MERMAID_BUNDLE__"></script>
  <script src="__DIAGRAM_SCRIPT__"></script>
</body>
</html>
"""
            .Replace("__PAYLOAD__", payload, StringComparison.Ordinal)
            .Replace("__MERMAID_BUNDLE__", mermaidBundleFileName, StringComparison.Ordinal)
            .Replace("__DASHBOARD_CSS__", dashboardCssFileName, StringComparison.Ordinal)
            .Replace("__DIAGRAM_SCRIPT__", diagramScriptFileName, StringComparison.Ordinal);
    }
}

internal static class DashboardCssTemplate
{
    public static string Render()
    {
        return """
:root {
  color-scheme: light;
  --bg: #f4efe7;
  --bg-accent: #ffd8c2;
  --panel: rgba(255, 250, 244, 0.94);
  --panel-strong: #fffdf9;
  --ink: #172230;
  --muted: #657182;
  --accent: #0d9488;
  --accent-strong: #0f766e;
  --accent-soft: #d8f3ef;
  --hot: #ef8354;
  --line: rgba(23, 34, 48, 0.12);
  --line-strong: rgba(23, 34, 48, 0.22);
  --shadow: 0 28px 60px rgba(23, 34, 48, 0.14);
  --radius: 24px;
}

* {
  box-sizing: border-box;
}

html {
  min-height: 100%;
}

body {
  margin: 0;
  min-height: 100vh;
  font-family: "Avenir Next", "Segoe UI", sans-serif;
  color: var(--ink);
  overflow-x: hidden;
  background:
    radial-gradient(circle at top left, rgba(13, 148, 136, 0.18), transparent 26%),
    radial-gradient(circle at top right, rgba(239, 131, 84, 0.16), transparent 32%),
    linear-gradient(135deg, #fff8f1 0%, var(--bg) 48%, #efe4d8 100%);
}

button,
input,
select {
  font: inherit;
}

button {
  cursor: pointer;
}

code {
  font-family: "SFMono-Regular", Consolas, monospace;
}

.app-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: minmax(280px, 320px) 1fr;
  gap: 22px;
  padding: 22px;
  min-width: 0;
}

.sidebar,
.workspace {
  display: grid;
  gap: 18px;
  min-width: 0;
}

.sidebar {
  align-content: start;
}

.workspace {
  grid-template-columns: minmax(0, 1fr) minmax(280px, 340px);
  align-items: start;
  min-width: 0;
}

.workspace.stage-collapsed {
  grid-template-columns: minmax(108px, 132px) minmax(0, 1fr);
}

.panel {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  backdrop-filter: blur(18px);
}

.brand,
.controls,
.results-panel,
.stage,
.inspector {
  min-width: 0;
}

.brand {
  padding: 22px;
}

.eyebrow {
  margin: 0 0 10px;
  text-transform: uppercase;
  letter-spacing: 0.14em;
  font-size: 0.77rem;
  color: var(--accent-strong);
  font-weight: 700;
}

h1,
h2,
h3,
h4,
p {
  margin: 0;
}

h1 {
  font-size: clamp(1.9rem, 2.8vw, 2.8rem);
  line-height: 1;
  white-space: nowrap;
}

.lede,
.hint,
.meta,
.count,
.refresh-indicator,
.empty {
  color: var(--muted);
}

.lede {
  margin-top: 12px;
  line-height: 1.5;
}

.controls,
.results-panel,
.stage,
.inspector {
  padding: 18px;
}

.controls,
.details-grid {
  display: grid;
  gap: 12px;
}

.controls {
  gap: 10px;
}

.field {
  display: grid;
  gap: 8px;
  font-size: 0.92rem;
}

.field-grow {
  min-width: min(360px, 100%);
}

input,
select {
  width: 100%;
  border: 1px solid rgba(23, 34, 48, 0.16);
  border-radius: 16px;
  padding: 12px 14px;
  background: #ffffff;
  color: var(--ink);
}

.section-heading {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 14px;
}

.results {
  display: grid;
  gap: 10px;
  max-height: calc(100vh - 340px);
  overflow: auto;
}

.result {
  width: 100%;
  text-align: left;
  border: 1px solid transparent;
  border-radius: 18px;
  background: var(--panel-strong);
  padding: 14px;
  transition: transform 160ms ease, border-color 160ms ease, background 160ms ease;
}

.result:hover,
.result:focus-visible,
.result.active {
  border-color: rgba(13, 148, 136, 0.32);
  background: linear-gradient(180deg, #ffffff 0%, var(--accent-soft) 100%);
  transform: translateY(-1px);
  outline: none;
}

.result-title {
  display: grid;
  gap: 8px;
  margin-bottom: 8px;
  min-width: 0;
}

.result-title strong {
  font-size: 1rem;
  min-width: 0;
  white-space: normal;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.result .meta {
  display: block;
  min-width: 0;
  white-space: normal;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.mode-pill-list,
.mode-switcher,
.toolbar-actions,
.legend,
.node-chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.pill,
.mode-switcher button,
.compare-toggle,
.badge,
.legend-item,
.toolbar-actions button,
.step-type,
.edge-chip {
  border-radius: 999px;
  padding: 8px 12px;
  border: 1px solid var(--line);
  background: #ffffff;
  color: var(--ink);
}

.pill,
.step-type,
.edge-chip {
  padding: 4px 10px;
  font-size: 0.78rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  border-color: transparent;
  background: linear-gradient(90deg, var(--accent-soft) 0%, #fff2ea 100%);
  color: var(--accent-strong);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 10px;
}

.stage {
  display: grid;
  gap: 16px;
  min-width: 0;
}

.stage.collapsed {
  align-content: start;
}

.stage.collapsed > :not(.stage-header) {
  display: none;
}

.stage.collapsed .stage-header {
  display: grid;
  gap: 12px;
}

.stage.collapsed .stage-header > div:first-child {
  display: none;
}

.stage.collapsed .stage-actions {
  justify-items: stretch;
}

.stage.collapsed .stage-actions > :not(.panel-toggle) {
  display: none;
}

.stage-header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: start;
}

.stage-actions {
  display: grid;
  gap: 10px;
  justify-items: end;
}

.mode-switcher button,
.panel-toggle,
.compare-toggle,
.toolbar-actions button {
  transition: border-color 160ms ease, background 160ms ease, color 160ms ease;
}

.mode-switcher button.active,
.panel-toggle.active,
.compare-toggle.active,
.toolbar-actions button:hover,
.toolbar-actions button:focus-visible {
  border-color: rgba(13, 148, 136, 0.36);
  background: var(--accent-soft);
  color: var(--accent-strong);
  font-weight: 700;
  outline: none;
}

.panel-toggle {
  border-radius: 999px;
  padding: 8px 12px;
  border: 1px solid var(--line);
  background: #ffffff;
  color: var(--ink);
}

.stage.collapsed .panel-toggle {
  width: 100%;
  min-height: 120px;
  white-space: normal;
  text-align: center;
  font-weight: 700;
}

.compare-toggle:disabled {
  cursor: default;
  opacity: 0.45;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px;
}

.summary-card {
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--panel-strong);
  padding: 14px;
}

.summary-card strong {
  display: block;
  margin-bottom: 6px;
  color: var(--muted);
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.summary-card span {
  display: block;
  font-size: 1.05rem;
  font-weight: 700;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  gap: 14px;
  align-items: end;
  flex-wrap: wrap;
}

.legend {
  min-height: 36px;
}

.legend-item {
  padding-inline: 10px;
  font-size: 0.82rem;
}

button.legend-item {
  cursor: pointer;
}

.legend-item.active,
button.legend-item:hover,
button.legend-item:focus-visible {
  border-color: rgba(13, 148, 136, 0.36);
  background: var(--accent-soft);
  color: var(--accent-strong);
  font-weight: 700;
  outline: none;
}

.diagram-grid {
  display: grid;
  gap: 16px;
  grid-template-columns: 1fr;
}

.diagram-grid.compare {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.diagram-card {
  border: 1px solid var(--line);
  border-radius: 22px;
  background: var(--panel-strong);
  padding: 16px;
  display: grid;
  gap: 14px;
  min-width: 0;
}

.diagram-card-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: baseline;
}

.diagram-card-title {
  font-size: 1.05rem;
}

.diagram-meta {
  font-size: 0.88rem;
  color: var(--muted);
}

.flow-stage {
  border: 1px solid var(--line);
  border-radius: 20px;
  background: linear-gradient(180deg, #fffdf9 0%, #fff8f2 100%);
  padding: 18px;
  overflow: auto;
}

.flow-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 14px;
}

.flow-step {
  position: relative;
  padding-left: calc(26px + var(--depth, 0) * 24px);
}

.flow-step::before {
  content: "";
  position: absolute;
  left: calc(8px + var(--depth, 0) * 24px);
  top: -12px;
  bottom: -18px;
  width: 2px;
  background: linear-gradient(180deg, rgba(13, 148, 136, 0) 0%, rgba(13, 148, 136, 0.2) 20%, rgba(13, 148, 136, 0.2) 80%, rgba(13, 148, 136, 0) 100%);
}

.flow-step:first-child::before {
  top: 18px;
}

.flow-step:last-child::before {
  bottom: calc(100% - 18px);
}

.step-button {
  width: 100%;
  text-align: left;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: #ffffff;
  padding: 14px;
  display: grid;
  gap: 10px;
  transition: transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease, opacity 160ms ease;
}

.step-button:hover,
.step-button:focus-visible {
  transform: translateX(2px);
  border-color: rgba(13, 148, 136, 0.36);
  box-shadow: 0 12px 28px rgba(13, 148, 136, 0.12);
  outline: none;
}

.step-button.active {
  border-color: rgba(13, 148, 136, 0.48);
  background: linear-gradient(180deg, #ffffff 0%, #e8fbf5 100%);
  box-shadow: 0 14px 30px rgba(13, 148, 136, 0.16);
}

.step-button.related {
  border-color: rgba(239, 131, 84, 0.3);
}

.step-button.dim {
  opacity: 0.45;
}

.step-button.match {
  box-shadow: 0 0 0 3px rgba(239, 131, 84, 0.18);
}

.step-heading,
.step-subheading,
.edge-list,
.node-list {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: start;
  flex-wrap: wrap;
}

.step-index {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--accent-strong);
  font-weight: 700;
}

.step-title {
  font-size: 1rem;
  font-weight: 700;
}

.step-meta,
.edge-list,
.node-list {
  color: var(--muted);
  font-size: 0.88rem;
}

.step-subheading {
  align-items: center;
}

.step-note {
  font-size: 0.9rem;
  color: var(--muted);
}

.step-type[data-kind="orchestratorstart"],
.legend-item[data-kind="orchestratorstart"] {
  background: rgba(13, 148, 136, 0.12);
}

.step-type[data-kind="activity"],
.legend-item[data-kind="activity"] {
  background: rgba(23, 34, 48, 0.06);
}

.step-type[data-kind="suborchestrator"],
.step-type[data-kind="retrysuborchestrator"],
.legend-item[data-kind="suborchestrator"],
.legend-item[data-kind="retrysuborchestrator"] {
  background: rgba(79, 70, 229, 0.12);
}

.step-type[data-kind="decision"],
.legend-item[data-kind="decision"] {
  background: rgba(245, 158, 11, 0.16);
}

.step-type[data-kind="retryactivity"],
.step-type[data-kind="retrysuborchestrator"],
.legend-item[data-kind="retryactivity"],
.legend-item[data-kind="retrysuborchestrator"] {
  background: rgba(239, 131, 84, 0.18);
}

.step-type[data-kind="externalevent"],
.legend-item[data-kind="externalevent"] {
  background: rgba(14, 165, 233, 0.16);
}

.step-type[data-kind="timer"],
.legend-item[data-kind="timer"] {
  background: rgba(99, 102, 241, 0.14);
}

.step-type[data-kind="fanout"],
.step-type[data-kind="fanin"],
.step-type[data-kind="parallelgroup"],
.legend-item[data-kind="fanout"],
.legend-item[data-kind="fanin"],
.legend-item[data-kind="parallelgroup"] {
  background: rgba(168, 85, 247, 0.14);
}

.edge-chip {
  background: #fff8f1;
}

.details-grid,
.node-details {
  display: grid;
  gap: 10px;
  min-width: 0;
}

.detail,
.node-panel {
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--panel-strong);
  padding: 12px 14px;
  min-width: 0;
  overflow: hidden;
}

.detail strong,
.node-panel strong {
  display: block;
  margin-bottom: 4px;
  font-size: 0.82rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
}

.detail-value {
  min-width: 0;
  max-width: 100%;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.warnings {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 10px;
}

.warnings li {
  border-radius: 16px;
  background: rgba(254, 243, 199, 0.75);
  color: #92400e;
  padding: 12px 14px;
}

.inspector {
  display: grid;
  gap: 18px;
  min-width: 0;
}

.inspector-section {
  display: grid;
  gap: 12px;
}

.source-panel {
  border-top: 1px solid var(--line);
  padding-top: 12px;
}

.source-panel summary {
  cursor: pointer;
  font-weight: 700;
}

.viewer-link,
.viewer-back-link {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
  color: var(--accent-strong);
  font-weight: 700;
  text-decoration: none;
}

.viewer-link.disabled {
  color: var(--muted);
  pointer-events: none;
}

.source {
  margin: 12px 0 0;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  word-break: break-word;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: #fbf7f1;
  padding: 14px;
  overflow: auto;
  font-family: "SFMono-Regular", Consolas, monospace;
  font-size: 0.9rem;
}

.diagram-viewer-body {
  min-height: 100vh;
}

.diagram-viewer-shell {
  min-height: 100vh;
  padding: 22px;
}

.diagram-viewer-panel {
  min-height: calc(100vh - 44px);
  padding: 24px;
  display: grid;
  gap: 18px;
}

.diagram-viewer-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.diagram-viewer-stage {
  border: 1px solid var(--line);
  border-radius: 22px;
  background: #fbf7f1;
  padding: 18px;
  min-height: 420px;
}

.diagram-render {
  white-space: pre-wrap;
}

@media (max-width: 1200px) {
  .workspace {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 960px) {
  .app-shell {
    grid-template-columns: 1fr;
  }

  .results {
    max-height: 280px;
  }

  .diagram-grid.compare {
    grid-template-columns: 1fr;
  }

  .diagram-viewer-header {
    flex-direction: column;
  }
}
""";
    }
}

internal static class DashboardScriptTemplate
{
    public static string Render()
    {
        return """
(function () {
  const bootstrapEl = document.getElementById('dashboard-bootstrap');
  const workspaceEl = document.getElementById('workspace');
  const stageEl = document.getElementById('stage');
  const resultsEl = document.getElementById('results');
  const resultCountEl = document.getElementById('result-count');
  const titleEl = document.getElementById('selected-title');
  const modeEl = document.getElementById('selected-mode');
  const detailsEl = document.getElementById('details');
  const warningsEl = document.getElementById('warnings');
  const sourceEl = document.getElementById('source');
  const openRenderedDiagramEl = document.getElementById('open-rendered-diagram');
  const diagramGridEl = document.getElementById('diagram-grid');
  const modeSwitcherEl = document.getElementById('mode-switcher');
  const toggleStageEl = document.getElementById('toggle-stage');
  const compareToggleEl = document.getElementById('compare-toggle');
  const refreshIndicatorEl = document.getElementById('refresh-indicator');
  const orchestratorFilterEl = document.getElementById('orchestrator-filter');
  const modeFilterEl = document.getElementById('mode-filter');
  const summaryCardsEl = document.getElementById('summary-cards');
  const legendEl = document.getElementById('legend');
  const nodeDetailsEl = document.getElementById('node-details');
  const nodeSearchEl = document.getElementById('node-search');
  const nodeSearchStatusEl = document.getElementById('node-search-status');
  const findStepEl = document.getElementById('find-step');
  const startStepEl = document.getElementById('start-step');
  const clearStepEl = document.getElementById('clear-step');
  const refreshMs = 3000;

  const state = {
    diagrams: readBootstrap(),
    groups: [],
    filtered: [],
    selectedOrchestrator: '',
    selectedMode: '',
    selectedNodeId: '',
    legendFilterKind: '',
    compareMode: false,
    stageCollapsed: false,
    lastSerialized: '',
    hasLiveRefresh: window.location.protocol !== 'file:',
    applyingPopState: false
  };

  if (window.mermaid && typeof window.mermaid.initialize === 'function') {
    window.mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });
  }

  hydrate(false);

  orchestratorFilterEl.addEventListener('input', applyFilters);
  modeFilterEl.addEventListener('change', applyFilters);
  compareToggleEl.addEventListener('click', function () {
    if (compareToggleEl.disabled) {
      return;
    }

    state.compareMode = !state.compareMode;
    renderSelection();
    writeUrlState('push');
  });
  toggleStageEl.addEventListener('click', function () {
    state.stageCollapsed = !state.stageCollapsed;
    applyStageState();
    writeUrlState('push');
  });
  nodeSearchEl.addEventListener('input', renderSelection);
  nodeSearchEl.addEventListener('keydown', function (event) {
    if (event.key === 'Enter') {
      event.preventDefault();
      selectFirstMatchingNode(true);
    }
  });
  findStepEl.addEventListener('click', function () {
    selectFirstMatchingNode(true);
  });
  startStepEl.addEventListener('click', function () {
    const selected = getSelectedArtifact();
    if (!selected) {
      return;
    }

    const startNode = getStartNode(selected);
    if (!startNode) {
      return;
    }

    state.selectedNodeId = isBusinessArtifact(selected)
      ? getVisibleBusinessFlowItems(buildBusinessFlow(selected))[0]?.key || ''
      : createFlatNodeKey(selected, startNode.id);
    renderSelection();
    writeUrlState('push');
  });
  clearStepEl.addEventListener('click', function () {
    state.selectedNodeId = '';
    renderSelection();
    writeUrlState('push');
  });
  window.addEventListener('popstate', function () {
    state.applyingPopState = true;
    try {
      applyUrlState();
      applyFilters();
    } finally {
      state.applyingPopState = false;
    }
  });
  document.addEventListener('keydown', handleGlobalKeydown);

  if (state.hasLiveRefresh) {
    refreshIndicatorEl.textContent = 'Polling localhost';
    window.setInterval(pollForUpdates, refreshMs);
  }

  function readBootstrap() {
    try {
      return JSON.parse(bootstrapEl.textContent || '[]');
    } catch {
      return [];
    }
  }

  function applyUrlState() {
    const params = new URLSearchParams(window.location.search);
    state.selectedOrchestrator = params.get('orchestrator') || state.selectedOrchestrator;
    state.selectedMode = params.get('mode') || state.selectedMode;
    state.selectedNodeId = params.get('node') || state.selectedNodeId;
    state.compareMode = params.get('compare') === '1';
    state.stageCollapsed = params.get('stage') === 'collapsed';
  }

  function writeUrlState(historyMode) {
    if (state.applyingPopState) {
      return;
    }

    const url = new URL(window.location.href);
    if (state.selectedOrchestrator) {
      url.searchParams.set('orchestrator', state.selectedOrchestrator);
    } else {
      url.searchParams.delete('orchestrator');
    }

    if (state.selectedMode) {
      url.searchParams.set('mode', state.selectedMode);
    } else {
      url.searchParams.delete('mode');
    }

    if (state.selectedNodeId) {
      url.searchParams.set('node', state.selectedNodeId);
    } else {
      url.searchParams.delete('node');
    }

    if (state.compareMode) {
      url.searchParams.set('compare', '1');
    } else {
      url.searchParams.delete('compare');
    }

    if (state.stageCollapsed) {
      url.searchParams.set('stage', 'collapsed');
    } else {
      url.searchParams.delete('stage');
    }

    const method = historyMode === 'push' ? 'pushState' : 'replaceState';
    window.history[method](null, '', url);
  }

  function applyStageState() {
    workspaceEl.classList.toggle('stage-collapsed', state.stageCollapsed);
    stageEl.classList.toggle('collapsed', state.stageCollapsed);
    toggleStageEl.classList.toggle('active', state.stageCollapsed);
    toggleStageEl.setAttribute('aria-pressed', state.stageCollapsed ? 'true' : 'false');
    toggleStageEl.textContent = state.stageCollapsed ? 'Expand stage' : 'Collapse stage';
  }

  async function pollForUpdates() {
    try {
      const response = await fetch('dashboard-data.json?t=' + Date.now(), { cache: 'no-store' });
      if (!response.ok) {
        refreshIndicatorEl.textContent = 'Waiting for localhost refresh';
        return;
      }

      const nextDiagrams = await response.json();
      const serialized = JSON.stringify(nextDiagrams);
      if (serialized === state.lastSerialized) {
        refreshIndicatorEl.textContent = 'Watching localhost';
        return;
      }

      state.diagrams = nextDiagrams;
      hydrate(true);
      refreshIndicatorEl.textContent = 'Updated from localhost';
    } catch {
      refreshIndicatorEl.textContent = 'Static snapshot';
    }
  }

  function hydrate(preserveSelection) {
    state.lastSerialized = JSON.stringify(state.diagrams);
    state.groups = groupDiagrams(state.diagrams);

    if (!preserveSelection) {
      applyUrlState();
    }

    const defaultGroup = state.groups[0] || null;
    if (!state.selectedOrchestrator && defaultGroup) {
      state.selectedOrchestrator = defaultGroup.orchestratorKey;
    }

    const selectedGroup = getSelectedGroup();
    if ((!state.selectedMode || !selectedGroup || !hasMode(selectedGroup, state.selectedMode)) && selectedGroup) {
      state.selectedMode = hasMode(selectedGroup, 'developer')
        ? 'developer'
        : selectedGroup.modes[0]
          ? selectedGroup.modes[0].mode
          : '';
    }

    ensureSelectedNode();
    applyStageState();
    applyFilters();
    writeUrlState('replace');
  }

  function groupDiagrams(diagrams) {
    const grouped = new Map();

    diagrams.forEach(function (diagram) {
      const orchestratorKey = getArtifactOrchestratorKey(diagram);
      const existing = grouped.get(orchestratorKey) || {
        orchestratorKey: orchestratorKey,
        orchestratorName: getArtifactOrchestratorLabel(diagram),
        sourceFile: diagram.sourceFile || '',
        sourceProjectPath: diagram.sourceProjectPath || '',
        modes: []
      };

      const existingMode = existing.modes.find(function (entry) { return entry.mode === diagram.mode; });
      if (!existingMode || new Date(existingMode.generatedAt) < new Date(diagram.generatedAt)) {
        existing.modes = existing.modes.filter(function (entry) { return entry.mode !== diagram.mode; }).concat([diagram]);
      }

      existing.modes.sort(function (left, right) {
        if (left.mode === 'developer') return -1;
        if (right.mode === 'developer') return 1;
        return left.mode.localeCompare(right.mode);
      });

      grouped.set(orchestratorKey, existing);
    });

    return Array.from(grouped.values()).sort(function (left, right) {
      return left.orchestratorName.localeCompare(right.orchestratorName);
    });
  }

  function applyFilters() {
    const nameFilter = orchestratorFilterEl.value.trim().toLowerCase();
    const modeFilter = modeFilterEl.value;

    state.filtered = state.groups.filter(function (group) {
      const matchesName = group.orchestratorName.toLowerCase().includes(nameFilter);
      const hasDeveloper = hasMode(group, 'developer');
      const hasBusiness = hasMode(group, 'business');
      const matchesMode =
        modeFilter === '' ||
        (modeFilter === 'developer' && hasDeveloper) ||
        (modeFilter === 'business' && hasBusiness) ||
        (modeFilter === 'both' && hasDeveloper && hasBusiness);

      return matchesName && matchesMode;
    });

    if (!state.filtered.some(function (group) { return group.orchestratorKey === state.selectedOrchestrator; })) {
      state.selectedOrchestrator = state.filtered[0] ? state.filtered[0].orchestratorKey : '';
    }

    const selectedGroup = getSelectedGroup();
    if (selectedGroup && !hasMode(selectedGroup, state.selectedMode)) {
      state.selectedMode = hasMode(selectedGroup, 'developer')
        ? 'developer'
        : selectedGroup.modes[0]
          ? selectedGroup.modes[0].mode
          : '';
    }

    ensureSelectedNode();
    renderResults();
    renderSelection();
    writeUrlState('replace');
  }

  function ensureSelectedNode() {
    const artifact = getSelectedArtifact();
    if (!artifact) {
      state.selectedNodeId = '';
      return;
    }

    if (isBusinessArtifact(artifact)) {
      const visibleItems = getVisibleBusinessFlowItems(buildBusinessFlow(artifact));
      if (state.selectedNodeId && visibleItems.some(function (item) { return item.key === state.selectedNodeId; })) {
        return;
      }

      state.selectedNodeId = visibleItems[0] ? visibleItems[0].key : '';
      return;
    }

    if (state.selectedNodeId && getNodeById(artifact, state.selectedNodeId)) {
      return;
    }

    const startNode = getStartNode(artifact);
    state.selectedNodeId = startNode ? createFlatNodeKey(artifact, startNode.id) : '';
  }

  function renderResults() {
    resultsEl.innerHTML = '';
    resultCountEl.textContent = state.filtered.length + ' total';

    if (state.filtered.length === 0) {
      resultsEl.innerHTML = '<div class="empty">No orchestrators match the current filters. Clear the filters to restore the list.</div>';
      return;
    }

    state.filtered.forEach(function (group) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'result';
      if (group.orchestratorKey === state.selectedOrchestrator) {
        button.classList.add('active');
      }

      const modes = group.modes.map(function (entry) {
        return '<span class="pill">' + escapeHtml(entry.mode) + '</span>';
      }).join('');

      button.innerHTML =
        '<div class="result-title">' +
          '<strong>' + escapeHtml(group.orchestratorName) + '</strong>' +
          '<div class="mode-pill-list">' + modes + '</div>' +
        '</div>' +
        '<div class="meta">' + escapeHtml(group.sourceProjectPath || group.sourceFile || 'Source unknown') + '</div>';

      button.addEventListener('click', function () {
        state.selectedOrchestrator = group.orchestratorKey;
        if (!hasMode(group, state.selectedMode)) {
          state.selectedMode = hasMode(group, 'developer') ? 'developer' : group.modes[0].mode;
        }
        ensureSelectedNode();
        renderResults();
        renderSelection();
        writeUrlState('push');
      });

      resultsEl.appendChild(button);
    });
  }

  function renderSelection() {
    const group = getSelectedGroup();
    const selected = getSelectedArtifact();

    if (!group || !selected) {
      modeEl.textContent = 'No selection';
      titleEl.textContent = 'Select a generated diagram';
      summaryCardsEl.innerHTML = '';
      legendEl.innerHTML = '';
      diagramGridEl.classList.remove('compare');
      diagramGridEl.innerHTML = '<div class="empty">Choose an orchestrator to inspect its generated diagrams.</div>';
      detailsEl.innerHTML = '';
      warningsEl.innerHTML = '';
      sourceEl.textContent = '';
      setViewerLink(null);
      modeSwitcherEl.innerHTML = '';
      nodeDetailsEl.className = 'node-details empty';
      nodeDetailsEl.textContent = 'Select a step to inspect its incoming and outgoing flow.';
      nodeSearchStatusEl.textContent = '';
      compareToggleEl.disabled = true;
      compareToggleEl.classList.remove('active');
      return;
    }

    const hasDeveloper = hasMode(group, 'developer');
    const hasBusiness = hasMode(group, 'business');
    compareToggleEl.disabled = !(hasDeveloper && hasBusiness);
    compareToggleEl.classList.toggle('active', state.compareMode && !compareToggleEl.disabled);
    if (compareToggleEl.disabled) {
      state.compareMode = false;
    }

    modeEl.textContent = selected.mode + ' view';
    titleEl.textContent = group.orchestratorName;
    if (!isBusinessArtifact(selected)) {
      state.legendFilterKind = '';
    }
    renderModeSwitcher(group);
    renderSummary(selected);
    renderLegend(selected);
    renderInspector(selected);
    renderDiagrams(group, selected);
    sourceEl.textContent = selected.mermaid || '';
    setViewerLink(selected);
    updateNodeSearchStatus(selected);
  }

  function renderModeSwitcher(group) {
    modeSwitcherEl.innerHTML = '';

    group.modes.forEach(function (entry) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = entry.mode + ' view';
      if (entry.mode === state.selectedMode) {
        button.classList.add('active');
      }

      button.addEventListener('click', function () {
        state.selectedMode = entry.mode;
        ensureSelectedNode();
        renderResults();
        renderSelection();
        writeUrlState('push');
      });

      modeSwitcherEl.appendChild(button);
    });
  }

  function renderSummary(selected) {
    const graph = buildGraph(selected);
    const branchCount = countBranchNodes(graph);
    const warningsCount = (selected.warnings || []).length;
    const summary = [
      ['Steps', String(graph.nodes.length)],
      ['Connections', String(graph.edges.length)],
      ['Branches', String(branchCount)],
      ['Warnings', String(warningsCount)],
      ['Flow shape', describeFlowShape(graph)],
      ['Updated', new Date(selected.generatedAt).toLocaleTimeString()]
    ];

    summaryCardsEl.innerHTML = summary.map(function (entry) {
      return '<div class="summary-card"><strong>' + escapeHtml(entry[0]) + '</strong><span>' + escapeHtml(entry[1]) + '</span></div>';
    }).join('');
  }

  function renderLegend(selected) {
    const graph = buildGraph(selected);
    const kinds = graph.nodes
      .map(function (node) { return String(node.nodeType || '').toLowerCase(); })
      .filter(function (value, index, all) { return value && all.indexOf(value) === index; });

    if (!isBusinessArtifact(selected)) {
      legendEl.innerHTML = kinds.map(function (kind) {
        return '<span class="legend-item" data-kind="' + escapeHtml(kind) + '">' + escapeHtml(formatNodeType(kind)) + '</span>';
      }).join('');
      return;
    }

    if (state.legendFilterKind && kinds.indexOf(state.legendFilterKind) < 0) {
      state.legendFilterKind = '';
    }

    legendEl.innerHTML = '';
    kinds.forEach(function (kind) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'legend-item';
      button.dataset.kind = kind;
      button.textContent = formatNodeType(kind);
      if (kind === state.legendFilterKind) {
        button.classList.add('active');
      }

      button.addEventListener('click', function () {
        state.legendFilterKind = state.legendFilterKind === kind ? '' : kind;
        ensureSelectedNode();
        renderSelection();
        writeUrlState('push');
      });

      legendEl.appendChild(button);
    });
  }

  function isBusinessArtifact(artifact) {
    return !!artifact && String(artifact.mode || '').toLowerCase() === 'business';
  }

  function renderInspector(selected) {
    const graph = buildGraph(selected);
    const selectedContext = getSelectedNodeContext(selected);
    const selectedNode = selectedContext ? selectedContext.node : null;

    const details = isBusinessArtifact(selected)
      ? buildBusinessDetails(selected)
      : [
          ['Generated', new Date(selected.generatedAt).toLocaleString()],
          ['Source project', selected.sourceProjectPath || 'Unknown'],
          ['Source file', selected.sourceFile || 'Unknown'],
          ['Artifact file', selected.mermaidFileName || 'Unknown'],
          ['Primary path', graph.nodes.map(function (node) { return node.displayLabel || node.name || node.id; }).join(' -> ')]
        ];

    detailsEl.innerHTML = details.map(function (entry) {
      return '<div class="detail"><strong>' + escapeHtml(entry[0]) + '</strong><div class="detail-value">' + escapeHtml(entry[1]) + '</div></div>';
    }).join('');

    const warnings = selected.warnings || [];
    warningsEl.innerHTML = warnings.length === 0
      ? '<li>No warnings for this artifact.</li>'
      : warnings.map(function (warning) { return '<li>' + escapeHtml(warning) + '</li>'; }).join('');

    if (!selectedNode) {
      nodeDetailsEl.className = 'node-details empty';
      nodeDetailsEl.textContent = isBusinessArtifact(selected)
        ? 'Select a stage to inspect its incoming and outgoing flow.'
        : 'Select a step to inspect its incoming and outgoing flow.';
      return;
    }

    const selectedGraph = selectedContext ? selectedContext.graph : graph;
    const incoming = (selectedGraph.incoming[selectedNode.id] || []).map(function (edge) {
      return describeEdge(selectedGraph, edge, 'from');
    });
    const outgoing = (selectedGraph.outgoing[selectedNode.id] || []).map(function (edge) {
      return describeEdge(selectedGraph, edge, 'to');
    });

    nodeDetailsEl.className = 'node-details';
    nodeDetailsEl.innerHTML =
      '<div class="node-panel"><strong>' + escapeHtml(isBusinessArtifact(selected) ? 'Stage' : 'Step') + '</strong><div class="detail-value">' + escapeHtml(selectedNode.displayLabel || selectedNode.name || selectedNode.id) + '</div></div>' +
      '<div class="node-panel"><strong>Type</strong><div class="detail-value">' + escapeHtml(formatNodeType(selectedNode.nodeType)) + '</div></div>' +
      '<div class="node-panel"><strong>XML summary</strong><div class="detail-value">' + escapeHtml(selectedNode.documentationSummary || 'No XML summary found.') + '</div></div>' +
      '<div class="node-panel"><strong>Source line</strong><div class="detail-value">' + escapeHtml(selectedNode.lineNumber ? String(selectedNode.lineNumber) : 'Unknown') + '</div></div>' +
      '<div class="node-panel"><strong>Incoming</strong><div class="node-list">' + renderNodeList(incoming, 'Start of workflow') + '</div></div>' +
      '<div class="node-panel"><strong>Outgoing</strong><div class="node-list">' + renderNodeList(outgoing, 'End of workflow') + '</div></div>';
  }

  function buildBusinessDetails(selected) {
    const flow = buildBusinessFlow(selected);
    const visibleItems = getVisibleBusinessFlowItems(flow);
    const selectedIndex = visibleItems.findIndex(function (item) { return item.key === state.selectedNodeId; });
    const selectedItem = selectedIndex >= 0 ? visibleItems[selectedIndex] : null;
    const previousItem = selectedIndex > 0 ? visibleItems[selectedIndex - 1] : null;
    const nextItem = selectedIndex >= 0 && selectedIndex < visibleItems.length - 1 ? visibleItems[selectedIndex + 1] : null;

    return [
      ['Highlighted stage', selectedItem ? getNodeLabel(selectedItem.node) : 'None'],
      ['Previous stage', previousItem ? getNodeLabel(previousItem.node) : 'None'],
      ['Next stage', nextItem ? getNodeLabel(nextItem.node) : 'None'],
      ['XML summary', selectedItem && selectedItem.node.documentationSummary ? selectedItem.node.documentationSummary : 'No XML summary found.']
    ];
  }

  function renderDiagrams(group, selected) {
    diagramGridEl.classList.toggle('compare', state.compareMode);

    const visible = state.compareMode
      ? ['developer', 'business'].map(function (mode) { return getMode(group, mode); }).filter(Boolean)
      : [selected];

    diagramGridEl.innerHTML = '';
    visible.forEach(function (artifact) {
      const card = document.createElement('article');
      card.className = 'diagram-card';
      card.innerHTML =
        '<div class="diagram-card-header">' +
          '<div>' +
            '<h3 class="diagram-card-title">' + escapeHtml(artifact.mode === 'developer' ? 'Developer view' : 'Business view') + '</h3>' +
            '<div class="diagram-meta">' + escapeHtml(describeFlowShape(buildGraph(artifact))) + ' · ' + escapeHtml(new Date(artifact.generatedAt).toLocaleString()) + '</div>' +
          '</div>' +
          '<span class="pill">' + escapeHtml(artifact.mode) + '</span>' +
        '</div>' +
        '<div class="flow-stage"></div>';

      const flowStage = card.querySelector('.flow-stage');
      renderFlowStage(flowStage, artifact);
      diagramGridEl.appendChild(card);
    });
  }

  function renderFlowStage(container, artifact) {
    if (isBusinessArtifact(artifact)) {
      renderBusinessFlowStage(container, artifact);
      return;
    }

    const graph = buildGraph(artifact);
    const selection = getFlatNodeId(state.selectedNodeId);
    const hasSelection = !!selection && !!graph.byId[selection];
    const pathSets = hasSelection ? tracePath(graph, selection) : { incoming: {}, outgoing: {} };
    const query = nodeSearchEl.value.trim().toLowerCase();

    const list = document.createElement('ol');
    list.className = 'flow-list';

    graph.nodes.forEach(function (node, index) {
      const item = document.createElement('li');
      item.className = 'flow-step';
      item.style.setProperty('--depth', '0');

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'step-button';
      button.dataset.nodeId = node.id;

      const nodeKey = createFlatNodeKey(artifact, node.id);
      const isSelected = nodeKey === state.selectedNodeId;
      const isRelated = !isSelected && hasSelection && (pathSets.incoming[node.id] || pathSets.outgoing[node.id]);
      const isDimmed = hasSelection && !isSelected && !isRelated;
      const isMatch = query && matchesNode(node, query);

      if (isSelected) {
        button.classList.add('active');
      }
      if (isRelated) {
        button.classList.add('related');
      }
      if (isDimmed) {
        button.classList.add('dim');
      }
      if (isMatch) {
        button.classList.add('match');
      }

      const outgoing = graph.outgoing[node.id] || [];
      const incoming = graph.incoming[node.id] || [];

      button.innerHTML =
        '<div class="step-heading">' +
          '<div class="step-subheading">' +
            '<span class="step-index">' + escapeHtml(String(index + 1)) + '</span>' +
            '<div>' +
              '<div class="step-title">' + escapeHtml(getNodeLabel(node)) + '</div>' +
              '<div class="step-meta">' + escapeHtml(node.name && node.name !== node.displayLabel ? node.name : '') + '</div>' +
            '</div>' +
          '</div>' +
          '<span class="step-type" data-kind="' + escapeHtml(String(node.nodeType || '').toLowerCase()) + '">' + escapeHtml(formatNodeType(node.nodeType)) + '</span>' +
        '</div>' +
        '<div class="step-subheading">' +
          '<span class="step-note">' + escapeHtml(describeConnectivity(incoming.length, outgoing.length, index === 0, index === graph.nodes.length - 1)) + '</span>' +
          '<span class="step-meta">' + escapeHtml(node.lineNumber ? 'Line ' + node.lineNumber : 'Line unknown') + '</span>' +
        '</div>' +
        '<div class="edge-list">' + renderEdgeChips(graph, outgoing) + '</div>';

      button.addEventListener('click', function () {
        state.selectedNodeId = nodeKey;
        renderSelection();
        writeUrlState('push');
        window.requestAnimationFrame(function () {
          button.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        });
      });

      item.appendChild(button);
      list.appendChild(item);
    });

    container.innerHTML = '';
    container.appendChild(list);
  }

  function renderBusinessFlowStage(container, artifact) {
    const flow = buildBusinessFlow(artifact);
    const visibleItems = getVisibleBusinessFlowItems(flow);
    const query = nodeSearchEl.value.trim().toLowerCase();

    const list = document.createElement('ol');
    list.className = 'flow-list';

    visibleItems.forEach(function (flowItem, index) {
      const node = flowItem.node;
      const incoming = flowItem.graph.incoming[node.id] || [];
      const outgoing = flowItem.graph.outgoing[node.id] || [];
      const item = document.createElement('li');
      item.className = 'flow-step';
      item.style.setProperty('--depth', String(flowItem.depth));

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'step-button';
      button.dataset.nodeId = node.id;

      const isSelected = flowItem.key === state.selectedNodeId;
      const isMatch = query && matchesNode(node, query);

      if (isSelected) {
        button.classList.add('active');
      }
      if (isMatch) {
        button.classList.add('match');
      }

      button.innerHTML =
        '<div class="step-heading">' +
          '<div class="step-subheading">' +
            '<span class="step-index">' + escapeHtml(String(index + 1)) + '</span>' +
            '<div>' +
              '<div class="step-title">' + escapeHtml(getNodeLabel(node)) + '</div>' +
              '<div class="step-meta">' + escapeHtml(getBusinessStepMeta(flowItem)) + '</div>' +
            '</div>' +
          '</div>' +
          '<span class="step-type" data-kind="' + escapeHtml(String(node.nodeType || '').toLowerCase()) + '">' + escapeHtml(formatNodeType(node.nodeType)) + '</span>' +
        '</div>' +
        '<div class="step-subheading">' +
          '<span class="step-note">' + escapeHtml(describeConnectivity(incoming.length, outgoing.length, index === 0, index === visibleItems.length - 1)) + '</span>' +
          '<span class="step-meta">' + escapeHtml(node.documentationSummary ? 'XML summary available' : 'No XML summary') + '</span>' +
        '</div>' +
        '<div class="edge-list">' + renderEdgeChips(flowItem.graph, outgoing) + '</div>';

      button.addEventListener('click', function () {
        state.selectedNodeId = flowItem.key;
        renderSelection();
        writeUrlState('push');
        window.requestAnimationFrame(function () {
          button.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        });
      });

      item.appendChild(button);
      list.appendChild(item);
    });

    container.innerHTML = '';
    container.appendChild(list);
  }

  function buildBusinessFlow(artifact) {
    const items = [];

    appendArtifact(artifact, 0, null, [getArtifactOrchestratorKey(artifact)], new Set());

    const byKey = {};
    items.forEach(function (item) {
      byKey[item.key] = item;
    });

    return {
      items: items,
      byKey: byKey
    };

    function appendArtifact(currentArtifact, baseDepth, parentKey, pathTokens, activeOrchestrators) {
      const artifactKey = String(getArtifactOrchestratorKey(currentArtifact) || '').toLowerCase();
      if (activeOrchestrators.has(artifactKey)) {
        return;
      }

      activeOrchestrators.add(artifactKey);
      const graph = buildGraph(currentArtifact);
      const startNode = getStartNode(currentArtifact);
      const startKey = startNode ? createNestedNodeKey(pathTokens, startNode.id) : '';

      graph.nodes.forEach(function (node) {
        const isStart = !!startNode && node.id === startNode.id;
        if (!parentKey && !isStart) {
          return;
        }
        if (parentKey && isStart) {
          return;
        }

        const nodeKey = createNestedNodeKey(pathTokens, node.id);
        const depth = parentKey ? baseDepth : 0;
        const effectiveParentKey = parentKey || null;

        items.push({
          key: nodeKey,
          parentKey: effectiveParentKey,
          depth: depth,
          artifact: currentArtifact,
          graph: graph,
          node: node
        });

        if (!parentKey && isStart) {
          appendRootChildren(nodeKey);
        }

        if (isSubOrchestratorNode(node)) {
          const childArtifact = findChildBusinessArtifact(node);
          if (childArtifact) {
            appendArtifact(
              childArtifact,
              depth + 1,
              nodeKey,
              pathTokens.concat([node.id + ':' + getArtifactOrchestratorKey(childArtifact)]),
              new Set(activeOrchestrators));
          }
        }
      });

      activeOrchestrators.delete(artifactKey);

      function appendRootChildren(rootKey) {
        graph.nodes.forEach(function (node) {
          if (startNode && node.id === startNode.id) {
            return;
          }

          const nodeKey = createNestedNodeKey(pathTokens, node.id);
          items.push({
            key: nodeKey,
            parentKey: rootKey,
            depth: 1,
            artifact: currentArtifact,
            graph: graph,
            node: node
          });

          if (isSubOrchestratorNode(node)) {
            const childArtifact = findChildBusinessArtifact(node);
            if (childArtifact) {
              appendArtifact(
                childArtifact,
                2,
                nodeKey,
                pathTokens.concat([node.id + ':' + getArtifactOrchestratorKey(childArtifact)]),
                new Set(activeOrchestrators));
            }
          }
        });
      }
    }
  }

  function getVisibleBusinessFlowItems(flow) {
    if (!state.legendFilterKind) {
      return flow.items;
    }

    const visibleKeys = {};
    flow.items.forEach(function (item) {
      if (String(item.node.nodeType || '').toLowerCase() !== state.legendFilterKind) {
        return;
      }

      let current = item;
      while (current) {
        visibleKeys[current.key] = true;
        current = current.parentKey ? flow.byKey[current.parentKey] : null;
      }
    });

    return flow.items.filter(function (item) {
      return !!visibleKeys[item.key];
    });
  }

  function findChildBusinessArtifact(node) {
    const childName = node.name || node.displayLabel;
    if (!childName) {
      return null;
    }

    const childGroup = state.groups.find(function (group) {
      return group.orchestratorName === childName
        || group.orchestratorKey === childName;
    });

    return childGroup ? (getMode(childGroup, 'business') || null) : null;
  }

  function isSubOrchestratorNode(node) {
    const kind = String(node.nodeType || '').toLowerCase();
    return kind === 'suborchestrator' || kind === 'retrysuborchestrator';
  }

  function createNestedNodeKey(pathTokens, nodeId) {
    return pathTokens.join('>') + '|' + nodeId;
  }

  function createFlatNodeKey(artifact, nodeId) {
    return getArtifactOrchestratorKey(artifact) + '|' + nodeId;
  }

  function getFlatNodeId(nodeKey) {
    const separatorIndex = String(nodeKey || '').lastIndexOf('|');
    return separatorIndex >= 0 ? String(nodeKey).slice(separatorIndex + 1) : '';
  }

  function getSelectedNodeContext(selected) {
    if (isBusinessArtifact(selected)) {
      const flow = buildBusinessFlow(selected);
      const item = flow.byKey[state.selectedNodeId] || null;
      return item ? { node: item.node, graph: item.graph, item: item } : null;
    }

    const graph = buildGraph(selected);
    const nodeId = getFlatNodeId(state.selectedNodeId);
    const node = nodeId ? graph.byId[nodeId] : null;
    return node ? { node: node, graph: graph, item: null } : null;
  }

  function getNodeLabel(node) {
    return node.displayLabel || node.name || node.id;
  }

  function getBusinessStepMeta(flowItem) {
    if (flowItem.depth === 0) {
      return 'Workflow root';
    }

    return getArtifactOrchestratorKey(flowItem.artifact) === getArtifactOrchestratorKey(getSelectedArtifact())
      ? 'Main orchestration'
      : 'Nested under ' + getArtifactOrchestratorLabel(flowItem.artifact);
  }

  function handleGlobalKeydown(event) {
    if (event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }

    const tagName = document.activeElement && document.activeElement.tagName;
    const isTyping = tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT';
    if (event.key === '/' && !isTyping) {
      event.preventDefault();
      nodeSearchEl.focus();
      nodeSearchEl.select();
      return;
    }

    if (isTyping) {
      return;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      moveOrchestratorSelection(1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      moveOrchestratorSelection(-1);
    } else if (event.key === 'ArrowRight') {
      event.preventDefault();
      moveModeSelection(1);
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      moveModeSelection(-1);
    }
  }

  function moveOrchestratorSelection(direction) {
    if (state.filtered.length === 0) {
      return;
    }

    const currentIndex = state.filtered.findIndex(function (group) {
      return group.orchestratorKey === state.selectedOrchestrator;
    });
    const nextIndex = clampIndex(currentIndex + direction, state.filtered.length);
    state.selectedOrchestrator = state.filtered[nextIndex].orchestratorKey;
    ensureSelectedNode();
    renderResults();
    renderSelection();
    writeUrlState('push');
  }

  function moveModeSelection(direction) {
    const group = getSelectedGroup();
    if (!group || group.modes.length === 0) {
      return;
    }

    const currentIndex = Math.max(0, group.modes.findIndex(function (entry) { return entry.mode === state.selectedMode; }));
    const nextIndex = clampIndex(currentIndex + direction, group.modes.length);
    state.selectedMode = group.modes[nextIndex].mode;
    ensureSelectedNode();
    renderResults();
    renderSelection();
    writeUrlState('push');
  }

  function clampIndex(index, length) {
    if (length <= 0) {
      return 0;
    }

    if (index < 0) {
      return length - 1;
    }

    if (index >= length) {
      return 0;
    }

    return index;
  }

  function selectFirstMatchingNode(pushHistory) {
    const selected = getSelectedArtifact();
    if (!selected) {
      return;
    }

    const query = nodeSearchEl.value.trim().toLowerCase();
    if (!query) {
      updateNodeSearchStatus(selected);
      return;
    }

    if (isBusinessArtifact(selected)) {
      const businessMatch = getVisibleBusinessFlowItems(buildBusinessFlow(selected)).find(function (item) {
        return matchesNode(item.node, query);
      });

      if (!businessMatch) {
        updateNodeSearchStatus(selected);
        return;
      }

      state.selectedNodeId = businessMatch.key;
      renderSelection();
      writeUrlState(pushHistory ? 'push' : 'replace');
      return;
    }

    const match = buildGraph(selected).nodes.find(function (node) {
      return matchesNode(node, query);
    });

    if (!match) {
      updateNodeSearchStatus(selected);
      return;
    }

    state.selectedNodeId = createFlatNodeKey(selected, match.id);
    renderSelection();
    writeUrlState(pushHistory ? 'push' : 'replace');
  }

  function updateNodeSearchStatus(selected) {
    const query = nodeSearchEl.value.trim().toLowerCase();
    if (!query) {
      nodeSearchStatusEl.textContent = isBusinessArtifact(selected)
        ? 'Search within the current business flow to jump directly to a stage.'
        : 'Search within the current diagram to jump directly to a step.';
      return;
    }

    const matches = isBusinessArtifact(selected)
      ? getVisibleBusinessFlowItems(buildBusinessFlow(selected)).filter(function (item) {
          return matchesNode(item.node, query);
        })
      : buildGraph(selected).nodes.filter(function (node) {
          return matchesNode(node, query);
        });

    nodeSearchStatusEl.textContent = matches.length === 0
      ? (isBusinessArtifact(selected) ? 'No matching stages in this view.' : 'No matching steps in this view.')
      : matches.length === 1
        ? (isBusinessArtifact(selected) ? '1 matching stage. Press Enter to jump.' : '1 matching step. Press Enter to jump.')
        : matches.length + (isBusinessArtifact(selected) ? ' matching stages. Press Enter to jump to the first.' : ' matching steps. Press Enter to jump to the first.');
  }

  function buildGraph(artifact) {
    const nodes = (artifact.nodes || []).slice().sort(function (left, right) {
      const lineDelta = (left.lineNumber || 0) - (right.lineNumber || 0);
      if (lineDelta !== 0) {
        return lineDelta;
      }

      return String(left.displayLabel || left.name || left.id).localeCompare(String(right.displayLabel || right.name || right.id));
    });
    const edges = (artifact.edges || []).slice();
    const byId = {};
    const incoming = {};
    const outgoing = {};

    nodes.forEach(function (node) {
      byId[node.id] = node;
      incoming[node.id] = [];
      outgoing[node.id] = [];
    });

    edges.forEach(function (edge) {
      if (outgoing[edge.fromNodeId]) {
        outgoing[edge.fromNodeId].push(edge);
      }
      if (incoming[edge.toNodeId]) {
        incoming[edge.toNodeId].push(edge);
      }
    });

    return {
      nodes: nodes,
      edges: edges,
      byId: byId,
      incoming: incoming,
      outgoing: outgoing
    };
  }

  function tracePath(graph, nodeId) {
    return {
      incoming: walk(graph.incoming, 'fromNodeId', nodeId),
      outgoing: walk(graph.outgoing, 'toNodeId', nodeId)
    };
  }

  function walk(index, key, originId) {
    const visited = {};
    const queue = [originId];

    while (queue.length > 0) {
      const currentId = queue.shift();
      const edges = index[currentId] || [];
      edges.forEach(function (edge) {
        const nextId = edge[key];
        if (visited[nextId]) {
          return;
        }

        visited[nextId] = true;
        queue.push(nextId);
      });
    }

    delete visited[originId];
    return visited;
  }

  function countBranchNodes(graph) {
    return graph.nodes.filter(function (node) {
      return (graph.outgoing[node.id] || []).length > 1;
    }).length;
  }

  function describeFlowShape(graph) {
    if (graph.nodes.length === 0) {
      return 'No flow';
    }

    const hasParallel = graph.nodes.some(function (node) {
      const kind = String(node.nodeType || '').toLowerCase();
      return kind === 'fanout' || kind === 'fanin' || kind === 'parallelgroup';
    });
    const branchCount = countBranchNodes(graph);

    if (hasParallel) {
      return 'Parallel-heavy flow';
    }

    if (branchCount > 0) {
      return 'Branching flow';
    }

    return graph.nodes.length > 6 ? 'Linear multi-step flow' : 'Linear flow';
  }

  function describeConnectivity(incomingCount, outgoingCount, isFirst, isLast) {
    if (isFirst && outgoingCount <= 1) {
      return 'Entry point';
    }

    if (isLast && incomingCount >= 1 && outgoingCount === 0) {
      return 'Terminal step';
    }

    if (outgoingCount > 1) {
      return outgoingCount + ' outgoing paths';
    }

    if (incomingCount > 1) {
      return incomingCount + ' incoming paths';
    }

    return 'Single path step';
  }

  function renderEdgeChips(graph, edges) {
    if (!edges || edges.length === 0) {
      return '<span class="edge-chip">End of flow</span>';
    }

    return edges.map(function (edge) {
      const target = graph.byId[edge.toNodeId];
      const label = target ? (target.displayLabel || target.name || target.id) : edge.toNodeId;
      const branch = edge.conditionLabel ? edge.conditionLabel + ' -> ' : '';
      return '<span class="edge-chip">' + escapeHtml(branch + label) + '</span>';
    }).join('');
  }

  function describeEdge(graph, edge, direction) {
    const targetId = direction === 'from' ? edge.fromNodeId : edge.toNodeId;
    const target = graph.byId[targetId];
    const label = target ? (target.displayLabel || target.name || target.id) : targetId;
    return edge.conditionLabel ? edge.conditionLabel + ' -> ' + label : label;
  }

  function renderNodeList(items, emptyLabel) {
    if (!items || items.length === 0) {
      return '<span>' + escapeHtml(emptyLabel) + '</span>';
    }

    return items.map(function (item) {
      return '<span class="edge-chip">' + escapeHtml(item) + '</span>';
    }).join('');
  }

  function getSelectedGroup() {
    return state.filtered.find(function (group) {
      return group.orchestratorKey === state.selectedOrchestrator;
    }) || state.groups.find(function (group) {
      return group.orchestratorKey === state.selectedOrchestrator;
    }) || null;
  }

  function getArtifactOrchestratorKey(artifact) {
    return String(artifact.orchestratorKey || artifact.orchestratorDisplayName || artifact.orchestratorName || '');
  }

  function getArtifactOrchestratorLabel(artifact) {
    return String(artifact.orchestratorDisplayName || artifact.orchestratorName || artifact.orchestratorKey || '');
  }

  function getSelectedArtifact() {
    const group = getSelectedGroup();
    return group ? getMode(group, state.selectedMode) : null;
  }

  function getMode(group, mode) {
    return group.modes.find(function (entry) { return entry.mode === mode; }) || null;
  }

  function hasMode(group, mode) {
    return !!getMode(group, mode);
  }

  function getStartNode(artifact) {
    return buildGraph(artifact).nodes.find(function (node) {
      return String(node.nodeType || '').toLowerCase() === 'orchestratorstart';
    }) || buildGraph(artifact).nodes[0] || null;
  }

  function getNodeById(artifact, nodeId) {
    return buildGraph(artifact).byId[getFlatNodeId(nodeId)] || null;
  }

  function matchesNode(node, query) {
    const haystack = [
      node.displayLabel,
      node.name,
      node.businessName,
      node.businessGroup,
      node.nodeType,
      node.documentationSummary,
      node.notes,
      node.retryHint
    ].join(' ').toLowerCase();

    return haystack.indexOf(query) >= 0;
  }

  function formatNodeType(kind) {
    const value = String(kind || '');
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/_/g, ' ')
      .replace(/\b\w/g, function (character) { return character.toUpperCase(); });
  }

  function escapeHtml(value) {
    return String(value || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
})();
""";
    }
}

internal static class DiagramViewerScriptTemplate
{
    public static string Render()
    {
        return """
(function () {
  const bootstrapEl = document.getElementById('dashboard-bootstrap');
  const titleEl = document.getElementById('viewer-title');
  const modeEl = document.getElementById('viewer-mode');
  const metaEl = document.getElementById('viewer-meta');
  const stageEl = document.getElementById('viewer-stage');
  const backLinkEl = document.getElementById('viewer-back-link');
  const refreshEl = document.getElementById('viewer-refresh');
  const refreshMs = 3000;

  const state = {
    diagrams: readBootstrap(),
    selectedArtifact: null,
    lastSerialized: '',
    hasLiveRefresh: window.location.protocol !== 'file:'
  };

  if (window.mermaid && typeof window.mermaid.initialize === 'function') {
    window.mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });
  }

  hydrate();

  if (state.hasLiveRefresh) {
    refreshEl.textContent = 'Polling localhost';
    window.setInterval(pollForUpdates, refreshMs);
  }

  function readBootstrap() {
    try {
      return JSON.parse(bootstrapEl.textContent || '[]');
    } catch {
      return [];
    }
  }

  async function pollForUpdates() {
    try {
      const response = await fetch('dashboard-data.json?t=' + Date.now(), { cache: 'no-store' });
      if (!response.ok) {
        refreshEl.textContent = 'Waiting for localhost refresh';
        return;
      }

      const nextDiagrams = await response.json();
      const serialized = JSON.stringify(nextDiagrams);
      if (serialized === state.lastSerialized) {
        refreshEl.textContent = 'Watching localhost';
        return;
      }

      state.diagrams = nextDiagrams;
      hydrate();
      refreshEl.textContent = 'Updated from localhost';
    } catch {
      refreshEl.textContent = 'Static snapshot';
    }
  }

  function hydrate() {
    state.lastSerialized = JSON.stringify(state.diagrams);
    state.selectedArtifact = resolveSelectedArtifact();
    renderSelection();
    normalizeUrl();
  }

  function resolveSelectedArtifact() {
    const params = new URLSearchParams(window.location.search);
    const orchestratorKey = params.get('orchestrator') || '';
    const requestedMode = params.get('mode') || '';

    if (!orchestratorKey) {
      return null;
    }

    const matches = state.diagrams
      .filter(function (artifact) { return getArtifactOrchestratorKey(artifact) === orchestratorKey; })
      .sort(function (left, right) {
        if (left.mode === 'developer') return -1;
        if (right.mode === 'developer') return 1;
        return String(left.mode || '').localeCompare(String(right.mode || ''));
      });

    if (matches.length === 0) {
      return null;
    }

    return matches.find(function (artifact) { return artifact.mode === requestedMode; })
      || matches.find(function (artifact) { return artifact.mode === 'developer'; })
      || matches[0];
  }

  function renderSelection() {
    backLinkEl.href = buildDashboardUrl(state.selectedArtifact);

    if (!state.selectedArtifact) {
      modeEl.textContent = 'No selection';
      titleEl.textContent = 'Diagram not found';
      metaEl.textContent = 'The requested rendered diagram could not be found. Return to the dashboard and choose another artifact.';
      stageEl.innerHTML = '<div class="empty">No rendered diagram matches the current URL.</div>';
      return;
    }

    const artifact = state.selectedArtifact;
    modeEl.textContent = String(artifact.mode || 'unknown') + ' view';
    titleEl.textContent = getArtifactOrchestratorLabel(artifact);
    metaEl.textContent = [
      artifact.sourceProjectPath || artifact.sourceFile || 'Source unknown',
      artifact.mermaidFileName || 'Artifact file unknown'
    ].join(' · ');

    stageEl.innerHTML = '<div id="diagram-render-target" class="diagram-render"></div>';

    const renderTarget = document.getElementById('diagram-render-target');
    renderTarget.textContent = artifact.mermaid || '';
    if (window.mermaid && typeof window.mermaid.run === 'function') {
      window.mermaid.run({ nodes: [renderTarget] });
    }
  }

  function normalizeUrl() {
    if (!state.selectedArtifact) {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set('orchestrator', getArtifactOrchestratorKey(state.selectedArtifact));
    if (state.selectedArtifact.mode) {
      url.searchParams.set('mode', state.selectedArtifact.mode);
    } else {
      url.searchParams.delete('mode');
    }

    window.history.replaceState(null, '', url);
  }

  function buildDashboardUrl(artifact) {
    const url = new URL('index.html', window.location.href);
    const params = new URLSearchParams(window.location.search);
    const orchestratorKey = artifact ? getArtifactOrchestratorKey(artifact) : (params.get('orchestrator') || '');
    const mode = artifact ? artifact.mode : (params.get('mode') || '');

    if (orchestratorKey) {
      url.searchParams.set('orchestrator', orchestratorKey);
    }

    if (mode) {
      url.searchParams.set('mode', mode);
    }

    return url.toString();
  }

  function getArtifactOrchestratorKey(artifact) {
    return String(artifact.orchestratorKey || artifact.orchestratorDisplayName || artifact.orchestratorName || '');
  }

  function getArtifactOrchestratorLabel(artifact) {
    return String(artifact.orchestratorDisplayName || artifact.orchestratorName || artifact.orchestratorKey || '');
  }
})();
""";
    }
}
