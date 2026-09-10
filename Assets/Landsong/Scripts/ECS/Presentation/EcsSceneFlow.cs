using System;
using System.IO;
using Landsong.ECS.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Presentation
{
    // Routing/IO boundary only. Never owns a second copy of live gameplay state.
    public static class EcsSceneFlow
    {
        public const string SceneRoot = "Assets/Landsong/Scenes/";
        public const string Boot = SceneRoot + "Boot.unity";
        public const string Menu = SceneRoot + "Start.unity";
        public const string Loading = SceneRoot + "LoadingTransition.unity";
        public const string Game = SceneRoot + "Game.unity";
        public const string MapSceneRoot = SceneRoot + "EntityMaps/";
        public static readonly string[] BuildScenes = { Boot, Menu, Loading, Game };
        public sealed class Request
        {
            public readonly string MapId;
            public readonly byte[] Snapshot;
            public readonly RunArchive Archive;
            public readonly bool Persist, RecoveredBackup;
            public readonly string DynastyName, Slot;
            public readonly RunArchiveStore Store;
            public Request(string mapId, byte[] snapshot = null, RunArchive archive = null, bool persist = false, bool recoveredBackup = false,string dynastyName=null,string slot=null,RunArchiveStore store=null)
            { MapId = mapId; Snapshot = snapshot; Archive = archive; Persist = persist; RecoveredBackup = recoveredBackup; DynastyName=dynastyName;Slot=slot;Store=store; }
        }
        public static Request Pending { get; private set; }
        public static bool Busy { get; private set; }
        public static bool GameReady { get; private set; }
        public static string MenuMessage { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Pending = null; Busy = GameReady = false; MenuMessage = null; }
        public static bool IsAppScene(string path) => Array.IndexOf(BuildScenes, path) >= 0;
        public static void NewGame(string mapId,string dynastyName=null) => Begin(new Request(mapId, persist: true,dynastyName:string.IsNullOrWhiteSpace(dynastyName)?"无名王朝":RunArchiveStore.DisplayName(dynastyName)));
        public static void ContinueRun(string run,string slot=null,bool backupOnly=false,RunArchiveStore store=null)
        {
            store ??= CheckpointSystem.DefaultStore;bool backup=backupOnly;
            var data=backupOnly?store.ReadBackup(run,slot):slot==null?store.Read(run,out backup):store.ReadSlot(run,slot,out backup);
            Begin(new Request(SnapshotCodec.ReadMapId(data.Current),archive:data,persist:true,recoveredBackup:backup,slot:slot,store:store));
        }
        public static void ContinueGame(RunArchiveStore store=null)
        {
            store ??= CheckpointSystem.DefaultStore;
            var archive = store.ReadContinue(out var backup);
            Begin(new Request(SnapshotCodec.ReadMapId(archive.Current), archive: archive, persist: true, recoveredBackup: backup, store: store));
        }
        public static void ReturnToMenu() => Begin(null);
        // Also used by development-only tests to verify restore without touching the player's save file.
        public static void Begin(Request request)
        {
            if (Busy) return;
            Busy = true; GameReady = false; Pending = request;
            try { SceneManager.LoadSceneAsync(Loading, LoadSceneMode.Single); }
            catch { Busy = false; Pending = null; throw; }
        }
        internal static void EnterGame() { GameReady = true; Busy = false; Pending = null; }
        internal static void EnterMenu(string message = null) { GameReady = Busy = false; Pending = null; MenuMessage = message; }
        public static string TakeMenuMessage() { var value = MenuMessage; MenuMessage = null; return value; }
    }
}
