namespace ConvertirJsonClaseC_.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// Propósito: Analiza un JSON de ejemplo y genera un conjunto de definiciones
    /// de clases (ClassDef) recursivamente para objetos y arreglos anidados.
    /// </summary>
    public static class JsonSchemaParser
    {
        /// <summary>
        /// Analiza el JSON y devuelve todas las clases detectadas (incluida la raíz).
        /// </summary>
        /// <param name="json">JSON de ejemplo (debe comenzar con un objeto).</param>
        /// <param name="rootClassName">Nombre deseado para la clase raíz.</param>
        public static List<ClassDef> FromSample(string json, string rootClassName)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("El JSON debe iniciar con un objeto { ... }.");

            var classes = new List<ClassDef>();
            ParseObject(root, ToPascal(rootClassName), classes); // root ya queda agregado
            return classes;
        }

        // --- Núcleo recursivo ---

        /// <summary>
        /// Crea una ClassDef para 'obj' con un nombre base y la agrega a 'classes'.
        /// Devuelve el nombre FINAL realmente utilizado (con sufijo si hubo colisión).
        /// </summary>
        private static string ParseObject(JsonElement obj, string baseName, List<ClassDef> classes)
        {
            var finalName = ToUniqueName(baseName, classes);
            var def = new ClassDef
            {
                ClassName = finalName,
                Properties = new List<PropertyDef>()
            };

            foreach (var p in obj.EnumerateObject())
            {
                var propName = ToPascal(p.Name);
                var propType = MapType(p.Value, propName, classes, finalName /*owner*/);
                def.Properties.Add(new PropertyDef { Name = propName, Type = propType });
            }

            classes.Add(def);
            return finalName;
        }

        private static string MapType(JsonElement value, string suggestedName, List<ClassDef> classes, string ownerClassName)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return "string";

                case JsonValueKind.Number:
                    // inferencia simple
                    return value.TryGetInt64(out _) ? "long" : "decimal";

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return "bool";

                case JsonValueKind.Object:
                    {
                        var desired = ToPascal(suggestedName);

                        // Si choca con una clase ya existente (especialmente la root/owner), intenta sufijos semánticos.
                        if (classes.Any(c => c.ClassName == desired))
                            desired = PreferReadableVariant(desired, classes);

                        var final = ParseObject(value, desired, classes);
                        return final; // devolvemos el nombre real creado
                    }

                case JsonValueKind.Array:
                    {
                        if (!value.EnumerateArray().Any())
                            return "List<object>";

                        // Elegimos el PRIMER elemento NO NULL para inferir tipo;
                        // si todos son null → List<object?>
                        var arr = value.EnumerateArray().ToList();
                        var firstNonNull = arr.FirstOrDefault(e => e.ValueKind != JsonValueKind.Null);

                        if (firstNonNull.ValueKind == JsonValueKind.Undefined || firstNonNull.ValueKind == JsonValueKind.Null)
                            return "List<object?>";

                        var innerType = MapType(firstNonNull, suggestedName + "Item", classes, ownerClassName);

                        // Si existen nulls en el arreglo y el innerType es de valor, lo hacemos nullable (ej. int? -> List<int?>)
                        var hasNulls = arr.Any(e => e.ValueKind == JsonValueKind.Null);
                        if (hasNulls && IsValueTypeName(innerType))
                            innerType = innerType.EndsWith("?", StringComparison.Ordinal) ? innerType : innerType + "?";

                        return $"List<{innerType}>";
                    }

                case JsonValueKind.Null:
                    // Sin más evidencia, marcamos como nullable de referencia.
                    // NOTA: si necesitas forzar heurística (id/is/has/etc.) para tipos valor, se puede extender aquí.
                    return "string?";

                default:
                    return "string";
            }
        }

        // --- Utilidades de nombres ---

        private static string ToPascal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Root";
            var parts = s.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        /// <summary>
        /// Intenta producir un nombre legible (sin números) si ya existe una clase con 'desired'.
        /// </summary>
        private static string PreferReadableVariant(string desired, List<ClassDef> classes)
        {
            var candidates = new[] { "Data", "Info", "Details", "Model" }
                .Select(sfx => desired + sfx);

            foreach (var c in candidates)
                if (!classes.Any(x => x.ClassName == c))
                    return c;

            return desired;
        }

        /// <summary>
        /// Si ya existe una clase con el mismo nombre, agrega sufijos legibles; si todos chocan, usa números.
        /// </summary>
        private static string ToUniqueName(string desired, List<ClassDef> classes)
        {
            if (!classes.Any(c => c.ClassName == desired))
                return desired;

            var readable = new[] { "Data", "Info", "Details", "Model" };
            foreach (var sfx in readable)
            {
                var candidate = desired + sfx;
                if (!classes.Any(c => c.ClassName == candidate))
                    return candidate;
            }

            int i = 2;
            var name = desired + i;
            while (classes.Any(c => c.ClassName == name))
                name = desired + (++i);

            return name;
        }

        /// <summary>
        /// Reconoce tipos valor básicos para poder aplicar '?'
        /// </summary>
        private static bool IsValueTypeName(string typeName)
        {
            var t = typeName.TrimEnd('?');
            return t is "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong"
                   or "float" or "double" or "decimal" or "DateTime" or "Guid";
        }
    }
}
