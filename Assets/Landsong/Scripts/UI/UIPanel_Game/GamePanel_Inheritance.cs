using System.Collections.Generic;
using Landsong.InheritanceSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_Inheritance : MonoBehaviour
    {
        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
        [SerializeField, LabelText("生育王子按钮")] private Button birthPrinceButton;
        [SerializeField, LabelText("生育按钮文本")] private TMP_Text birthPrinceButtonLabel;
        [SerializeField, LabelText("退位按钮")] private Button abdicateButton;
        [SerializeField, LabelText("退位按钮文本")] private TMP_Text abdicateButtonLabel;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("世代文本")] private TMP_Text generationLabel;
        [SerializeField, LabelText("当前国王文本")] private TMP_Text currentKingLabel;
        [SerializeField, LabelText("当前王后文本")] private TMP_Text currentQueenLabel;

        [TitleGroup("继承人")]
        [SerializeField, LabelText("继承人根节点")] private RectTransform heirRoot;
        [SerializeField, LabelText("继承人条目预制体")] private GamePanel_InheritanceCharacterItem heirItemPrefab;
        [SerializeField, LabelText("继承人空状态")] private GameObject heirEmptyRoot;
        [SerializeField, LabelText("继承人空状态文本")] private TMP_Text heirEmptyLabel;

        [TitleGroup("世代表")]
        [SerializeField, LabelText("角色根节点")] private RectTransform characterRoot;
        [SerializeField, LabelText("角色条目预制体")] private GamePanel_InheritanceCharacterItem characterItemPrefab;
        [SerializeField, LabelText("角色空状态")] private GameObject characterEmptyRoot;
        [SerializeField, LabelText("角色空状态文本")] private TMP_Text characterEmptyLabel;

        private readonly List<GamePanel_InheritanceCharacterItem> activeHeirItems =
            new List<GamePanel_InheritanceCharacterItem>();
        private readonly List<GamePanel_InheritanceCharacterItem> heirItemPool =
            new List<GamePanel_InheritanceCharacterItem>();
        private readonly List<GamePanel_InheritanceCharacterItem> activeCharacterItems =
            new List<GamePanel_InheritanceCharacterItem>();
        private readonly List<GamePanel_InheritanceCharacterItem> characterItemPool =
            new List<GamePanel_InheritanceCharacterItem>();
        private readonly List<RoyalCharacterState> heirScratch = new List<RoyalCharacterState>();

        private UIPanel_Game gamePanel;
        private GameSystem gameSystem;
        private bool subscribedToInheritance;
        private string lastStatusMessage = string.Empty;

        private void Reset()
        {
            heirRoot = transform as RectTransform;
            characterRoot = transform as RectTransform;
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeInheritance();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeInheritance();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeInheritance();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            SubscribeInheritance();
            Refresh();
        }

        public void Hide()
        {
            UnsubscribeInheritance();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveReferences();
            RefreshHeader();
            RefreshHeirs();
            RefreshCharacters();
        }

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            gameSystem = GameSystem.Instance;
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (birthPrinceButton != null)
            {
                birthPrinceButton.onClick.RemoveListener(HandleBirthPrinceClicked);
                birthPrinceButton.onClick.AddListener(HandleBirthPrinceClicked);
            }

            if (abdicateButton != null)
            {
                abdicateButton.onClick.RemoveListener(HandleAbdicateClicked);
                abdicateButton.onClick.AddListener(HandleAbdicateClicked);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            if (birthPrinceButton != null)
            {
                birthPrinceButton.onClick.RemoveListener(HandleBirthPrinceClicked);
            }

            if (abdicateButton != null)
            {
                abdicateButton.onClick.RemoveListener(HandleAbdicateClicked);
            }
        }

        private void SubscribeInheritance()
        {
            if (subscribedToInheritance || gameSystem == null)
            {
                return;
            }

            gameSystem.Services.Inheritance.StateChanged += HandleInheritanceChanged;
            subscribedToInheritance = true;
        }

        private void UnsubscribeInheritance()
        {
            if (!subscribedToInheritance || gameSystem == null)
            {
                subscribedToInheritance = false;
                return;
            }

            gameSystem.Services.Inheritance.StateChanged -= HandleInheritanceChanged;
            subscribedToInheritance = false;
        }

        private void RefreshHeader()
        {
            var service = gameSystem == null ? null : gameSystem.Services.Inheritance;
            if (service == null)
            {
                SetText(statusLabel, "继承系统未初始化");
                SetText(generationLabel, string.Empty);
                SetText(currentKingLabel, string.Empty);
                SetText(currentQueenLabel, string.Empty);
                SetButton(birthPrinceButton, birthPrinceButtonLabel, false, "生育王子");
                SetButton(abdicateButton, abdicateButtonLabel, false, "退位");
                return;
            }

            var king = service.CurrentKing;
            var queen = service.CurrentQueen;
            SetText(statusLabel, string.IsNullOrWhiteSpace(lastStatusMessage) ? "王族血脉延续中" : lastStatusMessage);
            SetText(generationLabel, $"第 {service.Generation} 代 / 成年年龄 {service.LegalHeirAge}");
            SetText(currentKingLabel, $"国王：{GamePanel_InheritanceText.FormatCharacterLine(king, service.LegalHeirAge)}");
            SetText(currentQueenLabel, $"王后：{GamePanel_InheritanceText.FormatCharacterLine(queen, service.LegalHeirAge)}");
            SetButton(birthPrinceButton, birthPrinceButtonLabel, king != null && queen != null, "生育王子");
            SetButton(abdicateButton, abdicateButtonLabel, king != null, "退位");
        }

        private void RefreshHeirs()
        {
            ReleaseActiveHeirItems();
            var service = gameSystem == null ? null : gameSystem.Services.Inheritance;
            if (service == null || heirRoot == null || heirItemPrefab == null)
            {
                SetEmptyState(heirEmptyRoot, heirEmptyLabel, true, "继承人列表未配置");
                return;
            }

            service.GetPotentialHeirs(heirScratch);
            for (var i = 0; i < heirScratch.Count; i++)
            {
                var item = GetHeirItemFromPool();
                item.Bind(heirScratch[i], service.LegalHeirAge);
                activeHeirItems.Add(item);
            }

            SetEmptyState(heirEmptyRoot, heirEmptyLabel, activeHeirItems.Count <= 0, "当前没有继承人");
            heirScratch.Clear();
        }

        private void RefreshCharacters()
        {
            ReleaseActiveCharacterItems();
            var service = gameSystem == null ? null : gameSystem.Services.Inheritance;
            if (service == null || characterRoot == null || characterItemPrefab == null)
            {
                SetEmptyState(characterEmptyRoot, characterEmptyLabel, true, "世代表列表未配置");
                return;
            }

            var characters = service.Characters;
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null)
                {
                    continue;
                }

                var item = GetCharacterItemFromPool();
                item.Bind(character, service.LegalHeirAge);
                activeCharacterItems.Add(item);
            }

            SetEmptyState(characterEmptyRoot, characterEmptyLabel, activeCharacterItems.Count <= 0, "没有王族记录");
        }

        private GamePanel_InheritanceCharacterItem GetHeirItemFromPool()
        {
            GamePanel_InheritanceCharacterItem item;
            var lastIndex = heirItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = heirItemPool[lastIndex];
                heirItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(heirItemPrefab);
            }

            item.transform.SetParent(heirRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private GamePanel_InheritanceCharacterItem GetCharacterItemFromPool()
        {
            GamePanel_InheritanceCharacterItem item;
            var lastIndex = characterItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = characterItemPool[lastIndex];
                characterItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(characterItemPrefab);
            }

            item.transform.SetParent(characterRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveHeirItems()
        {
            for (var i = 0; i < activeHeirItems.Count; i++)
            {
                var item = activeHeirItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                if (heirRoot != null)
                {
                    item.transform.SetParent(heirRoot, false);
                }

                heirItemPool.Add(item);
            }

            activeHeirItems.Clear();
        }

        private void ReleaseActiveCharacterItems()
        {
            for (var i = 0; i < activeCharacterItems.Count; i++)
            {
                var item = activeCharacterItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                if (characterRoot != null)
                {
                    item.transform.SetParent(characterRoot, false);
                }

                characterItemPool.Add(item);
            }

            activeCharacterItems.Clear();
        }

        private void HandleBirthPrinceClicked()
        {
            if (gameSystem == null)
            {
                return;
            }

            var born = gameSystem.Services.Inheritance.TryBirthPrince(string.Empty, out var prince);
            lastStatusMessage = born && prince != null ? $"王子出生：{prince.DisplayName}" : "当前无法生育王子";
            Refresh();
        }

        private void HandleAbdicateClicked()
        {
            if (gameSystem == null)
            {
                return;
            }

            var abdicated = gameSystem.Services.Inheritance.TryAbdicateCurrentKing(out var succession);
            lastStatusMessage = abdicated && succession.NewKing != null
                ? $"新王登基：{succession.NewKing.DisplayName}"
                : "退位失败：没有可用继承人";
            Refresh();
        }

        private void HandleInheritanceChanged(RoyalInheritanceService changedInheritance)
        {
            Refresh();
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Inheritance();
                return;
            }

            Hide();
        }

        private static void SetButton(Button button, TMP_Text label, bool interactable, string text)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }

            SetText(label, text);
        }

        private static void SetEmptyState(GameObject root, TMP_Text label, bool visible, string message)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }

            SetText(label, visible ? message : string.Empty);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
