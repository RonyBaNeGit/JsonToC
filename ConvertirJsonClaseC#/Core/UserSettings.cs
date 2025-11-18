using System;
using System.IO;
using System.Text.Json;

namespace ConvertirJsonClaseC_.Core
{
    public class UserSettings
    {
        public string PoeApiKey { get; set; } = "";
    }

    public static class UserSettingsStore
    {
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ConvertirJsonClaseC_");

        private static readonly string SettingsPath =
            Path.Combine(SettingsDir, "userSettings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new UserSettings();

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                return settings ?? new UserSettings();
            }
            catch
            {
                // Si algo falla, no rompemos la app: devolvemos defaults.
                return new UserSettings();
            }
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                var json = JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Podrías loguear si quieres, pero no tires excepción a la UI.
            }
        }
    }
}
