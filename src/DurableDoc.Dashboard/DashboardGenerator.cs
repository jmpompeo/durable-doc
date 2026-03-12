using System.Text;
using System.Text.Json;

namespace DurableDoc.Dashboard;

public sealed class GeneratedDiagramArtifact
{
    public string DiagramId { get; init; } = string.Empty;
    public string OrchestratorName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public string Mermaid { get; init; } = string.Empty;
    public string MermaidFileName { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public string? SourceProjectPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record DashboardBuildResult(string DashboardPath, int DiagramCount);

public static class DashboardGenerator
{
    private const string MermaidBundleFileName = "mermaid.min.js";

    public static DashboardBuildResult WriteArtifactsAndBuild(string outputDirectory, IEnumerable<GeneratedDiagramArtifact> diagrams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputPath = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputPath);

        var materialized = diagrams
            .OrderBy(diagram => diagram.OrchestratorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagram => diagram.Mode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (materialized.Length == 0)
        {
            throw new InvalidOperationException("No diagrams were provided for dashboard generation.");
        }

        foreach (var diagram in materialized)
        {
            DiagramArtifactStore.Write(outputPath, diagram);
        }

        return WriteDashboard(outputPath, DiagramArtifactStore.Read(outputPath));
    }

    public static DashboardBuildResult BuildFromArtifacts(string inputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);

        var inputPath = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException($"Input directory was not found: {inputPath}");
        }

        var diagrams = DiagramArtifactStore.Read(inputPath);
        if (diagrams.Count == 0)
        {
            throw new InvalidOperationException($"No generated diagram artifacts were found in {inputPath}");
        }

        return WriteDashboard(inputPath, diagrams);
    }

    private static DashboardBuildResult WriteDashboard(string outputDirectory, IReadOnlyList<GeneratedDiagramArtifact> diagrams)
    {
        File.WriteAllText(Path.Combine(outputDirectory, MermaidBundleFileName), MermaidCompatibilityBundle.Render());

        var dashboardPath = Path.Combine(outputDirectory, "index.html");
        File.WriteAllText(dashboardPath, DashboardHtmlTemplate.Render(diagrams, MermaidBundleFileName));
        return new DashboardBuildResult(dashboardPath, diagrams.Count);
    }
}

internal static class DiagramArtifactStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static void Write(string outputDirectory, GeneratedDiagramArtifact artifact)
    {
        var fileBaseName = CreateFileBaseName(artifact);
        var mermaidFileName = $"{fileBaseName}.mmd";
        var artifactFileName = $"{fileBaseName}.diagram.json";
        var persistedArtifact = new GeneratedDiagramArtifact
        {
            DiagramId = artifact.DiagramId,
            OrchestratorName = artifact.OrchestratorName,
            Mode = artifact.Mode,
            GeneratedAt = artifact.GeneratedAt,
            Mermaid = artifact.Mermaid,
            MermaidFileName = mermaidFileName,
            SourceFile = artifact.SourceFile,
            SourceProjectPath = artifact.SourceProjectPath,
            Warnings = artifact.Warnings,
        };

        File.WriteAllText(Path.Combine(outputDirectory, mermaidFileName), artifact.Mermaid);
        File.WriteAllText(
            Path.Combine(outputDirectory, artifactFileName),
            JsonSerializer.Serialize(persistedArtifact, SerializerOptions));
    }

    public static IReadOnlyList<GeneratedDiagramArtifact> Read(string inputDirectory)
    {
        return Directory.EnumerateFiles(inputDirectory, "*.diagram.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadArtifact)
            .ToArray();
    }

    private static GeneratedDiagramArtifact ReadArtifact(string path)
    {
        var json = File.ReadAllText(path);
        var artifact = JsonSerializer.Deserialize<GeneratedDiagramArtifact>(json, SerializerOptions);
        if (artifact is null)
        {
            throw new InvalidOperationException($"Generated diagram artifact could not be read: {path}");
        }

        return artifact;
    }

    private static string CreateFileBaseName(GeneratedDiagramArtifact artifact)
    {
        var rawValue = $"{artifact.OrchestratorName}-{artifact.Mode}-{artifact.DiagramId}";
        var builder = new StringBuilder(rawValue.Length);

        foreach (var character in rawValue)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "diagram" : normalized;
    }
}

