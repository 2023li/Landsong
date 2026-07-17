using Sirenix.OdinInspector;
using Landsong.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Landsong/Map/Map Definition", fileName = "MapDefinition")]
public sealed class MapDefinition : ScriptableObject
{
    [SerializeField, LabelText("稳定地图 ID")]
    private string mapId;

    [SerializeField, LabelText("地图名称")]
    private string displayName;

    [SerializeField, LabelText("地图图标")]
    private Sprite icon;

    [SerializeField, TextArea, LabelText("地图描述")]
    private string description;

    [SerializeField, LabelText("Addressable Content Scene")]
    private AssetReference contentScene;

    public string MapId => string.IsNullOrWhiteSpace(mapId) ? string.Empty : mapId.Trim();
    public string DisplayName => L10n.ContentName(
        "map",
        MapId,
        string.IsNullOrWhiteSpace(displayName) ? MapId : displayName.Trim());
    public Sprite Icon => icon;
    public string Description => L10n.ContentDescription(
        "map",
        MapId,
        string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim());
    public AssetReference ContentScene => contentScene;
    public bool IsValid => !string.IsNullOrWhiteSpace(MapId)
                           && contentScene != null
                           && contentScene.RuntimeKeyIsValid();

    private void OnValidate()
    {
        mapId = MapId;
        displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
    }
}
