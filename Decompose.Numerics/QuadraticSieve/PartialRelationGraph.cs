﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Decompose.Numerics
{
    public class PartialRelationEdge
    {
        public long Vertex1 { get; set; }
        public long Vertex2 { get; set; }
        public override string ToString()
        {
            return string.Format("Vertex1 = {0}, Vertex2 = {1}", Vertex1, Vertex2);
        }
    }

    /// <summary>
    /// A specialize graph that can contain either partial relations or
    /// partial partial relations.  The data structure is optimized for
    /// both kinds internally but exposes a unified interface.  It is
    /// essential that the graph be maintained so that it doesn't contain
    /// any cycles.  Instead of finding cycles, the client requests a
    /// path between two vertices when it has the edge that will complete
    /// the cycle.  If so, the client then removes that path from the
    /// graph, always preserving the fact that the graph is acyclic.
    /// </summary>
    /// <typeparam name="TEdge"></typeparam>
    public class PartialRelationGraph<TEdge> where TEdge : PartialRelationEdge, new()
    {
        /// <summary>
        /// A vertex dictionary is a specialized dictionary
        /// that can get very large.  The contents of the
        /// dictionary are distributed across a number of
        /// smaller dictionaries.  This facilitates memory
        /// management and increases the maximum size.
        /// </summary>
        /// <typeparam name="TValue">The dictionary value type.</typeparam>
        private class VertexDictionary<TValue>
        {
            private const int n = 16;
            private const int shift = 1;
            private const int mask = (n - 1) << shift;
            public Dictionary<long, TValue>[] dictionaries;
            public int Count
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < n; i++)
                        count += dictionaries[i].Count;
                    return count;
                }
            }
            public VertexDictionary()
            {
                dictionaries = new Dictionary<long, TValue>[n];
                for (int i = 0; i < n; i++)
                    dictionaries[i] = new Dictionary<long, TValue>();
            }
            public bool ContainsKey(long vertex)
            {
                return dictionaries[GetSlot(vertex)].ContainsKey(vertex);
            }
            public void Add(long vertex, TValue value)
            {
                dictionaries[GetSlot(vertex)].Add(vertex, value);
            }
            public void Remove(long vertex)
            {
                dictionaries[GetSlot(vertex)].Remove(vertex);
            }
            public TValue this[long vertex]
            {
                get { return dictionaries[GetSlot(vertex)][vertex]; }
                set { dictionaries[GetSlot(vertex)][vertex] = value; }
            }
            public bool TryGetValue(long vertex, out TValue value)
            {
                return dictionaries[GetSlot(vertex)].TryGetValue(vertex, out value);
            }
            private int GetSlot(long vertex)
            {
                return (int)(vertex & mask) >> shift;
            }
        }

        /// <summary>
        /// Dictionary mapping vertices to edges.  A vertex with a single
        /// edge is treated specially to conserve memory.
        /// </summary>
        private class EdgeMap
        {
            private VertexDictionary<object> map;
            public EdgeMap()
            {
                map = new VertexDictionary<object>();
            }
            public void Add(long vertex, TEdge edge)
            {
                object value;
                if (map.TryGetValue(vertex, out value))
                {
                    if (value is TEdge)
                        map[vertex] = new List<TEdge> { (TEdge)value, edge };
                    else
                        (value as List<TEdge>).Add(edge);
                }
                else
                    map.Add(vertex, edge);
            }
            public void Remove(long vertex, TEdge edge)
            {
                var value = map[vertex];
                if (value is TEdge)
                    map.Remove(vertex);
                else
                {
                    var list = value as List<TEdge>;
                    list.Remove(edge);
                    if (list.Count == 1)
                        map[vertex] = list[0];
                }
            }
            public bool HasEdges(long vertex)
            {
                return map.ContainsKey(vertex);
            }
            public bool GetEdges(long vertex, out TEdge edge, out List<TEdge> edges)
            {
                if (!map.ContainsKey(vertex))
                {
                    edge = null;
                    edges = null;
                    return false;
                }
                edge = map[vertex] as TEdge;
                edges = map[vertex] as List<TEdge>;
                return true;
            }
        }

        private VertexDictionary<TEdge> prMap;
        private EdgeMap pprMap;
        private int count;

        public int Count { get { return count; } }
        public int PartialRelations { get { return prMap.Count; } }
        public int PartialPartialRelations { get { return count - prMap.Count; } }

        public PartialRelationGraph()
        {
            prMap = new VertexDictionary<TEdge>();
            pprMap = new EdgeMap();
        }

        public void AddEdge(long vertex1, long vertex2)
        {
            AddEdge(new TEdge { Vertex1 = vertex1, Vertex2 = vertex2 });
        }

        public void AddEdge(TEdge edge)
        {
            if (edge.Vertex2 == 1)
                prMap.Add(edge.Vertex1, edge);
            else
            {
                pprMap.Add(edge.Vertex1, edge);
                pprMap.Add(edge.Vertex2, edge);
            }
            ++count;
        }

        public void RemoveEdge(TEdge edge)
        {
            if (edge.Vertex2 == 1)
                prMap.Remove(edge.Vertex1);
            else
            {
                pprMap.Remove(edge.Vertex1, edge);
                pprMap.Remove(edge.Vertex2, edge);
            }
            --count;
        }

        public TEdge FindEdge(long vertex1, long vertex2)
        {
            TEdge edge;
            List<TEdge> edges;
            if (vertex2 == 1)
            {
                return prMap.TryGetValue(vertex1, out edge) ? edge : null;
            }
            if (!pprMap.GetEdges(vertex1, out edge, out edges))
                return null;
            if (edge != null)
            {
                if (edge.Vertex1 == vertex2 || edge.Vertex2 == vertex2)
                    return edge;
                return null;
            }
            for (int i = 0; i < edges.Count; i++)
            {
                edge = edges[i];
                if (edge.Vertex1 == vertex2 || edge.Vertex2 == vertex2)
                    return edge;
            }
            return null;
        }

        /// <summary>
        /// Find a path between two vertices of the graph.
        /// </summary>
        /// <param name="start">The starting vertex.</param>
        /// <param name="end">The ending vertex.</param>
        /// <returns>A collection of edges comprising the path.</returns>
        public ICollection<TEdge> FindPath(long start, long end)
        {
            // Handle the special case of partial relations.
            if (end == 1)
            {
                // Look for a matching partial relation.
                if (prMap.ContainsKey(start))
                    return new List<TEdge> { prMap[start] };

                // Look for a route that terminates with a partial.
                return FindPathRecursive(start, 1, null);
            }

            // Check whether the path start or ends in the
            // partial relation map.
            var prHasStart = prMap.ContainsKey(start);
            var prHasEnd = prMap.ContainsKey(end);

            // If both do so, then we have a path using the
            // two partial relations.
            if (prHasStart && prHasEnd)
                return new List<TEdge> { prMap[end], prMap[start] };

            var result = null as List<TEdge>;

            // First try to find a direct path from start to
            // end just using the partial relation map, which
            // is only possible if there are edges to the end
            // vertex.
            if (pprMap.HasEdges(end))
            {
                result  = FindPathRecursive(start, end, null);
                if (result != null)
                    return result;
            }

            // If the path neither starts nor ends in the
            // partial relation map, try to find a path
            // from the start the the partial relation map
            // and another from the end to the partial
            // relation map.  Then combine them.
            if (!prHasStart && !prHasEnd)
            {
                var part1 = FindPathRecursive(start, 1, null);
                if (part1 != null)
                {
                    var part2 = FindPathRecursive(end, 1, null);
                    if (part2 != null)
                        return part1.Concat(part2).ToList();
                }
            }

            // If the path starts in the partial relation map,
            // try to find a path from the end to the partial
            // relation map.
            if (prHasStart)
            {
                result = FindPathRecursive(end, 1, null);
                if (result != null)
                    result.Add(prMap[start]);
            }

            // If the path ends in the partial relation map,
            // try to find a path from the start to the partial
            // relation map.
            if (prHasEnd)
            {
                result = FindPathRecursive(start, 1, null);
                if (result != null)
                    result.Add(prMap[end]);
            }
            return result;
        }

        private List<TEdge> FindPathRecursive(long start, long end, TEdge previous)
        {
            TEdge edge;
            List<TEdge> edges;
            if (end == 1)
            {
                if (prMap.TryGetValue(start, out edge) && edge != previous)
                    return new List<TEdge> { prMap[start] };
            }
            if (!pprMap.GetEdges(start, out edge, out edges))
                return null;
            if (edge != null)
                return CheckEdge(start, end, previous, edge);
            for (int i = 0; i < edges.Count; i++)
            {
                var result = CheckEdge(start, end, previous, edges[i]);
                if (result != null)
                    return result;
            }
            return null;
        }

        private List<TEdge> CheckEdge(long start, long end, TEdge previous, TEdge edge)
        {
            if (edge == previous)
                return null;
            var next = edge.Vertex1 == start ? edge.Vertex2 : edge.Vertex1;
            if (next == end)
                return new List<TEdge> { edge };
            var result = FindPathRecursive(next, end, edge);
            if (result != null)
            {
                result.Add(edge);
                return result;
            }
            return null;
        }
    }
}