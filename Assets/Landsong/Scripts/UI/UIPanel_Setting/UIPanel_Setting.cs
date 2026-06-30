using UnityEngine;
using UnityEngine.UI;
using Moyo.Unity;


    public class UIPanel_Setting : UIPanelBase
    {
        [System.Serializable]
        [ES3NonSerializable]
        private class ToggleToGO
        {
            public Toggle toggle;
            public GameObject gameObject;
        }

        [SerializeField] private ToggleToGO[] toggleObjects;

        [SerializeField] private Button btn_确定;
        [SerializeField] private Button btn_取消;

        private void Awake()
        {
            InitToggleObjects();

            if (btn_取消 != null)
            {
                btn_取消.onClick.AddListener(async () =>
                {
                    await UIManager.Instance.BackAsync();
                });
            }

            if (btn_确定 != null)
            {
                btn_确定.onClick.AddListener(async () =>
                {
                    await UIManager.Instance.BackAsync();
                });
            }
        }

        private void InitToggleObjects()
        {
            if (toggleObjects == null)
            {
                return;
            }

            foreach (ToggleToGO item in toggleObjects)
            {
                if (item == null || item.toggle == null || item.gameObject == null)
                {
                    continue;
                }

                Toggle toggle = item.toggle;
                GameObject targetGO = item.gameObject;

                // 初始化时同步一次状态
                targetGO.SetActive(toggle.isOn);

                // Toggle 状态变化时同步 GameObject 显隐
                toggle.onValueChanged.AddListener(isOn =>
                {
                    targetGO.SetActive(isOn);
                });
            }
        }
    }
