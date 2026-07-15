using UnityEngine;
using UnityEngine.UI;

// 这些设置属于 AppData，对所有存档生效。
public class SettingPanelItem_GamePlay : MonoBehaviour
{
    private void Start()
    {
        Start_地图网格();
    }

    private void OnDestroy()
    {
        OnDestroy_地图网格();
    }

    #region 地图网格
    /*
     * 我能保证所有字段引用合法，你不需要处理引用问题
     * 每组开关都是配对的
     * 打开一个应该关闭另一个
     * 相关的逻辑都放在这个region中
     * 例如 需要Start中执行的 都放在Start_地图网格中 然后在总的Start中调用
     * 一些通用的方法可以放在外部
     */

    // 这一组开关控制地图网格线
    [SerializeField] private Toggle tog_地图网格线_开;
    [SerializeField] private Toggle tog_地图网格线_关;

    // 这一组开关用于控制 BaseMap 瓦片地图是否显示
    [SerializeField] private Toggle tog_显示地图网格_开;
    [SerializeField] private Toggle tog_显示地图网格_关;

    // 这一组开关用于控制点击建筑时是否显示建筑占地
    [SerializeField] private Toggle tog_点击建筑显示占地_开;
    [SerializeField] private Toggle tog_点击建筑显示占地_关;

    private void Start_地图网格()
    {
        DataManager dataManager = DataManager.Instance;
        dataManager.EnsureAppDataLoaded();
        GameplayDisplaySaveData settings = dataManager.AppData.GameplayDisplay;

        设置成对开关(tog_地图网格线_开, tog_地图网格线_关, settings.MapGridLinesVisible);
        设置成对开关(tog_显示地图网格_开, tog_显示地图网格_关, settings.BaseTilemapVisible);
        设置成对开关(
            tog_点击建筑显示占地_开,
            tog_点击建筑显示占地_关,
            settings.SelectedBuildingFootprintVisible);

        tog_地图网格线_开.onValueChanged.AddListener(地图网格线_显示开关改变);
        tog_地图网格线_关.onValueChanged.AddListener(地图网格线_隐藏开关改变);
        tog_显示地图网格_开.onValueChanged.AddListener(BaseTilemap显示开关改变);
        tog_显示地图网格_关.onValueChanged.AddListener(BaseTilemap隐藏开关改变);
        tog_点击建筑显示占地_开.onValueChanged.AddListener(建筑占地显示开关改变);
        tog_点击建筑显示占地_关.onValueChanged.AddListener(建筑占地隐藏开关改变);
    }

    private void OnDestroy_地图网格()
    {
        tog_地图网格线_开.onValueChanged.RemoveListener(地图网格线_显示开关改变);
        tog_地图网格线_关.onValueChanged.RemoveListener(地图网格线_隐藏开关改变);
        tog_显示地图网格_开.onValueChanged.RemoveListener(BaseTilemap显示开关改变);
        tog_显示地图网格_关.onValueChanged.RemoveListener(BaseTilemap隐藏开关改变);
        tog_点击建筑显示占地_开.onValueChanged.RemoveListener(建筑占地显示开关改变);
        tog_点击建筑显示占地_关.onValueChanged.RemoveListener(建筑占地隐藏开关改变);
    }

    private void 地图网格线_显示开关改变(bool isOn)
    {
        if (选中成对开关(tog_地图网格线_开, tog_地图网格线_关, isOn))
        {
            DataManager.Instance.SetMapGridLinesVisible(true);
        }
    }

    private void 地图网格线_隐藏开关改变(bool isOn)
    {
        if (选中成对开关(tog_地图网格线_关, tog_地图网格线_开, isOn))
        {
            DataManager.Instance.SetMapGridLinesVisible(false);
        }
    }

    private void BaseTilemap显示开关改变(bool isOn)
    {
        if (选中成对开关(tog_显示地图网格_开, tog_显示地图网格_关, isOn))
        {
            DataManager.Instance.SetBaseTilemapVisible(true);
        }
    }

    private void BaseTilemap隐藏开关改变(bool isOn)
    {
        if (选中成对开关(tog_显示地图网格_关, tog_显示地图网格_开, isOn))
        {
            DataManager.Instance.SetBaseTilemapVisible(false);
        }
    }

    private void 建筑占地显示开关改变(bool isOn)
    {
        if (选中成对开关(tog_点击建筑显示占地_开, tog_点击建筑显示占地_关, isOn))
        {
            DataManager.Instance.SetSelectedBuildingFootprintVisible(true);
        }
    }

    private void 建筑占地隐藏开关改变(bool isOn)
    {
        if (选中成对开关(tog_点击建筑显示占地_关, tog_点击建筑显示占地_开, isOn))
        {
            DataManager.Instance.SetSelectedBuildingFootprintVisible(false);
        }
    }

    private static void 设置成对开关(Toggle openToggle, Toggle closeToggle, bool isOpen)
    {
        openToggle.SetIsOnWithoutNotify(isOpen);
        closeToggle.SetIsOnWithoutNotify(!isOpen);
    }

    private static bool 选中成对开关(Toggle selectedToggle, Toggle pairedToggle, bool isOn)
    {
        if (!isOn)
        {
            if (!pairedToggle.isOn)
            {
                selectedToggle.SetIsOnWithoutNotify(true);
            }

            return false;
        }

        pairedToggle.SetIsOnWithoutNotify(false);
        return true;
    }
    #endregion
}
