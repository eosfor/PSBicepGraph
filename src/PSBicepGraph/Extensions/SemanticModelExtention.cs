using Bicep.Core.Semantics;
using PSGraph.Model;

public static class SemanticModelExtention
{
    public static PSVertex ToPsVertex(this SemanticModel m)
    {
        var v = new PSVertex(m.SourceFile.Uri.AbsoluteUri);
        v.Metadata.Add("kind", m.GetType().ToString());
        v.OriginalObject = m;

        return v;
    }
}