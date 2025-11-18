using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ConvertirJsonClaseC_.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Win32;
using System.Text.Json;

namespace ConvertirJsonClaseC_.UI
{
    public partial class MainWindow : Window
    {
        // JSON -> C#
        private string? _selectedFilePath;

        // C# -> JSON
        private string? _selectedCsPath;

        // TAB 3: Fix comentarios
        private string? _selectedFixPath;

        public MainWindow()
        {
            InitializeComponent();

            // Tab 1
            BtnCopy.IsEnabled = false;
            SelectedFileLabel.Text = string.Empty;
            StatusText.Text = string.Empty;

            // Tab 2
            BtnCopyJson.IsEnabled = false;
            SelectedCsLabel.Text = string.Empty;
            StatusTextCs.Text = string.Empty;

            // Tab 3 (por defecto texto de estado vacío)
            FixStatusText.Text = string.Empty;

            // ==== Nueva sección: cargar configuración de usuario para POE ====
            try
            {
                var userSettings = UserSettingsStore.Load();

                if (!string.IsNullOrWhiteSpace(userSettings.PoeApiKey))
                {
                    PoeApiKeyBox.Password = userSettings.PoeApiKey;
                    PoeConfigStatus.Text = "API key configurada.";
                }
                else
                {
                    PoeConfigStatus.Text = "No hay API key configurada. Se usará la solución local (si está habilitada).";
                }
            }
            catch
            {
                PoeConfigStatus.Text = "No se pudo leer la configuración de usuario.";
            }
        }

        // Overlay reutilizable
        private void SetBusy(string message, bool busy)
        {
            BusyText.Text = message;
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================== Helpers de validación ==================

        private static bool IsValidJsonString(string? json, out string? error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "No se proporcionó contenido.";
                    return false;
                }
                using var _ = JsonDocument.Parse(json);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsValidCSharp(string code, out string? error)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var diags = tree.GetDiagnostics()
                                .Where(d => d.Severity == DiagnosticSeverity.Error)
                                .ToList();
                if (diags.Count == 0)
                {
                    error = null;
                    return true;
                }

                var d = diags[0];
                var span = d.Location.GetLineSpan();
                error = $"({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}) {d.GetMessage()}";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool ValidateRequired(params (TextBox box, string name)[] fields)
        {
            foreach (var (box, name) in fields)
            {
                if (box == null || string.IsNullOrWhiteSpace(box.Text))
                {
                    MessageBox.Show($"El campo \"{name}\" es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    box?.Focus();
                    return false;
                }
            }
            return true;
        }

        // ================== TAB 1: JSON -> C# ==================

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                JsonInput.Text = File.ReadAllText(_selectedFilePath);
                SelectedFileLabel.Text = $"Archivo: {System.IO.Path.GetFileName(_selectedFilePath)}";
                StatusText.Text = "JSON cargado desde archivo.";
            }
        }

