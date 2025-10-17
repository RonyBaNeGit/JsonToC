using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConvertirJsonClaseC_.Core
{
    /// <summary>Genera un JSON de ejemplo a partir del mapa de clases C#.</summary>
    public static class ClassToJsonGenerator
    {
        public static string GenerateSampleJson(
            Dictionary<string, CsClassInfo> classes,
            string rootClassName,
            bool pretty = true)
        {
            if (!classes.TryGetValue(rootClassName, out _))
                throw new InvalidOperationException($"No se encontró la clase raíz '{rootClassName}'.");

            var visited = new HashSet<string>();
            var node = BuildObject(classes, rootClassName, visited);

            var options = new JsonSerializerOptions { WriteIndented = pretty };
            return node.ToJsonString(options);
        }

        private static JsonObject BuildObject(
            Dictionary<string, CsClassInfo> classes, string className, HashSet<string> visited)
        {
            if (visited.Contains(className))
                return new JsonObject(); // evita ciclos (A->B->A)

            visited.Add(className);

            var obj = new JsonObject();
            var cls = classes[className];

            foreach (var kv in cls.Properties)
            {
                var propName = ToCamel(kv.Key);
                var typeName = NormalizeType(kv.Value);

                // Si es nullable (ej. int?, DateTime?, bool?, string?), emitimos null explícito
                if (typeName.EndsWith("?", StringComparison.Ordinal))
                {
                    obj[propName] = JsonValue.Create((string?)null);
                    continue;
                }

                obj[propName] = BuildValue(classes, typeName, visited);
            }

            visited.Remove(className);
            return obj;
        }

        private static JsonNode? BuildValue(
            Dictionary<string, CsClassInfo> classes, string typeName, HashSet<string> visited)
        {
            var notNull = KnownTypes.StripNullable(typeName);

            if (KnownTypes.IsString(notNull)) return JsonValue.Create("");
            if (KnownTypes.IsPrimitive(notNull)) return PrimitiveDefault(notNull);

            if (KnownTypes.IsCommonStruct(notNull))
                return notNull == "DateTime"
                    ? JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    : JsonValue.Create(Guid.NewGuid().ToString());

            if (KnownTypes.IsObject(notNull)) return new JsonObject();

            if (KnownTypes.IsList(notNull))
            {
                var inner = KnownTypes.GetListInner(notNull)!;

                // Si el elemento de la lista es nullable, devolvemos [null] como muestra
                if (inner.EndsWith("?", StringComparison.Ordinal))
                {
                    return new JsonArray { JsonValue.Create((string?)null) };
                }

                var arr = new JsonArray { BuildValue(classes, inner, visited) }; // 1 elemento de ejemplo
                return arr;
            }

            // Clase propia
            if (classes.ContainsKey(notNull))
                return BuildObject(classes, notNull, visited);

            // Desconocido: lo tratamos como string
            return JsonValue.Create("");
        }

        private static JsonNode PrimitiveDefault(string t) =>
            t switch
            {
                "bool" => JsonValue.Create(false),
                "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" => JsonValue.Create(0),
                "float" or "double" => JsonValue.Create(0.0),
                "decimal" => JsonValue.Create(0.0m),
                _ => JsonValue.Create("")
            };

        private static string NormalizeType(string t) =>
            t.Replace("System.Collections.Generic.", "")
             .Replace("System.", "")
             .Replace(" ", "");

        private static string ToCamel(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    }
}
