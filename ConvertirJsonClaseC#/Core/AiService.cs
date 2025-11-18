namespace ConvertirJsonClaseC_.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Servicio para generar descripciones con IA (Local HF + POE) para clases y propiedades.
    /// Flujo: Local (si está habilitado) → POE → fallback.
    /// </summary>
    public static class AiService
    {
        // ===== Config =====
        private static PoeConfig _poe = new();
        private static LocalConfig _local = new();

        // Para diagnóstico
        private static bool _configLoaded;
        private static string? _lastConfigError;

        // ===== HTTP =====
        private static readonly HttpClient _httpPoe = new();
        private static readonly HttpClient _httpLocal = new();

        private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

        // ===== Static ctor =====
        static AiService()
        {
            SafeLoadConfig();
            ConfigureHttpClients();
        }

        // ------- Configuración robusta -------
        private static void SafeLoadConfig()
        {
            try
            {
                LoadConfigInternal();
                _configLoaded = true;
                _lastConfigError = null;
            }
            catch (Exception ex)
            {
                _configLoaded = false;
                _lastConfigError = ex.Message;
#if DEBUG
                Debug.WriteLine($"[AiService] Error al cargar config: {ex}");
#endif
                // Mantener defaults
                _poe ??= new PoeConfig();
                _local ??= new LocalConfig();
            }
        }

        /// <summary>Vuelve a leer appsettings.json y reconfigura los HttpClient.</summary>
        public static void ReloadConfig()
        {
            SafeLoadConfig();
            ConfigureHttpClients();
        }

        /// <summary>Pequeño snapshot para diagnosticar.</summary>
        public static string GetDebugConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ConfigLoaded: {_configLoaded}");
            if (_lastConfigError != null) sb.AppendLine($"LastConfigError: {_lastConfigError}");

            sb.AppendLine("[POE]");
            sb.AppendLine($"  BaseUrl:  {_poe.BaseUrl}");
            sb.AppendLine($"  Model:    {_poe.Model}");
            sb.AppendLine($"  ApiKey?:  {!string.IsNullOrWhiteSpace(_poe.ApiKey)}");
            sb.AppendLine($"  Temp:     {_poe.Temperature}");
            sb.AppendLine($"  MaxTokens:{_poe.MaxTokens}");

            sb.AppendLine("[LOCAL]");
            sb.AppendLine($"  Enabled:      {_local.Enabled}");
            sb.AppendLine($"  PreferFirst:  {_local.PreferLocalFirst}");
            sb.AppendLine($"  BaseUrl:      {_local.BaseUrl}");
            sb.AppendLine($"  Endpoint:     {_local.DescribeEndpoint}");
            return sb.ToString();
        }

        private static void LoadConfigInternal()
        {
            // Resolver ruta del appsettings.json
            string? path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                var wd = Directory.GetCurrentDirectory();
                var alt = Path.Combine(wd, "appsettings.json");
                if (File.Exists(alt)) path = alt;
            }

            if (!File.Exists(path))
            {
                Debug.WriteLine("[AiService] appsettings.json no encontrado. Usando defaults.");

                return;
            }

            var json = File.ReadAllText(path);

            var opts = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            using var doc = JsonDocument.Parse(json, opts);
            var root = doc.RootElement;

            // --- POE ---
            if (root.TryGetProperty("poe", out var poeSection) ||
                root.TryGetProperty("Poe", out poeSection))
            {
                // NOTA: dejamos apikey vacío en appsettings, el real vendrá de la configuración de usuario
                if (poeSection.TryGetProperty("baseurl", out var u) || poeSection.TryGetProperty("BaseUrl", out u))
                    _poe.BaseUrl = u.GetString() ?? _poe.BaseUrl;

                if (poeSection.TryGetProperty("model", out var m) || poeSection.TryGetProperty("Model", out m))
                    _poe.Model = m.GetString() ?? _poe.Model;

                if (poeSection.TryGetProperty("temperature", out var t) || poeSection.TryGetProperty("Temperature", out t))
                    _poe.Temperature = t.GetDouble();

                if (poeSection.TryGetProperty("maxtokens", out var mt) || poeSection.TryGetProperty("MaxTokens", out mt))
                    _poe.MaxTokens = mt.GetInt32();

                // apikey se deja como está (_poe.ApiKey se llenará desde la configuración de usuario)
            }

            // --- LOCAL ---
            if (root.TryGetProperty("Local", out var local))
            {
                _local.BaseUrl = local.TryGetProperty("BaseUrl", out var u) ? (u.GetString() ?? _local.BaseUrl) : _local.BaseUrl;
                _local.DescribeEndpoint = local.TryGetProperty("DescribeEndpoint", out var e) ? (e.GetString() ?? _local.DescribeEndpoint) : _local.DescribeEndpoint;
                _local.Enabled = local.TryGetProperty("Enabled", out var en) && en.GetBoolean();
                _local.PreferLocalFirst = local.TryGetProperty("PreferLocalFirst", out var pf) && pf.GetBoolean();
            }

            // Normalizaciones
            _poe.BaseUrl = NormalizeBase(_poe.BaseUrl, "https://api.poe.com");
            _local.BaseUrl = NormalizeBase(_local.BaseUrl, "http://127.0.0.1:8000");
            _local.DescribeEndpoint = NormalizePath(_local.DescribeEndpoint, "/describe_class");

            // --- OVERRIDE con configuración de usuario (campo de configuración de tu herramienta) ---
            try
            {
                var userSettings = UserSettingsStore.Load();
                if (!string.IsNullOrWhiteSpace(userSettings.PoeApiKey))
                {
                    _poe.ApiKey = userSettings.PoeApiKey;
                }
                else
                {
                    // Si NO hay API key de usuario, no usamos Poe
                    _poe.ApiKey = string.Empty;
                }
            }
            catch
            {
                _poe.ApiKey = string.Empty;
            }
        }


        private static string NormalizeBase(string? baseUrl, string fallback)
        {
            var url = string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl!.Trim();
            if (url.EndsWith("/")) url = url.TrimEnd('/');
            return url;
        }

        private static string NormalizePath(string? path, string fallback)
        {
            var p = string.IsNullOrWhiteSpace(path) ? fallback : path!.Trim();
            if (!p.StartsWith("/")) p = "/" + p;
            return p;
        }

        private static void ConfigureHttpClients()
        {
            // POE
            _httpPoe.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrWhiteSpace(_poe.BaseUrl))
                _httpPoe.BaseAddress = new Uri(_poe.BaseUrl);
            if (!string.IsNullOrWhiteSpace(_poe.ApiKey))
                _httpPoe.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _poe.ApiKey);
            _httpPoe.Timeout = TimeSpan.FromSeconds(60);

            // LOCAL
            if (!string.IsNullOrWhiteSpace(_local.BaseUrl))
                _httpLocal.BaseAddress = new Uri(_local.BaseUrl);
            _httpLocal.Timeout = TimeSpan.FromSeconds(30);
        }

        // ===== API pública =====
        public static async Task PopulateDescriptionsAsync(List<ClassDef> classes)
        {
            foreach (var c in classes)
            {
                // Clase
                // Clase
                var classPrompt =
                    $"Redacta una descripción breve, formal y clara en español para documentación XML de C#, " +
                    $"que describa la finalidad de la clase '{c.ClassName}'. " +
                    "La clase representa una entidad de dominio basada en datos JSON. " +
                    "No incluyas comillas ni markdown, responde solo con la descripción.";
                var rawClassDesc = await AskAsync(classPrompt);
                var sanitizedClassDesc = Sanitize(rawClassDesc);

                // Si la IA respondió algo raro (pregunta, instrucción, etc.), usamos fallback
                if (LooksLikeInstructionOrQuestion(sanitizedClassDesc))
                {
                    c.ClassSummary = "Representa una entidad de dominio basada en datos JSON.";
                }
                else
                {
                    c.ClassSummary = sanitizedClassDesc;
                }

                // Propiedades en batch local (prioridad local)
                var props = c.Properties.Select(p => (p.Name, p.Type)).ToList();
                IReadOnlyList<string> localBatch = Array.Empty<string>();

                if (_local.Enabled) // prioridad local
                    localBatch = await TryAskLocalPropsBatch(props, 96);

                if (localBatch.Count == props.Count)
                {
                    for (int i = 0; i < props.Count; i++)
                        c.Properties[i].Summary = Sanitize(localBatch[i]);
                }
                else
                {
                    // Si el batch falló: fallback propiedades una a una
                    foreach (var p in c.Properties)
                    {
                        var propPrompt =
                            $"Redacta una descripción breve (una línea), en español, para la propiedad '{p.Name}' (tipo: {p.Type}). " +
                            $"No uses comillas ni markdown.";
                        p.Summary = Sanitize(await AskAsync(propPrompt));
                    }
                }
            }
        }

        public static void PopulateFallbackDescriptions(List<ClassDef> classes)
        {
            foreach (var c in classes)
            {
                c.ClassSummary = "Representa una entidad de dominio basada en datos JSON.";
                foreach (var p in c.Properties)
                    p.Summary = $"Campo {p.Name} ({p.Type}).";
            }
        }

        // ===== Núcleo: Local → POE → fallback =====
        private static async Task<string> AskAsync(string userPrompt)
        {
            // Disponibilidad real
            bool poeAvailable = !string.IsNullOrWhiteSpace(_poe.ApiKey) && _httpPoe.BaseAddress is not null;
            bool localAvailable = _local.Enabled && _httpLocal.BaseAddress is not null;

            // Caso 1: NO hay Poe → solo local (si está disponible)
            if (!poeAvailable && localAvailable)
            {
                var rLocal = await TryAskLocal(userPrompt);
                if (!string.IsNullOrWhiteSpace(rLocal))
                    return rLocal;

                // Si local falla, hacemos fallback
                return "Descripción generada automáticamente.";
            }

            // Caso 2: hay Poe y también local
            if (poeAvailable && localAvailable)
            {
                if (_local.PreferLocalFirst)
                {
                    var rLocal = await TryAskLocal(userPrompt);
                    if (!string.IsNullOrWhiteSpace(rLocal))
                        return rLocal;

                    var rPoe = await TryAskPoe(userPrompt);
                    if (!string.IsNullOrWhiteSpace(rPoe))
                        return rPoe;
                }
                else
                {
                    var rPoe = await TryAskPoe(userPrompt);
                    if (!string.IsNullOrWhiteSpace(rPoe))
                        return rPoe;

                    var rLocal = await TryAskLocal(userPrompt);
                    if (!string.IsNullOrWhiteSpace(rLocal))
                        return rLocal;
                }

                return "Descripción generada automáticamente.";
            }

            // Caso 3: solo Poe disponible
            if (poeAvailable && !localAvailable)
            {
                var rPoe = await TryAskPoe(userPrompt);
                if (!string.IsNullOrWhiteSpace(rPoe))
                    return rPoe;

                return "Descripción generada automáticamente.";
            }

            // Caso 4: no hay ni Poe ni Local
            return "Descripción generada automáticamente.";
        }


        // ===== LOCAL =====
        private static async Task<string> TryAskLocal(string userPrompt)
        {
            try
            {
                if (!_local.Enabled || _httpLocal.BaseAddress is null)
                    return string.Empty;

                var payload = new { prompt = userPrompt, max_new_tokens = 96 };
                var json = JsonSerializer.Serialize(payload, _jsonOpts);
                using var req = new HttpRequestMessage(HttpMethod.Post, _local.DescribeEndpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var resp = await _httpLocal.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return string.Empty;

                var content = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("text", out var t))
                    return (t.GetString() ?? "").Trim();

                return string.Empty;
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[AiService] TryAskLocal error: {ex.Message}");
#endif
                return string.Empty;
            }
        }

        private static async Task<IReadOnlyList<string>> TryAskLocalPropsBatch(IEnumerable<(string Name, string CsType)> props, int maxNewTokens = 96)
        {
            try
            {
                if (!_local.Enabled || _httpLocal.BaseAddress is null) return Array.Empty<string>();

                var prompts = props.Select(p => $"{p.Name}|{p.CsType}").ToList();
                var payload = new { prompts, max_new_tokens = maxNewTokens };
                var json = JsonSerializer.Serialize(payload, _jsonOpts);

                using var req = new HttpRequestMessage(HttpMethod.Post, "/describe_properties")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                using var resp = await _httpLocal.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return Array.Empty<string>();

                var content = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("texts", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    return arr.EnumerateArray().Select(e => (e.GetString() ?? "").Trim()).ToList();
                }
                return Array.Empty<string>();
            }
            catch { return Array.Empty<string>(); }
        }

        // ===== POE =====
        private static async Task<string> TryAskPoe(string userPrompt)
        {
            try
            {
                if (_httpPoe.BaseAddress is null || string.IsNullOrWhiteSpace(_poe.ApiKey))
                    return string.Empty;

                var payload = new
                {
                    model = _poe.Model,
                    messages = new object[]
                    {
                        new {
                            role = "system",
                            content = "Eres un asistente que redacta descripciones técnicas en español para XML docs de C#. " +
                                      "No uses comillas ni markdown, no incluyas nombres de clase, responde en una o dos frases claras."
                        },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = _poe.Temperature,
                    max_tokens = _poe.MaxTokens,
                    stream = false
                };

                var json = JsonSerializer.Serialize(payload, _jsonOpts);
                using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var resp = await _httpPoe.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return string.Empty;

                var content = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var msg = doc.RootElement.GetProperty("choices")[0]
                                         .GetProperty("message")
                                         .GetProperty("content")
                                         .GetString();
                return (msg ?? "").Trim();
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[AiService] TryAskPoe error: {ex.Message}");
#endif
                return string.Empty;
            }
        }

        // ===== Utils =====
        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            // 1) Elimina nuestros tags si el modelo los ecoa
            s = System.Text.RegularExpressions.Regex.Replace(
                s,
                @"</?\s*(INST|RESP|CLASE|PROP|TIPO)\s*>",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // 2) Elimina cualquier otro tag HTML/XML residual <...>
            s = System.Text.RegularExpressions.Regex.Replace(
                s,
                "<[^>]+>",
                ""
            );

            // 3) Borra líneas que parecen instrucciones (en ES o EN)
            var banned = new[]
            {
        "Redacta ", "No incluyas", "No uses", "Contexto:", "Corrige", "Devuélveme",
        "Mantén", "Escribe ", "Create a description", "Write a", "Do not", "Don't"
    };
            var lines = new List<string>();
            foreach (var ln in s.Split('\n'))
            {
                var t = ln.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (banned.Any(b => t.StartsWith(b, StringComparison.OrdinalIgnoreCase))) continue;
                lines.Add(t);
            }
            s = string.Join(" ", lines);

            // 4) Limpieza básica
            s = s.Replace("///", "").Replace("*", "").Replace("`", "").Replace("#", "");
            // *** IMPORTANTE: NO conviertas < y > a paréntesis ***
            // s = s.Replace("<", "(").Replace(">", ")");

            // 5) Quédate con la primera oración corta (evita que se cuele ruido)
            var period = s.IndexOf('.');
            if (period > 0) s = s.Substring(0, period + 1);

            // Colapsa espacios
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s;
        }

        private static bool LooksLikeInstructionOrQuestion(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            var t = text.Trim();

            // Si huele a pregunta
            if (t.EndsWith("?"))
                return true;

            // Patrones típicos de “contra-prompt”
            var patterns = new[]
            {
        "necesito que me proporciones",
        "necesito que me indiques",
        "dime qué",
        "dime que",
        "indica qué",
        "proporciona el código",
        "proporciona el nombre",
        "por favor proporciona",
        "qué clase necesitas",
        "qué método necesitas",
        "qué propiedad necesitas",
        "provide the code",
        "please provide",
        "i need you to provide"
    };

            foreach (var p in patterns)
            {
                if (t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }



        private class PoeConfig
        {
            public string ApiKey { get; set; } = "";
            public string BaseUrl { get; set; } = "https://api.poe.com";
            public string Model { get; set; } = "Claude-Sonnet-4";
            public double Temperature { get; set; } = 0.2;
            public int MaxTokens { get; set; } = 200;
        }

        private class LocalConfig
        {
            public string BaseUrl { get; set; } = "http://172.21.103.77:8000";
            public string DescribeEndpoint { get; set; } = "/describe_class";
            public bool Enabled { get; set; } = true;
            public bool PreferLocalFirst { get; set; } = true;
        }
    }
}
