
using Moyo.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


    public class UIPanel_LoadGame : UIPanelBase
    {
        [Header("Save Item")]
        [SerializeField] private LoadGamePanelItem_SaveItem prefab_SaveItem;
        [SerializeField] private RectTransform rt_SaveItemParent;

        [Header("Buttons")]
        [SerializeField] private Button btn_关闭页面;

        [Header("Empty State")]
        [SerializeField] private GameObject go_没有存档;

        [Header("Load Event")]
        [SerializeField] private GameDataEvent onLoadGameDataSuccess;

        private readonly List<LoadGamePanelItem_SaveItem> saveItems = new List<LoadGamePanelItem_SaveItem>();

        private bool initialized;
        private LoadGamePanelItem_SaveItem selectedSaveItem;

        [Serializable]
        public class GameDataEvent : UnityEvent<GameData> { }

        public override Task OnCreateAsync()
        {
            Initialize();
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            Initialize();
            RefreshSaveItems();
            return base.OnOpenAsync(args);
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (prefab_SaveItem != null)
            {
                prefab_SaveItem.gameObject.SetActive(false);
            }

            if (btn_关闭页面 != null)
            {
                btn_关闭页面.onClick.AddListener(ClosePanel);
            }



            Init_确认选择存档面板();
        }

        private async void ClosePanel()
        {
            await UIManager.Instance.BackAsync();
        }

        public void RefreshSaveItems()
        {
            ClearSaveItems();

            if (DataManager.Instance == null)
            {
                Debug.LogWarning("刷新存档列表失败：DataManager.Instance 为空。");
                SetEmptyState(true);
                return;
            }

            // 这里主动重载索引，确保外部新增/删除的 Slot 文件也能被重新扫描到。
            DataManager.Instance.LoadGameDataIndex();

            IReadOnlyList<GameDataMeta> metas = DataManager.Instance.GetAllGameDataMeta();

            if (metas == null || metas.Count <= 0)
            {
                SetEmptyState(true);
                return;
            }

            SetEmptyState(false);

            for (int i = 0; i < metas.Count; i++)
            {
                CreateSaveItem(metas[i]);
            }
        }

        private void CreateSaveItem(GameDataMeta meta)
        {
            if (prefab_SaveItem == null)
            {
                Debug.LogWarning("创建存档 Item 失败：prefab_SaveItem 为空。");
                return;
            }

            if (rt_SaveItemParent == null)
            {
                Debug.LogWarning("创建存档 Item 失败：rt_SaveItemParent 为空。");
                return;
            }

            if (meta == null || string.IsNullOrEmpty(meta.SaveGuid))
            {
                return;
            }

            LoadGamePanelItem_SaveItem item = Instantiate(prefab_SaveItem, rt_SaveItemParent);
            item.gameObject.SetActive(true);
            item.Initialize(this, meta);

            saveItems.Add(item);
        }

        private void ClearSaveItems()
        {
            for (int i = 0; i < saveItems.Count; i++)
            {
                if (saveItems[i] != null)
                {
                    Destroy(saveItems[i].gameObject);
                }
            }

            saveItems.Clear();
            selectedSaveItem = null;
        }

        private void SetEmptyState(bool isEmpty)
        {
            if (go_没有存档 != null)
            {
                go_没有存档.SetActive(isEmpty);
            }
        }

        #region POP确认选择存档面板

        [Header("POP确认选择存档面板")]
        [SerializeField] private GameObject pop_确认选择存档面板;
        [SerializeField] private Button pop_确认选择_确认;
        [SerializeField] private Button pop_删除存档;

        private void Init_确认选择存档面板()
        {
            if (pop_确认选择存档面板 != null)
            {
                pop_确认选择存档面板.SetActive(false);
            }

            if (pop_确认选择_确认 != null)
            {
                pop_确认选择_确认.onClick.AddListener(ConfirmLoadSelectedSave);
            }

            if (pop_删除存档 != null)
            {
                pop_删除存档.onClick.AddListener(() =>
                {
                    if (selectedSaveItem == null || selectedSaveItem.Meta == null)
                    {
                        Debug.LogWarning("删除存档失败：没有选择有效存档。");
                        Hide_确认选择存档面板();
                        return;
                    }
                    string saveGuid = selectedSaveItem.Meta.SaveGuid;
                    if (string.IsNullOrEmpty(saveGuid))
                    {
                        Debug.LogWarning("删除存档失败：SaveGuid 为空。");
                        Hide_确认选择存档面板();
                        return;
                    }
                    DataManager.Instance.DeleteGameData(saveGuid);
                    Hide_确认选择存档面板();
                    RefreshSaveItems();
                });
            }
        }

        public void Show_确认选择存档面板(LoadGamePanelItem_SaveItem saveItem)
        {
            if (saveItem == null || saveItem.Meta == null)
            {
                return;
            }

            selectedSaveItem = saveItem;

            if (pop_确认选择_确认 != null)
            {
                pop_确认选择_确认.interactable = true;
            }

            if (pop_确认选择存档面板 != null)
            {
                pop_确认选择存档面板.SetActive(true);
            }
        }

        private void Hide_确认选择存档面板()
        {
            selectedSaveItem = null;

            if (pop_确认选择存档面板 != null)
            {
                pop_确认选择存档面板.SetActive(false);
            }
        }

        private void ConfirmLoadSelectedSave()
        {
            if (selectedSaveItem == null || selectedSaveItem.Meta == null)
            {
                Debug.LogWarning("加载存档失败：没有选择有效存档。");
                Hide_确认选择存档面板();
                return;
            }

            string saveGuid = selectedSaveItem.Meta.SaveGuid;

            if (string.IsNullOrEmpty(saveGuid))
            {
                Debug.LogWarning("加载存档失败：SaveGuid 为空。");
                Hide_确认选择存档面板();
                return;
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("加载存档失败：SceneTransitionManager 不存在。");
                return;
            }

            if (SceneTransitionManager.Instance.IsTransitioning)
            {
                Debug.LogWarning("加载存档失败：当前正在切换场景，请稍后再试。");
                return;
            }

            GameData gameData = DataManager.Instance.LoadGameData(saveGuid);

            if (gameData == null)
            {
                Debug.LogWarning($"加载存档失败：{saveGuid}");
                Hide_确认选择存档面板();
                RefreshSaveItems();
                return;
            }

            onLoadGameDataSuccess?.Invoke(gameData);

            if (!LoadScene_Game.Load())
            {
                Debug.LogWarning($"加载存档失败：场景切换请求未被接受。{saveGuid}");
                return;
            }

            if (pop_确认选择_确认 != null)
            {
                pop_确认选择_确认.interactable = false;
            }

            Debug.Log($"开始加载存档：{saveGuid}");
        }

        #endregion
    }
