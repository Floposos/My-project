using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Kürzester Weg über das Straßennetz (A*, 4 Nachbarn, Kosten 1 je Feld). Gleichstände werden über
    /// die Einfüge-Reihenfolge entschieden, daher immer dasselbe Ergebnis (wie die Browser-Version).
    /// </summary>
    public static class Pathfinding
    {
        struct Node { public Cell Cell; public int Key, F, Seq; }

        /// <summary>
        /// Felder von Start bis Ziel (beide enthalten) oder null. avoid sperrt Felder (Umweg bei Stau).
        /// Der Start selbst muss keine Straße sein.
        /// </summary>
        public static List<Cell> FindPath(RoadNetwork network, Cell from, Cell to, ISet<int> avoid = null)
        {
            if (!network.Has(to)) return null;
            int goal = RoadNetwork.CellKey(to);
            var g = new Dictionary<int, int> { [RoadNetwork.CellKey(from)] = 0 };
            var cameFrom = new Dictionary<int, Cell>();
            var open = new List<Node>();
            var closed = new HashSet<int>();
            int seq = 0;
            Push(open, new Node { Cell = from, Key = RoadNetwork.CellKey(from), F = H(from, to), Seq = seq++ });
            while (open.Count > 0)
            {
                var node = Pop(open);
                if (closed.Contains(node.Key)) continue;
                if (node.Key == goal) return Rebuild(cameFrom, node.Cell);
                closed.Add(node.Key);
                int cost = (g.TryGetValue(node.Key, out var gv) ? gv : 0) + 1;
                for (int d = 0; d < 4; d++)
                {
                    var next = new Cell(node.Cell.X + RoadNetwork.Dx[d], node.Cell.Z + RoadNetwork.Dz[d]);
                    if (!network.Has(next)) continue;
                    int key = RoadNetwork.CellKey(next);
                    if ((avoid != null && avoid.Contains(key)) || closed.Contains(key)) continue;
                    if (g.TryGetValue(key, out var old) && cost >= old) continue;
                    g[key] = cost;
                    cameFrom[key] = node.Cell;
                    Push(open, new Node { Cell = next, Key = key, F = cost + H(next, to), Seq = seq++ });
                }
            }
            return null;
        }

        static int H(Cell c, Cell to) => System.Math.Abs(c.X - to.X) + System.Math.Abs(c.Z - to.Z);

        static List<Cell> Rebuild(Dictionary<int, Cell> cameFrom, Cell end)
        {
            var path = new List<Cell> { end };
            var key = RoadNetwork.CellKey(end);
            while (cameFrom.TryGetValue(key, out var prev))
            {
                path.Add(prev);
                key = RoadNetwork.CellKey(prev);
            }
            path.Reverse();
            return path;
        }

        static bool Less(Node a, Node b) => a.F < b.F || (a.F == b.F && a.Seq < b.Seq);

        static void Push(List<Node> items, Node node)
        {
            items.Add(node);
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (!Less(node, items[parent])) break;
                items[i] = items[parent];
                i = parent;
            }
            items[i] = node;
        }

        static Node Pop(List<Node> items)
        {
            var top = items[0];
            var last = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            if (items.Count == 0) return top;
            int i = 0;
            for (;;)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                var best = last;
                if (l < items.Count && Less(items[l], best)) { m = l; best = items[l]; }
                if (r < items.Count && Less(items[r], best)) { m = r; best = items[r]; }
                if (m == i) break;
                items[i] = best;
                i = m;
            }
            items[i] = last;
            return top;
        }
    }
}
