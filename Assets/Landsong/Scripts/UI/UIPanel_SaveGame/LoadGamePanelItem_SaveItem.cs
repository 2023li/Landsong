using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadGamePanelItem_SaveItem : MonoBehaviour
{
    private UIPanel_LoadGame loadGamePanel;
    private GameDataMeta meta;

    public GameDataMeta Meta => meta;
    public string SaveGuid => meta != null ? meta.SaveGuid : string.Empty;

    [SerializeField, LabelText("player名称")]
    private TMP_Text txt_PlayerName;

    [SerializeField, LabelText("地图名称")]
    private TMP_Text txt_MapName;

    [SerializeField, LabelText("阶段名称")]
    private TMP_Text txt_Stage;

    [SerializeField, LabelText("回合数")]
    private TMP_Text txt_RoundCount;

    [SerializeField, LabelText("最后游玩时间")]
    private TMP_Text txt_LastPlayedTime;

   

   
   

    [SerializeField]
    private Button btn;

    private void Awake()
    {


        if (btn != null)
        {
            btn.onClick.AddListener(OnClick);
        }

    }



    public void Initialize(UIPanel_LoadGame ownerPanel, GameDataMeta gameDataMeta)
    {
        loadGamePanel = ownerPanel;
        meta = gameDataMeta;

        RefreshView();
    }

    private void RefreshView()
    {
        if (meta == null)
        {
            if (txt_PlayerName != null)
            {
                txt_PlayerName.text = "无效存档";
            }

            if (txt_LastPlayedTime != null)
            {
                txt_LastPlayedTime.text = "-";
            }

            if (txt_MapName != null)
            {
                txt_MapName.text = "-";
            }

            if (txt_Stage != null)
            {
                txt_Stage.text = "-";
            }

            if (txt_RoundCount != null)
            {
                txt_RoundCount.text = "-";
            }

            return;
        }

        meta.Validate();

        if (txt_PlayerName != null)
        {
            txt_PlayerName.text = FormatOptionalText(meta.SaveName);
        }

        if (txt_MapName != null)
        {
            txt_MapName.text = FormatOptionalText(meta.MapDisplayName);
        }

        if (txt_Stage != null)
        {
            txt_Stage.text = FormatOptionalText(meta.Stage);
        }

        if (txt_LastPlayedTime != null)
        {
            txt_LastPlayedTime.text = meta.GetLastSaveLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (txt_RoundCount != null)
        {
            txt_RoundCount.text = meta.RoundCount.ToString();
        }
    }

    private string FormatOptionalText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
    }

    private void OnClick()
    {
        if (loadGamePanel == null || meta == null)
        {
            return;
        }

        loadGamePanel.Show_确认选择存档面板(this);
    }
}
