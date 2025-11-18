// Core/SqlCreateTableParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConvertirJsonClaseC_.Core
{
    public static class SqlCreateTableParser
    {
        private static readonly string[] _constraintStarters = new[]
        {
            "constraint", "primary", "foreign", "unique", "check", "index"
        };

        public static ClassDef ParseCreateTable(string sql, string? explicitClassName = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("El SQL está vacío.", nameof(sql));

            // 1) Quitar comentarios de línea:  -- ... (hasta fin de línea)
            var withoutComments = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);

            // 2) Normalizar espacios sobre el SQL sin comentarios
            var normalized = withoutComments.Replace("\r", " ").Replace("\n", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ");

            // 3) Buscar el CREATE TABLE
            var m = Regex.Match(normalized, @"create\s+table\s+([^\(\s]+)\s*\(", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new InvalidOperationException("No se encontró un CREATE TABLE válido.");

            var fullTableName = m.Groups[1].Value.Trim(); // dbo.[Tabla] o [dbo].[Tabla] o Tabla

            var className = !string.IsNullOrWhiteSpace(explicitClassName)
                ? explicitClassName.Trim()
                : ToPascal(GetTableNameOnly(fullTableName));

            // 4) Extraer contenido entre paréntesis (definición de columnas)
            int start = normalized.IndexOf('(', m.Index + m.Length - 1);
            int end = normalized.LastIndexOf(')');
            if (start < 0 || end <= start)
                throw new InvalidOperationException("No se pudo extraer la definición de columnas.");

            var body = normalized.Substring(start + 1, end - start - 1);

            // 5) Split por comas de nivel superior
            var columns = SplitColumns(body);

            var properties = new List<PropertyDef>();

            foreach (var col in columns)
            {
                var line = col.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var lower = line.ToLowerInvariant();

                // Saltar constraints
                if (_constraintStarters.Any(x => lower.StartsWith(x)))
                    continue;

                // Extra seguridad: saltar fragmentos que empiecen con comentario, por si alguno sobreviviera
                if (lower.StartsWith("--"))
                    continue;

                // Primer token: nombre de columna
                // Segundo token: tipo SQL
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var rawName = parts[0];
                var rawType = parts[1];

                var colName = CleanName(rawName);
                if (string.IsNullOrWhiteSpace(colName))
                    continue;

                var isNullable = !lower.Contains("not null");

                var csType = SqlTypeMapper.SqlToCSharp(rawType, isNullable);
                var propName = ToPascal(colName);

                if (string.IsNullOrWhiteSpace(propName))
                    propName = colName;   // respaldo por si ToPascal deja cadena vacía

                properties.Add(new PropertyDef
                {
                    Name = propName,
                    Type = csType
                });
            }

            return new ClassDef
            {
                ClassName = className,
                Properties = properties
            };
        }


        private static List<string> SplitColumns(string body)
        {
            var result = new List<string>();
            int parenLevel = 0;
            int lastPos = 0;

            for (int i = 0; i < body.Length; i++)
            {
                var ch = body[i];
                if (ch == '(') parenLevel++;
                else if (ch == ')') parenLevel--;
                else if (ch == ',' && parenLevel == 0)
                {
                    var segment = body.Substring(lastPos, i - lastPos);
                    result.Add(segment);
                    lastPos = i + 1;
                }
            }

            if (lastPos < body.Length)
            {
                result.Add(body.Substring(lastPos));
            }

            return result;
        }

        private static string CleanName(string raw)
        {
            var name = raw.Trim();

            if (name.StartsWith("[") && name.EndsWith("]") && name.Length > 2)
                name = name.Substring(1, name.Length - 2);

            name = name.Trim('\"', '\'', '`');
            return name;
        }

        private static string GetTableNameOnly(string full)
        {
            // dbo.Tabla -> Tabla
            // [dbo].[Tabla] -> Tabla
            var s = full.Trim();

            // quitar [ ] extra
            s = s.Replace("[", "").Replace("]", "");

            var parts = s.Split('.');
            return parts.Length == 0 ? s : parts[^1];
        }

        private static string ToPascal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Root";

            var parts = s.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }
    }
}
