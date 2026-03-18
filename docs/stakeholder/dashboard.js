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
  const stakeholderOverviewEl = document.getElementById('stakeholder-overview');
  const nodeSearchEl = document.getElementById('node-search');
  const nodeSearchStatusEl = document.getElementById('node-search-status');
  const findStepEl = document.getElementById('find-step');
  const startStepEl = document.getElementById('start-step');
  const clearStepEl = document.getElementById('clear-step');
  const audience = (document.body.getAttribute('data-audience') || 'developer').toLowerCase();
  const isStakeholderAudience = audience === 'stakeholder';
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

  configureAudience();
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

    state.selectedNodeId = isStakeholderAudience
      ? getVisibleBusinessFlowItems(buildBusinessFlow(selected))[0]?.key || ''
      : startNode.id;
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

  function configureAudience() {
    if (!isStakeholderAudience) {
      return;
    }

    document.getElementById('brand-eyebrow').textContent = 'Business Flow Library';
    document.getElementById('brand-title').textContent = 'durable-doc';
    document.getElementById('brand-lede').textContent = 'Review the flow, inspect the current stage, and share a clean business snapshot without reading orchestration code.';
    document.getElementById('orchestrator-filter-label').textContent = 'Filter workflows';
    orchestratorFilterEl.placeholder = 'Search by workflow or capability';
    document.getElementById('controls-hint').innerHTML = 'Use Up and Down to move between workflows, and <code>/</code> to focus stage search.';
    document.getElementById('results-heading').textContent = 'Workflows';
    document.getElementById('stage-hint').textContent = 'Review the business flow, select a stage to inspect what happens before and after it, and share the static output with non-engineering partners.';
    document.getElementById('node-search-label').textContent = 'Jump to stage';
    nodeSearchEl.placeholder = 'Find stage, event, decision, or note';
    findStepEl.textContent = 'Find stage';
    startStepEl.textContent = 'First stage';
    clearStepEl.textContent = 'Clear selection';
    document.getElementById('details-heading').textContent = 'Workflow summary';
    document.getElementById('node-details-heading').textContent = 'Selected stage';
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
      state.selectedMode = getPreferredMode(selectedGroup);
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
        businessName: diagram.businessName || '',
        capability: diagram.capability || '',
        summary: diagram.summary || '',
        audienceNotes: diagram.audienceNotes || '',
        orchestratorNotes: diagram.orchestratorNotes || '',
        outcomes: diagram.outcomes || [],
        sourceFile: diagram.sourceFile || '',
        sourceProjectPath: diagram.sourceProjectPath || '',
        modes: []
      };

      if (!existing.businessName && diagram.businessName) {
        existing.businessName = diagram.businessName;
      }
      if (!existing.capability && diagram.capability) {
        existing.capability = diagram.capability;
      }
      if (!existing.summary && diagram.summary) {
        existing.summary = diagram.summary;
      }
      if (!existing.audienceNotes && diagram.audienceNotes) {
        existing.audienceNotes = diagram.audienceNotes;
      }
      if (!existing.orchestratorNotes && diagram.orchestratorNotes) {
        existing.orchestratorNotes = diagram.orchestratorNotes;
      }
      if ((!existing.outcomes || existing.outcomes.length === 0) && diagram.outcomes && diagram.outcomes.length > 0) {
        existing.outcomes = diagram.outcomes;
      }

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
      state.selectedMode = getPreferredMode(selectedGroup);
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

    if (isStakeholderAudience) {
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
    state.selectedNodeId = startNode ? startNode.id : '';
  }

  function renderResults() {
    resultsEl.innerHTML = '';
    resultCountEl.textContent = state.filtered.length + ' total';

    if (state.filtered.length === 0) {
      resultsEl.innerHTML = '<div class="empty">No orchestrators match the current filters. Clear the filters to restore the list.</div>';
      return;
    }

    if (isStakeholderAudience) {
      const capabilityGroups = new Map();
      state.filtered.forEach(function (group) {
        const capability = group.capability || 'Uncategorized';
        if (!capabilityGroups.has(capability)) {
          capabilityGroups.set(capability, []);
        }

        capabilityGroups.get(capability).push(group);
      });

      Array.from(capabilityGroups.keys()).sort(function (left, right) {
        return left.localeCompare(right);
      }).forEach(function (capability) {
        const section = document.createElement('section');
        section.className = 'result-group';

        const heading = document.createElement('h3');
        heading.className = 'result-group-title';
        heading.textContent = capability;
        section.appendChild(heading);

        capabilityGroups.get(capability).sort(function (left, right) {
          return getGroupTitle(left).localeCompare(getGroupTitle(right));
        }).forEach(function (group) {
          section.appendChild(renderResultButton(group));
        });

        resultsEl.appendChild(section);
      });

      return;
    }

    state.filtered.forEach(function (group) {
      resultsEl.appendChild(renderResultButton(group));
    });
  }

  function renderResultButton(group) {
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
          '<strong>' + escapeHtml(getGroupTitle(group)) + '</strong>' +
          '<div class="mode-pill-list">' + modes + '</div>' +
        '</div>' +
        '<div class="meta">' + escapeHtml(getGroupMeta(group)) + '</div>';

      button.addEventListener('click', function () {
        state.selectedOrchestrator = group.orchestratorName;
        if (!hasMode(group, state.selectedMode)) {
          state.selectedMode = getPreferredMode(group);
        }
        ensureSelectedNode();
        renderResults();
        renderSelection();
        writeUrlState('push');
      });

      return button;
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
      stakeholderOverviewEl.innerHTML = '';
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

    modeEl.textContent = isStakeholderAudience
      ? (selected.mode === 'business' ? 'Business flow' : 'Workflow view')
      : selected.mode + ' view';
    if (!isStakeholderAudience) {
      state.legendFilterKind = '';
    }
    titleEl.textContent = getGroupTitle(group);
    renderModeSwitcher(group);
    renderSummary(selected);
    renderLegend(selected);
    renderInspector(selected);
    renderDiagrams(group, selected);
    sourceEl.textContent = selected.mermaid || '';
    updateNodeSearchStatus(selected);
  }

  function renderModeSwitcher(group) {
    if (isStakeholderAudience) {
      modeSwitcherEl.innerHTML = '';
      return;
    }

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
    if (isStakeholderAudience) {
      const summary = [
        ['Capability', selected.capability || 'Uncategorized'],
        ['Stages', String(graph.nodes.length)],
        ['Outcomes', String((selected.outcomes || []).length)],
        ['Flow shape', describeFlowShape(graph)],
        ['Updated', new Date(selected.generatedAt).toLocaleDateString()]
      ];

      summaryCardsEl.innerHTML = summary.map(function (entry) {
        return '<div class="summary-card"><strong>' + escapeHtml(entry[0]) + '</strong><span>' + escapeHtml(entry[1]) + '</span></div>';
      }).join('');
      return;
    }

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

    if (!isStakeholderAudience) {
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

  function renderInspector(selected) {
    const graph = buildGraph(selected);
    const selectedContext = isStakeholderAudience ? getSelectedNodeContext(selected) : null;
    const selectedNode = isStakeholderAudience
      ? (selectedContext ? selectedContext.node : null)
      : (state.selectedNodeId ? graph.byId[state.selectedNodeId] : null);

    if (isStakeholderAudience) {
      stakeholderOverviewEl.innerHTML = renderStakeholderOverview(selected);

      const details = buildBusinessDetails(selected);

      detailsEl.innerHTML = details.map(function (entry) {
        return '<div class="detail"><strong>' + escapeHtml(entry[0]) + '</strong><div class="detail-value">' + escapeHtml(entry[1]) + '</div></div>';
      }).join('');

      warningsEl.innerHTML = '';

      if (!selectedNode) {
        nodeDetailsEl.className = 'node-details empty';
        nodeDetailsEl.textContent = 'Select a stage to inspect what leads into it and what follows.';
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
        '<div class="node-panel"><strong>Stage</strong><div class="detail-value">' + escapeHtml(selectedNode.displayLabel || selectedNode.name || selectedNode.id) + '</div></div>' +
        '<div class="node-panel"><strong>Type</strong><div class="detail-value">' + escapeHtml(formatNodeType(selectedNode.nodeType)) + '</div></div>' +
        '<div class="node-panel"><strong>XML summary</strong><div class="detail-value">' + escapeHtml(selectedNode.documentationSummary || 'No XML summary found.') + '</div></div>' +
        '<div class="node-panel"><strong>What leads here</strong><div class="node-list">' + renderNodeList(incoming, 'Start of workflow') + '</div></div>' +
        '<div class="node-panel"><strong>What follows</strong><div class="node-list">' + renderNodeList(outgoing, 'End of workflow') + '</div></div>';
      return;
    }

    stakeholderOverviewEl.innerHTML = '';

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
    diagramGridEl.classList.toggle('compare', state.compareMode && !isStakeholderAudience);

    const visible = state.compareMode && !isStakeholderAudience
      ? ['developer', 'business'].map(function (mode) { return getMode(group, mode); }).filter(Boolean)
      : [selected];

    diagramGridEl.innerHTML = '';
    visible.forEach(function (artifact) {
      const card = document.createElement('article');
      card.className = 'diagram-card';
      const cardTitle = isStakeholderAudience
        ? (artifact.mode === 'business' ? 'Business flow' : 'Workflow view')
        : (artifact.mode === 'developer' ? 'Developer view' : 'Business view');
      card.innerHTML =
        '<div class="diagram-card-header">' +
          '<div>' +
            '<h3 class="diagram-card-title">' + escapeHtml(cardTitle) + '</h3>' +
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
    if (isStakeholderAudience) {
      renderBusinessFlowStage(container, artifact);
      return;
    }

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
              '<div class="step-meta">' + escapeHtml(describeSecondaryStepMeta(node)) + '</div>' +
            '</div>' +
          '</div>' +
          '<span class="step-type" data-kind="' + escapeHtml(String(node.nodeType || '').toLowerCase()) + '">' + escapeHtml(formatNodeType(node.nodeType)) + '</span>' +
        '</div>' +
        '<div class="step-subheading">' +
          '<span class="step-note">' + escapeHtml(describeConnectivity(incoming.length, outgoing.length, index === 0, index === graph.nodes.length - 1)) + '</span>' +
          '<span class="step-meta">' + escapeHtml(describeTertiaryStepMeta(node)) + '</span>' +
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

      if (flowItem.key === state.selectedNodeId) {
        button.classList.add('active');
      }
      if (query && matchesNode(node, query)) {
        button.classList.add('match');
      }

      button.innerHTML =
        '<div class="step-heading">' +
          '<div class="step-subheading">' +
            '<span class="step-index">' + escapeHtml(String(index + 1)) + '</span>' +
            '<div>' +
              '<div class="step-title">' + escapeHtml(node.displayLabel || node.name || node.id) + '</div>' +
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

    appendArtifact(artifact, 0, null, [artifact.orchestratorName], new Set());

    const byKey = {};
    items.forEach(function (item) {
      byKey[item.key] = item;
    });

    return {
      items: items,
      byKey: byKey
    };

    function appendArtifact(currentArtifact, baseDepth, parentKey, pathTokens, activeOrchestrators) {
      const orchestratorKey = String(currentArtifact.orchestratorName || '').toLowerCase();
      if (activeOrchestrators.has(orchestratorKey)) {
        return;
      }

      activeOrchestrators.add(orchestratorKey);
      const graph = buildGraph(currentArtifact);
      const startNode = getStartNode(currentArtifact);

      graph.nodes.forEach(function (node) {
        const isStart = !!startNode && node.id === startNode.id;
        if (!parentKey && !isStart) {
          return;
        }
        if (parentKey && isStart) {
          return;
        }

        const nodeKey = createNestedNodeKey(pathTokens, node.id);
        items.push({
          key: nodeKey,
          parentKey: parentKey || null,
          depth: parentKey ? baseDepth : 0,
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
              (parentKey ? baseDepth : 1) + 1,
              nodeKey,
              pathTokens.concat([node.id + ':' + childArtifact.orchestratorName]),
              new Set(activeOrchestrators));
          }
        }
      });

      activeOrchestrators.delete(orchestratorKey);

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
                pathTokens.concat([node.id + ':' + childArtifact.orchestratorName]),
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
      return group.orchestratorName === childName;
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

  function getSelectedNodeContext(selected) {
    const flow = buildBusinessFlow(selected);
    const item = flow.byKey[state.selectedNodeId] || null;
    return item ? { node: item.node, graph: item.graph, item: item } : null;
  }

  function buildBusinessDetails(selected) {
    const flow = buildBusinessFlow(selected);
    const visibleItems = getVisibleBusinessFlowItems(flow);
    const selectedIndex = visibleItems.findIndex(function (item) { return item.key === state.selectedNodeId; });
    const selectedItem = selectedIndex >= 0 ? visibleItems[selectedIndex] : null;
    const previousItem = selectedIndex > 0 ? visibleItems[selectedIndex - 1] : null;
    const nextItem = selectedIndex >= 0 && selectedIndex < visibleItems.length - 1 ? visibleItems[selectedIndex + 1] : null;

    return [
      ['Highlighted stage', selectedItem ? (selectedItem.node.displayLabel || selectedItem.node.name || selectedItem.node.id) : 'None'],
      ['Previous stage', previousItem ? (previousItem.node.displayLabel || previousItem.node.name || previousItem.node.id) : 'None'],
      ['Next stage', nextItem ? (nextItem.node.displayLabel || nextItem.node.name || nextItem.node.id) : 'None'],
      ['XML summary', selectedItem && selectedItem.node.documentationSummary ? selectedItem.node.documentationSummary : 'No XML summary found.']
    ];
  }

  function getBusinessStepMeta(flowItem) {
    if (flowItem.depth === 0) {
      return 'Workflow root';
    }

    return flowItem.artifact.orchestratorName === getSelectedArtifact().orchestratorName
      ? 'Main orchestration'
      : 'Nested under ' + flowItem.artifact.orchestratorName;
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
    } else if (!isStakeholderAudience && event.key === 'ArrowRight') {
      event.preventDefault();
      moveModeSelection(1);
    } else if (!isStakeholderAudience && event.key === 'ArrowLeft') {
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

    if (isStakeholderAudience) {
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

    state.selectedNodeId = match.id;
    renderSelection();
    writeUrlState(pushHistory ? 'push' : 'replace');
  }

  function updateNodeSearchStatus(selected) {
    const query = nodeSearchEl.value.trim().toLowerCase();
    if (!query) {
      nodeSearchStatusEl.textContent = isStakeholderAudience
        ? 'Search within the current flow to jump directly to a stage.'
        : 'Search within the current diagram to jump directly to a step.';
      return;
    }

    const matches = isStakeholderAudience
      ? getVisibleBusinessFlowItems(buildBusinessFlow(selected)).filter(function (item) {
          return matchesNode(item.node, query);
        })
      : buildGraph(selected).nodes.filter(function (node) {
          return matchesNode(node, query);
        });

    nodeSearchStatusEl.textContent = matches.length === 0
      ? (isStakeholderAudience ? 'No matching stages in this view.' : 'No matching steps in this view.')
      : matches.length === 1
        ? (isStakeholderAudience ? '1 matching stage. Press Enter to jump.' : '1 matching step. Press Enter to jump.')
        : matches.length + (isStakeholderAudience ? ' matching stages. Press Enter to jump to the first.' : ' matching steps. Press Enter to jump to the first.');
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

  function renderStakeholderOverview(selected) {
    const cards = [];

    if (selected.summary) {
      cards.push(
        '<div class="stakeholder-card"><strong>Summary</strong><p>' + escapeHtml(selected.summary) + '</p></div>'
      );
    }

    const outcomes = selected.outcomes || [];
    if (outcomes.length > 0) {
      cards.push(
        '<div class="stakeholder-card"><strong>Outcomes</strong><ul class="stakeholder-list">' +
          outcomes.map(function (outcome) { return '<li>' + escapeHtml(outcome) + '</li>'; }).join('') +
        '</ul></div>'
      );
    }

    const notes = selected.audienceNotes || selected.orchestratorNotes;
    if (notes) {
      cards.push(
        '<div class="stakeholder-card"><strong>Notes</strong><p>' + escapeHtml(notes) + '</p></div>'
      );
    }

    if (cards.length === 0) {
      cards.push(
        '<div class="stakeholder-card"><strong>Summary</strong><p>No stakeholder summary metadata is configured for this workflow yet.</p></div>'
      );
    }

    return cards.join('');
  }

  function describeSecondaryStepMeta(node) {
    if (isStakeholderAudience) {
      return node.notes || '';
    }

    return node.name && node.name !== node.displayLabel ? node.name : '';
  }

  function describeTertiaryStepMeta(node) {
    if (isStakeholderAudience) {
      return node.businessGroup || '';
    }

    return node.lineNumber ? 'Line ' + node.lineNumber : 'Line unknown';
  }

  function getGroupTitle(group) {
    return isStakeholderAudience
      ? (group.businessName || group.orchestratorName)
      : group.orchestratorName;
  }

  function getGroupMeta(group) {
    if (isStakeholderAudience) {
      return group.summary || group.orchestratorNotes || group.orchestratorName;
    }

    return group.sourceProjectPath || group.sourceFile || 'Source unknown';
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

  function getPreferredMode(group) {
    if (isStakeholderAudience && hasMode(group, 'business')) {
      return 'business';
    }

    return hasMode(group, 'developer')
      ? 'developer'
      : group.modes[0]
        ? group.modes[0].mode
        : '';
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
