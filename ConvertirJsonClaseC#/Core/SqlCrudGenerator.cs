using System;
using System.Linq;
using System.Text;
using static ConvertirJsonClaseC_.Core.SqlCreateTableParser;

namespace ConvertirJsonClaseC_.Core
{
    public static class SqlCrudGenerator
    {
        /// <summary>
        /// A partir de un script CREATE TABLE genera SPs CRUD básicos.
        /// </summary>
        public static string GenerateCrudFromCreateTable(string sql, string? defaultSchema = "dbo")
        {
            var tableInfo = SqlCreateTableParser.ParseTableInfo(sql, defaultSchema);
            return GenerateCrudFromTableInfo(tableInfo);
        }

        /// <summary>
        /// Genera:
        /// - sp_[Tabla]_Insert
        /// - sp_[Tabla]_GetById
        /// - sp_[Tabla]_GetAll
        /// - sp_[Tabla]_Update
        /// - sp_[Tabla]_Delete
        /// </summary>
        public static string GenerateCrudFromTableInfo(SqlTableInfo table)
        {
            if (table.Columns.Count == 0)
                throw new InvalidOperationException("La tabla no tiene columnas.");

            var schema = string.IsNullOrWhiteSpace(table.Schema) ? "dbo" : table.Schema;
            var tableName = table.TableName;
            var fullTableName = $"[{schema}].[{tableName}]";

            // PK: primero que marque IsPrimaryKey o, si no hay, la primera columna
            var pk = table.Columns.FirstOrDefault(c => c.IsPrimaryKey)
                     ?? table.Columns.First();

            var nonIdentityCols = table.Columns.Where(c => !c.IsIdentity).ToList();
            var nonPkCols = table.Columns.Where(c => !string.Equals(c.Name, pk.Name, StringComparison.OrdinalIgnoreCase)).ToList();

            var sb = new StringBuilder();

            // ==== INSERT ====
            sb.AppendLine($"-- INSERT {tableName}");
            sb.AppendLine($"CREATE PROCEDURE [{schema}].[sp_{tableName}_Insert]");

            for (int i = 0; i < nonIdentityCols.Count; i++)
            {
                var col = nonIdentityCols[i];
                var sep = (i == nonIdentityCols.Count - 1) ? "" : ",";
                sb.AppendLine($"    @{col.Name} {col.SqlType}{sep}");
            }

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");

            var insertColList = string.Join(", ",
                nonIdentityCols.Select(c => $"[{c.Name}]"));
            var insertParamList = string.Join(", ",
                nonIdentityCols.Select(c => $"@{c.Name}"));

            sb.AppendLine();
            sb.AppendLine($"    INSERT INTO {fullTableName} ({insertColList})");
            sb.AppendLine($"    VALUES ({insertParamList});");

            if (pk.IsIdentity)
            {
                sb.AppendLine();
                sb.AppendLine("    SELECT SCOPE_IDENTITY() AS NewId;");
            }

            sb.AppendLine("END");
            sb.AppendLine("GO");
            sb.AppendLine();

            // ==== GET BY ID ====
            sb.AppendLine($"-- GET BY ID {tableName}");
            sb.AppendLine($"CREATE PROCEDURE [{schema}].[sp_{tableName}_GetById]");
            sb.AppendLine($"    @{pk.Name} {pk.SqlType}");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();
            sb.AppendLine($"    SELECT");

            var allColsSelect = string.Join(",\r\n        ",
                table.Columns.Select(c => $"[{c.Name}]"));

            sb.AppendLine($"        {allColsSelect}");
            sb.AppendLine($"    FROM {fullTableName}");
            sb.AppendLine($"    WHERE [{pk.Name}] = @{pk.Name};");
            sb.AppendLine("END");
            sb.AppendLine("GO");
            sb.AppendLine();

            // ==== GET ALL ====
            sb.AppendLine($"-- GET ALL {tableName}");
            sb.AppendLine($"CREATE PROCEDURE [{schema}].[sp_{tableName}_GetAll]");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();
            sb.AppendLine($"    SELECT");
            sb.AppendLine($"        {allColsSelect}");
            sb.AppendLine($"    FROM {fullTableName};");
            sb.AppendLine("END");
            sb.AppendLine("GO");
            sb.AppendLine();

            // ==== UPDATE ====
            sb.AppendLine($"-- UPDATE {tableName}");
            sb.AppendLine($"CREATE PROCEDURE [{schema}].[sp_{tableName}_Update]");

            // parámetros = todas las columnas (incluyendo PK)
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                var sep = (i == table.Columns.Count - 1) ? "" : ",";
                sb.AppendLine($"    @{col.Name} {col.SqlType}{sep}");
            }

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();

            var setList = string.Join(",\r\n        ",
                nonPkCols.Select(c => $"[{c.Name}] = @{c.Name}"));

            sb.AppendLine($"    UPDATE {fullTableName}");
            sb.AppendLine("    SET");
            sb.AppendLine($"        {setList}");
            sb.AppendLine($"    WHERE [{pk.Name}] = @{pk.Name};");
            sb.AppendLine("END");
            sb.AppendLine("GO");
            sb.AppendLine();

            // ==== DELETE ====
            sb.AppendLine($"-- DELETE {tableName}");
            sb.AppendLine($"CREATE PROCEDURE [{schema}].[sp_{tableName}_Delete]");
            sb.AppendLine($"    @{pk.Name} {pk.SqlType}");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine();
            sb.AppendLine($"    DELETE FROM {fullTableName}");
            sb.AppendLine($"    WHERE [{pk.Name}] = @{pk.Name};");
            sb.AppendLine("END");
            sb.AppendLine("GO");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
