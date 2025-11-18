using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConvertirJsonClaseC_.Core
{
    public class DescriptionGlossaryData
    {
        /// <summary>
        /// Descripciones de clases: ClassName -> Summary.
        /// </summary>
        public Dictionary<string, string> ClassDescriptions { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Descripciones de propiedades por clase: "ClassName.PropertyName" -> Summary.
        /// </summary>
        public Dictionary<string, string> PropertyDescriptions { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Descripciones genéricas por nombre de propiedad: "PropertyName" -> Summary.
        /// Útil para cosas repetidas como UserId, CreatedDate, etc.
        /// </summary>
        public Dictionary<string, string> GenericPropertyDescriptions { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Glosario persistente de descripciones para clases y propiedades,
    /// almacenado en AppData\ConvertirJsonClaseC_\glossary.json.
    /// </summary>
    public static class DescriptionGlossary
    {
        public static string GetGlossaryDirectory()
        {
            return GlossaryDir;
        }

        public static string GetGlossaryFilePath()
        {
            return GlossaryPath;
        }

        private static readonly string GlossaryDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ConvertirJsonClaseC_");

        private static readonly string GlossaryPath =
            Path.Combine(GlossaryDir, "glossary.json");

        private static readonly object _lock = new();
        private static DescriptionGlossaryData _data = new();

        static DescriptionGlossary()
        {
            Load();
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(GlossaryPath))
                {
                    _data = new DescriptionGlossaryData();

                    // Crear carpeta si no existe
                    if (!Directory.Exists(GlossaryDir))
                        Directory.CreateDirectory(GlossaryDir);

                    // Crear archivo vacío inicial
                    var json = JsonSerializer.Serialize(
                        _data,
                        new JsonSerializerOptions { WriteIndented = true });

                    File.WriteAllText(GlossaryPath, json);

                    return;
                }

                var content = File.ReadAllText(GlossaryPath);
                var data = JsonSerializer.Deserialize<DescriptionGlossaryData>(content);

                _data = data ?? new DescriptionGlossaryData();
            }
            catch
            {
                _data = new DescriptionGlossaryData();
            }
        }


        private static void Save()
        {
            try
            {
                if (!Directory.Exists(GlossaryDir))
                    Directory.CreateDirectory(GlossaryDir);

                var json = JsonSerializer.Serialize(
                    _data,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(GlossaryPath, json);
            }
            catch
            {
                // No rompemos la app si no se puede guardar
            }
        }

        public static bool TryGetClassDescription(string className, out string description)
        {
            lock (_lock)
            {
                return _data.ClassDescriptions.TryGetValue(className, out description!);
            }
        }

        public static void AddOrUpdateClassDescription(string className, string description)
        {
            if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(description))
                return;

            lock (_lock)
            {
                _data.ClassDescriptions[className] = description.Trim();
                Save();
            }
        }

        /// <summary>
        /// Busca primero descripción específica de clase+propiedad,
        /// luego por nombre genérico de propiedad.
        /// </summary>
        public static bool TryGetPropertyDescription(string className, string propertyName, out string description)
        {
            description = string.Empty;
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            var key = BuildPropertyKey(className, propertyName);

            lock (_lock)
            {
                // 1) Específica de la clase
                if (!string.IsNullOrWhiteSpace(className) &&
                    _data.PropertyDescriptions.TryGetValue(key, out description!))
                {
                    return true;
                }

                // 2) Genérica por nombre de propiedad
                if (_data.GenericPropertyDescriptions.TryGetValue(propertyName, out description!))
                {
                    return true;
                }
            }

            return false;
        }

        public static void AddOrUpdatePropertyDescription(string className, string propertyName, string description)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(description))
                return;

            var key = BuildPropertyKey(className, propertyName);

            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(className))
                {
                    _data.PropertyDescriptions[key] = description.Trim();
                }

                // Siempre también alimentamos el genérico por nombre,
                // para reutilizar entre distintas clases.
                _data.GenericPropertyDescriptions[propertyName] = description.Trim();

                Save();
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> GetGenericPropertyEntries()
        {
            lock (_lock)
            {
                // Devolvemos una copia para no exponer la colección interna
                return _data.GenericPropertyDescriptions.ToList();
            }
        }

        public static void ReplaceGenericPropertyEntries(IEnumerable<(string PropertyName, string Description)> entries)
        {
            lock (_lock)
            {
                _data.GenericPropertyDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var e in entries)
                {
                    if (string.IsNullOrWhiteSpace(e.PropertyName) || string.IsNullOrWhiteSpace(e.Description))
                        continue;

                    var name = e.PropertyName.Trim();
                    var desc = e.Description.Trim();

                    if (name.Length == 0 || desc.Length == 0)
                        continue;

                    _data.GenericPropertyDescriptions[name] = desc;
                }

                Save();
            }
        }
        public static IEnumerable<KeyValuePair<string, string>> GetClassEntries()
        {
            lock (_lock)
            {
                return _data.ClassDescriptions.ToList();
            }
        }

        public static void ReplaceClassEntries(IEnumerable<(string ClassName, string Description)> entries)
        {
            lock (_lock)
            {
                _data.ClassDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var e in entries)
                {
                    if (string.IsNullOrWhiteSpace(e.ClassName) || string.IsNullOrWhiteSpace(e.Description))
                        continue;

                    var name = e.ClassName.Trim();
                    var desc = e.Description.Trim();

                    if (name.Length == 0 || desc.Length == 0)
                        continue;

                    _data.ClassDescriptions[name] = desc;
                }

                Save();
            }
        }



        private static string BuildPropertyKey(string className, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(className))
                return propertyName;

            return $"{className}.{propertyName}";
        }


    }
}
