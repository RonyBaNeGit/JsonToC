// Core/SqlTypeMapper.cs
using System;

namespace ConvertirJsonClaseC_.Core
{
    public static class SqlTypeMapper
    {
        public static string SqlToCSharp(string sqlType, bool isNullable)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                return "string";

            var t = sqlType.Trim().ToLowerInvariant();

            // Quitar tamaño/precisión: varchar(50) -> varchar
            var paren = t.IndexOf('(');
            if (paren >= 0)
                t = t.Substring(0, paren);

            string result = t switch
            {
                "bigint" => "long",
                "int" => "int",
                "smallint" => "short",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" or "numeric"
                    or "money" or "smallmoney" => "decimal",
                "float" or "real" => "double",
                "datetime" or "datetime2"
                    or "smalldatetime"
                    or "date" or "time" => "DateTime",
                "uniqueidentifier" => "Guid",
                "varbinary" or "binary"
                    or "image" => "byte[]",

                // Casi todo lo demás lo tratamos como texto
                "varchar" or "nvarchar"
                    or "char" or "nchar"
                    or "text" or "ntext"
                    or "xml" or "json" => "string",

                _ => "string"
            };

            if (result == "string" || result == "byte[]")
                return result; // referencia: nulabilidad no se marca con '?'

            return isNullable ? result + "?" : result;
        }

        public static string CSharpToSql(string csType, bool isNullable)
        {
            if (string.IsNullOrWhiteSpace(csType))
                return "NVARCHAR(MAX)";

            var t = csType.Trim();

            // quitar '?'
            var isNullableType = t.EndsWith("?");
            if (isNullableType)
                t = t[..^1];

            t = t.ToLowerInvariant();

            string sqlType = t switch
            {
                "int" => "INT",
                "long" => "BIGINT",
                "short" => "SMALLINT",
                "byte" => "TINYINT",
                "bool" => "BIT",
                "decimal" => "DECIMAL(18, 2)",
                "double" => "FLOAT",
                "float" => "REAL",
                "datetime" => "DATETIME2",
                "guid" => "UNIQUEIDENTIFIER",
                "byte[]" => "VARBINARY(MAX)",
                "string" => "NVARCHAR(MAX)",
                _ => "NVARCHAR(MAX)"
            };

            // Para decidir NULL/NOT NULL usamos:
            // - Nullable value type (int?) -> NULL
            // - string/byte[] -> NULL por defecto
            bool nullableByType = (csType.EndsWith("?") || csType == "string" || csType == "string?" || csType == "byte[]" || csType == "byte[]?");
            bool finalNullable = isNullable || nullableByType;

            return sqlType + (finalNullable ? " NULL" : " NOT NULL");
        }
    }
}
