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
        btn_仓库.onClick.AddListener(gamePanel.Show_Building);
        btn_仓库.onClick.AddListener(gamePanel.Show_Inventory);
    }
   
    private void Start()
    {
        
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
