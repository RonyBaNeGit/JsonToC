using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConvertirJsonClaseC_.Core
{
    /// <summary>
    /// Analiza un código fuente C# real (con namespaces, usings, regiones, etc.)
    /// y extrae todas las clases públicas y sus propiedades públicas.
    /// </summary>
    public static class CsModelParser
    {
        /// <summary>
        /// Analiza el código C# y devuelve un mapa de clases con sus propiedades.
        /// </summary>
        public static Dictionary<string, CsClassInfo> ParseClasses(string csharpSource)
        {
            if (string.IsNullOrWhiteSpace(csharpSource))
                throw new System.ArgumentException("El código C# está vacío.");

            var tree = CSharpSyntaxTree.ParseText(csharpSource);
            var root = tree.GetCompilationUnitRoot();

            // Tomamos todas las clases públicas dentro de namespaces o fuera de ellos
            var classDecls = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                .ToList();

            if (classDecls.Count == 0)
                throw new System.InvalidOperationException("No se encontraron clases públicas en el código C#.");

            var map = new Dictionary<string, CsClassInfo>();

            foreach (var cls in classDecls)
            {
                var className = cls.Identifier.ValueText.Trim();
                if (string.IsNullOrEmpty(className))
                    continue;

                var info = new CsClassInfo { Name = className };

                // Propiedades públicas de autoimplementación o normales
                var props = cls.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));

                foreach (var prop in props)
                {
                    var propName = prop.Identifier.ValueText.Trim();
                    if (string.IsNullOrEmpty(propName))
                        continue;

                    // Obtenemos el tipo y lo normalizamos conservando nulabilidad (NO quitamos '?')
                    var typeName = CleanType(prop.Type.ToString());

                    info.Properties[propName] = typeName;
                }

                map[className] = info;
            }

            return map;
        }

        /// <summary>
        /// Limpia el nombre del tipo, eliminando calificaciones del sistema pero conservando '?'
        /// </summary>
        private static string CleanType(string rawType)
        {
            if (string.IsNullOrWhiteSpace(rawType))
                return "object";

            var cleaned = rawType
                .Replace("global::", "")
                .Replace("System.Collections.Generic.", "")
                .Replace("System.", "")
                .Trim();

            // Conservamos '?' para respetar tipos nullable ya declarados (ej. int?, DateTime?, string?)
            return cleaned;
        }
    }
}
