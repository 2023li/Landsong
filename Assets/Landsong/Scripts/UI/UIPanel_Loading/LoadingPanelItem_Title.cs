using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class LoadingPanelItem_Title : MonoBehaviour
{
   
    

    [SerializeField, BoxGroup("引用"), Required]
    private CanvasGroup waitAnyKeyCanvasGroup;

    [SerializeField, BoxGroup("动画"), MinValue(0f)]
    private float waitConfirmFadeDuration = 0.5f;

    private Tween waitConfirmTween;

    public void Show()
    {
        gameObject.SetActive(true);
        ResetWaitConfirmState();
    }

    public void Hide()
    {
        ResetWaitConfirmState();
        gameObject.SetActive(false);
    }

    public void BeginWaitPlayerConfirm()
    {
        if (waitAnyKeyCanvasGroup == null)
        {
            return;
        }

        waitConfirmTween?.Kill();

        waitAnyKeyCanvasGroup.gameObject.SetActive(true);
        waitAnyKeyCanvasGroup.alpha = 0f;

        waitConfirmTween = waitAnyKeyCanvasGroup
            .DOFade(1f, waitConfirmFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        waitConfirmTween?.Kill();
        waitConfirmTween = null;
    }

    private void ResetWaitConfirmState()
    {
        waitConfirmTween?.Kill();
        waitConfirmTween = null;

        if (waitAnyKeyCanvasGroup == null)
        {
            return;
        }

        waitAnyKeyCanvasGroup.alpha = 0f;
        waitAnyKeyCanvasGroup.gameObject.SetActive(false);
    }

   
}
