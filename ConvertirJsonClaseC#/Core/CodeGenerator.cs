using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ConvertirJsonClaseC_.Core
{
    public static class CodeGenerator
    {
        /// <summary>
        /// Genera todas las clases independientes, cada una con su propio namespace.
        /// Incluye inicializaciones en el constructor para List<> y tipos de clase personalizados.
        /// </summary>
        public static string GenerateClasses(string ns, string author, List<ClassDef> classes)
        {
            var now = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            var sb = new StringBuilder();

            for (int i = 0; i < classes.Count; i++)
            {
                var cls = classes[i];
                var usesList = cls.Properties.Any(p => p.Type.StartsWith("List<", StringComparison.Ordinal));

                // using por clase (solo si realmente usa List<>)
                if (usesList)
                    sb.AppendLine("using System.Collections.Generic;");

                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");

                // --- Cabecera XML ---
                var cleanSummary = SanitizeSummary(
                    cls.ClassSummary ?? "Representa una entidad de dominio basada en datos JSON.",
                    cls.ClassName
                );

                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Propósito: {cleanSummary}");
                sb.AppendLine($"    /// Fecha de creación: {now}.");
                sb.AppendLine($"    /// Creador: {author}.");
                sb.AppendLine("    /// Modificó:");
                sb.AppendLine("    /// Dependencias de conexiones e interfaces: No Aplica.");
                sb.AppendLine("    /// </summary>");

                // --- Clase ---
                sb.AppendLine($"    public class {cls.ClassName}");
                sb.AppendLine("    {");
                sb.AppendLine("        #region Constructores");
                sb.AppendLine();

                // Preparamos las líneas de inicialización del constructor
                var ctorInits = BuildConstructorInitializations(cls.Properties);

                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Inicializa una nueva instancia de la clase <see cref=\"{cls.ClassName}\"/>.");
                sb.AppendLine("        /// </summary>");

                if (ctorInits.Count == 0)
                {
                    // Constructor vacío
                    sb.AppendLine($"        public {cls.ClassName}() {{ }}");
                }
                else
                {
                    // Constructor con inicializaciones
                    sb.AppendLine($"        public {cls.ClassName}()");
                    sb.AppendLine("        {");
                    foreach (var line in ctorInits)
                        sb.AppendLine($"            {line}");
                    sb.AppendLine("        }");
                }

                sb.AppendLine();
                sb.AppendLine("        #endregion");
                sb.AppendLine();
                sb.AppendLine("        #region Propiedades");
                sb.AppendLine();

                foreach (var p in cls.Properties)
                {
                    var propSummary = SanitizeInline(p.Summary ?? p.Name);
                    var defaultInit = p.Type == "string" ? " = string.Empty;" : "";
                    sb.AppendLine("        /// <summary>");
                    sb.AppendLine($"        /// {propSummary}");
                    sb.AppendLine("        /// </summary>");
                    sb.AppendLine($"        public {p.Type} {p.Name} {{ get; set; }}{defaultInit}");
                    sb.AppendLine();
                }

                sb.AppendLine("        #endregion");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        // Construye las líneas de inicialización para el constructor según el tipo de la propiedad
        private static List<string> BuildConstructorInitializations(List<PropertyDef> properties)
        {
            var lines = new List<string>();

            foreach (var p in properties)
            {
                var type = p.Type;

                // List<T> -> new List<T>();
                if (type.StartsWith("List<", StringComparison.Ordinal))
                {
                    lines.Add($"{p.Name} = new {type}();");
                    continue;
                }

                // string ya se maneja con = string.Empty en la propiedad.
                if (string.Equals(type, "string", StringComparison.Ordinal)) continue;

                // Tipos primitivos y algunos conocidos: no inicializamos
                if (IsKnownValueType(type)) continue;

                // object genérico: comúnmente lo dejamos sin inicializar
                if (string.Equals(type, "object", StringComparison.OrdinalIgnoreCase)) continue;

                // Para tipos de clase personalizados -> new Tipo();
                lines.Add($"{p.Name} = new {type}();");
            }

            return lines;
        }

        // Heurística de tipos "valor/conocidos" que no queremos instanciar con 'new T()' aquí
        private static bool IsKnownValueType(string type)
        {
            // Quita nulabilidad si la hubiera (ej: int?, DateTime?)
            var t = type.TrimEnd('?');

            // Primitivos y conocidos
            switch (t)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "float":
                case "double":
                case "decimal":
                case "DateTime":
                case "Guid":
                    return true;
                default:
                    // structs personalizados no se detectan aquí (asumimos clases para los demás)
                    return false;
            }
        }

        // --- Limpieza de texto ---

        private static string SanitizeSummary(string s, string className)
        {
            s = SanitizeInline(s);
            s = Regex.Replace(s, $@"\b{Regex.Escape(className)}\b", "", RegexOptions.IgnoreCase).Trim();
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s;
        }

        private static string SanitizeInline(string s)
        {
            s = s.Replace("///", "");
            s = s.Replace("<", "(").Replace(">", ")");
            s = s.Replace("*", "").Replace("`", "").Replace("#", "");
            s = s.Replace("—", "-").Replace("–", "-");
            return s.Trim();
        }
    }
}
