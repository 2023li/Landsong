using System;
using System.Collections;
using Landsong.AudioSystem;
using Landsong.AppSystem;

using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

public class AppBoot : MonoBehaviour
{
    [SerializeField]
    private bool autoStart;
    [SerializeField]
    private float autoStartTime=2;
   
    private bool starting = false;

    private void Awake()
    {
       
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {


        if (autoStart)
        {
            StartCoroutine(AutoStart());

          
        }
      

    }

    [Button]
    private void LoadStart()
    {
        if (starting) return;

        starting = true;

        InitializeManagers();

       LoadScene_Start.Load();

        starting = false;
    }

    private void InitializeManagers()
    {
        _ = AppManager.Instance;
        IOManager.Instance.Initialize();
        DataManager.Instance.Initialize();
        AudioPlayer.Instance.Initialize();
        GameLocalizationManager.Instance.Initialize();
    }

    private IEnumerator AutoStart()
    {
        yield return new WaitForSeconds(autoStartTime);

       
            LoadStart();
        
       
    }
   
}
