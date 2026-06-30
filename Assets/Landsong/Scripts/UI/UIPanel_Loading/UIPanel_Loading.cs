using System.Threading.Tasks;
using Landsong.InputSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class UIPanel_Loading : UIPanelBase
{
    public enum LoadingItemType
    {
        Item_标题,
        Item_进度条,
    }

    [SerializeField, BoxGroup("加载项")]
    private LoadingItemType defaultItemType = LoadingItemType.Item_标题;

    [SerializeField, BoxGroup("加载项"), Required]
    private LoadingPanelItem_Title titleItem;

    [SerializeField, BoxGroup("加载项"), Required]
    private LoadingPanelItem_ProgressBar progressBarItem;

    [ShowInInspector, ReadOnly, BoxGroup("运行时")]
    private LoadingItemType currentItemType;

    [ShowInInspector, ReadOnly, BoxGroup("运行时")]
    private bool isListeningAnyKey;

    public override Task OnOpenAsync(object args)
    {
        isListeningAnyKey = false;

        LoadingItemType itemType = args is LoadingItemType loadingItemType
            ? loadingItemType
            : defaultItemType;

        SwitchItem(itemType);

        return base.OnOpenAsync(args);
    }

    private void Update()
    {
        if (!isListeningAnyKey)
        {
            return;
        }
        if (InputController.Instance == null || !InputController.Instance.InputAnyKeyDown())
        {
            return;
        }

        isListeningAnyKey = false;
        SceneTransitionManager.Instance?.Confirm();
    }

    private void OnDisable()
    {
        isListeningAnyKey = false;
    }

    public void ProgressChanged(float progress)
    {
        if (currentItemType != LoadingItemType.Item_进度条)
        {
            return;
        }

        progressBarItem?.SetProgress(progress);
    }

    public void BeginWaitPlayerConfirm()
    {
        switch (currentItemType)
        {
            case LoadingItemType.Item_标题:
                titleItem?.BeginWaitPlayerConfirm();
                break;

            case LoadingItemType.Item_进度条:
                progressBarItem?.BeginWaitPlayerConfirm();
                break;
        }

        isListeningAnyKey = true;
    }

    private void SwitchItem(LoadingItemType itemType)
    {
        currentItemType = itemType;

        HideAllItems();

        switch (currentItemType)
        {
            case LoadingItemType.Item_标题:
                titleItem?.Show();
                break;

            case LoadingItemType.Item_进度条:
                progressBarItem?.Show();
                break;
        }
    }

    private void HideAllItems()
    {
        titleItem?.Hide();
        progressBarItem?.Hide();
    }
}
