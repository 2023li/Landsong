using System;
using System.Collections;
using System.Threading.Tasks;
using Moyo.Unity;
using UnityEngine;

    public class LoadScene_Start : SceneLoadingPipeline
    {
        public override string TargetSceneName => "Start";


        UIPanel_Loading panel;



        public override IEnumerator OnPrepareTransition()
        {
            Task<UIPanel_Loading> openTask = UIManager.Instance.OpenAsync<UIPanel_Loading>(UIPanel_Loading.LoadingItemType.Item_标题);

            yield return new WaitUntil(() => openTask.IsCompleted);

            panel = openTask.Result;

        }

       

        public override IEnumerator OnBeginWaitPlayerConfirm()
        {
            yield return UIManager.Instance.PreloadAsync<UIPanel_MainMenu>();

            panel?.BeginWaitPlayerConfirm();

            yield return base.OnBeginWaitPlayerConfirm();
        }
        public override IEnumerator OnPlayerConfirmed()
        {
            yield return UIManager.Instance.OpenAsync<UIPanel_MainMenu>();
            yield return null;

            yield return UIManager.Instance.CloseAsync<UIPanel_Loading>();
        }

        public static bool Load()
        {
            return SceneTransitionManager.Instance.StartTransition(new LoadScene_Start());
        }

        public static bool ReturnFromGame()
        {
            UIManager.Instance.CloseAsync<UIPanel_Game>();

            return SceneTransitionManager.Instance.StartTransition(new LoadScene_Start());
        }
    }

    public class LoadScene_Game : SceneLoadingPipeline
    {
        public override string TargetSceneName => throw new System.NotImplementedException();

        internal static bool Load()
        {
            throw new NotImplementedException();
        }

        public override IEnumerator OnPlayerConfirmed()
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator OnPrepareTransition()
        {
            throw new System.NotImplementedException();
        }
    }
