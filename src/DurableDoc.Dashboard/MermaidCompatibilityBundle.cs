namespace DurableDoc.Dashboard;

internal static class MermaidCompatibilityBundle
{
    public static string Render()
    {
        return """
(function (global) {
  var SVG_NS = 'http://www.w3.org/2000/svg';

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

  function buildLayout(parsed) {
    var nodeWidth = 220;
    var baseNodeHeight = 84;
    var lineHeight = 18;
    var horizontalGap = 64;
    var verticalGap = 120;
    var padding = 48;
    var nodesById = {};
    var outgoing = {};
    var incoming = {};
    var indegree = {};
    var rankById = {};
    var nodeMetrics = {};
    var queue = [];

    parsed.nodes.forEach(function (node) {
      var lines = wrapLabel(node.label, 18);
      nodesById[node.id] = node;
      outgoing[node.id] = [];
      incoming[node.id] = [];
      indegree[node.id] = 0;
      rankById[node.id] = 0;
      nodeMetrics[node.id] = {
        lines: lines,
        height: baseNodeHeight + (Math.max(lines.length - 1, 0) * lineHeight)
      };
    });

    parsed.edges.forEach(function (edge) {
      outgoing[edge.from].push(edge);
      incoming[edge.to].push(edge);
      indegree[edge.to] += 1;
    });

    parsed.nodes.forEach(function (node) {
      if (indegree[node.id] === 0) {
        queue.push(node.id);
      }
    });

    while (queue.length > 0) {
      var currentId = queue.shift();
      outgoing[currentId].forEach(function (edge) {
        rankById[edge.to] = Math.max(rankById[edge.to], rankById[currentId] + 1);
        indegree[edge.to] -= 1;
        if (indegree[edge.to] === 0) {
          queue.push(edge.to);
        }
      });
    }

    var ranks = [];
    parsed.nodes.forEach(function (node) {
      var rank = rankById[node.id];
      if (!ranks[rank]) {
        ranks[rank] = [];
      }

      ranks[rank].push(node);
    });

    var nonEmptyRanks = ranks.filter(Boolean);
    var maxColumns = nonEmptyRanks.reduce(function (max, rankNodes) {
      return Math.max(max, rankNodes.length);
    }, 1);
    var width = (maxColumns * nodeWidth) + ((maxColumns - 1) * horizontalGap) + (padding * 2);
    var positions = {};

    var currentY = padding;
    nonEmptyRanks.forEach(function (rankNodes) {
      var rowWidth = (rankNodes.length * nodeWidth) + ((rankNodes.length - 1) * horizontalGap);
      var startX = padding + ((width - (padding * 2) - rowWidth) / 2);
      var rowHeight = rankNodes.reduce(function (max, node) {
        return Math.max(max, nodeMetrics[node.id].height);
      }, baseNodeHeight);

      rankNodes.forEach(function (node, columnIndex) {
        positions[node.id] = {
          x: startX + (columnIndex * (nodeWidth + horizontalGap)),
          y: currentY,
          width: nodeWidth,
          height: nodeMetrics[node.id].height,
          lines: nodeMetrics[node.id].lines
        };
      });

      currentY += rowHeight + (verticalGap - baseNodeHeight);
    });

    var height = currentY - (verticalGap - baseNodeHeight) + padding;
    return {
      width: Math.max(width, 320),
      height: Math.max(height, 220),
      positions: positions
    };
  }

  function createArrowDefinitions(svg) {
    var defs = createSvgElement('defs');
    var marker = createSvgElement('marker');
    marker.setAttribute('id', 'arrowhead');
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

  function appendEdge(svg, layout, edge) {
    var from = layout.positions[edge.from];
    var to = layout.positions[edge.to];
    if (!from || !to) {
      return;
    }

    var startX = from.x + (from.width / 2);
    var startY = from.y + from.height;
    var endX = to.x + (to.width / 2);
    var endY = to.y;
    var midY = startY + ((endY - startY) / 2);

    var path = createSvgElement('path');
    path.setAttribute('d', 'M ' + startX + ' ' + startY + ' L ' + startX + ' ' + midY + ' L ' + endX + ' ' + midY + ' L ' + endX + ' ' + endY);
    path.setAttribute('fill', 'none');
    path.setAttribute('stroke', '#657182');
    path.setAttribute('stroke-width', '2.5');
    path.setAttribute('stroke-linecap', 'round');
    path.setAttribute('stroke-linejoin', 'round');
    path.setAttribute('marker-end', 'url(#arrowhead)');
    svg.appendChild(path);

    if (!edge.label) {
      return;
    }

    var labelBackground = createSvgElement('rect');
    var label = createSvgElement('text');
    var labelText = decodeLabel(edge.label);
    var labelX = (startX + endX) / 2;
    var labelY = midY - 10;
    var labelWidth = Math.max((labelText.length * 7) + 18, 42);

    labelBackground.setAttribute('x', String(labelX - (labelWidth / 2)));
    labelBackground.setAttribute('y', String(labelY - 14));
    labelBackground.setAttribute('width', String(labelWidth));
    labelBackground.setAttribute('height', '24');
    labelBackground.setAttribute('rx', '12');
    labelBackground.setAttribute('fill', '#fff8f1');
    labelBackground.setAttribute('stroke', 'rgba(23, 34, 48, 0.12)');
    svg.appendChild(labelBackground);

    label.setAttribute('x', String(labelX));
    label.setAttribute('y', String(labelY + 2));
    label.setAttribute('text-anchor', 'middle');
    label.setAttribute('font-size', '12');
    label.setAttribute('font-family', 'Avenir Next, Segoe UI, sans-serif');
    label.setAttribute('fill', '#657182');
    label.textContent = labelText;
    svg.appendChild(label);
  }

  function appendNode(svg, layout, node) {
    var position = layout.positions[node.id];
    if (!position) {
      return;
    }

    var group = createSvgElement('g');
    var rect = createSvgElement('rect');
    var type = createSvgElement('text');
    var lines = position.lines || wrapLabel(node.label, 18);
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
    type.textContent = String(node.type || 'step').toUpperCase();
    group.appendChild(type);

    label.setAttribute('x', String(position.x + (position.width / 2)));
    label.setAttribute('y', String(position.y + 42));
    label.setAttribute('text-anchor', 'middle');
    label.setAttribute('font-size', '15');
    label.setAttribute('font-family', 'Avenir Next, Segoe UI, sans-serif');
    label.setAttribute('font-weight', '700');
    label.setAttribute('fill', '#172230');
    lines.forEach(function (line, index) {
      var tspan = createSvgElement('tspan');
      tspan.setAttribute('x', String(position.x + (position.width / 2)));
      tspan.setAttribute('dy', index === 0 ? '0' : '18');
      tspan.textContent = line;
      label.appendChild(tspan);
    });
    group.appendChild(label);

    svg.appendChild(group);
  }

  function render(container, source) {
    var parsed = parse(source);
    container.innerHTML = '';

    if (parsed.nodes.length === 0) {
      container.textContent = source || '';
      return;
    }

    var layout = buildLayout(parsed);
    var svg = createSvgElement('svg');
    svg.setAttribute('viewBox', '0 0 ' + layout.width + ' ' + layout.height);
    svg.setAttribute('width', String(layout.width));
    svg.setAttribute('height', String(layout.height));
    svg.setAttribute('role', 'img');
    svg.setAttribute('aria-label', 'Workflow diagram');

    createArrowDefinitions(svg);
    parsed.edges.forEach(function (edge) {
      appendEdge(svg, layout, edge);
    });
    parsed.nodes.forEach(function (node) {
      appendNode(svg, layout, node);
    });

    container.appendChild(svg);
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
