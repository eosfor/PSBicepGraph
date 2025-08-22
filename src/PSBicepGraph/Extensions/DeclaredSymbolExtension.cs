using Bicep.Core.Semantics;
using PSGraph.Model;

public static class DeclaredSymbolExtensions
{
    public static PSVertex ToPsVertex(this DeclaredSymbol s)
    {
        string label = $"{s.Name}: {s.Kind.ToString()}";
        var v = new PSVertex(label);
        v.Metadata.Add("kind", s.Kind.ToString());
        v.OriginalObject = s;

        return v;
    }
}