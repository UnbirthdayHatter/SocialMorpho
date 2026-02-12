using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using SocialMorpho.Data;
using System.IO;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestTrackerWindow : Window
{
    private const float TrackerWidth = 380f;
    private const float TitleWrapWidth = 320f;
    private const float ObjectiveWrapWidth = 300f;

    private Plugin Plugin;
    private QuestManager QuestManager;
    private object? CustomQuestIcon;
    private ImTextureID CustomQuestIconHandle;
    private bool HasCustomQuestIcon;

    // FFXIV color scheme
    private readonly Vector4 FFXIVGold = new(0.83f, 0.69f, 0.22f, 1.0f);      // #D4AF37
    private readonly Vector4 FFXIVCyan = new(0.0f, 0.81f, 0.82f, 1.0f);       // #00CED1

    public QuestTrackerWindow(Plugin plugin, QuestManager questManager)
        : base("Quest Tracker##SocialMorphoTracker",
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.NoFocusOnAppearing)
    {
        Plugin = plugin;
        QuestManager = questManager;
        LoadCustomIcon();

        // Position will be set in PreDraw to ensure accurate screen size
        PositionCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(TrackerWidth, 0f);
        SizeCondition = ImGuiCond.FirstUseEver;

        // Semi-transparent background
        BgAlpha = 0.75f;
    }

    private void LoadCustomIcon()
    {
        try
        {
            var assemblyDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrEmpty(assemblyDir))
            {
                Plugin.PluginLog.Warning("Assembly directory path is null or empty, cannot load custom quest icon");
                return;
            }

            var iconPath = Path.Combine(assemblyDir, "Resources", "quest_icon.png");

            // Development fallback to legacy location.
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(assemblyDir, "LoveQuest.png");
            }

            if (!File.Exists(iconPath))
            {
                Plugin.PluginLog.Warning($"Custom quest icon not found at: {iconPath}");
                return;
            }

            var loadImageMethod = Plugin.PluginInterface.UiBuilder.GetType().GetMethod("LoadImage", new[] { typeof(string) });
            if (loadImageMethod == null)
            {
                Plugin.PluginLog.Warning("UiBuilder.LoadImage() is not available in this Dalamud version");
                return;
            }

            CustomQuestIcon = loadImageMethod.Invoke(Plugin.PluginInterface.UiBuilder, new object[] { iconPath });
            if (CustomQuestIcon == null)
            {
                Plugin.PluginLog.Warning($"LoadImage returned null for icon path: {iconPath}");
                return;
            }

            var handleProperty = CustomQuestIcon.GetType().GetProperty("ImGuiHandle");
            if (handleProperty?.GetValue(CustomQuestIcon) is ImTextureID textureHandle)
            {
                CustomQuestIconHandle = textureHandle;
                HasCustomQuestIcon = true;
                Plugin.PluginLog.Info($"Custom quest icon loaded from: {iconPath}");
            }
            else
            {
                Plugin.PluginLog.Warning($"Loaded icon did not expose ImGuiHandle for path: {iconPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"Failed to load custom quest icon: {ex}");
        }
    }

    public override void PreDraw()
    {
        if (!Plugin.Configuration.ShowQuestTracker)
        {
            IsOpen = false;
            return;
        }

        if (!Position.HasValue)
        {
            Position = new Vector2(ImGui.GetIO().DisplaySize.X - 380, 100);
        }

        base.PreDraw();
    }

    public override void Draw()
    {
        var activeQuests = QuestManager.GetActiveQuests();
        if (activeQuests.Count == 0)
            return;

        foreach (var quest in activeQuests)
        {
            DrawQuestEntry(quest);
        }
    }

    private void DrawQuestEntry(QuestData quest)
    {
        DrawCustomIcon();

        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVGold);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + TitleWrapWidth);
        ImGui.TextUnformatted(quest.Title);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();

        ImGui.Indent(20f);

        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVCyan);
        ImGui.Text("\u25BA");
        ImGui.SameLine(0f, 6f);

        var objectiveText = !string.IsNullOrEmpty(quest.Description)
            ? quest.Description
            : $"Complete {quest.GoalCount} objectives";

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ObjectiveWrapWidth);
        ImGui.TextUnformatted(objectiveText);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, FFXIVCyan);
        ImGui.Text($"({quest.CurrentCount}/{quest.GoalCount})");
        ImGui.PopStyleColor();

        ImGui.Unindent(20f);
        ImGui.Spacing();
    }

    private void DrawCustomIcon()
    {
        if (HasCustomQuestIcon)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            drawList.AddImage(
                CustomQuestIconHandle,
                new Vector2(pos.X + 1, pos.Y + 1),
                new Vector2(pos.X + 21, pos.Y + 21),
                Vector2.Zero,
                Vector2.One,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f))
            );

            ImGui.Image(CustomQuestIconHandle, new Vector2(20, 20));
            ImGui.SameLine(0f, 6f);
            return;
        }

        var fallbackPos = ImGui.GetCursorScreenPos();
        var fallbackDrawList = ImGui.GetWindowDrawList();

        fallbackDrawList.AddText(
            new Vector2(fallbackPos.X + 1, fallbackPos.Y + 1),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)),
            "!"
        );

        ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "!");
        ImGui.SameLine(0f, 6f);
    }

    public void Dispose()
    {
        if (CustomQuestIcon is IDisposable disposable)
            disposable.Dispose();
    }
}
