using Dalamud.Plugin.Services;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SocialMorpho.Services;

public sealed class TitleSyncService : IDisposable
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    private readonly Plugin plugin;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;
    private readonly Dictionary<string, SyncedTitleRecord> cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime nextPullAtUtc = DateTime.MinValue; // cloud pull schedule
    private DateTime nextHonorificPullAtUtc = DateTime.MinValue;
    private DateTime nextHonorificPushAtUtc = DateTime.MinValue;
    private DateTime nextPushAtUtc = DateTime.MinValue;
    private DateTime nextHonorificCheckAtUtc = DateTime.MinValue;
    private DateTime cloudUnavailableUntilUtc = DateTime.MinValue;
    private string lastPushedTitle = string.Empty;
    private string lastPushedColor = string.Empty;
    private string lastHonorificTitle = string.Empty;
    private string lastHonorificColor = string.Empty;
    private bool honorificBridgeActive;
    private int consecutiveCloudFailures;
    private bool pullInFlight;
    private bool pushInFlight;
    private DateTime lastPullErrorLogUtc = DateTime.MinValue;
    private DateTime lastPushErrorLogUtc = DateTime.MinValue;
    private DateTime lastHonorificWarnUtc = DateTime.MinValue;
    private DateTime lastHonorificInfoUtc = DateTime.MinValue;
    private DateTime lastHonorificCommandFallbackUtc = DateTime.MinValue;

    public TitleSyncService(Plugin plugin, IClientState clientState, IObjectTable objectTable, IPluginLog log)
    {
        this.plugin = plugin;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.log = log;
        this.clientState.Login += OnLogin;
    }

    public void Tick()
    {
        var cfg = this.plugin.Configuration;
        if (!cfg.EnableTitleSync)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now >= this.nextHonorificCheckAtUtc)
        {
            this.honorificBridgeActive = DetectHonorificBridgeAvailable();
            this.nextHonorificCheckAtUtc = now.AddSeconds(30);
        }

        if (cfg.ShareTitleSync && this.honorificBridgeActive && now >= this.nextHonorificPushAtUtc)
        {
            PushLocalTitleToHonorific();
            this.nextHonorificPushAtUtc = now.AddSeconds(20);
        }

        if (IsHonorificFallbackActive(cfg, now))
        {
            // Fallback remains active for reads and cloud-failure resilience.
        }
        
        // Always keep Honorific cache fresh when available.
        if (cfg.ShowSyncedTitles && this.honorificBridgeActive && now >= this.nextHonorificPullAtUtc)
        {
            PullHonorificTitlesIntoCache();
            this.nextHonorificPullAtUtc = now.AddSeconds(10);
        }

        if (cfg.ShareTitleSync && now >= this.nextPushAtUtc && !this.pushInFlight)
        {
            _ = PushLocalTitleAsync();
            this.nextPushAtUtc = now.AddMinutes(2);
        }

        if (cfg.ShowSyncedTitles && now >= this.nextPullAtUtc && !this.pullInFlight)
        {
            _ = PullNearbyTitlesAsync();
            this.nextPullAtUtc = now.AddSeconds(30);
        }
    }

    public bool TryGetSyncedForGameObjectId(ulong gameObjectId, out SyncedTitleRecord record)
    {
        record = default!;
        var characterOnlyKey = TryBuildCharacterOnlyKeyForGameObject(gameObjectId);
        var key = TryBuildKeyForGameObject(gameObjectId);
        if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(characterOnlyKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(key) && this.cache.TryGetValue(key, out record!))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(characterOnlyKey) && this.cache.TryGetValue(characterOnlyKey, out record!))
        {
            return true;
        }

        return false;
    }

    public bool TryGetSyncedForCharacter(string characterName, out SyncedTitleRecord record)
    {
        record = default!;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        var charOnlyKey = $"title:{characterName.Trim().ToLowerInvariant()}";
        if (this.cache.TryGetValue(charOnlyKey, out record!))
        {
            return true;
        }

        foreach (var kvp in this.cache)
        {
            if (!kvp.Key.StartsWith(charOnlyKey + "@", StringComparison.Ordinal))
            {
                continue;
            }

            record = kvp.Value;
            return true;
        }

        return false;
    }

    public void RequestPushSoon()
    {
        this.nextPushAtUtc = DateTime.MinValue;
    }

    public bool IsHonorificBridgeActive => IsHonorificFallbackActive(this.plugin.Configuration, DateTime.UtcNow);

    public string GetProviderLabel()
    {
        var now = DateTime.UtcNow;
        if (IsHonorificFallbackActive(this.plugin.Configuration, now))
        {
            return "Honorific/Lightless (fallback active)";
        }

        return $"SocialMorpho Cloud (primary) ({this.plugin.Configuration.TitleSyncApiUrl})";
    }

    private void OnLogin()
    {
        this.nextPullAtUtc = DateTime.MinValue;
        this.nextHonorificPullAtUtc = DateTime.MinValue;
        this.nextHonorificPushAtUtc = DateTime.MinValue;
        this.nextPushAtUtc = DateTime.MinValue;
        this.nextHonorificCheckAtUtc = DateTime.MinValue;
    }

    private bool DetectHonorificBridgeAvailable()
    {
        if (TryInvokeIpcFunc<bool>("Honorific.Ready", out var ready) && ready)
        {
            return true;
        }

        // Honorific API compatibility used by Lightless is 3.1+.
        if (TryInvokeIpcFunc<(uint major, uint minor)>("Honorific.ApiVersion", out var version))
        {
            return version.major == 3 && version.minor >= 1;
        }

        return false;
    }

    private bool IsHonorificFallbackActive(Configuration cfg, DateTime nowUtc)
    {
        return cfg.PreferHonorificSync &&
               this.honorificBridgeActive &&
               nowUtc < this.cloudUnavailableUntilUtc;
    }

    private void MarkCloudFailure()
    {
        this.consecutiveCloudFailures++;
        if (this.consecutiveCloudFailures >= 3)
        {
            this.cloudUnavailableUntilUtc = DateTime.UtcNow.AddMinutes(20);
        }
    }

    private void MarkCloudSuccess()
    {
        this.consecutiveCloudFailures = 0;
        this.cloudUnavailableUntilUtc = DateTime.MinValue;
    }

    private void PushLocalTitleToHonorific()
    {
        try
        {
            var title = this.plugin.QuestManager.GetStats().UnlockedTitle?.Trim();
            var colorPreset = string.IsNullOrWhiteSpace(this.plugin.Configuration.RewardTitleColorPreset)
                ? "Gold"
                : this.plugin.Configuration.RewardTitleColorPreset.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var encodedTitle = EncodeTitleForHonorific(title, colorPreset);
            if (string.Equals(encodedTitle, this.lastHonorificTitle, StringComparison.Ordinal) &&
                string.Equals(colorPreset, this.lastHonorificColor, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryGetLocalObjectIndex(out var objectIndex))
            {
                return;
            }

            // Primary path: direct IPC so Lightless observes normal Honorific title state.
            var pushed = TryInvokeIpcAction<int, string>("Honorific.SetCharacterTitle", objectIndex, encodedTitle);
            if (!pushed && TryGetLocalCharacter(out var localCharacter, out _))
            {
                // Alternate shape used by some builds: character name + title.
                pushed = TryInvokeIpcAction<string, string>("Honorific.SetCharacterTitle", localCharacter, encodedTitle);
            }

            if (!pushed)
            {
                // Controlled fallback to Honorific command path (max once per 15s).
                var now = DateTime.UtcNow;
                if (now - this.lastHonorificCommandFallbackUtc >= TimeSpan.FromSeconds(15))
                {
                    this.lastHonorificCommandFallbackUtc = now;
                    var escapedTitle = encodedTitle.Replace("\"", "\\\"", StringComparison.Ordinal);
                    pushed = this.plugin.TryRunCommandText($"/honorific force set \"{escapedTitle}\"");
                }
            }

            if (!pushed)
            {
                if (DateTime.UtcNow - this.lastHonorificWarnUtc >= TimeSpan.FromMinutes(1))
                {
                    this.lastHonorificWarnUtc = DateTime.UtcNow;
                    this.log.Warning("Honorific fallback active but all title-set paths failed (IPC + command).");
                }
                return;
            }

            this.lastHonorificTitle = encodedTitle;
            this.lastHonorificColor = colorPreset;
        }
        catch (Exception ex)
        {
            this.log.Warning($"Honorific handoff failed: {ex.Message}");
        }
    }

    private void PullHonorificTitlesIntoCache()
    {
        try
        {
            var localId = this.objectTable.LocalPlayer?.GameObjectId ?? 0;
            var added = 0;
            foreach (var obj in this.objectTable)
            {
                if (obj == null || obj.GameObjectId == 0 || obj.GameObjectId == localId)
                {
                    continue;
                }

                var objectIndex = TryGetObjectIndex(obj);
                if (objectIndex < 0)
                {
                    continue;
                }

                if (!TryInvokeIpcFuncDynamic("Honorific.GetCharacterTitle", [objectIndex], out var rawTitle))
                {
                    continue;
                }

                var character = TryGetName(obj);
                var title = ExtractHonorificTitleText(rawTitle, out var colorPreset);
                if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var record = new SyncedTitleRecord
                {
                    character = character.Trim(),
                    world = TryGetWorld(obj) ?? string.Empty,
                    title = title,
                    colorPreset = string.IsNullOrWhiteSpace(colorPreset) ? "Gold" : colorPreset,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                };

                this.cache[$"title:{record.character.ToLowerInvariant()}"] = record;
                added++;
            }

            if (added == 0 && DateTime.UtcNow - this.lastHonorificWarnUtc >= TimeSpan.FromMinutes(1))
            {
                this.lastHonorificWarnUtc = DateTime.UtcNow;
                this.log.Warning("Honorific fallback active but no nearby titles were returned.");
            }
            else if (added > 0 && DateTime.UtcNow - this.lastHonorificInfoUtc >= TimeSpan.FromMinutes(1))
            {
                this.lastHonorificInfoUtc = DateTime.UtcNow;
                this.log.Info($"Honorific title cache refreshed for {added} nearby player(s).");
            }
        }
        catch
        {
        }
    }

    private bool TryGetLocalObjectIndex(out int objectIndex)
    {
        objectIndex = -1;
        try
        {
            var player = this.objectTable.LocalPlayer;
            if (player == null)
            {
                return false;
            }

            var prop = player.GetType().GetProperty("ObjectIndex", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                return false;
            }

            var raw = prop.GetValue(player);
            if (raw == null)
            {
                return false;
            }

            objectIndex = Convert.ToInt32(raw);
            return objectIndex >= 0;
        }
        catch
        {
            return false;
        }
    }

    private bool TryInvokeIpcFunc<TReturn>(string callGateName, out TReturn value)
    {
        value = default!;
        try
        {
            var methods = this.plugin.PluginInterface.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => string.Equals(m.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
                            m.IsGenericMethodDefinition &&
                            m.GetGenericArguments().Length == 1 &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == typeof(string))
                .ToArray();
            if (methods.Length == 0)
            {
                return false;
            }

            foreach (var method in methods)
            {
                object? subscriber = null;
                try
                {
                    var generic = method.MakeGenericMethod(typeof(TReturn));
                    subscriber = generic.Invoke(this.plugin.PluginInterface, [callGateName]);
                }
                catch
                {
                    continue;
                }

                if (subscriber == null)
                {
                    continue;
                }

                var invokeFunc = subscriber.GetType().GetMethod("InvokeFunc", BindingFlags.Public | BindingFlags.Instance);
                if (invokeFunc == null)
                {
                    continue;
                }

                var result = invokeFunc.Invoke(subscriber, null);
                if (result is TReturn typed)
                {
                    value = typed;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryInvokeIpcAction<T1, T2>(string callGateName, T1 arg1, T2 arg2)
    {
        try
        {
            var methods = this.plugin.PluginInterface.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => string.Equals(m.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
                            m.IsGenericMethodDefinition &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == typeof(string))
                .ToArray();

            if (methods.Length == 0)
            {
                return false;
            }

            foreach (var method in methods)
            {
                var arity = method.GetGenericArguments().Length;
                object? subscriber = null;
                try
                {
                    if (arity == 2)
                    {
                        var generic = method.MakeGenericMethod(typeof(T1), typeof(T2));
                        subscriber = generic.Invoke(this.plugin.PluginInterface, [callGateName]);
                    }
                    else if (arity == 3)
                    {
                        var generic = method.MakeGenericMethod(typeof(T1), typeof(T2), typeof(object));
                        subscriber = generic.Invoke(this.plugin.PluginInterface, [callGateName]);
                    }
                    else
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                if (subscriber == null)
                {
                    continue;
                }

                var invokeAction = subscriber.GetType().GetMethod("InvokeAction", BindingFlags.Public | BindingFlags.Instance);
                if (invokeAction == null)
                {
                    continue;
                }

                invokeAction.Invoke(subscriber, [arg1, arg2]);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryInvokeIpcFuncDynamic(string callGateName, object[] args, out object? value)
    {
        value = null;
        try
        {
            var argTypes = args.Select(a => a.GetType()).ToArray();
            var methods = this.plugin.PluginInterface.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => string.Equals(m.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
                            m.IsGenericMethodDefinition &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == typeof(string) &&
                            m.GetGenericArguments().Length == argTypes.Length + 1)
                .ToArray();

            foreach (var method in methods)
            {
                try
                {
                    var genericArgs = new Type[argTypes.Length + 1];
                    Array.Copy(argTypes, genericArgs, argTypes.Length);
                    genericArgs[^1] = typeof(object);
                    var generic = method.MakeGenericMethod(genericArgs);
                    var subscriber = generic.Invoke(this.plugin.PluginInterface, [callGateName]);
                    if (subscriber == null)
                    {
                        continue;
                    }

                    var invokeFunc = subscriber.GetType().GetMethod("InvokeFunc", BindingFlags.Public | BindingFlags.Instance);
                    if (invokeFunc == null)
                    {
                        continue;
                    }

                    value = invokeFunc.Invoke(subscriber, args);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractHonorificTitleText(object? value, out string colorPreset)
    {
        colorPreset = "Gold";
        var rawTitle = ExtractHonorificTitleTextCore(value);
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return null;
        }

        return DecodeTitleFromHonorific(rawTitle, out colorPreset);
    }

    private static string? ExtractHonorificTitleTextCore(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var nested = ExtractHonorificTitleTextCore(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        if (value is string s)
        {
            var parsedFromJson = TryExtractTitleFromJsonText(s);
            if (!string.IsNullOrWhiteSpace(parsedFromJson))
            {
                return parsedFromJson;
            }

            var parsedLoose = TryExtractTitleFromLoosePayload(s);
            if (!string.IsNullOrWhiteSpace(parsedLoose))
            {
                return parsedLoose;
            }

            if (LooksLikeHonorificPayload(s))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(s) ? null : StripWrappingQuotes(s.Trim());
        }

        try
        {
            var t = value.GetType();

            // Handle KeyValuePair-like wrappers where title lives in .Value.
            var valueProp = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp != null)
            {
                var nestedValue = valueProp.GetValue(value);
                var nestedTitle = ExtractHonorificTitleTextCore(nestedValue);
                if (!string.IsNullOrWhiteSpace(nestedTitle))
                {
                    return nestedTitle;
                }
            }

            foreach (var propName in new[] { "DisplayTitle", "Title", "Text" })
            {
                var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                var rawValue = prop?.GetValue(value);
                var nestedTitle = ExtractHonorificTitleTextCore(rawValue);
                if (!string.IsNullOrWhiteSpace(nestedTitle))
                {
                    return nestedTitle;
                }
            }

            var asText = value.ToString();
            var parsedFromToString = TryExtractTitleFromJsonText(asText ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(parsedFromToString))
            {
                return parsedFromToString;
            }

            var parsedLoose = TryExtractTitleFromLoosePayload(asText ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(parsedLoose))
            {
                return parsedLoose;
            }

            if (LooksLikeHonorificPayload(asText ?? string.Empty))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(asText))
            {
                return StripWrappingQuotes(asText.Trim());
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryExtractTitleFromJsonText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) &&
            !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var title = TryReadTitleProperty(item);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                return TryReadTitleProperty(root);
            }
        }
        catch
        {
            // Not valid JSON payload; fall back to raw text.
        }

        return null;
    }

    private static string? TryReadTitleProperty(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty("Title", out var titleProp) &&
            !element.TryGetProperty("title", out titleProp))
        {
            return null;
        }

        var text = titleProp.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : StripWrappingQuotes(text.Trim());
    }

    private static string? TryExtractTitleFromLoosePayload(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = Regex.Match(input, "\\\"Title\\\"\\s*:\\s*\\\"(?<t>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups["t"].Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Unescape common JSON escapes seen in IPC payload strings.
        var unescaped = raw.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrWhiteSpace(unescaped) ? null : StripWrappingQuotes(unescaped);
    }

    private static bool LooksLikeHonorificPayload(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var hasTitleKey = input.Contains("\"Title\"", StringComparison.OrdinalIgnoreCase) ||
                          input.Contains("\\\"Title\\\"", StringComparison.OrdinalIgnoreCase);
        var hasStyleKey = input.Contains("GradientAnimationStyle", StringComparison.OrdinalIgnoreCase);
        return hasTitleKey && hasStyleKey;
    }

    private static string EncodeTitleForHonorific(string title, string colorPreset)
    {
        var cleanTitle = StripWrappingQuotes(title);
        var cleanPreset = Regex.Replace(colorPreset ?? "Gold", "[^A-Za-z0-9 _-]", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanPreset))
        {
            cleanPreset = "Gold";
        }

        return $"{cleanTitle} [[SMC:{cleanPreset}]]";
    }

    private static string DecodeTitleFromHonorific(string input, out string colorPreset)
    {
        colorPreset = "Gold";
        var trimmed = StripWrappingQuotes(input.Trim());
        var match = Regex.Match(trimmed, @"\s*\[\[SMC:(?<preset>[^\]]+)\]\]\s*$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return trimmed;
        }

        var preset = match.Groups["preset"].Value.Trim();
        if (!string.IsNullOrWhiteSpace(preset))
        {
            colorPreset = preset;
        }

        return StripWrappingQuotes(trimmed[..match.Index].Trim());
    }

    private static string StripWrappingQuotes(string value)
    {
        var result = value;
        if (result.Length >= 2)
        {
            if ((result[0] == '"' && result[^1] == '"') ||
                (result[0] == '\'' && result[^1] == '\''))
            {
                result = result[1..^1].Trim();
            }
        }

        return result;
    }

    private async Task PushLocalTitleAsync()
    {
        var cfg = this.plugin.Configuration;
        var baseUrl = cfg.TitleSyncApiUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        if (!TryGetLocalCharacter(out var character, out var world))
        {
            return;
        }

        var title = this.plugin.QuestManager.GetStats().UnlockedTitle;
        var color = cfg.RewardTitleColorPreset;
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        if (string.Equals(title, this.lastPushedTitle, StringComparison.Ordinal) &&
            string.Equals(color, this.lastPushedColor, StringComparison.Ordinal))
        {
            return;
        }

        this.pushInFlight = true;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1/title/update");
            req.Headers.TryAddWithoutValidation("x-client-version", GetClientVersion());

            var payload = new
            {
                character,
                world,
                title,
                colorPreset = color,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
            };
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await HttpClient.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (DateTime.UtcNow - this.lastPushErrorLogUtc >= TimeSpan.FromMinutes(2))
                {
                    this.lastPushErrorLogUtc = DateTime.UtcNow;
                    this.log.Warning($"Title sync push failed HTTP {(int)res.StatusCode} ({baseUrl}): {body}");
                }
                this.nextPushAtUtc = DateTime.UtcNow.AddMinutes(10);
                MarkCloudFailure();
                return;
            }

            this.lastPushedTitle = title;
            this.lastPushedColor = color;
            MarkCloudSuccess();
        }
        catch (Exception ex)
        {
            this.log.Warning($"Title sync push failed: {ex.Message}");
            MarkCloudFailure();
        }
        finally
        {
            this.pushInFlight = false;
        }
    }

    private async Task PullNearbyTitlesAsync()
    {
        var cfg = this.plugin.Configuration;
        var baseUrl = cfg.TitleSyncApiUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        var players = BuildNearbyPlayers();
        if (players.Count == 0)
        {
            return;
        }

        this.pullInFlight = true;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1/title/batch");
            req.Headers.TryAddWithoutValidation("x-client-version", GetClientVersion());

            var payload = new { players };
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await HttpClient.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (DateTime.UtcNow - this.lastPullErrorLogUtc >= TimeSpan.FromMinutes(1))
                {
                    this.lastPullErrorLogUtc = DateTime.UtcNow;
                    this.log.Warning($"Title sync pull failed HTTP {(int)res.StatusCode} ({baseUrl}): {body}");
                }
                this.nextPullAtUtc = DateTime.UtcNow.AddMinutes(3);
                MarkCloudFailure();
                return;
            }

            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<BatchResponse>(json);
            if (parsed?.records == null || parsed.records.Count == 0)
            {
                return;
            }

            foreach (var pair in parsed.records)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                this.cache[pair.Key] = pair.Value;
                var characterOnlyKey = $"title:{pair.Value.character.ToLowerInvariant()}";
                this.cache[characterOnlyKey] = pair.Value;
            }

            MarkCloudSuccess();
        }
        catch (Exception ex)
        {
            this.log.Warning($"Title sync pull failed: {ex.Message}");
            MarkCloudFailure();
        }
        finally
        {
            this.pullInFlight = false;
        }
    }

    private List<LookupPlayer> BuildNearbyPlayers()
    {
        var result = new List<LookupPlayer>(24);
        var localId = this.objectTable.LocalPlayer?.GameObjectId ?? 0;

        foreach (var obj in this.objectTable)
        {
            if (obj == null || obj.GameObjectId == 0 || obj.GameObjectId == localId)
            {
                continue;
            }

            var character = TryGetName(obj);
            var world = TryGetWorld(obj);
            if (string.IsNullOrWhiteSpace(character))
            {
                continue;
            }

            result.Add(new LookupPlayer
            {
                character = character,
                world = string.IsNullOrWhiteSpace(world) ? string.Empty : world,
            });
            if (result.Count >= 24)
            {
                break;
            }
        }

        return result;
    }

    private string? TryBuildKeyForGameObject(ulong gameObjectId)
    {
        foreach (var obj in this.objectTable)
        {
            if (obj == null || !IsObjectMatch(obj, gameObjectId))
            {
                continue;
            }

            var character = TryGetName(obj);
            var world = TryGetWorld(obj);
            if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(world))
            {
                return null;
            }

            return $"title:{character.ToLowerInvariant()}@{world.ToLowerInvariant()}";
        }

        return null;
    }

    private string? TryBuildCharacterOnlyKeyForGameObject(ulong gameObjectId)
    {
        foreach (var obj in this.objectTable)
        {
            if (obj == null || !IsObjectMatch(obj, gameObjectId))
            {
                continue;
            }

            var character = TryGetName(obj);
            if (string.IsNullOrWhiteSpace(character))
            {
                return null;
            }

            return $"title:{character.ToLowerInvariant()}";
        }

        return null;
    }

    public bool TryGetCharacterNameForGameObjectId(ulong gameObjectId, out string characterName)
    {
        characterName = string.Empty;
        foreach (var obj in this.objectTable)
        {
            if (obj == null || !IsObjectMatch(obj, gameObjectId))
            {
                continue;
            }

            var name = TryGetName(obj);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            characterName = name;
            return true;
        }

        return false;
    }

    public bool TryGetHonorificTitleForGameObjectId(ulong gameObjectId, out SyncedTitleRecord record)
    {
        record = default!;
        if (!this.honorificBridgeActive)
        {
            return false;
        }

        foreach (var obj in this.objectTable)
        {
            if (obj == null || !IsObjectMatch(obj, gameObjectId))
            {
                continue;
            }

            var objectIndex = TryGetObjectIndex(obj);
            if (objectIndex < 0)
            {
                return false;
            }

            if (!TryInvokeIpcFuncDynamic("Honorific.GetCharacterTitle", [objectIndex], out var raw))
            {
                return false;
            }

            var title = ExtractHonorificTitleText(raw, out var colorPreset);
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            record = new SyncedTitleRecord
            {
                character = TryGetName(obj) ?? string.Empty,
                world = TryGetWorld(obj) ?? string.Empty,
                title = title,
                colorPreset = string.IsNullOrWhiteSpace(colorPreset) ? "Gold" : colorPreset,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
            };
            return true;
        }

        return false;
    }

    private bool TryGetLocalCharacter(out string character, out string world)
    {
        character = string.Empty;
        world = "unknown";

        var player = this.objectTable.LocalPlayer;
        if (player == null)
        {
            return false;
        }

        character = TryGetName(player) ?? string.Empty;
        var localWorld = TryGetWorld(player);
        if (!string.IsNullOrWhiteSpace(localWorld))
        {
            world = localWorld;
        }

        return !string.IsNullOrWhiteSpace(character);
    }

    private static int TryGetObjectIndex(object obj)
    {
        try
        {
            var prop = obj.GetType().GetProperty("ObjectIndex", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                return -1;
            }

            var raw = prop.GetValue(obj);
            if (raw == null)
            {
                return -1;
            }

            return Convert.ToInt32(raw);
        }
        catch
        {
            return -1;
        }
    }

    private static string? TryGetName(object source)
    {
        try
        {
            var prop = source.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(source);
            if (value == null)
            {
                return null;
            }

            var textValueProp = value.GetType().GetProperty("TextValue", BindingFlags.Public | BindingFlags.Instance);
            var text = textValueProp?.GetValue(value) as string;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }

            var raw = value.ToString();
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetWorld(object source)
    {
        // Keep this flexible across Dalamud API surface changes.
        var candidates = new[] { "HomeWorld", "CurrentWorld", "World", "HomeWorldId", "WorldId" };
        foreach (var name in candidates)
        {
            try
            {
                var prop = source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                var value = prop?.GetValue(source);
                if (value == null)
                {
                    continue;
                }

                // Some wrappers expose .Value or .RowId
                var rowId = value.GetType().GetProperty("RowId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
                if (rowId != null)
                {
                    var row = Convert.ToUInt32(rowId);
                    if (row > 0)
                    {
                        return row.ToString();
                    }
                }

                var wrappedValue = value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
                if (wrappedValue != null)
                {
                    var v = wrappedValue.ToString();
                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        return v.Trim();
                    }
                }

                var raw = value.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool IsObjectMatch(dynamic obj, ulong nameplateGameObjectId)
    {
        try
        {
            if ((ulong)obj.GameObjectId == nameplateGameObjectId)
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            // Some contexts pass 32-bit entity ids in nameplate handlers.
            var entityId = (uint)obj.EntityId;
            if ((ulong)entityId == nameplateGameObjectId)
            {
                return true;
            }

            if (((ulong)obj.GameObjectId & 0xFFFFFFFFUL) == (ulong)entityId &&
                nameplateGameObjectId == (ulong)entityId)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private string GetClientVersion()
    {
        try
        {
            return this.plugin.GetType().Assembly.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        this.clientState.Login -= OnLogin;
    }

    private sealed class BatchResponse
    {
        public bool ok { get; set; }
        public Dictionary<string, SyncedTitleRecord>? records { get; set; }
    }

    private sealed class LookupPlayer
    {
        public string character { get; set; } = string.Empty;
        public string world { get; set; } = string.Empty;
    }
}

public sealed class SyncedTitleRecord
{
    public string character { get; set; } = string.Empty;
    public string world { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string colorPreset { get; set; } = "Gold";
    public string updatedAtUtc { get; set; } = string.Empty;
}
