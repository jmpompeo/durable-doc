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
  font-size: clamp(2rem, 3vw, 3rem);
  line-height: 0.96;
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
  padding-left: 26px;
}

.flow-step::before {
  content: "";
  position: absolute;
  left: 8px;
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

    state.selectedNodeId = startNode.id;
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
      state.selectedOrchestrator = defaultGroup.orchestratorName;
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
      const existing = grouped.get(diagram.orchestratorName) || {
        orchestratorName: diagram.orchestratorName,
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

      grouped.set(diagram.orchestratorName, existing);
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

    if (!state.filtered.some(function (group) { return group.orchestratorName === state.selectedOrchestrator; })) {
      state.selectedOrchestrator = state.filtered[0] ? state.filtered[0].orchestratorName : '';
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

    if (state.selectedNodeId && getNodeById(artifact, state.selectedNodeId)) {
      return;
    }

    const startNode = getStartNode(artifact);
    state.selectedNodeId = startNode ? startNode.id : '';
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
      if (group.orchestratorName === state.selectedOrchestrator) {
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
        state.selectedOrchestrator = group.orchestratorName;
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
    renderModeSwitcher(group);
    renderSummary(selected);
    renderLegend(selected);
    renderInspector(selected);
    renderDiagrams(group, selected);
    sourceEl.textContent = selected.mermaid || '';
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

    legendEl.innerHTML = kinds.map(function (kind) {
      return '<span class="legend-item" data-kind="' + escapeHtml(kind) + '">' + escapeHtml(formatNodeType(kind)) + '</span>';
    }).join('');
  }

  function renderInspector(selected) {
    const graph = buildGraph(selected);
    const selectedNode = state.selectedNodeId ? graph.byId[state.selectedNodeId] : null;

    const details = [
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
      nodeDetailsEl.textContent = 'Select a step to inspect its incoming and outgoing flow.';
      return;
    }

    const incoming = (graph.incoming[selectedNode.id] || []).map(function (edge) {
      return describeEdge(graph, edge, 'from');
    });
    const outgoing = (graph.outgoing[selectedNode.id] || []).map(function (edge) {
      return describeEdge(graph, edge, 'to');
    });

    nodeDetailsEl.className = 'node-details';
    nodeDetailsEl.innerHTML =
      '<div class="node-panel"><strong>Step</strong><div class="detail-value">' + escapeHtml(selectedNode.displayLabel || selectedNode.name || selectedNode.id) + '</div></div>' +
      '<div class="node-panel"><strong>Type</strong><div class="detail-value">' + escapeHtml(formatNodeType(selectedNode.nodeType)) + '</div></div>' +
      '<div class="node-panel"><strong>Source line</strong><div class="detail-value">' + escapeHtml(selectedNode.lineNumber ? String(selectedNode.lineNumber) : 'Unknown') + '</div></div>' +
      '<div class="node-panel"><strong>Incoming</strong><div class="node-list">' + renderNodeList(incoming, 'Start of workflow') + '</div></div>' +
      '<div class="node-panel"><strong>Outgoing</strong><div class="node-list">' + renderNodeList(outgoing, 'End of workflow') + '</div></div>';
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
    const graph = buildGraph(artifact);
    const selection = graph.byId[state.selectedNodeId] ? state.selectedNodeId : '';
    const pathSets = selection ? tracePath(graph, selection) : { incoming: {}, outgoing: {} };
    const query = nodeSearchEl.value.trim().toLowerCase();

    const list = document.createElement('ol');
    list.className = 'flow-list';

    graph.nodes.forEach(function (node, index) {
      const item = document.createElement('li');
      item.className = 'flow-step';

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'step-button';
      button.dataset.nodeId = node.id;

      const isSelected = node.id === selection;
      const isRelated = !isSelected && (pathSets.incoming[node.id] || pathSets.outgoing[node.id]);
      const isDimmed = !!selection && !isSelected && !isRelated;
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
              '<div class="step-title">' + escapeHtml(node.displayLabel || node.name || node.id) + '</div>' +
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
        state.selectedNodeId = node.id;
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
      return group.orchestratorName === state.selectedOrchestrator;
    });
    const nextIndex = clampIndex(currentIndex + direction, state.filtered.length);
    state.selectedOrchestrator = state.filtered[nextIndex].orchestratorName;
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

    const match = buildGraph(selected).nodes.find(function (node) {
      return matchesNode(node, query);
    });

    if (!match) {
      updateNodeSearchStatus(selected);
      return;
    }

    state.selectedNodeId = match.id;
    renderSelection();
    writeUrlState(pushHistory ? 'push' : 'replace');
  }

  function updateNodeSearchStatus(selected) {
    const query = nodeSearchEl.value.trim().toLowerCase();
    if (!query) {
      nodeSearchStatusEl.textContent = 'Search within the current diagram to jump directly to a step.';
      return;
    }

    const matches = buildGraph(selected).nodes.filter(function (node) {
      return matchesNode(node, query);
    });

    nodeSearchStatusEl.textContent = matches.length === 0
      ? 'No matching steps in this view.'
      : matches.length === 1
        ? '1 matching step. Press Enter to jump.'
        : matches.length + ' matching steps. Press Enter to jump to the first.';
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
      return group.orchestratorName === state.selectedOrchestrator;
    }) || state.groups.find(function (group) {
      return group.orchestratorName === state.selectedOrchestrator;
    }) || null;
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
    return buildGraph(artifact).byId[nodeId] || null;
  }

  function matchesNode(node, query) {
    const haystack = [
      node.displayLabel,
      node.name,
      node.businessName,
      node.businessGroup,
      node.nodeType,
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
