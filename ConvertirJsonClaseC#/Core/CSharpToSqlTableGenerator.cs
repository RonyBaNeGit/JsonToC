// Core/CSharpToSqlTableGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConvertirJsonClaseC_.Core
{
    public static class CSharpToSqlTableGenerator
    {
        /// <summary>
        /// Genera un script CREATE TABLE para una clase concreta.
        /// Usa un mapeo simple C# -> SQL Server.
        /// </summary>
        public static string GenerateCreateTable(
            Dictionary<string, CsClassInfo> classes,
            string className,
            string schema = "dbo")
        {
            if (!classes.TryGetValue(className, out var cls))
                throw new InvalidOperationException($"No se encontró la clase '{className}'.");

            var sb = new StringBuilder();

            var tableName = className; // podrías aplicar diferente naming convention aquí
            sb.AppendLine($"CREATE TABLE [{schema}].[{tableName}]");
            sb.AppendLine("(");

            var props = cls.Properties.ToList();

            for (int i = 0; i < props.Count; i++)
            {
                var kv = props[i];
                var propName = kv.Key;
                var csType = kv.Value;

                bool isNullable = csType.EndsWith("?", StringComparison.Ordinal);

                var sqlType = SqlTypeMapper.CSharpToSql(csType, isNullable);

                sb.Append($"    [{propName}] {sqlType}");

                if (i < props.Count - 1)
                    sb.Append(",");

                sb.AppendLine();
            }

            sb.AppendLine(");");

            return sb.ToString();
        }
    }
}
