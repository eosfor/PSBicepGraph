using Bicep.Core.Semantics;
using Bicep.Core.TypeSystem;
using PSGraph.Model;
using QuikGraph;
using QuikGraph.Algorithms.Search;

namespace PSBicepGraph;

public class SemanticGraphCondencationAlgorithm
{

    public PsBidirectionalGraph Condence(PsBidirectionalGraph g, SymbolKind vertexKind)
    {
        Dictionary<PSVertex, HashSet<PSVertex>> reachability;
        Dictionary<PSVertex, PSVertex> reverseIndex;

        BuildReachabilityMap(g, vertexKind, out reachability, out reverseIndex);

        // building output condenced graph
        return GenerateReducedGraph(g, reachability, reverseIndex);
    }

    // public BidirectionalGraph<DeclaredSymbol, Edge<DeclaredSymbol>> CondenceToGraph(BidirectionalGraph<DeclaredSymbol, Edge<DeclaredSymbol>> graph, SymbolKind vertexKind)
    // {
    //     Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> reachability;
    //     Dictionary<DeclaredSymbol, DeclaredSymbol> reverseIndex;

    //     BuildReachabilityMap(graph, vertexKind, out reachability, out reverseIndex);

    //     // building output condenced graph
    //     return GenerateReducedGraph(graph, reachability, reverseIndex);
    // }

    // public Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> CondenceToHashTable(BidirectionalGraph<DeclaredSymbol, Edge<DeclaredSymbol>> graph, SymbolKind vertexKind, out Dictionary<DeclaredSymbol, DeclaredSymbol> collapsedMap)
    // {
    //     Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> reachability;
    //     Dictionary<DeclaredSymbol, DeclaredSymbol> reverseIndex;

    //     BuildReachabilityMap(graph, vertexKind, out reachability, out reverseIndex);


    //     collapsedMap = reverseIndex;

    //     // building output condenced graph
    //     return GenerateReducedDependencyMap(graph, reachability, reverseIndex);
    // }

    private static void BuildReachabilityMap(PsBidirectionalGraph graph,
                                             SymbolKind vertexKind,
                                             out Dictionary<PSVertex, HashSet<PSVertex>> reachability,
                                             out Dictionary<PSVertex, PSVertex> reverseIndex)
    {
        reachability = new Dictionary<PSVertex, HashSet<PSVertex>>();
        reverseIndex = new();
        foreach (var vertex in graph.Vertices)
        {
            //TODO: fix this
            if (vertex.Metadata["kind"] == vertexKind.ToString())
            {
                var visited = new HashSet<PSVertex>();
                var treeEdges = new List<Edge<PSVertex>>();

                var dfs = new DepthFirstSearchAlgorithm<PSVertex, PSEdge>(graph);

                dfs.DiscoverVertex += v => visited.Add(v);
                dfs.TreeEdge += e => treeEdges.Add(e);
                // dfs.FinishVertex += v => { /* при необходимости */ };

                dfs.Compute(vertex);
                visited.Remove(vertex); // Если не нужно включать саму стартовую
                reachability[vertex] = visited;
            }
        }

        //HashSet<DeclaredSymbol> toBeCollapsed = reachability.SelectMany(v => v.Value).ToHashSet();
        foreach (var deps in reachability)
        {
            foreach (var item in deps.Value)
            {
                reverseIndex[item] = deps.Key;
            }
        }
    }

    private static PsBidirectionalGraph GenerateReducedGraph(PsBidirectionalGraph graph,
                                                             Dictionary<PSVertex, HashSet<PSVertex>> reachability,
                                                             Dictionary<PSVertex, PSVertex> reverseIndex)
    {

        PsBidirectionalGraph g = new();

        foreach (var group in reachability.Keys)
        {
            g.AddVertex(group);
        }

        foreach (var e in graph.Edges)
        {
            if (reverseIndex.ContainsKey(e.Source) && !reverseIndex.ContainsKey(e.Target))
            {
                if (reverseIndex[e.Source] != e.Target)
                {
                    var ne = new PSEdge(reverseIndex[e.Source], e.Target);
                    if (!g.ContainsEdge(reverseIndex[e.Source], e.Target))
                        g.AddVerticesAndEdge(ne);

                }
            }

            if (!reverseIndex.ContainsKey(e.Source) && reverseIndex.ContainsKey(e.Target))
            {
                if (e.Source != reverseIndex[e.Target])
                {
                    var ne = new PSEdge(e.Source, reverseIndex[e.Target]);
                    if (!g.ContainsEdge(e.Source, reverseIndex[e.Target]))
                        g.AddVerticesAndEdge(new PSEdge(e.Source, reverseIndex[e.Target]));
                }
            }

            if (!reverseIndex.ContainsKey(e.Source) && !reverseIndex.ContainsKey(e.Target))
            {
                var ne = new Edge<PSVertex>(e.Source, e.Target);
                if (!g.ContainsEdge(e.Source, e.Target))
                    g.AddVerticesAndEdge(new PSEdge(e.Source, e.Target));
            }

            if (reverseIndex.ContainsKey(e.Source) && reverseIndex.ContainsKey(e.Target))
            {
                if (reverseIndex[e.Source] != reverseIndex[e.Target])
                {
                    var ne = new PSEdge(reverseIndex[e.Source], reverseIndex[e.Target]);
                    if (!g.ContainsEdge(reverseIndex[e.Source], reverseIndex[e.Target]))
                        g.AddVerticesAndEdge(new PSEdge(reverseIndex[e.Source], reverseIndex[e.Target]));
                }
            }
        }

        return g;
    }

    // private static Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> GenerateReducedDependencyMap(
    //     BidirectionalGraph<DeclaredSymbol, Edge<DeclaredSymbol>> graph,
    //     Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> reachability,
    //     Dictionary<DeclaredSymbol, DeclaredSymbol> reverseIndex)
    // {
    //     // Результат: для каждого "представителя" -> множество его зависимостей (также представителей)
    //     var g = new Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>>();

    //     // Инициализируем словарь всеми представителями (ключами reachability)
    //     foreach (var rep in reachability.Keys)
    //     {
    //         g[rep] = new HashSet<DeclaredSymbol>();
    //     }

    //     foreach (var e in graph.Edges)
    //     {
    //         // Представитель (если вершина схлопнута, берём её группу; иначе сама вершина)
    //         var srcRep = reverseIndex.TryGetValue(e.Source, out var sRep) ? sRep : e.Source;
    //         var tgtRep = reverseIndex.TryGetValue(e.Target, out var tRep) ? tRep : e.Target;

    //         // Пропускаем петли внутри одной группы
    //         if (srcRep == tgtRep)
    //             continue;

    //         // Гарантируем наличие исходного представителя в словаре
    //         if (!g.TryGetValue(srcRep, out var set))
    //         {
    //             set = new HashSet<DeclaredSymbol>();
    //             g[srcRep] = set;
    //         }

    //         set.Add(tgtRep);
    //     }

    //     return g;
    // }
}