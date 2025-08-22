using Bicep.Core.Semantics;
using Newtonsoft.Json.Linq;
using PSBicepGraph.Extensions;
using PSGraph.Model;
using QuikGraph;
using System;
using System.Collections.Generic;

public static class GraphBuilderHelper
{

    private enum Direction
    {
        Forward,
        Backward
    }
    public static PsBidirectionalGraph Build(Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> dependencyMap,
                                             Dictionary<SemanticModel, (HashSet<DeclaredSymbol>, HashSet<DeclaredSymbol>)> virtualNodes,
                                             Dictionary<DeclaredSymbol, HashSet<JToken>> armNodes)
    {
        var psVertexDepMap = dependencyMap.ToPsVertexMap();
        var psVirtualNodes = virtualNodes.ToPsVertexMap();
        var psArmNodes = armNodes.ToPsVertexMap();

        var ret = new PsBidirectionalGraph();
        WriteGraph(psVertexDepMap, ret);
        WriteGraph(psVirtualNodes, ret);
        WriteGraph(psArmNodes, ret);

        return ret;
    }

    private static void WriteGraph(Dictionary<PSVertex, (HashSet<PSVertex>, HashSet<PSVertex>)> dependencyMap, PsBidirectionalGraph g)
    {
        foreach (var kvp in dependencyMap)
        {
            var model = kvp.Key;
            g.AddVertex(model);

            var (sources, sinks) = kvp.Value;

            // из модели в sources
            foreach (var source in sources)
            {
                g.AddVertex(source);
                g.AddEdge(new PSEdge(model, source, new PSEdgeTag(string.Empty)));
            }

            // из sink в модель
            foreach (var sink in sinks)
            {
                g.AddEdge(new PSEdge(sink, model, new PSEdgeTag(string.Empty)));
            }
        }

    }

    private static void WriteGraph(Dictionary<PSVertex, HashSet<PSVertex>> dependencyMap, PsBidirectionalGraph g)
    {
        foreach (var kvp in dependencyMap)
        {
            g.AddVertex(kvp.Key);

            foreach (var child in kvp.Value)
            {
                g.AddVertex(child);
                g.AddEdge(new PSEdge(kvp.Key, child, new PSEdgeTag(string.Empty)));
            }
        }

    }

    // public static BidirectionalGraph<TVertex, Edge<TVertex>> Build<TVertex>(Dictionary<TVertex, HashSet<TVertex>> dependencyMap)
    //     where TVertex : notnull
    // {
    //     var graph = new BidirectionalGraph<TVertex, Edge<TVertex>>();

    //     foreach (var v in dependencyMap.Keys)
    //     {
    //         graph.AddVertex(v);
    //     }

    //     foreach (var (source, targets) in dependencyMap)
    //     {
    //         foreach (var target in targets)
    //         {
    //             if (!graph.ContainsVertex(target))
    //             {
    //                 graph.AddVertex(target);
    //             }

    //             var edge = new Edge<TVertex>(source, target);
    //             graph.AddEdge(edge);
    //         }
    //     }

    //     return graph;
    // }


    // public static BidirectionalGraph<TVertex, Edge<TVertex>> Build<TVertex>(Dictionary<TVertex, HashSet<TVertex>> dependencyMap,
    //                                                                         Dictionary<SemanticModel, (HashSet<TVertex>, HashSet<TVertex>)> virtualNodes)
    // where TVertex : notnull
    // {
    //     var graph = Build(dependencyMap);

    //     // Для каждой модели добавляем виртуальные рёбра
    //     foreach (var kvp in virtualNodes)
    //     {
    //         var model = kvp.Key;
    //         var (sources, sinks) = kvp.Value;
    //         // Вершина модели с SourceFile uri
    //         //var modelVertex = new PSVertex(model.SourceFile.Uri.ToString());
    //         graph.AddVertex(modelVertex);

    //         // Из всех sinks в модель
    //         foreach (var sink in sinks)
    //         {
    //             var sinkVertex = new PSVertex($"{sink.Name} ({sink.Kind})");
    //             graph.AddVertex(sinkVertex);
    //             graph.AddEdge(new PSEdge(sinkVertex, modelVertex, new PSEdgeTag("virtual_sink")));
    //         }

    //         // Из модели во все sources
    //         foreach (var source in sources)
    //         {
    //             var sourceVertex = new PSVertex($"{source.Name} ({source.Kind})");
    //             graph.AddVertex(sourceVertex);
    //             graph.AddEdge(new PSEdge(modelVertex, sourceVertex, new PSEdgeTag("virtual_source")));
    //         }
    //     }

    //     return graph;
    // }

    // public static PsBidirectionalGraph ToPSBidirectionalGraph<TVertex>(this BidirectionalGraph<TVertex, Edge<TVertex>> g)
    //     where TVertex : DeclaredSymbol
    // {
    //     var psGraph = new PsBidirectionalGraph(false);

    //     foreach (var vertex in g.Vertices)
    //     {
    //         var v = new PSVertex(vertex.Name);
    //         v.OriginalObject = vertex;
    //         psGraph.AddVertex(v);
    //     }

    //     foreach (var edge in g.Edges)
    //     {
    //         var tag = new PSEdgeTag(string.Empty);

    //         var s = new PSVertex(edge.Source.Name);
    //         s.OriginalObject = edge.Source;

    //         var t = new PSVertex(edge.Target.Name);
    //         t.OriginalObject = edge.Target;

    //         var e = new PSEdge(s, t, tag);
    //         psGraph.AddEdge(e);
    //     }

    //     return psGraph;
    // }
}