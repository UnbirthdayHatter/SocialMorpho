using Dalamud.Plugin.Services;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Reflection;

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
    private DateTime nextPullAtUtc = DateTime.MinValue;
    private DateTime nextPushAtUtc = DateTime.MinValue;
    private string lastPushedTitle = string.Empty;
    private string lastPushedColor = string.Empty;
    private bool pullInFlight;
    private bool pushInFlight;

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
        if (cfg.ShareTitleSync && now >= this.nextPushAtUtc && !this.pushInFlight)
        {
            _ = PushLocalTitleAsync();
            this.nextPushAtUtc = now.AddSeconds(45);
        }

        if (cfg.ShowSyncedTitles && now >= this.nextPullAtUtc && !this.pullInFlight)
        {
            _ = PullNearbyTitlesAsync();
            this.nextPullAtUtc = now.AddSeconds(5);
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

    private void OnLogin()
    {
        this.nextPullAtUtc = DateTime.MinValue;
        this.nextPushAtUtc = DateTime.MinValue;
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
                return;
            }

            this.lastPushedTitle = title;
            this.lastPushedColor = color;
        }
        catch (Exception ex)
        {
            this.log.Warning($"Title sync push failed: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            this.log.Warning($"Title sync pull failed: {ex.Message}");
        }
        finally
        {
            this.pullInFlight = false;
        }
    }

    private List<LookupPlayer> BuildNearbyPlayers()
    {
        var result = new List<LookupPlayer>(32);
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
            if (result.Count >= 100)
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
            if (obj == null || obj.GameObjectId != gameObjectId)
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
            if (obj == null || obj.GameObjectId != gameObjectId)
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
