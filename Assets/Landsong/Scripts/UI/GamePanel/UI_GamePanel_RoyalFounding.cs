using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RoyalFounding : MonoBehaviour
    {
        [LabelText("弹窗交互组"), Required] public CanvasGroup ModalGroup;
        [LabelText("拥立说明"), Required] public TMP_Text Message;
        [LabelText("姓名输入"), Required] public TMP_InputField NameInput;
        [LabelText("男性按钮"), Required] public Button MaleButton;
        [LabelText("女性按钮"), Required] public Button FemaleButton;
        [LabelText("男性标签"), Required] public TMP_Text MaleLabel;
        [LabelText("女性标签"), Required] public TMP_Text FemaleLabel;
        [LabelText("确认按钮"), Required] public Button ConfirmButton;
        [LabelText("校验提示"), Required] public TMP_Text Feedback;

        GameUiSessionHandle session;
        GameUiCommandWriter commands;
        PersonGender gender;
        bool submitted;
        public bool IsOpen => gameObject.activeSelf;

        public void ValidateConfiguration()
        {
            if (Message == null || NameInput == null || MaleButton == null || FemaleButton == null ||
                MaleLabel == null || FemaleLabel == null || ConfirmButton == null || Feedback == null)
                throw new InvalidOperationException("王室拥立弹窗检查器引用不完整。");
            if (ModalGroup == null || !ModalGroup.interactable || !ModalGroup.blocksRaycasts || !ModalGroup.ignoreParentGroups)
                throw new InvalidOperationException("王室拥立弹窗必须能在主界面输入被遮罩时独立交互。");
        }

        internal void Bind(GameUiSessionHandle gameSession, GameUiCommandWriter writer)
        {
            ValidateConfiguration();
            session = gameSession ?? throw new ArgumentNullException(nameof(gameSession));
            commands = writer ?? throw new ArgumentNullException(nameof(writer));
            Message.text = "人们认为你是一名优秀的领导者，拥护你成为这片土地的领导者";
            NameInput.characterLimit = 24;
            NameInput.onValueChanged.RemoveAllListeners();
            NameInput.onValueChanged.AddListener(_ => UpdateControls());
            MaleButton.onClick.RemoveAllListeners();
            MaleButton.onClick.AddListener(() => SelectGender(PersonGender.Male));
            FemaleButton.onClick.RemoveAllListeners();
            FemaleButton.onClick.AddListener(() => SelectGender(PersonGender.Female));
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(Confirm);
            ResetSession();
        }

        internal void ResetSession()
        {
            submitted = false;
            gender = PersonGender.Unspecified;
            if (NameInput != null)
                NameInput.text = string.Empty;
            if (Feedback != null)
                Feedback.text = string.Empty;
            gameObject.SetActive(false);
            if (MaleLabel != null && FemaleLabel != null && ConfirmButton != null)
                UpdateControls();
        }

        internal void Refresh()
        {
            if (session == null || !session.IsBound || !RoyalFoundingOps.CanFound(session.em, session.root))
            {
                if (IsOpen)
                    ResetSession();
                return;
            }

            if (!IsOpen)
            {
                gameObject.SetActive(true);
                NameInput.Select();
                NameInput.ActivateInputField();
            }

            if (!submitted)
                return;
            foreach (var request in session.em.GetBuffer<QueuedGameplayRequest>(session.root))
                if (request.Kind == CommandKind.FoundRoyal)
                    return;
            submitted = false;
            Feedback.text = "未能完成拥立，请检查姓名与性别后重试。";
            UpdateControls();
        }

        void SelectGender(PersonGender value)
        {
            gender = value;
            UpdateControls();
        }

        void UpdateControls()
        {
            MaleLabel.text = gender == PersonGender.Male ? "男 ✓" : "男";
            FemaleLabel.text = gender == PersonGender.Female ? "女 ✓" : "女";
            ConfirmButton.interactable = !submitted && gender != PersonGender.Unspecified && !string.IsNullOrWhiteSpace(NameInput.text);
        }

        void Confirm()
        {
            var name = NameInput.text.Trim();
            if (name.Length == 0 || gender == PersonGender.Unspecified)
            {
                Feedback.text = "请输入姓名并选择性别。";
                UpdateControls();
                return;
            }

            if (commands.TryQueue(new FoundRoyalRequest { Name = new FixedString128Bytes(name), Gender = gender }))
            {
                submitted = true;
                Feedback.text = string.Empty;
                UpdateControls();
            }
        }
    }
}