internal static class DashboardHtmlTemplate
{
    public static string Render(IReadOnlyList<GeneratedDiagramArtifact> diagrams, string mermaidBundleFileName)
    {
        var payload = JsonSerializer.Serialize(diagrams, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }).Replace("</", "<\\/", StringComparison.Ordinal);

        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>durable-doc dashboard</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f0e8;
      --panel: #fffdf8;
      --ink: #1d2a31;
      --muted: #60717c;
      --accent: #0f766e;
      --accent-soft: #dff4ef;
      --line: #d9d0c4;
      --warning: #92400e;
      --warning-soft: #fef3c7;
      --shadow: 0 18px 50px rgba(17, 24, 39, 0.10);
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-height: 100vh;
      font-family: "Iowan Old Style", "Palatino Linotype", serif;
      color: var(--ink);
      background:
        radial-gradient(circle at top left, rgba(15, 118, 110, 0.16), transparent 30%),
        linear-gradient(135deg, #f8f3eb 0%, var(--bg) 55%, #efe4d4 100%);
    }

    .shell {
      display: grid;
      grid-template-columns: minmax(280px, 340px) 1fr;
      gap: 24px;
      min-height: 100vh;
      padding: 24px;
    }

    .panel {
      background: rgba(255, 253, 248, 0.92);
      border: 1px solid rgba(217, 208, 196, 0.9);
      border-radius: 24px;
      box-shadow: var(--shadow);
      backdrop-filter: blur(18px);
    }

    .sidebar {
      padding: 24px;
    }

    .viewer {
      padding: 24px;
      display: grid;
      gap: 16px;
      align-content: start;
      min-width: 0;
    }

    h1 {
      margin: 0 0 8px;
      font-size: clamp(1.8rem, 3vw, 2.5rem);
      line-height: 1;
    }

    h2 {
      margin: 6px 0 0;
    }

    .lede,
    .meta,
    .hint,
    .empty {
      color: var(--muted);
      font-size: 0.94rem;
    }

    .filters {
      display: grid;
      gap: 12px;
      margin-bottom: 18px;
    }

    input, select {
      width: 100%;
      border: 1px solid var(--line);
      border-radius: 14px;
      padding: 12px 14px;
      font: inherit;
      background: #fff;
      color: var(--ink);
    }

    .results {
      display: grid;
      gap: 10px;
      max-height: calc(100vh - 260px);
      overflow: auto;
      padding-right: 4px;
    }

    .result {
      width: 100%;
      text-align: left;
      border: 1px solid transparent;
      border-radius: 18px;
      background: #fff;
      padding: 14px;
      cursor: pointer;
      transition: transform 160ms ease, border-color 160ms ease, background 160ms ease;
      min-width: 0;
    }

    .result:hover,
    .result.active {
      border-color: rgba(15, 118, 110, 0.35);
      background: var(--accent-soft);
      transform: translateY(-1px);
    }

    .result strong {
      display: block;
      margin-bottom: 4px;
      font-size: 1rem;
      white-space: normal;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .result .meta {
      display: block;
      white-space: normal;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .viewer-header {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      justify-content: space-between;
      align-items: flex-start;
      min-width: 0;
    }

    .viewer-header > div:first-child {
      min-width: 0;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent);
      padding: 6px 12px;
      font-size: 0.85rem;
      font-weight: 600;
      letter-spacing: 0.03em;
      text-transform: uppercase;
    }

    .mode-switcher {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
    }

    .mode-switcher button {
      border: 1px solid var(--line);
      border-radius: 999px;
      background: #fff;
      color: var(--ink);
      padding: 8px 14px;
      cursor: pointer;
      font: inherit;
    }

    .mode-switcher button.active {
      border-color: rgba(15, 118, 110, 0.35);
      background: var(--accent-soft);
      color: var(--accent);
      font-weight: 600;
    }

    .details-grid {
      display: grid;
      gap: 8px;
      border: 1px solid var(--line);
      border-radius: 20px;
      background: #fff;
      padding: 16px 18px;
      min-width: 0;
    }

    .detail-row {
      display: grid;
      grid-template-columns: max-content minmax(0, 1fr);
      gap: 8px;
      align-items: start;
      min-width: 0;
    }

    .detail-label {
      color: var(--muted);
      white-space: nowrap;
    }

    .detail-value {
      min-width: 0;
      white-space: normal;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .warnings {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .warnings li {
      border-radius: 14px;
      background: var(--warning-soft);
      color: var(--warning);
      padding: 10px 12px;
    }

    #diagram {
      min-height: 360px;
      overflow: auto;
      border: 1px solid var(--line);
      border-radius: 20px;
      background: white;
      padding: 20px;
    }

    #source {
      margin: 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      word-break: break-word;
      border: 1px solid var(--line);
      border-radius: 20px;
      background: #f8f6f1;
      padding: 18px;
      overflow: auto;
      font-family: "SFMono-Regular", Consolas, monospace;
      font-size: 0.9rem;
    }

    .mermaid-local {
      display: grid;
      gap: 18px;
    }

    .mermaid-node-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      min-width: 0;
    }

