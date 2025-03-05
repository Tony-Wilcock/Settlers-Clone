using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Path
{
    public int Id { get; private set; }
    public List<int> Nodes { get; private set; }
    public int StartFlag => Nodes[0];
    public int EndFlag => Nodes[Nodes.Count - 1];

    private HexGridManager manager;

    public Path(int id, List<int> nodes, HexGridManager manager)
    {
        Id = id;
        Nodes = new List<int>(nodes);
        this.manager = manager;
    }

    public int Midpoint => manager.PathManager.GetCentreNodeOfPath(Id);

    public bool ContainsNode(int node)
    {
        return Nodes.Contains(node);
    }

    public List<int> GetSubPath(int fromNode, int toNode)
    {
        int fromIndex = Nodes.IndexOf(fromNode);
        int toIndex = Nodes.IndexOf(toNode);
        if (fromIndex == -1 || toIndex == -1) return new List<int>();
        return fromIndex < toIndex
            ? Nodes.GetRange(fromIndex, toIndex - fromIndex + 1)
            : Nodes.GetRange(toIndex, fromIndex - toIndex + 1).AsEnumerable().Reverse().ToList();
    }

    public bool IsValid(HexGridManager manager, bool isTemporary = false, List<int> originalPathNodes = null)
    {
        if (Nodes == null || Nodes.Count < 2)
        {
            Debug.LogWarning($"Path ID {Id} invalid: Too few nodes ({Nodes?.Count ?? 0})");
            return false;
        }

        HashSet<int> pathNodeSet = new HashSet<int>(Nodes); // For quick lookup
        for (int i = 0; i < Nodes.Count; i++)
        {
            NodeData nodeData = manager.NodeManager.GetNodeData(Nodes[i]);
            if (!manager.NodeManager.IsNodeValidForPath(Nodes[i]))
            {
                Debug.LogWarning($"Path ID {Id} invalid: Node {Nodes[i]} is not valid for paths");
                return false;
            }
            if (!isTemporary && nodeData.HasPath && i != 0 && i != Nodes.Count - 1 && !nodeData.HasFlag)
            {
                // Allow if part of this path or original path being split
                if (!pathNodeSet.Contains(Nodes[i]) && (originalPathNodes == null || !originalPathNodes.Contains(Nodes[i])))
                {
                    Debug.LogWarning($"Path ID {Id} invalid: Node {Nodes[i]} already has a path and isn't a flag");
                    return false;
                }
            }
        }
        return true;
    }
}