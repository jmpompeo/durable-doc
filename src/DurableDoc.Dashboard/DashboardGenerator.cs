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
}

public sealed record DashboardBuildResult(string DashboardPath, int DiagramCount);

public static class DashboardGenerator
{
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
        var dashboardPath = Path.Combine(outputDirectory, "index.html");
        File.WriteAllText(dashboardPath, DashboardHtmlTemplate.Render(diagrams));
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
    public static string Render(IReadOnlyList<GeneratedDiagramArtifact> diagrams)
    {
        var payload = JsonSerializer.Serialize(diagrams, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }).Replace("</", "<\\/", StringComparison.Ordinal);

        return $$"""
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

    h1 {
      margin: 0 0 8px;
      font-size: clamp(1.8rem, 3vw, 2.5rem);
      line-height: 1;
    }

    .lede {
      margin: 0 0 18px;
      color: var(--muted);
      font-size: 0.98rem;
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
    }

    .meta,
    .empty {
      color: var(--muted);
      font-size: 0.9rem;
    }

    .viewer {
      padding: 24px;
      display: grid;
      gap: 16px;
      align-content: start;
    }

    .viewer-header {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      justify-content: space-between;
      align-items: baseline;
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
      border: 1px solid var(--line);
      border-radius: 20px;
      background: #f8f6f1;
      padding: 18px;
      overflow: auto;
      font-family: "SFMono-Regular", Consolas, monospace;
      font-size: 0.9rem;
    }

    .hint {
      margin: 0;
      color: var(--muted);
      font-size: 0.92rem;
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
          <option value="">All modes</option>
          <option value="developer">Developer</option>
          <option value="business">Business</option>
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
        <p id="selected-timestamp" class="meta"></p>
      </div>
      <p class="hint">The dashboard is fully static. Open this file in a browser after running <code>durable-doc generate</code> or <code>durable-doc dashboard</code>.</p>
      <div id="diagram"></div>
      <pre id="source"></pre>
    </main>
  </div>

  <script id="dashboard-data" type="application/json">{{payload}}</script>
  <script src="https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js"></script>
  <script>
    const diagrams = JSON.parse(document.getElementById('dashboard-data').textContent);
    const resultsEl = document.getElementById('results');
    const titleEl = document.getElementById('selected-title');
    const modeEl = document.getElementById('selected-mode');
    const timestampEl = document.getElementById('selected-timestamp');
    const diagramEl = document.getElementById('diagram');
    const sourceEl = document.getElementById('source');
    const orchestratorFilterEl = document.getElementById('orchestrator-filter');
    const modeFilterEl = document.getElementById('mode-filter');

    let filtered = diagrams.slice();
    let selectedKey = filtered[0] ? filtered[0].diagramId + ':' + filtered[0].mode : '';

    mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

    function applyFilters() {
      const nameFilter = orchestratorFilterEl.value.trim().toLowerCase();
      const modeFilter = modeFilterEl.value;

      filtered = diagrams.filter((diagram) => {
        const matchesName = diagram.orchestratorName.toLowerCase().includes(nameFilter);
        const matchesMode = modeFilter === '' || diagram.mode === modeFilter;
        return matchesName && matchesMode;
      });

      if (!filtered.some((diagram) => diagram.diagramId + ':' + diagram.mode === selectedKey)) {
        selectedKey = filtered[0] ? filtered[0].diagramId + ':' + filtered[0].mode : '';
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

      filtered.forEach((diagram) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'result';
        if (diagram.diagramId + ':' + diagram.mode === selectedKey) {
          button.classList.add('active');
        }

        button.innerHTML =
          '<strong></strong>' +
          '<div class="meta"></div>' +
          '<div class="meta"></div>';

        button.querySelector('strong').textContent = diagram.orchestratorName;
        button.querySelectorAll('.meta')[0].textContent = diagram.mode + ' mode';
        button.querySelectorAll('.meta')[1].textContent = new Date(diagram.generatedAt).toLocaleString();
        button.addEventListener('click', () => {
          selectedKey = diagram.diagramId + ':' + diagram.mode;
          renderResults();
          renderSelection();
        });

        resultsEl.appendChild(button);
      });
    }

    function renderSelection() {
      const selected = filtered.find((diagram) => diagram.diagramId + ':' + diagram.mode === selectedKey);

      if (!selected) {
        modeEl.textContent = 'No selection';
        titleEl.textContent = 'Select a generated diagram';
        timestampEl.textContent = '';
        diagramEl.innerHTML = '';
        sourceEl.textContent = '';
        return;
      }

      modeEl.textContent = selected.mode + ' mode';
      titleEl.textContent = selected.orchestratorName;
      timestampEl.textContent = 'Generated ' + new Date(selected.generatedAt).toLocaleString();
      sourceEl.textContent = selected.mermaid;

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
""";
    }
}
