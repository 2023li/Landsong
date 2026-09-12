using System;
using Landsong.ECS.Persistence;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public interface IApplicationUi
    {
        EventSystem InputEvents { get; }
        void OpenSettings();
        void OpenArchives(ArchiveOpenRequest request);
        void Confirm(string title, string message, Action confirmed, Func<bool> stillValid = null);
        void Quit();
    }

    public sealed class MenuOpenContext
    {
        public IApplicationUi Navigation;
        public RunArchiveStore Store;
    }

    public enum ArchiveOpenMode { Load, Save, Manage }

    public sealed class ArchiveOpenRequest
    {
        public IApplicationUi Navigation;
        public ArchiveOpenMode Mode;
        public RunArchiveStore Store;
        public string CurrentRun;
        public Func<bool> IsSessionValid;
        public Func<bool> CanWrite;
        public Action<string, string, bool> Load;
        public Action<string, ulong, string> Save;
        public Action<string, string, ulong, bool> ManageSlot;
    }

    public sealed class ConfirmationOpenContext
    {
        public string Title;
        public string Message;
        public Action Confirmed;
        public Func<bool> StillValid;
    }
}
