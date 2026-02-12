using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using SocialMorpho.Data;
using System.Collections.Generic;
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
        : base("New Quest Offer##SocialMorphoOffer",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground)
    {
        this.plugin = plugin;
        this.Size = new Vector2(760, 760);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.BgAlpha = 0.0f;
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

        // Draw the content image first so the frame sits on top of it.
        if (this.offerImage != null)
        {
            // Fill the frame slot directly (stretch) to avoid any bottom/side gaps.
            var contentStart = frameStart + new Vector2(frameSize.X * 0.100f, frameSize.Y * 0.274f);
            var contentEnd = frameStart + new Vector2(frameSize.X * 0.900f, frameSize.Y * 0.545f);
            this.TryDrawTexture(this.offerImage, contentStart, contentEnd);
        }

        // Draw the quest window frame over the image.
        var frameDrawn = false;
        if (this.frameImage != null && this.TryDrawTexture(this.frameImage, frameStart, frameEnd))
            frameDrawn = true;

        if (!frameDrawn)
        {
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.6f, 1f), $"Frame image not available: {this.loadedFrameImagePath ?? "<unset>"}");
            ImGui.Dummy(new Vector2(1f, 20f));
            return;
        }

        // Overlay title, text and buttons aligned to the frame's designed content areas.
        var titleText = this.currentOffer?.PopupTitle ?? string.Empty;
        const float titleScale = 1.95f;
        const float descHeaderScale = 1.15f;
        const float descBodyScale = 1.28f;

        var titleSize = ImGui.CalcTextSize(titleText) * titleScale;
        var titleX = frameStart.X + ((frameSize.X - titleSize.X) * 0.5f);
        var titleY = frameStart.Y + (frameSize.Y * 0.158f);
        var blackTextVec = new Vector4(0.10f, 0.10f, 0.10f, 1f);
        this.DrawScaledText(titleText, new Vector2(titleX, titleY), blackTextVec, titleScale);

        var textLeft = frameStart.X + (frameSize.X * 0.10f);
        var textTop = frameStart.Y + (frameSize.Y * 0.61f);
        var textWidth = frameSize.X * 0.80f;
        var textBottom = frameStart.Y + (frameSize.Y * 0.865f);
        var textLineHeight = ImGui.GetTextLineHeightWithSpacing() * descBodyScale;
        var descriptionTitle = "Description";
        this.DrawScaledText(descriptionTitle, new Vector2(textLeft, textTop - (ImGui.GetTextLineHeightWithSpacing() * 1.10f)), blackTextVec, descHeaderScale);

        var lines = this.WrapTextToWidth(this.currentOffer?.PopupDescription ?? string.Empty, textWidth, descBodyScale);
        var y = textTop;
        foreach (var line in lines)
        {
            if ((y + textLineHeight) > textBottom)
            {
                break;
            }

            this.DrawScaledText(line, new Vector2(textLeft, y), blackTextVec, descBodyScale);
            y += textLineHeight;
        }

        var buttonY = frameStart.Y + (frameSize.Y * 0.878f);
        var buttonWidth = frameSize.X * 0.31f;
        var buttonHeight = frameSize.Y * 0.078f;
        var buttonGap = frameSize.X * 0.12f;
        var leftButtonX = frameStart.X + (frameSize.X * 0.135f);
        var rightButtonX = leftButtonX + buttonWidth + buttonGap;

        ImGui.SetCursorScreenPos(new Vector2(leftButtonX, buttonY));
        if (ImGui.InvisibleButton("##accept_offer", new Vector2(buttonWidth, buttonHeight)))
        {
            this.onAccept?.Invoke(this.currentOffer!);
            this.CloseOffer();
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(rightButtonX, buttonY));
        if (ImGui.InvisibleButton("##decline_offer", new Vector2(buttonWidth, buttonHeight)))
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

    private void TryDrawTextureFit(ISharedImmediateTexture texture, Vector2 topLeft, Vector2 bottomRight)
    {
        var regionSize = bottomRight - topLeft;
        if (regionSize.X <= 0f || regionSize.Y <= 0f)
        {
            return;
        }

        if (!TryGetTextureSize(texture, out var textureSize) || textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            this.TryDrawTexture(texture, topLeft, bottomRight);
            return;
        }

        var texAspect = textureSize.X / textureSize.Y;
        var regionAspect = regionSize.X / regionSize.Y;

        Vector2 drawSize;
        if (texAspect > regionAspect)
        {
            drawSize = new Vector2(regionSize.X, regionSize.X / texAspect);
        }
        else
        {
            drawSize = new Vector2(regionSize.Y * texAspect, regionSize.Y);
        }

        var offset = new Vector2((regionSize.X - drawSize.X) * 0.5f, (regionSize.Y - drawSize.Y) * 0.5f);
        var drawStart = topLeft + offset;
        var drawEnd = drawStart + drawSize;
        this.TryDrawTexture(texture, drawStart, drawEnd);
    }

    private static bool TryGetTextureSize(ISharedImmediateTexture texture, out Vector2 size)
    {
        size = Vector2.Zero;
        if (!texture.TryGetWrap(out var wrap, out _) || wrap == null)
        {
            return false;
        }

        try
        {
            var wrapType = wrap.GetType();
            var widthProp = wrapType.GetProperty("Width");
            var heightProp = wrapType.GetProperty("Height");
            if (widthProp == null || heightProp == null)
            {
                return false;
            }

            var w = widthProp.GetValue(wrap);
            var h = heightProp.GetValue(wrap);
            if (w == null || h == null)
            {
                return false;
            }

            var width = Convert.ToSingle(w);
            var height = Convert.ToSingle(h);
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            size = new Vector2(width, height);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void DrawScaledText(string text, Vector2 pos, Vector4 color, float scale)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.SetCursorScreenPos(pos);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);
    }

    private List<string> WrapTextToWidth(string text, float maxWidth, float scale)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var current = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = $"{current} {words[i]}";
                if ((ImGui.CalcTextSize(candidate).X * scale) <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = words[i];
                }
            }

            result.Add(current);
        }

        return result;
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
