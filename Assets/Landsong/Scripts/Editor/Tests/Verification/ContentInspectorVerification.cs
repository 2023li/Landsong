#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.ECS.Authoring;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ContentInspectorVerification
    {
        static readonly (string path,string label)[] Labels={
            ("Data.Name","名称"),("Data.Level","最高等级"),("Data.Building.CanMove","允许移动"),
            ("Data.Building.RuinMovementCost","废墟通行消耗"),("Data.QuestWeight","任务抽取权重"),
            ("Data.Combat.DetectionRadius","索敌半径"),("Data.Modules","建筑功能模块"),
            ("Data.Modules.Construction","建造与施工"),("Data.Modules.Construction.PlacementCosts","放置材料"),
            ("Data.Modules.Upgrade.MaintenanceRequirements","升级需维护"),
            ("Data.Modules.Workforce.EfficiencyTiers","工作效率档位"),("Data.Modules.Garrison.InitialUnits","开局驻军"),("Data.Configuration.Objectives.SubmittedItems","提交物品"),("Data.Configuration.Rewards.Blueprints","蓝图奖励"),("Data.Configuration.Expeditions.Supplies","远征补给")};
        public static string Run()
        {
            var log=new StringBuilder();int checks=0;
            void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);checks++;log.AppendLine("PASS "+message);}
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var copy=UnityEngine.Object.Instantiate(catalog.Definitions[catalog.Find("b王宫")]);
            var before=EditorJsonUtility.ToJson(copy);var inspector=UnityEditor.Editor.CreateEditor(copy);
            try
            {
                Check(inspector is ContentInspector&&inspector is OdinEditor,"Content asset uses the Odin inspector entry point");
                using var tree=PropertyTree.Create(copy);tree.UpdateTree();
                foreach(var (path,label) in Labels)
                {
                    var property=tree.GetPropertyAtPath(path);var attribute=property?.GetAttribute<LabelTextAttribute>();
                    Check(attribute!=null&&ValueResolver.GetForString(property,attribute.Text).GetValue()==label,"Odin resolves Chinese field: "+path);
                }
                var types=new[]{typeof(ContentSource),typeof(BuildingPolicySource),typeof(CombatProfile),typeof(SoldierGrowth),typeof(HeroGrowth),typeof(OpportunityProfile),typeof(TheftProfile),typeof(ContentModules),typeof(OrderedContentEntry),typeof(BuildingModules)}
                    .Concat(typeof(ContentModules).GetFields().Select(f=>f.FieldType)).Concat(typeof(OrderedContentEntry).Assembly.GetTypes().Where(t=>t.IsSubclassOf(typeof(OrderedContentEntry)))).Concat(typeof(BuildingModules).GetFields().Select(f=>f.FieldType)).Concat(typeof(BuildingModuleEntry).Assembly.GetTypes().Where(t=>t.IsSubclassOf(typeof(BuildingModuleEntry))));
                Check(types.SelectMany(t=>t.GetFields(BindingFlags.Public|BindingFlags.Instance)).All(f=>f.GetCustomAttribute<LabelTextAttribute>()!=null),"All ContentSource and nested module/profile fields carry Odin Chinese labels");
                Check(types.SelectMany(t=>t.GetFields(BindingFlags.Public|BindingFlags.Instance)).All(f=>f.GetCustomAttribute<MinAttribute>()==null),"Localized Odin fields avoid the Unity Min drawer that overrides Chinese labels");
                Check(EditorJsonUtility.ToJson(copy)==before,"Reading Odin labels does not rewrite serialized data");
                var soldier=copy.Data.Modules.Garrison.InitialUnits[0].Soldier;
                const string pathRef="Data.Modules.Garrison.InitialUnits.Array.data[0].Soldier";
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
                try
                {
                    ContentReferenceDrawer.Assign(copy,pathRef,null,ContentKind.Soldier);Undo.FlushUndoRecordObjects();
                    Check(copy.Data.Modules.Garrison.InitialUnits[0].Soldier==null,"Odin reference picker commits through Unity serialization");
                    Undo.RevertAllDownToGroup(group);
                    Check(copy.Data.Modules.Garrison.InitialUnits[0].Soldier==soldier,"Undo restores the exact referenced soldier asset");
                    bool rejected=false;try{ContentReferenceDrawer.Assign(copy,pathRef,catalog.Definitions[catalog.Find("金币")],ContentKind.Soldier);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&copy.Data.Modules.Garrison.InitialUnits[0].Soldier==soldier,"Wrong asset type is rejected without replacing the reference");
                }
                finally{Undo.ClearUndo(copy);}
                log.AppendLine("Assertions: "+checks);return log.ToString();
            }
            finally{UnityEngine.Object.DestroyImmediate(inspector);UnityEngine.Object.DestroyImmediate(copy);File.WriteAllText("Library/LandsongEcs/content-inspector-verification.txt",log.ToString());}
        }
        public static void RunGui()=>ContentInspectorProbe.Open();
    }
    public sealed class ContentInspectorProbe : EditorWindow
    {
        ContentInspector inspector;GameDefinitionAsset copy;Vector2 scroll;int frames, captureStage;bool complete;double started;
        const string Report="Library/LandsongEcs/content-inspector-gui.txt";
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play");
            var window=CreateInstance<ContentInspectorProbe>();window.titleContent=new GUIContent("内容检查器验证");window.position=new Rect(100,80,820,900);
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            window.copy=Instantiate(catalog.Definitions.First(d=>d.Data.Kind==ContentKind.Building&&d.Data.Modules.Construction.Enabled&&d.Data.Level>1));
            window.inspector=(ContentInspector)UnityEditor.Editor.CreateEditor(window.copy);window.started=EditorApplication.timeSinceStartup;window.ShowUtility();window.Focus();
            File.WriteAllText(Report,"Started "+DateTimeOffset.Now.ToString("O"));
        }
        void Update()
        {
            if(complete){Close();return;}
            if(inspector==null){Close();return;}
            // Own the capture state in Update: delayCall may be postponed indefinitely by an idle editor.
            if(captureStage==1)
            {
                try{Capture("content-inspector-top.png");scroll.y=900;frames=0;captureStage=2;Repaint();}
                catch(Exception error){complete=true;File.WriteAllText(Report,"FAIL "+error);}
                return;
            }
            if(captureStage==3){complete=true;Finish();return;}
            if(EditorApplication.timeSinceStartup-started>45){complete=true;File.WriteAllText(Report,"INCOMPLETE "+DateTimeOffset.Now.ToString("O")+"\nInspector did not receive visible repaint events; restore the interactive desktop and retry.");Close();}
            else Repaint();
        }
        void OnGUI()
        {
            if(inspector==null)return;
            try
            {
                EditorGUIUtility.labelWidth=280;scroll=EditorGUILayout.BeginScrollView(scroll);inspector.OnInspectorGUI();EditorGUILayout.EndScrollView();
                var tree=inspector.Tree;
                foreach(var path in new[]{"Data","Data.Building","Data.Combat","Data.Modules","Data.Modules.Construction","Data.Modules.Construction.PlacementCosts","Data.Modules.Upgrade","Data.Modules.Maintenance","Data.Modules.Workforce"})
                {var property=tree.GetPropertyAtPath(path);if(property!=null)property.State.Expanded=true;}
                if(Event.current.type==EventType.Repaint&&!complete&&++frames>=8)
                {
                    if(captureStage==0)captureStage=1;
                    else if(captureStage==2)captureStage=3;
                }
                Repaint();
            }
            catch(Exception error){complete=true;File.WriteAllText(Report,"FAIL "+error);throw;}
        }
        void Capture(string name)
        {
            var pixels=UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position,(int)position.width,(int)position.height);
            var texture=new Texture2D((int)position.width,(int)position.height,TextureFormat.RGB24,false);
            try{texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes("Library/LandsongEcs/"+name,texture.EncodeToPNG());}finally{DestroyImmediate(texture);}
        }
        void Finish(){try{Capture("content-inspector-modules.png");File.WriteAllText(Report,"PASS "+DateTimeOffset.Now.ToString("O")+"\nActual Odin ContentInspector drawn with expanded nested modules; temporary asset only.");}catch(Exception error){File.WriteAllText(Report,"FAIL "+error);}finally{Close();}}
        void OnDisable(){if(inspector!=null)DestroyImmediate(inspector);if(copy!=null)DestroyImmediate(copy);}
    }
}
#endif
