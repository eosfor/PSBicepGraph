using Bicep.Core.Semantics;
using Newtonsoft.Json.Linq;
using PSGraph.Model;

public static class HashSetExtensions
{
    private static HashSet<PSVertex> ConvertHashSet<T>(
        HashSet<T> source,
        Func<T, PSVertex> projector,
        Dictionary<object, PSVertex>? cache = null)
        where T : notnull
    {
        var ret = new HashSet<PSVertex>(source.Count);
        if (cache is null)
        {
            foreach (var item in source)
                ret.Add(projector(item));
            return ret;
        }

        // С кешем (чтобы не плодить повторные PSVertex для одних и тех же объектов)
        foreach (var item in source)
        {
            if (!cache.TryGetValue(item, out var ps))
            {
                ps = projector(item);
                cache[item] = ps;
            }
            ret.Add(ps);
        }
        return ret;
    }

    private static HashSet<PSVertex> ConvertHashSet(this HashSet<DeclaredSymbol> s)
        => ConvertHashSet(s, d => d.ToPsVertex());

    private static HashSet<PSVertex> ConvertHashSet(this HashSet<JToken> s)
        => ConvertHashSet(s, t => t.ToPsVertex());
}