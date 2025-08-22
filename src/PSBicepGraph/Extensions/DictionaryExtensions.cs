using System;
using System.Collections.Generic;
using Bicep.Core.Semantics;
using Newtonsoft.Json.Linq;
using PSGraph.Model;

namespace PSBicepGraph.Extensions;

public static class DictionaryPsVertexExtensions
{
    // 1. Зависимости DeclaredSymbol -> { DeclaredSymbol }
    public static Dictionary<PSVertex, HashSet<PSVertex>> ToPsVertexMap(
        this Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var cache = new Dictionary<object, PSVertex>(source.Count * 2);
        var result = new Dictionary<PSVertex, HashSet<PSVertex>>(source.Count);

        foreach (var (key, targets) in source)
        {
            var pk = GetOrAdd(cache, key, static o => ((DeclaredSymbol)o).ToPsVertex());
            if (!result.TryGetValue(pk, out var hs))
            {
                hs = new HashSet<PSVertex>();
                result[pk] = hs;
            }

            foreach (var t in targets)
                hs.Add(GetOrAdd(cache, t, static o => ((DeclaredSymbol)o).ToPsVertex()));
        }

        return result;
    }

    // 2. ARM ресурсы: DeclaredSymbol (модуль) -> { JToken ресурсы }
    public static Dictionary<PSVertex, HashSet<PSVertex>> ToPsVertexMap(
        this Dictionary<DeclaredSymbol, HashSet<JToken>> armNodes)
    {
        if (armNodes == null) throw new ArgumentNullException(nameof(armNodes));

        var cache = new Dictionary<object, PSVertex>(armNodes.Count * 2);
        var result = new Dictionary<PSVertex, HashSet<PSVertex>>(armNodes.Count);

        foreach (var (key, tokens) in armNodes)
        {
            var pk = GetOrAdd(cache, key, static o => ((DeclaredSymbol)o).ToPsVertex());
            if (!result.TryGetValue(pk, out var hs))
            {
                hs = new HashSet<PSVertex>();
                result[pk] = hs;
            }

            foreach (var tok in tokens)
                hs.Add(GetOrAdd(cache, tok, static o => ((JToken)o).ToPsVertex()));
        }

        return result;
    }

    // 3. virtualNodes: SemanticModel -> (sources, sinks) -> PSVertex представление
    public static Dictionary<PSVertex, (HashSet<PSVertex> sources, HashSet<PSVertex> sinks)> ToPsVertexMap(
        this Dictionary<SemanticModel, (HashSet<DeclaredSymbol> sources, HashSet<DeclaredSymbol> sinks)> virtualNodes)
    {
        if (virtualNodes == null) throw new ArgumentNullException(nameof(virtualNodes));

        var cache = new Dictionary<object, PSVertex>(virtualNodes.Count * 3);
        var result = new Dictionary<PSVertex, (HashSet<PSVertex>, HashSet<PSVertex>)>(virtualNodes.Count);

        foreach (var (model, tuple) in virtualNodes)
        {
            var modelV = GetOrAdd(cache, model, static o => ((SemanticModel)o).ToPsVertex());
            var (srcDecls, sinkDecls) = tuple;

            var srcSet = new HashSet<PSVertex>(srcDecls.Count);
            foreach (var s in srcDecls)
                srcSet.Add(GetOrAdd(cache, s, static o => ((DeclaredSymbol)o).ToPsVertex()));

            var sinkSet = new HashSet<PSVertex>(sinkDecls.Count);
            foreach (var s in sinkDecls)
                sinkSet.Add(GetOrAdd(cache, s, static o => ((DeclaredSymbol)o).ToPsVertex()));

            result[modelV] = (srcSet, sinkSet);
        }

        return result;
    }

    private static PSVertex GetOrAdd(
        Dictionary<object, PSVertex> cache,
        object key,
        Func<object, PSVertex> factory)
    {
        if (!cache.TryGetValue(key, out var v))
        {
            v = factory(key);
            cache[key] = v;
        }
        return v;
    }
}