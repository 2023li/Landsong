using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingOperationBar : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [SerializeField] private Button detailButton;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button reachableRangeButton;
        [SerializeField] private TMP_Text reachableRangeLabel;

        [SerializeField] private string detailText = "详情";
        [SerializeField] private string showReachableRangeText = "可达";
        [SerializeField] private string hideReachableRangeText = "隐藏";

        private BuildingBase building;
        private Action<BuildingBase> detailClicked;
        private Action<BuildingBase> reachableRangeClicked;

        private void Reset()
        {
            root = gameObject;
            ResolveReferences();
        }

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            ResolveReferences();
            SetText(detailLabel, detailText);
            SetReachableRangeVisible(false);
        }

        private void OnEnable()
        {
            if (detailButton != null)
            {
                detailButton.onClick.AddListener(HandleDetailClicked);
            }

            if (reachableRangeButton != null)
            {
                reachableRangeButton.onClick.AddListener(HandleReachableRangeClicked);
            }
        }

        private void OnDisable()
        {
            if (detailButton != null)
            {
                detailButton.onClick.RemoveListener(HandleDetailClicked);
            }

            if (reachableRangeButton != null)
            {
                reachableRangeButton.onClick.RemoveListener(HandleReachableRangeClicked);
            }
        }

        public void Bind(
            BuildingBase targetBuilding,
            bool isReachableRangeVisible,
            Action<BuildingBase> onDetailClicked,
            Action<BuildingBase> onReachableRangeClicked)
        {
            building = targetBuilding;
            detailClicked = onDetailClicked;
            reachableRangeClicked = onReachableRangeClicked;
            SetText(detailLabel, detailText);
            SetReachableRangeVisible(isReachableRangeVisible);
            SetActive(root, true);
        }

        public void Unbind()
        {
            building = null;
            detailClicked = null;
            reachableRangeClicked = null;
        }

        public void SetReachableRangeVisible(bool visible)
        {
            SetText(reachableRangeLabel, visible ? hideReachableRangeText : showReachableRangeText);
        }

        private void ResolveReferences()
        {
            if (detailButton == null)
            {
                detailButton = GetComponentInChildren<Button>(true);
            }

            if (detailLabel == null && detailButton != null)
            {
                detailLabel = detailButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (reachableRangeButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != detailButton)
                    {
                        reachableRangeButton = buttons[i];
                        break;
                    }
                }
            }

            if (reachableRangeLabel == null && reachableRangeButton != null)
            {
                reachableRangeLabel = reachableRangeButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void HandleDetailClicked()
        {
            if (building != null)
            {
                detailClicked?.Invoke(building);
            }
        }

        private void HandleReachableRangeClicked()
        {
            if (building != null)
            {
                reachableRangeClicked?.Invoke(building);
            }
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
