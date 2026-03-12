(function () {
  const bootstrapEl = document.getElementById('dashboard-bootstrap');
  const resultsEl = document.getElementById('results');
  const resultCountEl = document.getElementById('result-count');
  const titleEl = document.getElementById('selected-title');
  const modeEl = document.getElementById('selected-mode');
  const detailsEl = document.getElementById('details');
  const warningsEl = document.getElementById('warnings');
  const sourceEl = document.getElementById('source');
  const diagramGridEl = document.getElementById('diagram-grid');
  const modeSwitcherEl = document.getElementById('mode-switcher');
  const compareToggleEl = document.getElementById('compare-toggle');
  const refreshIndicatorEl = document.getElementById('refresh-indicator');
  const orchestratorFilterEl = document.getElementById('orchestrator-filter');
  const modeFilterEl = document.getElementById('mode-filter');
  const refreshMs = 3000;

  const state = {
    diagrams: readBootstrap(),
    groups: [],
    filtered: [],
    selectedOrchestrator: '',
    selectedMode: 'developer',
    compareMode: false,
    lastSerialized: '',
    hasLiveRefresh: window.location.protocol !== 'file:'
  };

  window.mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

  hydrate();
  orchestratorFilterEl.addEventListener('input', applyFilters);
  modeFilterEl.addEventListener('change', applyFilters);
  compareToggleEl.addEventListener('click', function () {
    if (compareToggleEl.disabled) {
      return;
    }

    state.compareMode = !state.compareMode;
    renderSelection();
  });

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

  async function pollForUpdates() {
    try {
      const response = await fetch('dashboard-data.json?t=' + Date.now(), { cache: 'no-store' });
      if (!response.ok) {
        return;
      }

      const nextDiagrams = await response.json();
      const serialized = JSON.stringify(nextDiagrams);
      if (serialized === state.lastSerialized) {
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

    if (!preserveSelection || !state.selectedOrchestrator) {
      const defaultGroup = state.groups[0];
      state.selectedOrchestrator = defaultGroup ? defaultGroup.orchestratorName : '';
      state.selectedMode = defaultGroup && hasMode(defaultGroup, 'developer')
        ? 'developer'
        : defaultGroup && defaultGroup.modes[0]
          ? defaultGroup.modes[0].mode
          : '';
    }

    applyFilters();
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

    renderResults();
    renderSelection();
  }

  function renderResults() {
    resultsEl.innerHTML = '';
    resultCountEl.textContent = state.filtered.length + ' total';

    if (state.filtered.length === 0) {
      resultsEl.innerHTML = '<div class="empty">No orchestrators match the current filters.</div>';
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
        renderResults();
        renderSelection();
      });

      resultsEl.appendChild(button);
    });
  }

  function renderSelection() {
    const group = getSelectedGroup();
    const selected = group ? getMode(group, state.selectedMode) : null;

    if (!group || !selected) {
      modeEl.textContent = 'No selection';
      titleEl.textContent = 'Select a generated diagram';
      diagramGridEl.innerHTML = '<div class="empty">Choose an orchestrator to inspect its generated diagrams.</div>';
      detailsEl.innerHTML = '';
      warningsEl.innerHTML = '';
      sourceEl.textContent = '';
      modeSwitcherEl.innerHTML = '';
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

    modeEl.textContent = selected.mode + ' mode';
    titleEl.textContent = group.orchestratorName;
    renderModeSwitcher(group);
    renderInspector(selected);
    renderDiagrams(group, selected);
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
        renderResults();
        renderSelection();
      });

      modeSwitcherEl.appendChild(button);
    });
  }

  function renderInspector(selected) {
    const nodeCount = Array.isArray(selected.nodes) ? selected.nodes.length : 0;
    const edgeCount = Array.isArray(selected.edges) ? selected.edges.length : 0;

    const details = [
      ['Generated', new Date(selected.generatedAt).toLocaleString()],
      ['Source project', selected.sourceProjectPath || 'Unknown'],
      ['Source file', selected.sourceFile || 'Unknown'],
      ['Artifact file', selected.mermaidFileName || 'Unknown'],
      ['Graph size', nodeCount + ' nodes / ' + edgeCount + ' edges']
    ];

    detailsEl.innerHTML = details.map(function (entry) {
      return '<div class="detail"><strong>' + escapeHtml(entry[0]) + '</strong><span>' + escapeHtml(entry[1]) + '</span></div>';
    }).join('');

    const warnings = selected.warnings || [];
    warningsEl.innerHTML = warnings.length === 0
      ? '<li>No warnings for this artifact.</li>'
      : warnings.map(function (warning) { return '<li>' + escapeHtml(warning) + '</li>'; }).join('');

    sourceEl.textContent = selected.mermaid || '';
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
            '<div class="diagram-meta">' + escapeHtml(new Date(artifact.generatedAt).toLocaleString()) + '</div>' +
          '</div>' +
          '<span class="pill">' + escapeHtml(artifact.mode) + '</span>' +
        '</div>' +
        '<div class="diagram-frame"></div>';

      const frame = card.querySelector('.diagram-frame');
      const container = document.createElement('pre');
      container.className = 'mermaid';
      container.textContent = artifact.mermaid || '';
      frame.appendChild(container);

      diagramGridEl.appendChild(card);

      window.mermaid.run({ nodes: [container] }).catch(function (error) {
        frame.innerHTML = '<pre class="source"></pre>';
        frame.querySelector('pre').textContent = (artifact.mermaid || '') + '\n\nMermaid render failed: ' + error.message;
      });
    });
  }

  function getSelectedGroup() {
    return state.filtered.find(function (group) {
      return group.orchestratorName === state.selectedOrchestrator;
    }) || null;
  }

  function getMode(group, mode) {
    return group.modes.find(function (entry) { return entry.mode === mode; }) || null;
  }

  function hasMode(group, mode) {
    return !!getMode(group, mode);
  }

  function escapeHtml(value) {
    return String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
})();
