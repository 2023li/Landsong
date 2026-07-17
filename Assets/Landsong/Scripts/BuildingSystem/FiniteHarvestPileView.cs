using UnityEngine;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 有限采集资源堆的纯表现组件。三个节点可以各自承载 Sprite、动画、粒子等美术内容；
    /// 没有美术时允许保留空节点，组件本身不保存采集状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FiniteHarvestPileView : MonoBehaviour,
        IBuildingViewStateConsumer,
        IBuildingViewStateConfiguration
    {
        [SerializeField] private GameObject remainingThreeRoot;
        [SerializeField] private GameObject remainingTwoRoot;
        [SerializeField] private GameObject remainingOneRoot;

        public GameObject RemainingThreeRoot => remainingThreeRoot;
        public GameObject RemainingTwoRoot => remainingTwoRoot;
        public GameObject RemainingOneRoot => remainingOneRoot;
        public bool HasCompleteStateRoots => IsOwnedStateRoot(remainingThreeRoot)
                                             && IsOwnedStateRoot(remainingTwoRoot)
                                             && IsOwnedStateRoot(remainingOneRoot)
                                             && remainingThreeRoot != remainingTwoRoot
                                             && remainingThreeRoot != remainingOneRoot
                                             && remainingTwoRoot != remainingOneRoot;

        public void Refresh(BuildingBase building)
        {
            if (building == null
                || !building.TryGetCapability<IBuildingFiniteHarvestState>(out var state))
            {
                SetVisibleState(0);
                return;
            }

            // 第三次采集成功后建筑会在同一调用链中拆除。销毁真正发生前仍保持最后一档，
            // 避免出现一帧三个节点全部隐藏的闪烁。
            var remaining = state.RemainingHarvests <= 0 ? 1 : state.RemainingHarvests;
            SetVisibleState(remaining >= 3 ? 3 : remaining);
        }

        public bool TryValidateConfiguration(out string error)
        {
            if (HasCompleteStateRoots)
            {
                error = string.Empty;
                return true;
            }

            error = "有限采集表现必须配置三个互不重复、且位于该表现组件之下的剩余次数节点。";
            return false;
        }

        private void SetVisibleState(int remaining)
        {
            SetActive(remainingThreeRoot, remaining == 3);
            SetActive(remainingTwoRoot, remaining == 2);
            SetActive(remainingOneRoot, remaining == 1);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private bool IsOwnedStateRoot(GameObject target)
        {
            return target != null
                   && target != gameObject
                   && target.transform.IsChildOf(transform);
        }
    }
}
