namespace DurableDoc.Dashboard;

internal static class MermaidCompatibilityBundle
{
    public static string Render()
    {
        return """
(function (global) {
  var SVG_NS = 'http://www.w3.org/2000/svg';
  var nextMarkerId = 0;
  var resizeFrame = 0;
  var resizeListenerAttached = false;

  function decodeLabel(value) {
    return value
      .replace(/\\"/g, '"')
      .replace(/<br\s*\/?>/gi, ' ')
      .replace(/&quot;/g, '"')
      .replace(/&amp;/g, '&')
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>');
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
    var nodeIndex = {};
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
      var node = {
        id: nodeMatch[1],
        label: decodeLabel(labelMatch ? labelMatch[1] : nodeMatch[1]),
        type: inferType(shape)
      };
      nodeIndex[node.id] = node;
      nodes.push(node);
    });

    edges.forEach(function (edge) {
      if (!nodeIndex[edge.from]) {
        nodeIndex[edge.from] = { id: edge.from, label: edge.from, type: 'step' };
        nodes.push(nodeIndex[edge.from]);
      }

      if (!nodeIndex[edge.to]) {
        nodeIndex[edge.to] = { id: edge.to, label: edge.to, type: 'step' };
        nodes.push(nodeIndex[edge.to]);
      }
    });

    return { nodes: nodes, edges: edges };
  }

  function splitLabelParts(label) {
    return String(label || '')
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .split(/\s+/)
      .filter(Boolean)
      .reduce(function (parts, word) {
        if (word.length <= 18) {
          parts.push(word);
          return parts;
        }

        for (var index = 0; index < word.length; index += 18) {
          parts.push(word.slice(index, index + 18));
        }

        return parts;
      }, []);
  }

  function wrapLabel(label, maxCharacters) {
    var words = splitLabelParts(label);
    if (words.length === 0) {
      return [''];
    }

    var lines = [];
    var current = words[0];
    for (var index = 1; index < words.length; index += 1) {
      var candidate = current + ' ' + words[index];
      if (candidate.length > maxCharacters) {
        lines.push(current);
        current = words[index];
      } else {
        current = candidate;
      }
    }

    lines.push(current);
    return lines;
  }

  function createSvgElement(tagName) {
    return document.createElementNS(SVG_NS, tagName);
  }

  function formatTypeLabel(value) {
    return String(value || 'step').replace(/\b\w/g, function (character) {
      return character.toUpperCase();
    });
  }

  function buildMetrics(container) {
    var availableWidth = Math.max(container.clientWidth || 0, 320);
    var compact = availableWidth < 720;

    return {
      availableWidth: availableWidth,
      nodeWidth: compact ? 188 : 220,
      baseNodeHeight: compact ? 80 : 88,
      lineHeight: compact ? 16 : 18,
      horizontalGap: compact ? 22 : 30,
      rowGap: compact ? 66 : 84,
      padding: compact ? 20 : 32,
      labelWidth: compact ? 14 : 18
    };
  }

  function buildLayout(parsed, metrics) {
    var measuredNodes = parsed.nodes.map(function (node) {
      var lines = wrapLabel(node.label, metrics.labelWidth);
      return {
        id: node.id,
        lines: lines,
        height: metrics.baseNodeHeight + (Math.max(lines.length - 1, 0) * metrics.lineHeight)
      };
    });
    var measuredById = {};
    measuredNodes.forEach(function (item) {
      measuredById[item.id] = item;
    });

    var maxNodesPerRow = Math.max(1, Math.floor((metrics.availableWidth - (metrics.padding * 2) + metrics.horizontalGap) / (metrics.nodeWidth + metrics.horizontalGap)));
    var rows = [];
    for (var index = 0; index < parsed.nodes.length; index += maxNodesPerRow) {
      rows.push(parsed.nodes.slice(index, index + maxNodesPerRow));
    }

    var positions = {};
    var currentY = metrics.padding;
    rows.forEach(function (row, rowIndex) {
      var rowHeight = row.reduce(function (maxHeight, node) {
        var measured = measuredById[node.id];
        return Math.max(maxHeight, measured ? measured.height : metrics.baseNodeHeight);
      }, metrics.baseNodeHeight);

      row.forEach(function (node, columnIndex) {
        var measured = measuredById[node.id] || { lines: [''], height: metrics.baseNodeHeight };
        var x = metrics.padding + (columnIndex * (metrics.nodeWidth + metrics.horizontalGap));
        var y = currentY + ((rowHeight - measured.height) / 2);

        positions[node.id] = {
          x: x,
          y: y,
          width: metrics.nodeWidth,
          height: measured.height,
          lines: measured.lines,
          rowIndex: rowIndex
        };
      });

      currentY += rowHeight + metrics.rowGap;
    });

    return {
      width: metrics.availableWidth,
      height: Math.max((currentY - metrics.rowGap) + metrics.padding, metrics.baseNodeHeight + (metrics.padding * 2)),
      positions: positions,
      rows: rows,
      metrics: metrics
    };
  }

  function createArrowDefinitions(svg, markerId) {
    var defs = createSvgElement('defs');
    var marker = createSvgElement('marker');
    marker.setAttribute('id', markerId);
    marker.setAttribute('markerWidth', '10');
    marker.setAttribute('markerHeight', '7');
    marker.setAttribute('refX', '8');
    marker.setAttribute('refY', '3.5');
    marker.setAttribute('orient', 'auto');

    var polygon = createSvgElement('polygon');
    polygon.setAttribute('points', '0 0, 10 3.5, 0 7');
    polygon.setAttribute('fill', '#657182');
    marker.appendChild(polygon);
    defs.appendChild(marker);
    svg.appendChild(defs);
  }

  function appendConnector(svg, markerId, points) {
    var path = createSvgElement('path');
    path.setAttribute('d', points.map(function (point, index) {
      return (index === 0 ? 'M ' : ' L ') + point.x + ' ' + point.y;
    }).join(''));
    path.setAttribute('fill', 'none');
    path.setAttribute('stroke', '#657182');
    path.setAttribute('stroke-width', '2.25');
    path.setAttribute('stroke-linecap', 'round');
    path.setAttribute('stroke-linejoin', 'round');
    path.setAttribute('marker-end', 'url(#' + markerId + ')');
    svg.appendChild(path);
  }

  function appendSequentialConnector(svg, layout, markerId, fromNodeId, toNodeId) {
    var from = layout.positions[fromNodeId];
    var to = layout.positions[toNodeId];
    if (!from || !to) {
      return;
    }

    appendConnector(svg, markerId, [
      { x: from.x + from.width, y: from.y + (from.height / 2) },
      { x: to.x, y: to.y + (to.height / 2) }
    ]);
  }

  function appendWrappedConnector(svg, layout, markerId, fromNodeId, toNodeId) {
    var from = layout.positions[fromNodeId];
    var to = layout.positions[toNodeId];
    if (!from || !to) {
      return;
    }

    var rightEdge = layout.width - layout.metrics.padding;
    var connectorY = from.y + from.height + (layout.metrics.rowGap / 2);
    var targetX = to.x + (to.width / 2);

    appendConnector(svg, markerId, [
      { x: from.x + from.width, y: from.y + (from.height / 2) },
      { x: rightEdge, y: from.y + (from.height / 2) },
      { x: rightEdge, y: connectorY },
      { x: targetX, y: connectorY },
      { x: targetX, y: to.y }
    ]);
  }

  function appendLane(svg, layout, markerId) {
    layout.rows.forEach(function (row, rowIndex) {
      for (var index = 0; index < row.length - 1; index += 1) {
        appendSequentialConnector(svg, layout, markerId, row[index].id, row[index + 1].id);
      }

      var currentLast = row[row.length - 1];
      var nextRow = layout.rows[rowIndex + 1];
      if (currentLast && nextRow && nextRow[0]) {
        appendWrappedConnector(svg, layout, markerId, currentLast.id, nextRow[0].id);
      }
    });
  }

  function appendNode(svg, layout, node) {
    var position = layout.positions[node.id];
    if (!position) {
      return;
    }

    var group = createSvgElement('g');
    var rect = createSvgElement('rect');
    var type = createSvgElement('text');
    var lines = position.lines || wrapLabel(node.label, layout.metrics.labelWidth);
    var label = createSvgElement('text');

    rect.setAttribute('x', String(position.x));
    rect.setAttribute('y', String(position.y));
    rect.setAttribute('width', String(position.width));
    rect.setAttribute('height', String(position.height));
    rect.setAttribute('rx', node.type === 'timer' ? '28' : '22');
    rect.setAttribute('fill', node.type === 'orchestrator' ? '#e8fbf5' : '#ffffff');
    rect.setAttribute('stroke', node.type === 'decision' ? '#ef8354' : '#8bd4cc');
    rect.setAttribute('stroke-width', '2');
    if (node.type === 'timer' || node.type === 'external event') {
      rect.setAttribute('stroke-dasharray', '8 6');
    }
    group.appendChild(rect);

    type.setAttribute('x', String(position.x + (position.width / 2)));
    type.setAttribute('y', String(position.y + 22));
    type.setAttribute('text-anchor', 'middle');
    type.setAttribute('font-size', '11');
    type.setAttribute('font-family', 'Avenir Next, Segoe UI, sans-serif');
    type.setAttribute('font-weight', '700');
    type.setAttribute('letter-spacing', '0.08em');
    type.setAttribute('fill', '#657182');
    type.textContent = formatTypeLabel(node.type).toUpperCase();
    group.appendChild(type);

    label.setAttribute('x', String(position.x + (position.width / 2)));
    label.setAttribute('y', String(position.y + 46));
    label.setAttribute('text-anchor', 'middle');
    label.setAttribute('font-size', '15');
    label.setAttribute('font-family', 'Avenir Next, Segoe UI, sans-serif');
    label.setAttribute('font-weight', '700');
    label.setAttribute('fill', '#172230');
    lines.forEach(function (line, index) {
      var tspan = createSvgElement('tspan');
      tspan.setAttribute('x', String(position.x + (position.width / 2)));
      tspan.setAttribute('dy', index === 0 ? '0' : String(layout.metrics.lineHeight));
      tspan.textContent = line;
      label.appendChild(tspan);
    });
    group.appendChild(label);

    svg.appendChild(group);
  }

  function ensureResizeListener() {
    if (resizeListenerAttached) {
      return;
    }

    resizeListenerAttached = true;
    global.addEventListener('resize', function () {
      if (resizeFrame) {
        global.cancelAnimationFrame(resizeFrame);
      }

      resizeFrame = global.requestAnimationFrame(function () {
        resizeFrame = 0;
        renderAll();
      });
    });
  }

  function renderAll() {
    document.querySelectorAll('[data-mermaid-source]').forEach(function (node) {
      render(node, node.getAttribute('data-mermaid-source') || '');
    });
  }

  function render(container, source) {
    var parsed = parse(source);
    container.setAttribute('data-mermaid-source', source || '');
    container.innerHTML = '';

    if (parsed.nodes.length === 0) {
      container.textContent = source || '';
      return;
    }

    var metrics = buildMetrics(container);
    var layout = buildLayout(parsed, metrics);
    var markerId = 'arrowhead-' + (nextMarkerId += 1);
    var shell = document.createElement('div');
    shell.className = 'diagram-render-shell';
    var svg = createSvgElement('svg');
    svg.setAttribute('viewBox', '0 0 ' + layout.width + ' ' + layout.height);
    svg.setAttribute('width', String(layout.width));
    svg.setAttribute('height', String(layout.height));
    svg.setAttribute('role', 'img');
    svg.setAttribute('aria-label', 'Workflow diagram');
    svg.setAttribute('class', 'diagram-render-svg');

    createArrowDefinitions(svg, markerId);
    appendLane(svg, layout, markerId);
    parsed.nodes.forEach(function (node) {
      appendNode(svg, layout, node);
    });

    shell.appendChild(svg);
    container.appendChild(shell);
  }

  global.mermaid = {
    initialize: function () {
      ensureResizeListener();
    },
    run: function (options) {
      ensureResizeListener();
      (options.nodes || []).forEach(function (node) {
        render(node, node.textContent || node.getAttribute('data-mermaid-source') || '');
      });
      return Promise.resolve();
    }
  };
})(window);
""";
    }
}
