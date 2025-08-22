using Bicep.Core.Semantics;
using Newtonsoft.Json.Linq;
using PSGraph.Model;

public static class JTokenExtensions
{
    public static PSVertex ToPsVertex(this JToken t)
    {
        var (name, typeName) = ExtractArmResourceInfo(t);
        string label = $"{name}: ARM({typeName})";
        var v = new PSVertex(label);
        v.Metadata.Add("kind", typeName);
        v.OriginalObject = t;

        return v;
    }

    private static (string logicalName, string typeName) ExtractArmResourceInfo(JToken token)
    {
        string logicalName = "unresolved";
        string typeName = "unknown";
        JObject? resourceObj = null;

        switch (token)
        {
            case JProperty prop:
                logicalName = prop.Name;
                resourceObj = prop.Value as JObject;
                break;
            case JObject jo:
                resourceObj = jo;
                logicalName = jo["name"]?.ToString() ?? logicalName;
                break;
        }

        if (resourceObj != null)
        {
            typeName = resourceObj["type"]?.ToString() ?? typeName;
        }

        return (logicalName, typeName);
    }
}