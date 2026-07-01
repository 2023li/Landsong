using UnityEngine;
using UnityEngine.UI;

public class GamePanel_HUD : MonoBehaviour
{
    [SerializeField] private Button btn_建造;
    [SerializeField] private Button btn_仓库;

    private UIPanel_Game gamePanel;

    private void Awake()
    {
        gamePanel = GetComponentInParent<UIPanel_Game>();
        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void BindButtons()
    {
        if (gamePanel == null)
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} cannot bind buttons because {nameof(UIPanel_Game)} was not found.", this);
            return;
        }

        if (btn_建造 != null)
        {
            btn_建造.onClick.RemoveListener(gamePanel.Show_Building);
            btn_建造.onClick.AddListener(gamePanel.Show_Building);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no build button assigned.", this);
        }

        if (btn_仓库 != null)
        {
            btn_仓库.onClick.RemoveListener(gamePanel.Show_Inventory);
            btn_仓库.onClick.AddListener(gamePanel.Show_Inventory);
        }
        else
        {
            Debug.LogWarning($"{nameof(GamePanel_HUD)} has no inventory button assigned.", this);
        }
    }

    private void UnbindButtons()
    {
        if (gamePanel == null)
        {
            return;
        }

        if (btn_建造 != null)
        {
            btn_建造.onClick.RemoveListener(gamePanel.Show_Building);
        }

        if (btn_仓库 != null)
        {
            btn_仓库.onClick.RemoveListener(gamePanel.Show_Inventory);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