        private void JsonInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedFilePath))
            {
                _selectedFilePath = null;
                SelectedFileLabel.Text = string.Empty;
                StatusText.Text = "Edición manual detectada.";
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedFilePath = null;
            JsonInput.Clear();
            SelectedFileLabel.Text = string.Empty;
            StatusText.Text = "Campo JSON limpiado.";
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ✅ Ahora ID también es obligatorio
                if (!ValidateRequired(
                        (NamespaceBox, "Namespace"),
                        (ClassNameBox, "Class name"),
                        (AuthorNameBox, "Autor"),
                        (AuthorIdBox, "ID")))
                    return;

                var ns = NamespaceBox.Text.Trim();
                var className = ClassNameBox.Text.Trim();
                var authorName = AuthorNameBox.Text.Trim();
                var authorId = AuthorIdBox.Text.Trim();
                var author = string.IsNullOrWhiteSpace(authorId) ? authorName : $"{authorName} ({authorId})";

                var json = _selectedFilePath != null ? File.ReadAllText(_selectedFilePath) : JsonInput.Text;

                if (!IsValidJsonString(json, out var jsonErr))
                {
                    StatusText.Text = $"JSON inválido: {jsonErr}";
                    MessageBox.Show($"JSON inválido:\n{jsonErr}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SetBusy("Procesando JSON → C#…", true);
                StatusText.Text = "Procesando…";
                OutputCode.Clear();
                BtnCopy.IsEnabled = false;

                var classes = await Task.Run(() => JsonSchemaParser.FromSample(json!, className));

                if (UseAiDescriptions.IsChecked == true)
                    await AiService.PopulateDescriptionsAsync(classes);
                else
                    AiService.PopulateFallbackDescriptions(classes);

                var code = CodeGenerator.GenerateClasses(ns, author, classes);
                OutputCode.Text = code;

                BtnCopy.IsEnabled = !string.IsNullOrWhiteSpace(code);
                StatusText.Text = "Listo ✔";
            }
            catch (Exception ex)
            {
                OutputCode.Text = $"// Error: {ex.Message}\r\n// {ex}";
                BtnCopy.IsEnabled = false;
                StatusText.Text = "Error ✖";
            }
            finally
            {
                SetBusy("Procesando…", false);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(OutputCode.Text))
            {
                Clipboard.SetText(OutputCode.Text);
                StatusText.Text = "Copiado al portapapeles.";
            }
        }

        private void OutputCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            BtnCopy.IsEnabled = !string.IsNullOrWhiteSpace(OutputCode.Text);
        }

        // ================== TAB 2: C# -> JSON ==================

        private void BtnImportCs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _selectedCsPath = dlg.FileName;
                CsInput.Text = File.ReadAllText(_selectedCsPath);
                SelectedCsLabel.Text = $"Archivo: {System.IO.Path.GetFileName(_selectedCsPath)}";
                StatusTextCs.Text = "Código C# cargado desde archivo.";
            }
        }

        private void BtnClearCs_Click(object sender, RoutedEventArgs e)
        {
            _selectedCsPath = null;
            CsInput.Clear();
            SelectedCsLabel.Text = string.Empty;
            StatusTextCs.Text = "Campo C# limpiado.";
        }

        private void BtnGenerateJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rootName = string.IsNullOrWhiteSpace(RootClassBox.Text) ? "Root" : RootClassBox.Text.Trim();

                var csharp = _selectedCsPath != null ? File.ReadAllText(_selectedCsPath) : CsInput.Text;
                if (string.IsNullOrWhiteSpace(csharp))
                {
                    StatusTextCs.Text = "Proporciona una clase C#.";
                    return;
                }

                if (!IsValidCSharp(csharp, out var csErr))
                {
                    StatusTextCs.Text = $"C# inválido: {csErr}";
                    MessageBox.Show($"C# inválido:\n{csErr}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SetBusy("Procesando C# → JSON…", true);

                StatusTextCs.Text = "Procesando…";
                JsonOutput.Clear();
                BtnCopyJson.IsEnabled = false;

                var classes = CsModelParser.ParseClasses(csharp);
                var pretty = PrettyJson.IsChecked == true;
                var json = ClassToJsonGenerator.GenerateSampleJson(classes, rootName, pretty);

                JsonOutput.Text = json;
                BtnCopyJson.IsEnabled = !string.IsNullOrWhiteSpace(json);
                StatusTextCs.Text = "Listo ✔";
            }
            catch (Exception ex)
            {
                JsonOutput.Text = $"// Error: {ex.Message}\r\n// {ex}";
                BtnCopyJson.IsEnabled = false;
                StatusTextCs.Text = "Error ✖";
            }
            finally
            {
                SetBusy("Procesando…", false);
            }
        }

        private void BtnCopyJson_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(JsonOutput.Text))
            {
                Clipboard.SetText(JsonOutput.Text);
                StatusTextCs.Text = "JSON copiado al portapapeles.";
            }
        }

        // ============== TAB 3: Arreglar comentarios (C#) ==============

        private void BtnImportFix_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _selectedFixPath = dlg.FileName;
                FixInput.Text = File.ReadAllText(_selectedFixPath);
                SelectedFixLabel.Text = $"Archivo: {System.IO.Path.GetFileName(_selectedFixPath)}";
                FixStatusText.Text = "Código C# cargado desde archivo.";
            }
        }

        private void BtnClearFix_Click(object sender, RoutedEventArgs e)
        {
            _selectedFixPath = null;
            FixInput.Clear();
            SelectedFixLabel.Text = string.Empty;
            FixStatusText.Text = "Campo C# limpiado.";
        }

        private async void BtnFixComments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ✅ Ahora ID también es obligatorio
                if (!ValidateRequired(
                        (FixAuthorNameBox, "Autor"),
                        (FixAuthorIdBox, "ID")))
                    return;

                SetBusy("Arreglando comentarios…", true);

                FixStatusText.Text = "Procesando…";
                FixOutput.Clear();
                BtnCopyFix.IsEnabled = false;

                var authorName = FixAuthorNameBox.Text.Trim();
                var authorId = FixAuthorIdBox.Text.Trim();
                var author = string.IsNullOrWhiteSpace(authorId) ? authorName : $"{authorName} ({authorId})";

                var source = _selectedFixPath != null ? File.ReadAllText(_selectedFixPath) : FixInput.Text;
                if (string.IsNullOrWhiteSpace(source))
                {
                    FixStatusText.Text = "Proporciona una clase C#.";
                    return;
                }

                var useAi = FixUseAi.IsChecked == true;

                var fixedCode = await Task.Run(() =>
                    CommentFixer.FixComments(source, author, useAi)
                );

                FixOutput.Text = fixedCode;
                BtnCopyFix.IsEnabled = !string.IsNullOrWhiteSpace(fixedCode);
                FixStatusText.Text = "Listo ✔";
            }
            catch (Exception ex)
            {
                FixOutput.Text = $"// Error: {ex.Message}\r\n// {ex}";
                BtnCopyFix.IsEnabled = false;
                FixStatusText.Text = "Error ✖";
            }
            finally
            {
                SetBusy("Arreglando comentarios…", false);
            }
        }

        private void BtnCopyFix_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FixOutput.Text))
            {
                Clipboard.SetText(FixOutput.Text);
                FixStatusText.Text = "Código copiado al portapapeles.";
            }
        }

        // ============== TAB 4: Configuración (POE) ==============

        private void BtnSavePoeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var key = PoeApiKeyBox.Password?.Trim() ?? string.Empty;

                var settings = UserSettingsStore.Load();
                settings.PoeApiKey = key;
                UserSettingsStore.Save(settings);

                // Recargar AiService para que tome la nueva key (o la deje vacía)
                AiService.ReloadConfig();

                PoeConfigStatus.Text = string.IsNullOrWhiteSpace(key)
                    ? "Se guardó una API key vacía. No se usará Poe; se intentará usar la solución local."
                    : "API key guardada correctamente. Poe está habilitado.";
            }
            catch (Exception ex)
            {
                PoeConfigStatus.Text = "Error al guardar la API key.";
                MessageBox.Show(
                    $"Ocurrió un error al guardar la configuración:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
