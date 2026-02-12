using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private readonly Plugin Plugin;
    private readonly QuestManager QuestManager;
    private object? CustomQuestIcon;
    private ImTextureID CustomQuestIconHandle;
    private bool HasCustomQuestIcon;
    private bool LoggedSharedIconDrawFailure;

    // Soft halo tones tuned toward native quest styling.
    private readonly Vector4 FFXIVGold = new(0.70f, 0.64f, 0.45f, 0.46f);
    private readonly Vector4 FFXIVBlue = new(0.36f, 0.63f, 0.74f, 0.44f);
    private readonly Vector4 WhiteText = new(1.0f, 1.0f, 1.0f, 1.0f);

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager)
        : base("Quest Tracker##SocialMorphoTracker",
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.AlwaysAutoResize |
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.Plugin = plugin;
        this.QuestManager = questManager;

        this.LoadCustomIcon();

        // Position will be set in PreDraw to ensure accurate screen size.
        this.PositionCondition = ImGuiCond.FirstUseEver;
        this.BgAlpha = 0.0f; // Transparent like native quest UI.
    }

    private void LoadCustomIcon()
    {
        try
        {
            var assemblyDir = this.Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrEmpty(assemblyDir))
            {
                this.Plugin.PluginLog.Warning("Assembly directory path is null or empty, cannot load custom quest icon");
                return;
            }

            var iconPath = Path.Combine(assemblyDir, "Resources", "quest_icon.png");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(assemblyDir, "LoveQuest.png");
            }

            if (!File.Exists(iconPath))
            {
                this.Plugin.PluginLog.Warning($"Custom quest icon not found at: {iconPath}");
                return;
            }

            this.CustomQuestIcon = this.Plugin.TextureProvider.GetFromFile(iconPath);
            if (this.CustomQuestIcon == null)
            {
                this.Plugin.PluginLog.Warning(
                    $"Unable to load custom icon. UiBuilder type: {this.Plugin.PluginInterface.UiBuilder.GetType().FullName}");
                return;
            }

            if (this.TryExtractIconHandle(this.CustomQuestIcon, out var textureHandle))
            {
                this.CustomQuestIconHandle = textureHandle;
                this.HasCustomQuestIcon = true;
                this.Plugin.PluginLog.Info($"Custom quest icon loaded from: {iconPath}");
            }
            else
            {
                this.Plugin.PluginLog.Warning($"Loaded icon but could not resolve a direct draw handle for path: {iconPath}. Will try shared texture draw path.");
            }
        }
        catch (Exception ex)
        {
            this.Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    private bool TryExtractIconHandle(object textureObject, out ImTextureID textureHandle)
    {
        if (this.TryExtractImGuiHandle(textureObject, out textureHandle))
            return true;

        // ISharedImmediateTexture path in newer Dalamud: TryGetWrap(out wrap, out ex)
        try
        {
            var tryGetWrap = textureObject.GetType().GetMethod("TryGetWrap", BindingFlags.Public | BindingFlags.Instance);
            if (tryGetWrap != null)
            {
                var args = new object?[] { null, null };
                var ok = tryGetWrap.Invoke(textureObject, args);
                var wrapped = args[0];
                if (ok is bool success && success && wrapped != null && this.TryExtractImGuiHandle(wrapped, out textureHandle))
                {
                    return true;
                }
            }

            var getWrapOrEmpty = textureObject.GetType().GetMethod("GetWrapOrEmpty", Type.EmptyTypes);
            if (getWrapOrEmpty != null)
            {
                var wrap = getWrapOrEmpty.Invoke(textureObject, Array.Empty<object>());
                if (wrap != null && this.TryExtractImGuiHandle(wrap, out textureHandle))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        textureHandle = default;
        return false;
    }

    private bool TryExtractImGuiHandle(object textureObject, out ImTextureID textureHandle)
    {
        var handleProperties = textureObject.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var property in handleProperties)
        {
            object? value;
            try
            {
                value = property.GetValue(textureObject);
            }
            catch
            {
                continue;
            }

            if (TryCreateTextureIdFromValue(value, out var id))
            {
                textureHandle = id;
                return true;
            }
        }

        // Some wrappers expose Handle only via interface (explicit implementation).
        foreach (var iface in textureObject.GetType().GetInterfaces())
        {
            if (!iface.Name.Contains("TextureWrap", StringComparison.OrdinalIgnoreCase) &&
                !iface.Name.Contains("DrawList", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var handleProp = iface.GetProperty("Handle", BindingFlags.Public | BindingFlags.Instance);
            if (handleProp == null)
                continue;

            object? value;
            try
            {
                value = handleProp.GetValue(textureObject);
            }
            catch
            {
                continue;
            }

            if (TryCreateTextureIdFromValue(value, out var id))
            {
                textureHandle = id;
                return true;
            }
        }

        textureHandle = default;
        return false;
    }

    private static bool TryCreateTextureIdFromValue(object? value, out ImTextureID textureId)
    {
        if (value is ImTextureID direct)
        {
            textureId = direct;
            return true;
        }

        if (value == null)
        {
            textureId = default;
            return false;
        }

        var textureType = typeof(ImTextureID);
        var valueType = value.GetType();

        // Common handle primitives in Dalamud wrappers.
        if (TryCreateTextureIdViaOperators(textureType, value, valueType, out textureId))
            return true;

        if (value is IntPtr ip)
        {
            if (TryCreateTextureIdViaOperators(textureType, ip.ToInt64(), typeof(long), out textureId))
                return true;
            if (TryCreateTextureIdViaOperators(textureType, (ulong)ip.ToInt64(), typeof(ulong), out textureId))
                return true;
        }

        if (value is UIntPtr uip)
        {
            if (TryCreateTextureIdViaOperators(textureType, uip.ToUInt64(), typeof(ulong), out textureId))
                return true;
        }

        var constructors = textureType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var ctorParams = ctor.GetParameters();
            if (ctorParams.Length != 1)
                continue;

            var targetType = ctorParams[0].ParameterType;
            object? argValue = null;
            var canConvert = false;

            if (targetType.IsAssignableFrom(valueType))
            {
                argValue = value;
                canConvert = true;
            }
            else
            {
                try
                {
                    if (targetType == typeof(IntPtr) && value is long l)
                        argValue = new IntPtr(l);
                    else if (targetType == typeof(UIntPtr) && value is ulong ul)
                        argValue = new UIntPtr(ul);
                    else
                        argValue = Convert.ChangeType(value, targetType);
                    canConvert = true;
                }
                catch
                {
                }
            }

            if (!canConvert)
                continue;

            try
            {
                var created = ctor.Invoke(new[] { argValue });
                if (created is ImTextureID typed)
                {
                    textureId = typed;
                    return true;
                }
            }
            catch
            {
            }
        }

        textureId = default;
        return false;
    }

    private static bool TryCreateTextureIdViaOperators(Type textureType, object value, Type valueType, out ImTextureID textureId)
    {
        var operators = textureType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m =>
                (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                m.ReturnType == textureType &&
                m.GetParameters().Length == 1)
            .ToArray();

        foreach (var op in operators)
        {
            var pType = op.GetParameters()[0].ParameterType;
            object? arg = null;
            try
            {
                if (pType.IsAssignableFrom(valueType))
                {
                    arg = value;
                }
                else if (pType == typeof(IntPtr) && value is long l)
                {
                    arg = new IntPtr(l);
                }
                else if (pType == typeof(UIntPtr) && value is ulong ul)
                {
                    arg = new UIntPtr(ul);
                }
                else
                {
                    arg = Convert.ChangeType(value, pType);
                }

                var created = op.Invoke(null, new[] { arg });
                if (created is ImTextureID typed)
                {
                    textureId = typed;
                    return true;
                }
            }
            catch
            {
            }
        }

        textureId = default;
        return false;
    }

    public override void PreDraw()
    {
        if (!this.Plugin.Configuration.ShowQuestTracker)
        {
            this.IsOpen = false;
            return;
        }

        if (!this.Position.HasValue)
        {
            this.Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);
        }

        base.PreDraw();
    }

    public override void Draw()
    {
        var activeQuests = this.QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0)
            return;

        foreach (var quest in activeQuests)
        {
            this.DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";

        var baseCursor = ImGui.GetCursorScreenPos();
        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - 14f;
        var iconWidth = (this.HasCustomQuestIcon || this.CustomQuestIcon != null) ? 20f : 12f;
        var lineHeight = ImGui.GetTextLineHeight();
        var titleY = baseCursor.Y;
        var objectiveY = titleY + lineHeight + 2f;
        var progressY = objectiveY + lineHeight + 1f;
        var nextEntryY = progressY + lineHeight + 6f;

        const float titleScale = 1.06f;
        var titlePos = this.SetCursorForRightAlignedText(quest.Title, rightEdge - iconWidth - 6f, titleY, titleScale);
        this.DrawHaloText(quest.Title, this.FFXIVGold, titlePos, titleScale);
        this.DrawCustomIconAt(rightEdge - iconWidth, titlePos.Y);

        var objectivePos = this.SetCursorForRightAlignedText(objectiveText, rightEdge, objectiveY);
        this.DrawHaloText(objectiveText, this.FFXIVBlue, objectivePos);

        var progressText = $"{quest.CurrentCount}/{quest.GoalCount}";
        var progressPos = this.SetCursorForRightAlignedText(progressText, rightEdge, progressY);
        this.DrawHaloText(progressText, this.FFXIVBlue, progressPos);

        ImGui.SetCursorScreenPos(new Vector2(baseCursor.X, nextEntryY));
        ImGui.Spacing();
    }

    private Vector2 SetCursorForRightAlignedText(string text, float rightEdgeX, float y, float scale = 1.0f)
    {
        var textSize = ImGui.CalcTextSize(text) * scale;
        var leftX = MathF.Max(ImGui.GetWindowPos().X + 6f, rightEdgeX - textSize.X);
        leftX = MathF.Round(leftX);
        y = MathF.Round(y);
        ImGui.SetCursorScreenPos(new Vector2(leftX, y));
        return new Vector2(leftX, y);
    }

    private void DrawHaloText(string text, Vector4 haloColor, Vector2 textPos, float scale = 1.0f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var haloU32 = ImGui.ColorConvertFloat4ToU32(haloColor);
        // Single halo set (4-neighbor) for crisp glow without blur.
        drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X - 1, textPos.Y + 0), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y + 1), haloU32, text);
        drawList.AddText(new Vector2(textPos.X + 0, textPos.Y - 1), haloU32, text);

        ImGui.PushStyleColor(ImGuiCol.Text, this.WhiteText);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void DrawCustomIconAt(float x, float y)
    {
        if (!this.HasCustomQuestIcon && this.CustomQuestIcon != null)
        {
            if (this.TryExtractIconHandle(this.CustomQuestIcon, out var lazyHandle))
            {
                this.CustomQuestIconHandle = lazyHandle;
                this.HasCustomQuestIcon = true;
            }
        }

        x = MathF.Round(x);
        y = MathF.Round(y);
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        if (this.HasCustomQuestIcon)
        {
            ImGui.Image(this.CustomQuestIconHandle, new Vector2(20, 20));
            return;
        }

        if (this.CustomQuestIcon != null && this.TryDrawSharedIcon(this.CustomQuestIcon, new Vector2(x, y), new Vector2(20, 20)))
            return;

        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(this.FFXIVGold), "!");
    }

    private bool TryDrawSharedIcon(object textureObject, Vector2 min, Vector2 size)
    {
        try
        {
            var dalamudAssembly = this.Plugin.PluginInterface.UiBuilder.GetType().Assembly;
            var utilType = dalamudAssembly.GetType("Dalamud.Interface.ImGuiNotification.NotificationUtilities");
            if (utilType == null)
                return false;

            // Try overloads with ISharedImmediateTexture first.
            if (TryInvokeDrawIconFrom(utilType, textureObject, min, size))
                return true;

            // Then try overloads with IDalamudTextureWrap by resolving wrap.
            var wrapped = TryGetWrappedTexture(textureObject);
            if (wrapped != null && TryInvokeDrawIconFrom(utilType, wrapped, min, size))
                return true;
        }
        catch
        {
        }

        if (!this.LoggedSharedIconDrawFailure)
        {
            this.LoggedSharedIconDrawFailure = true;
            this.Plugin.PluginLog.Warning("Shared icon draw fallback did not find a compatible DrawIconFrom overload.");
        }

        return false;
    }

    private static bool TryInvokeDrawIconFrom(Type utilType, object textureArg, Vector2 min, Vector2 size)
    {
        try
        {
            var methods = utilType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "DrawIconFrom")
                .ToArray();
            foreach (var method in methods)
            {
                var p = method.GetParameters();
                if (p.Length != 3 ||
                    p[0].ParameterType != typeof(Vector2) ||
                    p[1].ParameterType != typeof(Vector2))
                {
                    continue;
                }

                if (!p[2].ParameterType.IsInstanceOfType(textureArg))
                    continue;

                var max = new Vector2(min.X + size.X, min.Y + size.Y);
                method.Invoke(null, new object[] { min, max, textureArg });
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static object? TryGetWrappedTexture(object textureObject)
    {
        try
        {
            var tryGetWrap = textureObject.GetType().GetMethod("TryGetWrap", BindingFlags.Public | BindingFlags.Instance);
            if (tryGetWrap != null)
            {
                var args = new object?[] { null, null };
                var ok = tryGetWrap.Invoke(textureObject, args);
                if (ok is bool success && success && args[0] != null)
                    return args[0];
            }

            var getWrapOrEmpty = textureObject.GetType().GetMethod("GetWrapOrEmpty", Type.EmptyTypes);
            if (getWrapOrEmpty != null)
                return getWrapOrEmpty.Invoke(textureObject, Array.Empty<object>());
        }
        catch
        {
        }

        return null;
    }

    public void Dispose()
    {
        if (this.CustomQuestIcon is IDisposable disposable)
            disposable.Dispose();
    }
}
