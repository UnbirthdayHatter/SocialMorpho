using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SocialMorpho.Data;
using SocialMorpho.Windows;
using SocialMorpho.Services;
using System.IO;
using System.Runtime.InteropServices;

namespace SocialMorpho;

public sealed class Plugin : IDalamudPlugin
{
    private const uint SndAsync = 0x0001;
    private const uint SndFilename = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string pszSound, nint hmod, uint fdwSound);

    public string Name => "Social Morpho";
    private const string CommandName = "/morpho";
    private const string CommandNameAlt = "/sm";

    public IDalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    private IClientState ClientState { get; init; }
    private ICondition Condition { get; init; }
    private IObjectTable ObjectTable { get; init; }
    private IChatGui ChatGui { get; init; }
    private IToastGui ToastGui { get; init; }
    private INamePlateGui NamePlateGui { get; init; }
    public IPluginLog PluginLog { get; init; }
    public ITextureProvider TextureProvider { get; init; }
    
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("SocialMorpho");

    private MainWindow MainWindow { get; init; }
    public QuestTrackerWindow QuestTrackerWindow { get; init; }
    private QuestOfferWindow QuestOfferWindow { get; init; }
    public QuestManager QuestManager { get; init; }
    private QuestNotificationService QuestNotificationService { get; init; }
    private QuestOfferService QuestOfferService { get; init; }
    private TitleSyncService TitleSyncService { get; init; }
    private NameplateTitleService NameplateTitleService { get; init; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        IChatGui chatGui,
        IToastGui toastGui,
        INamePlateGui namePlateGui,
        IPluginLog pluginLog,
        ITextureProvider textureProvider)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        ClientState = clientState;
        Condition = condition;
        ObjectTable = objectTable;
        ChatGui = chatGui;
        ToastGui = toastGui;
        NamePlateGui = namePlateGui;
        PluginLog = pluginLog;
        TextureProvider = textureProvider;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);
        MigrateTitleSyncDefaults();
        MigrateDoteQuestText();
        MigrateLegacyStarterQuests();

        QuestManager = new QuestManager(Configuration);
        
        if (Configuration.AutoLoadJsonQuests)
        {
            // Optional import path for advanced users. Disabled by default so the
            // plugin starts with just the 3 daily quests.
            try
            {
                QuestManager.LoadQuestsFromJson(PluginInterface.ConfigDirectory.FullName);
                PluginLog.Info("Quests loaded from JSON successfully");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error loading quests from JSON: {ex.Message}");
            }
        }

        // Check and reset quests based on schedule
        QuestManager.CheckAndResetQuests();
        QuestManager.EnsureDailySocialQuests(DateTime.Now);

        QuestNotificationService = new QuestNotificationService(this, ClientState, ChatGui, PluginLog);

        MainWindow = new MainWindow(this, QuestManager);
        QuestTrackerWindow = new QuestTrackerWindow(this, QuestManager);
        QuestOfferWindow = new QuestOfferWindow(this);
        QuestOfferService = new QuestOfferService(this, ClientState, ToastGui, PluginLog, QuestOfferWindow);
        TitleSyncService = new TitleSyncService(this, ClientState, ObjectTable, PluginLog);
        NameplateTitleService = new NameplateTitleService(this, NamePlateGui, ObjectTable, TitleSyncService);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(QuestTrackerWindow);
        WindowSystem.AddWindow(QuestOfferWindow);
        
        // Show quest tracker if configured
        if (Configuration.ShowQuestTracker)
        {
            QuestTrackerWindow.IsOpen = true;
        }

        // Register commands
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Social Morpho quest menu"
        });
        CommandManager.AddHandler(CommandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Social Morpho quest menu"
        });

        ChatGui.ChatMessage += OnChatMessage;

        // Subscribe to draw event
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

        PluginLog.Info("Social Morpho initialized successfully");
    }

    private void DrawUI()
    {
        TitleSyncService.Tick();
        WindowSystem.Draw();
    }

    private void DrawConfigUI()
    {
        MainWindow.IsOpen = true;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        try
        {
            var text = message.TextValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var previousTitle = QuestManager.GetStats().UnlockedTitle;
            var result = QuestManager.IncrementQuestProgressFromChatDetailed(text);
            var newTitle = QuestManager.GetStats().UnlockedTitle;
            var leveledUp = !string.Equals(previousTitle, newTitle, StringComparison.Ordinal);

            if (result != null)
            {
                ShowProgressToast(result);
                if (result.CompletedNow && !leveledUp)
                {
                    PlayCustomSound("soft_bubble.wav");
                }
            }

            if (leveledUp)
            {
                PlayCustomSound("cheery_tune.wav");
                TitleSyncService.RequestPushSoon();
            }
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Error processing chat message for quest progress: {ex.Message}");
        }
    }

    private void ShowProgressToast(ProgressUpdateResult result)
    {
        if (!result.CompletedNow)
        {
            return;
        }

        try
        {
            var message = $"{result.QuestTitle} complete! ({result.NewCount}/{result.GoalCount})";
            ToastGui.ShowQuest(message, new QuestToastOptions
            {
                Position = QuestToastPosition.Centre,
                IconId = GetToastIconId(result.QuestType),
                DisplayCheckmark = true,
                PlaySound = false,
            });
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Failed to show progress toast: {ex.Message}");
        }
    }

    private void PlayCustomSound(string fileName)
    {
        if (!Configuration.SoundEnabled)
        {
            return;
        }

        try
        {
            var assemblyDir = PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrWhiteSpace(assemblyDir))
            {
                return;
            }

            var soundPath = Path.Combine(assemblyDir, "Resources", fileName);
            if (!File.Exists(soundPath))
            {
                return;
            }

            if (!PlaySound(soundPath, nint.Zero, SndFilename | SndAsync))
            {
                PluginLog.Warning($"Failed to play custom sound '{fileName}' from: {soundPath}");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Failed to queue custom sound '{fileName}': {ex.Message}");
        }
    }

    private static uint GetToastIconId(QuestType type)
    {
        const uint SocialMorphoDefaultIconId = 63926u;

        return type switch
        {
            QuestType.Buff => 61413u,
            QuestType.Emote => SocialMorphoDefaultIconId,
            QuestType.Custom => SocialMorphoDefaultIconId,
            _ => SocialMorphoDefaultIconId,
        };
    }

    private void MigrateDoteQuestText()
    {
        var changed = false;
        foreach (var quest in Configuration.SavedQuests)
        {
            if (quest.Title.Contains("Dotted", StringComparison.OrdinalIgnoreCase))
            {
                quest.Title = quest.Title.Replace("Dotted", "Doted", StringComparison.OrdinalIgnoreCase);
                changed = true;
            }

            if (quest.Description.Contains("DoT effects", StringComparison.OrdinalIgnoreCase))
            {
                quest.Description = "Have 3 different players use /dote on you";
                changed = true;
            }

            if ((quest.Title.Contains("doted", StringComparison.OrdinalIgnoreCase) ||
                 quest.Description.Contains("/dote", StringComparison.OrdinalIgnoreCase)) &&
                quest.TriggerPhrases.Count == 0)
            {
                quest.TriggerPhrases.Add("dotes on you");
                quest.TriggerPhrases.Add("dotes you");
                changed = true;
            }
        }

        if (changed)
        {
            Configuration.Save();
        }
    }

    private void MigrateLegacyStarterQuests()
    {
        var removed = Configuration.SavedQuests.RemoveAll(q =>
            q.Id <= 3 &&
            (q.Title.Contains("Get Doted", StringComparison.OrdinalIgnoreCase) ||
             q.Title.Contains("Get Dotted", StringComparison.OrdinalIgnoreCase) ||
             q.Title.Contains("Hug Four Players", StringComparison.OrdinalIgnoreCase) ||
             q.Title.Contains("Social Butterfly", StringComparison.OrdinalIgnoreCase)));

        if (removed > 0)
        {
            Configuration.Save();
        }
    }

    public void Dispose()
    {
        Configuration.Save();
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameAlt);

        ChatGui.ChatMessage -= OnChatMessage;

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

        QuestNotificationService?.Dispose();
        QuestOfferService?.Dispose();
        TitleSyncService?.Dispose();
        NameplateTitleService?.Dispose();
        QuestOfferWindow?.Dispose();
        QuestTrackerWindow?.Dispose();

        PluginLog.Info("Social Morpho disposed");
    }

    public void TriggerQuestOfferTest()
    {
        if (!QuestOfferService.TriggerTestOfferPopup())
        {
            PluginLog.Warning("No quest offer available for manual popup test.");
        }
    }

    private void MigrateTitleSyncDefaults()
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(Configuration.TitleSyncApiUrl))
        {
            Configuration.TitleSyncApiUrl = Configuration.DefaultTitleSyncApiUrl;
            changed = true;
        }

        if (Configuration.Version < 2)
        {
            Configuration.EnableTitleSync = true;
            Configuration.ShareTitleSync = true;
            Configuration.ShowSyncedTitles = true;
            Configuration.Version = 2;
            changed = true;
        }

        if (changed)
        {
            Configuration.Save();
        }
    }

    public void TriggerToastIconPreview()
    {
        try
        {
            var previewIcons = new[] { 61412u, 61413u, 61414u, 63926u };
            foreach (var icon in previewIcons)
            {
                ToastGui.ShowQuest($"SocialMorpho Icon Preview ({icon})", new Dalamud.Game.Gui.Toast.QuestToastOptions
                {
                    Position = Dalamud.Game.Gui.Toast.QuestToastPosition.Centre,
                    IconId = icon,
                    DisplayCheckmark = false,
                    PlaySound = false,
                });
            }
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Failed to show toast icon preview: {ex.Message}");
        }
    }

    public void RefreshNameplateTitlePreview()
    {
        NameplateTitleService.RequestRedraw();
        TitleSyncService.RequestPushSoon();
    }

    public void RequestTitleSyncNow()
    {
        TitleSyncService.RequestPushSoon();
    }

    public bool ShouldHideQuestOverlay()
    {
        if (!ClientState.IsLoggedIn)
        {
            return true;
        }

        // Hide during loading/zone transition to match native UI feel.
        if (Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51])
        {
            return true;
        }

        // Hide during any cutscene-like state for better immersion.
        if (Condition[ConditionFlag.WatchingCutscene] ||
            Condition[ConditionFlag.WatchingCutscene78] ||
            Condition[ConditionFlag.OccupiedInCutSceneEvent])
        {
            return true;
        }

        return false;
    }
}
