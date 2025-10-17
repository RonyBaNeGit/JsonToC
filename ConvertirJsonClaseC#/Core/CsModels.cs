// Core/CsModels.cs
using System.Collections.Generic;

namespace ConvertirJsonClaseC_.Core
{
    // HAZLA PUBLIC
    public class CsClassInfo
    {
        public string Name { get; set; } = "";
        public Dictionary<string, string> Properties { get; set; } = new(); // PropName -> TypeName
    }

    // Puede quedarse internal porque no aparece en firmas públicas
    internal static class KnownTypes
    {
        public static readonly HashSet<string> Primitives = new()
        {
            "bool","byte","sbyte","short","ushort","int","uint","long","ulong",
            "float","double","decimal"
        };

        public static bool IsPrimitive(string t) => Primitives.Contains(t);
        public static bool IsString(string t) => t == "string";
        public static bool IsObject(string t) => t == "object";
        public static bool IsCommonStruct(string t) => t is "DateTime" or "Guid";
        public static bool IsList(string t) => t.StartsWith("List<") && t.EndsWith(">");

        public static string? GetListInner(string t)
        {
            if (!IsList(t)) return null;
            return t.Substring("List<".Length, t.Length - "List<".Length - 1);
        }

        public static string StripNullable(string t) => t.EndsWith("?") ? t[..^1] : t;
    }
}
