using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.IO;
using System.Linq;
using System.Numerics;

namespace SocialMorpho.Windows;

public class QuestOfferWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private QuestOfferDefinition? currentOffer;
    private ISharedImmediateTexture? offerImage;
    private ISharedImmediateTexture? frameImage;
    private string? loadedOfferImagePath;
    private string? loadedFrameImagePath;
    private Action<QuestOfferDefinition>? onAccept;
    private Action<QuestOfferDefinition>? onDecline;

    public QuestOfferWindow(Plugin plugin)
        : base("New Quest Offer##SocialMorphoOffer", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        this.Size = new Vector2(760, 760);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.IsOpen = false;
    }

    public void ShowOffer(
        QuestOfferDefinition offer,
        Action<QuestOfferDefinition> acceptCallback,
        Action<QuestOfferDefinition> declineCallback)
    {
        this.currentOffer = offer;
        this.onAccept = acceptCallback;
        this.onDecline = declineCallback;
        this.LoadOfferImage(offer.ImageFileName);
        this.IsOpen = true;
    }

    public override void Draw()
    {
        if (this.currentOffer == null)
        {
            return;
        }

        this.DrawOfferImage();
    }

    private void DrawOfferImage()
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var frameWidth = MathF.Min(740f, availWidth);
        var frameSize = new Vector2(frameWidth, frameWidth);
        var frameStart = ImGui.GetCursorScreenPos();
        var frameEnd = frameStart + frameSize;
        var drawList = ImGui.GetWindowDrawList();

        // Draw the quest window frame first.
        var frameDrawn = false;
        if (this.frameImage != null &&
            this.TryDrawTexture(this.frameImage, frameStart, frameEnd))
        {
            frameDrawn = true;
        }

        if (!frameDrawn)
        {
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.6f, 1f), $"Frame image not available: {this.loadedFrameImagePath ?? "<unset>"}");
            ImGui.Dummy(new Vector2(1f, 20f));
            return;
        }

        // Draw the content image in the transparent top slot of the frame.
        if (this.offerImage != null)
        {
            var contentStart = frameStart + new Vector2(frameSize.X * 0.132f, frameSize.Y * 0.306f);
            var contentEnd = frameStart + new Vector2(frameSize.X * 0.867f, frameSize.Y * 0.538f);
            this.TryDrawTexture(this.offerImage, contentStart, contentEnd);
        }

        // Overlay title, text and buttons aligned to the frame's designed content areas.
        var titleText = this.currentOffer?.PopupTitle ?? string.Empty;
        var titleSize = ImGui.CalcTextSize(titleText);
        var titleX = frameStart.X + ((frameSize.X - titleSize.X) * 0.5f);
        var titleY = frameStart.Y + (frameSize.Y * 0.175f);
        drawList.AddText(new Vector2(titleX, titleY), ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)), titleText);

        var textLeft = frameStart.X + (frameSize.X * 0.12f);
        var textTop = frameStart.Y + (frameSize.Y * 0.56f);
        var textWidth = frameSize.X * 0.76f;

        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        ImGui.PushTextWrapPos(textLeft + textWidth);
        ImGui.TextWrapped(this.currentOffer?.PopupDescription ?? string.Empty);
        ImGui.PopTextWrapPos();

        var buttonY = frameStart.Y + (frameSize.Y * 0.90f);
        var buttonWidth = frameSize.X * 0.27f;
        var buttonGap = frameSize.X * 0.12f;
        var leftButtonX = frameStart.X + (frameSize.X * 0.17f);
        var rightButtonX = leftButtonX + buttonWidth + buttonGap;

        ImGui.SetCursorScreenPos(new Vector2(leftButtonX, buttonY));
        if (ImGui.Button("Accept", new Vector2(buttonWidth, 0f)))
        {
            this.onAccept?.Invoke(this.currentOffer!);
            this.CloseOffer();
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(rightButtonX, buttonY));
        if (ImGui.Button("Decline", new Vector2(buttonWidth, 0f)))
        {
            this.onDecline?.Invoke(this.currentOffer!);
            this.CloseOffer();
            return;
        }

        ImGui.Dummy(frameSize);
    }

    private void LoadOfferImage(string imageFileName)
    {
        this.offerImage = null;
        this.frameImage = null;
        this.loadedOfferImagePath = null;
        this.loadedFrameImagePath = null;

        try
        {
            var assemblyDir = this.plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (string.IsNullOrWhiteSpace(assemblyDir))
            {
                return;
            }

            var candidates = new[]
            {
                Path.Combine(assemblyDir, "Resources", imageFileName),
                Path.Combine(assemblyDir, "Resources", "Square.png"),
                Path.Combine(assemblyDir, "Resources", "square.png"),
                Path.Combine(assemblyDir, "Resources", "aCmbspI.png"),
                Path.Combine(assemblyDir, "Resources", "LoveQuest.png"),
            };

            var candidate = candidates.FirstOrDefault(File.Exists);
            if (candidate == null)
            {
                this.plugin.PluginLog.Warning($"Quest offer image not found. Tried: {string.Join(", ", candidates)}");
            }
            else
            {
                this.offerImage = this.plugin.TextureProvider.GetFromFile(candidate);
                this.loadedOfferImagePath = candidate;
            }

            var frameCandidates = new[]
            {
                Path.Combine(assemblyDir, "Resources", "aCmbspI.png"),
                Path.Combine(assemblyDir, "Resources", "acmbspi.png"),
            };

            var framePath = frameCandidates.FirstOrDefault(File.Exists);
            if (framePath == null)
            {
                this.plugin.PluginLog.Warning($"Quest offer frame image not found. Tried: {string.Join(", ", frameCandidates)}");
                return;
            }

            this.frameImage = this.plugin.TextureProvider.GetFromFile(framePath);
            this.loadedFrameImagePath = framePath;
        }
        catch (Exception ex)
        {
            this.plugin.PluginLog.Error($"Failed to load quest offer image: {ex.Message}");
        }
    }

    private bool TryDrawTexture(ISharedImmediateTexture texture, Vector2 topLeft, Vector2 bottomRight)
    {
        if (texture.TryGetWrap(out var wrap, out _) && wrap is IDrawListTextureWrap drawListWrap)
        {
            drawListWrap.Draw(ImGui.GetWindowDrawList(), topLeft, bottomRight);
            return true;
        }

        if (texture.TryGetWrap(out var wrapForHandle, out _) && TryGetImGuiTextureFromWrap(wrapForHandle, out var textureId))
        {
            ImGui.SetCursorScreenPos(topLeft);
            ImGui.Image(textureId, new Vector2(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));
            return true;
        }

        return false;
    }

    private static bool TryGetImGuiTextureFromWrap(IDalamudTextureWrap wrap, out ImTextureID textureId)
    {
        try
        {
            var handleValue = wrap.GetType().GetProperty("Handle")?.GetValue(wrap);
            if (TryCreateTextureIdFromValue(handleValue, out var id))
            {
                textureId = id;
                return true;
            }
        }
        catch
        {
        }

        textureId = default;
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

        var operators = textureType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m =>
                (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                m.ReturnType == textureType &&
                m.GetParameters().Length == 1);
        foreach (var op in operators)
        {
            var paramType = op.GetParameters()[0].ParameterType;
            try
            {
                var arg = paramType.IsAssignableFrom(valueType) ? value : Convert.ChangeType(value, paramType);
                var result = op.Invoke(null, new[] { arg });
                if (result is ImTextureID converted)
                {
                    textureId = converted;
                    return true;
                }
            }
            catch
            {
            }
        }

        foreach (var ctor in textureType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
        {
            var p = ctor.GetParameters();
            if (p.Length != 1)
                continue;

            try
            {
                var arg = p[0].ParameterType.IsAssignableFrom(valueType) ? value : Convert.ChangeType(value, p[0].ParameterType);
                var created = ctor.Invoke(new[] { arg });
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

    private void CloseOffer()
    {
        this.currentOffer = null;
        this.onAccept = null;
        this.onDecline = null;
        this.offerImage = null;
        this.frameImage = null;
        this.loadedOfferImagePath = null;
        this.loadedFrameImagePath = null;
        this.IsOpen = false;
    }

    public void Dispose()
    {
    }
}
