using System.Text;
using DurableDoc.Domain;

namespace DurableDoc.Rendering.Mermaid;

public static class MermaidRenderer
{
    public static string Render(WorkflowDiagram diagram, MermaidRenderMode mode = MermaidRenderMode.Developer)
    {
        ArgumentNullException.ThrowIfNull(diagram);

        var effectiveDiagram = mode == MermaidRenderMode.Business
            ? BusinessWorkflowTransformer.Transform(diagram)
            : diagram;

        return MermaidFlowchartFormatter.Format(effectiveDiagram);
    }

    private static class MermaidFlowchartFormatter
    {
        public static string Format(WorkflowDiagram diagram)
        {
            var builder = new StringBuilder();
            var renderIds = diagram.Nodes
                .Select((node, index) => new { node.Id, RenderId = $"n{index}" })
                .ToDictionary(x => x.Id, x => x.RenderId, StringComparer.Ordinal);

            builder.AppendLine("flowchart TD");

            foreach (var node in diagram.Nodes)
            {
                builder.Append("    ")
                    .Append(renderIds[node.Id])
                    .Append(GetNodeShape(node))
                    .AppendLine();
            }

            foreach (var edge in diagram.Edges)
            {
                if (!renderIds.TryGetValue(edge.FromNodeId, out var fromId) ||
                    !renderIds.TryGetValue(edge.ToNodeId, out var toId))
                {
                    continue;
                }

                builder.Append("    ")
                    .Append(fromId)
                    .Append(" -->");

                if (!string.IsNullOrWhiteSpace(edge.ConditionLabel))
                {
                    builder.Append('|')
                        .Append(FormatEdgeLabel(edge.ConditionLabel))
                        .Append('|');
                }

                builder.Append(' ')
                    .Append(toId)
                    .AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetNodeShape(WorkflowNode node)
        {
            var label = FormatNodeLabel(string.IsNullOrWhiteSpace(node.DisplayLabel) ? node.Name : node.DisplayLabel);

            return node.NodeType switch
            {
                WorkflowNodeType.OrchestratorStart => $"([\"{label}\"])",
                WorkflowNodeType.RetryActivity => $"{{{{\"{label}\"}}}}",
                WorkflowNodeType.ExternalEvent => $"[[\"{label}\"]]",
                WorkflowNodeType.Timer => $"[/\"{label}\"/]",
                WorkflowNodeType.ParallelGroup => $"((\"{label}\"))",
                WorkflowNodeType.FanOut => $"((\"{label}\"))",
                WorkflowNodeType.FanIn => $"((\"{label}\"))",
                WorkflowNodeType.Decision => $"{{\"{label}\"}}",
                _ => $"[\"{label}\"]",
            };
        }

        private static string FormatNodeLabel(string label)
        {
            return label
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r\n", "<br/>", StringComparison.Ordinal)
                .Replace("\n", "<br/>", StringComparison.Ordinal)
                .Replace("\r", "<br/>", StringComparison.Ordinal);
        }

        private static string FormatEdgeLabel(string label)
        {
            return label
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("|", "/", StringComparison.Ordinal)
                .Trim();
        }
    }

    private static class BusinessWorkflowTransformer
    {
        public static WorkflowDiagram Transform(WorkflowDiagram diagram)
        {
            var visibleNodes = new List<WorkflowNode>();
            var sourceToBusinessNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
            var businessNodesById = new Dictionary<string, WorkflowNode>(StringComparer.Ordinal);

            foreach (var node in diagram.Nodes)
            {
                if (!ShouldIncludeInBusinessView(node))
                {
                    continue;
                }

                var businessNodeId = GetBusinessNodeId(node);
                sourceToBusinessNodeId[node.Id] = businessNodeId;

                if (businessNodesById.ContainsKey(businessNodeId))
                {
                    continue;
                }

                var businessNode = new WorkflowNode
                {
                    Id = businessNodeId,
                    DisplayLabel = GetBusinessNodeName(node),
                    NodeType = GetBusinessNodeType(node),
                    Name = GetBusinessNodeName(node),
                    SourceFile = node.SourceFile,
                    LineNumber = node.LineNumber,
                };

                businessNodesById.Add(businessNodeId, businessNode);
                visibleNodes.Add(businessNode);
            }

            var outgoingEdges = diagram.Edges
                .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

            var businessEdges = new List<WorkflowEdge>();
            var seenEdges = new HashSet<string>(StringComparer.Ordinal);

            foreach (var sourceNode in diagram.Nodes.Where(ShouldIncludeInBusinessView))
            {
                var sourceBusinessNodeId = sourceToBusinessNodeId[sourceNode.Id];
                var frontier = new Queue<(string NodeId, string? Label)>();
                var visited = new HashSet<string>(StringComparer.Ordinal);

                if (!outgoingEdges.TryGetValue(sourceNode.Id, out var directEdges))
                {
                    continue;
                }

                foreach (var edge in directEdges)
                {
                    frontier.Enqueue((edge.ToNodeId, edge.ConditionLabel));
                }

                while (frontier.Count > 0)
                {
                    var (nextNodeId, label) = frontier.Dequeue();
                    var visitKey = $"{nextNodeId}|{label}";

                    if (!visited.Add(visitKey))
                    {
                        continue;
                    }

                    if (sourceToBusinessNodeId.TryGetValue(nextNodeId, out var targetBusinessNodeId))
                    {
                        if (targetBusinessNodeId != sourceBusinessNodeId)
                        {
                            var edgeKey = $"{sourceBusinessNodeId}|{targetBusinessNodeId}|{label}";

                            if (seenEdges.Add(edgeKey))
                            {
                                businessEdges.Add(new WorkflowEdge
                                {
                                    FromNodeId = sourceBusinessNodeId,
                                    ToNodeId = targetBusinessNodeId,
                                    ConditionLabel = label,
                                });
                            }

                            continue;
                        }
                    }

                    if (!outgoingEdges.TryGetValue(nextNodeId, out var nestedEdges))
                    {
                        continue;
                    }

                    foreach (var edge in nestedEdges)
                    {
                        frontier.Enqueue((edge.ToNodeId, CoalesceLabel(label, edge.ConditionLabel)));
                    }
                }
            }

            return new WorkflowDiagram
            {
                Id = diagram.Id,
                OrchestratorName = diagram.OrchestratorName,
                SourceFile = diagram.SourceFile,
                CreatedTimestamp = diagram.CreatedTimestamp,
                Nodes = visibleNodes,
                Edges = businessEdges,
            };
        }

        private static bool ShouldIncludeInBusinessView(WorkflowNode node)
        {
            return !node.HideInBusiness &&
                node.NodeType != WorkflowNodeType.RetryActivity &&
                node.NodeType != WorkflowNodeType.FanOut &&
                node.NodeType != WorkflowNodeType.FanIn &&
                node.NodeType != WorkflowNodeType.Wrapper;
        }

        private static string GetBusinessNodeId(WorkflowNode node)
        {
            return string.IsNullOrWhiteSpace(node.BusinessGroup)
                ? node.Id
                : $"group:{node.BusinessGroup.Trim()}";
        }

        private static string GetBusinessNodeName(WorkflowNode node)
        {
            if (!string.IsNullOrWhiteSpace(node.BusinessGroup))
            {
                return node.BusinessGroup.Trim();
            }

            return string.IsNullOrWhiteSpace(node.BusinessName)
                ? node.Name
                : node.BusinessName.Trim();
        }

        private static WorkflowNodeType GetBusinessNodeType(WorkflowNode node)
        {
            return string.IsNullOrWhiteSpace(node.BusinessGroup)
                ? node.NodeType
                : WorkflowNodeType.Activity;
        }

        private static string? CoalesceLabel(string? existingLabel, string? nextLabel)
        {
            return string.IsNullOrWhiteSpace(existingLabel) ? nextLabel : existingLabel;
        }
    }
}