    .mermaid-node {
      border: 1px solid var(--line);
      border-radius: 18px;
      padding: 12px;
      background: #faf7f2;
      min-width: 0;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .mermaid-node div {
      white-space: normal;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .mermaid-node small {
      display: block;
      margin-bottom: 6px;
      color: var(--muted);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .mermaid-edges {
      margin: 0;
      padding-left: 18px;
      color: var(--muted);
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    @media (max-width: 960px) {
      .shell {
        grid-template-columns: 1fr;
      }

      .results {
        max-height: 280px;
      }
    }
  </style>
</head>
<body>
  <div class="shell">
    <aside class="panel sidebar">
      <h1>durable-doc</h1>
      <p class="lede">Static dashboard for generated workflow diagrams.</p>
      <div class="filters">
        <input id="orchestrator-filter" type="search" placeholder="Filter by orchestrator">
        <select id="mode-filter">
          <option value="">All mode availability</option>
          <option value="developer">Has developer mode</option>
          <option value="business">Has business mode</option>
          <option value="both">Has both modes</option>
        </select>
      </div>
      <div id="results" class="results"></div>
    </aside>

    <main class="panel viewer">
      <div class="viewer-header">
        <div>
          <div id="selected-mode" class="badge">No selection</div>
          <h2 id="selected-title">Select a generated diagram</h2>
        </div>
        <div id="mode-switcher" class="mode-switcher"></div>
      </div>
      <p class="hint">The dashboard is fully local. Open this file directly, or run <code>durable-doc generate --open</code> or <code>durable-doc dashboard --open</code> for a localhost preview.</p>
      <div id="details" class="details-grid"></div>
      <ul id="warnings" class="warnings"></ul>
      <div id="diagram"></div>
      <pre id="source"></pre>
    </main>
  </div>

  <script id="dashboard-data" type="application/json">__PAYLOAD__</script>
  <script src="__MERMAID_BUNDLE__"></script>
  <script>
    const diagrams = JSON.parse(document.getElementById('dashboard-data').textContent);
    const resultsEl = document.getElementById('results');
    const titleEl = document.getElementById('selected-title');
    const modeEl = document.getElementById('selected-mode');
    const detailsEl = document.getElementById('details');
    const diagramEl = document.getElementById('diagram');
    const sourceEl = document.getElementById('source');
    const warningsEl = document.getElementById('warnings');
    const orchestratorFilterEl = document.getElementById('orchestrator-filter');
    const modeFilterEl = document.getElementById('mode-filter');
    const modeSwitcherEl = document.getElementById('mode-switcher');

    const groups = groupDiagrams(diagrams);
    let filtered = groups.slice();
    let selectedOrchestrator = filtered[0] ? filtered[0].orchestratorName : '';
    let selectedMode = filtered[0] && filtered[0].modes[0] ? filtered[0].modes[0].mode : '';

    mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

    function groupDiagrams(items) {
      const grouped = new Map();

      items.forEach((diagram) => {
        const existing = grouped.get(diagram.orchestratorName) || {
          orchestratorName: diagram.orchestratorName,
          sourceFile: diagram.sourceFile || '',
          sourceProjectPath: diagram.sourceProjectPath || '',
          modes: []
        };

        const existingMode = existing.modes.find((entry) => entry.mode === diagram.mode);
        if (!existingMode || new Date(existingMode.generatedAt) < new Date(diagram.generatedAt)) {
          existing.modes = existing.modes.filter((entry) => entry.mode !== diagram.mode).concat([diagram]);
        }

        existing.modes.sort((left, right) => left.mode.localeCompare(right.mode));
        grouped.set(diagram.orchestratorName, existing);
      });

      return Array.from(grouped.values()).sort((left, right) => left.orchestratorName.localeCompare(right.orchestratorName));
    }

    function applyFilters() {
      const nameFilter = orchestratorFilterEl.value.trim().toLowerCase();
      const modeFilter = modeFilterEl.value;

      filtered = groups.filter((group) => {
        const matchesName = group.orchestratorName.toLowerCase().includes(nameFilter);
        const hasDeveloper = group.modes.some((entry) => entry.mode === 'developer');
        const hasBusiness = group.modes.some((entry) => entry.mode === 'business');
        const matchesMode =
          modeFilter === '' ||
          (modeFilter === 'developer' && hasDeveloper) ||
          (modeFilter === 'business' && hasBusiness) ||
          (modeFilter === 'both' && hasDeveloper && hasBusiness);

        return matchesName && matchesMode;
      });

      if (!filtered.some((group) => group.orchestratorName === selectedOrchestrator)) {
        selectedOrchestrator = filtered[0] ? filtered[0].orchestratorName : '';
      }

      const selectedGroup = filtered.find((group) => group.orchestratorName === selectedOrchestrator);
      if (!selectedGroup || !selectedGroup.modes.some((entry) => entry.mode === selectedMode)) {
        selectedMode = selectedGroup && selectedGroup.modes[0] ? selectedGroup.modes[0].mode : '';
      }

      renderResults();
      renderSelection();
    }

    function renderResults() {
      resultsEl.innerHTML = '';

      if (filtered.length === 0) {
        const empty = document.createElement('p');
        empty.className = 'empty';
        empty.textContent = 'No diagrams match the current filters.';
        resultsEl.appendChild(empty);
        return;
      }

      filtered.forEach((group) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'result';
        if (group.orchestratorName === selectedOrchestrator) {
          button.classList.add('active');
        }

        const modes = group.modes.map((entry) => entry.mode).join(', ');
        button.innerHTML =
          '<strong></strong>' +
          '<span class="meta"></span>' +
          '<span class="meta"></span>';

        button.querySelector('strong').textContent = group.orchestratorName;
        button.querySelectorAll('.meta')[0].textContent = 'Modes: ' + modes;
        button.querySelectorAll('.meta')[1].textContent = group.sourceProjectPath || group.sourceFile || 'Source unknown';
        button.addEventListener('click', () => {
          selectedOrchestrator = group.orchestratorName;
          if (!group.modes.some((entry) => entry.mode === selectedMode)) {
            selectedMode = group.modes[0].mode;
          }
          renderResults();
          renderSelection();
        });

        resultsEl.appendChild(button);
      });
    }

    function renderSelection() {
      const group = filtered.find((entry) => entry.orchestratorName === selectedOrchestrator);
      const selected = group ? group.modes.find((entry) => entry.mode === selectedMode) : null;

      if (!group || !selected) {
        modeEl.textContent = 'No selection';
        titleEl.textContent = 'Select a generated diagram';
        detailsEl.innerHTML = '';
        warningsEl.innerHTML = '';
        diagramEl.innerHTML = '';
        sourceEl.textContent = '';
        modeSwitcherEl.innerHTML = '';
        return;
      }

      modeEl.textContent = selected.mode + ' mode';
      titleEl.textContent = group.orchestratorName;
      sourceEl.textContent = selected.mermaid;

      modeSwitcherEl.innerHTML = '';
      group.modes.forEach((entry) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = entry.mode + ' mode';
        if (entry.mode === selectedMode) {
          button.classList.add('active');
        }

        button.addEventListener('click', () => {
          selectedMode = entry.mode;
          renderSelection();
          renderResults();
        });

        modeSwitcherEl.appendChild(button);
      });

      detailsEl.innerHTML = '';
      [
        ['Generated', new Date(selected.generatedAt).toLocaleString()],
        ['Source file', selected.sourceFile || 'Unknown'],
        ['Source project', selected.sourceProjectPath || 'Unknown'],
        ['Artifact file', selected.mermaidFileName || 'Unknown']
      ].forEach(([label, value]) => {
        const row = document.createElement('div');
        row.className = 'detail-row';

        const labelEl = document.createElement('span');
        labelEl.className = 'detail-label';
        labelEl.textContent = label + ':';

        const valueEl = document.createElement('span');
        valueEl.className = 'detail-value';
        valueEl.textContent = value;

        row.appendChild(labelEl);
        row.appendChild(valueEl);
        detailsEl.appendChild(row);
      });

      warningsEl.innerHTML = '';
      (selected.warnings || []).forEach((warning) => {
        const item = document.createElement('li');
        item.textContent = warning;
        warningsEl.appendChild(item);
      });

      diagramEl.innerHTML = '';
      const container = document.createElement('pre');
      container.className = 'mermaid';
      container.textContent = selected.mermaid;
      diagramEl.appendChild(container);

      mermaid.run({ nodes: [container] }).catch((error) => {
        diagramEl.innerHTML = '<pre></pre>';
        diagramEl.querySelector('pre').textContent = selected.mermaid + '\n\nMermaid render failed: ' + error.message;
      });
    }

    orchestratorFilterEl.addEventListener('input', applyFilters);
    modeFilterEl.addEventListener('change', applyFilters);

    applyFilters();
  </script>
</body>
</html>
"""
            .Replace("__PAYLOAD__", payload, StringComparison.Ordinal)
            .Replace("__MERMAID_BUNDLE__", mermaidBundleFileName, StringComparison.Ordinal);
    }
}

internal static class MermaidCompatibilityBundle
{
    public static string Render()
    {
        return """
(function (global) {
  function decodeLabel(value) {
    return value
      .replace(/\\"/g, '"')
      .replace(/<br\/>/g, ' ')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
  }

  function inferType(shape) {
    if (shape.indexOf('{{') >= 0) return 'retry';
    if (shape.indexOf('[[') >= 0) return 'external event';
    if (shape.indexOf('[/') >= 0) return 'timer';
    if (shape.indexOf('((') >= 0) return 'parallel';
    if (shape.indexOf('{"') >= 0 || shape.indexOf('{') >= 0) return 'decision';
    if (shape.indexOf('([') >= 0) return 'orchestrator';
    return 'step';
  }

  function parse(source) {
    var nodeRegex = /^\s*([A-Za-z0-9_]+)\s*(.+)$/;
    var edgeRegex = /^\s*([A-Za-z0-9_]+)\s*-->\s*(?:\|([^|]*)\|\s*)?([A-Za-z0-9_]+)\s*$/;
    var labelRegex = /"((?:\\.|[^"])*)"/;
    var nodes = [];
    var edges = [];

    source.split(/\r?\n/).forEach(function (line) {
      if (!line || line.indexOf('flowchart') === 0) {
        return;
      }

      var edgeMatch = line.match(edgeRegex);
      if (edgeMatch) {
        edges.push({ from: edgeMatch[1], label: edgeMatch[2] || '', to: edgeMatch[3] });
        return;
      }

      var nodeMatch = line.match(nodeRegex);
      if (!nodeMatch) {
        return;
      }

      var shape = nodeMatch[2];
      var labelMatch = shape.match(labelRegex);
      nodes.push({
        id: nodeMatch[1],
        label: decodeLabel(labelMatch ? labelMatch[1] : nodeMatch[1]),
        type: inferType(shape)
      });
    });

    return { nodes: nodes, edges: edges };
  }

  function render(container, source) {
    var parsed = parse(source);
    var nodeIndex = {};
    parsed.nodes.forEach(function (node) { nodeIndex[node.id] = node; });

    var nodeHtml = parsed.nodes.map(function (node) {
      return '<div class="mermaid-node"><small>' + node.type + '</small><div>' + node.label + '</div></div>';
    }).join('');

    var edgeHtml = parsed.edges.map(function (edge) {
      var from = nodeIndex[edge.from] ? nodeIndex[edge.from].label : edge.from;
      var to = nodeIndex[edge.to] ? nodeIndex[edge.to].label : edge.to;
      var label = edge.label ? ' [' + edge.label + ']' : '';
      return '<li>' + from + label + ' -> ' + to + '</li>';
    }).join('');

    container.innerHTML =
      '<div class="mermaid-local">' +
        '<div class="mermaid-node-grid">' + nodeHtml + '</div>' +
        '<ol class="mermaid-edges">' + edgeHtml + '</ol>' +
      '</div>';
  }

  global.mermaid = {
    initialize: function () {},
    run: function (options) {
      (options.nodes || []).forEach(function (node) {
        render(node, node.textContent || '');
      });
      return Promise.resolve();
    }
  };
})(window);
""";
    }
}
