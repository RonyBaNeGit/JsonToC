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
              string rootClassName,
              string schema = "dbo")
        {
            if (!classes.TryGetValue(rootClassName, out var root))
                throw new InvalidOperationException($"No se encontró la clase raíz '{rootClassName}'.");

            if (root.Properties.Count == 0)
                throw new InvalidOperationException("La clase raíz no tiene propiedades públicas.");

            var sb = new StringBuilder();

            var finalSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
            var tableName = root.Name;
            var fullTableName = $"[{finalSchema}].[{tableName}]";

            sb.AppendLine($"CREATE TABLE {fullTableName}");
            sb.AppendLine("(");

            bool identityAssigned = false;
            int index = 0;

            foreach (var kv in root.Properties)
            {
                var propName = kv.Key;
                var csType = kv.Value;

                // Usamos el mapper que ya modificamos (VARCHAR(100) y NOT NULL)
                var baseSqlType = SqlTypeMapper.CSharpToSql(csType, isNullable: false);

                var line = new StringBuilder();
                line.Append("    [").Append(propName).Append("] ").Append(baseSqlType);

                // Regla: primer campo llamado Id/ID -> IDENTITY(1,1) UNIQUE
                if (!identityAssigned &&
                    string.Equals(propName, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    line.Append(" IDENTITY(1,1) PRIMARY KEY UNIQUE");
                    identityAssigned = true;
                }

                // coma si no es la última columna
                if (index < root.Properties.Count - 1)
                    line.Append(",");

                sb.AppendLine(line.ToString());
                index++;
            }

            sb.AppendLine(");");

            return sb.ToString();
        }
    }
}
