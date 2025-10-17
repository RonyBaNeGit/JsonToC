using System.Collections.Generic;
using System.Linq;

namespace ConvertirJsonClaseC_.Core
{
    /// <summary>
    /// A partir de código C# existente:
    /// - Detecta namespace y clases públicas con sus propiedades.
    /// - Genera XML docs (clase, constructor, propiedades).
    /// - Inserta inicializaciones en el constructor (List<> y tipos de clase).
    /// - Opcionalmente usa IA para redactar descripciones.
    /// </summary>
    public static class CommentFixer
    {
        public static string FixComments(
            string csharpSource,
            string author,          // "Nombre (ID)"
            bool useAiDescriptions  // true = IA, false = fallback
        )
        {
            // 1) Parsear clases y namespace
            var classesMap = CsModelParser.ParseClasses(csharpSource);
            var ns = CsNamespaceHelper.ExtractNamespace(csharpSource) ?? "MyCompany.Domain.Models";

            // 2) Convertir al modelo común (ClassDef) que entiende el CodeGenerator
            var classDefs = ToClassDefs(classesMap);

            // 3) IA o fallback para descripciones
            if (useAiDescriptions)
                AiService.PopulateDescriptionsAsync(classDefs).GetAwaiter().GetResult();
            else
                AiService.PopulateFallbackDescriptions(classDefs);

            // 4) Generar C# completo (una clase por bloque, con encabezado XML, props y ctor con inits)
            return CodeGenerator.GenerateClasses(ns, author, classDefs);
        }

        private static List<ClassDef> ToClassDefs(Dictionary<string, CsClassInfo> map)
        {
            var list = new List<ClassDef>();

            foreach (var kv in map)
            {
                var cls = new ClassDef
                {
                    ClassName = kv.Key,
                    Properties = kv.Value.Properties
                        .Select(p => new PropertyDef
                        {
                            Name = p.Key,
                            Type = p.Value
                        })
                        .ToList()
                };

                list.Add(cls);
            }

            return list;
        }
    }
}
