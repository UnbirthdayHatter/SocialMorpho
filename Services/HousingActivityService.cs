using Dalamud.Plugin.Services;

namespace SocialMorpho.Services;

public sealed class HousingActivityService : IDisposable
{
    // Core residential districts (best-effort):
    // Mist, Lavender Beds, The Goblet, Shirogane, Empyreum.
    private static readonly HashSet<ushort> HousingTerritories = new()
    {
        339, 340, 341, 641, 979,
    };

    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly Action onHousingEntered;

    public HousingActivityService(IClientState clientState, IPluginLog log, Action onHousingEntered)
    {
        this.clientState = clientState;
        this.log = log;
        this.onHousingEntered = onHousingEntered;
        this.clientState.TerritoryChanged += OnTerritoryChanged;
    }

    private void OnTerritoryChanged(ushort territoryId)
    {
        try
        {
            if (!HousingTerritories.Contains(territoryId))
            {
                return;
            }

            this.onHousingEntered.Invoke();
        }
        catch (Exception ex)
        {
            this.log.Warning($"Housing territory detector failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        this.clientState.TerritoryChanged -= OnTerritoryChanged;
    }
}
