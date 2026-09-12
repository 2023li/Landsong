#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Odin draws editable fields from attributes. This is a read-only capability quote.
    public static class BuildingModuleInspector
    {
        public static void DrawSummary(ContentSource source)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            try { BuildingModuleValidation.Validate(source,catalog); }
            catch(Exception error) { EditorGUILayout.HelpBox(error.Message,MessageType.Error);return; }
            EditorGUILayout.HelpBox("配置检查通过。开局驻军仅用于新游戏中的初始建筑。",MessageType.Info);
            EditorGUILayout.LabelField("等级能力预览",EditorStyles.boldLabel);var rules=BuildingModuleCompiler.Compile(source.Modules,new ContentReferenceResolver(catalog));
            for(int level=1;level<=source.Level;level++)
            {
                var active=rules.Where(r=>r.Level==0||r.Level==level).ToArray();var parts=new List<string>();
                int Sum(RuleKind kind)=>active.Where(r=>r.Kind==kind).Sum(r=>r.Amount);
                void Add(string label,int count){if(count>0)parts.Add(label+" "+count);}
                Add("人口",Sum(RuleKind.Population)+Sum(RuleKind.Residence));Add("岗位",Sum(RuleKind.Workforce));Add("库存格",Sum(RuleKind.Warehouse));Add("科研/回合",Sum(RuleKind.ResearchOutput));Add("驻军槽",Sum(RuleKind.Garrison));Add("开局驻军",Sum(RuleKind.InitialGarrison));Add("任务槽",Sum(RuleKind.QuestCapacity));Add("邀约槽",Sum(RuleKind.QuestSource));
                EditorGUILayout.LabelField("等级 "+level,parts.Count==0?"无上述容量产出":string.Join(" · ",parts),EditorStyles.wordWrappedLabel);
            }
        }
    }
    // Odin supplies LabelText; Unity serialized paths retain asset identity and undo.
    public sealed class ContentReferenceDrawer : OdinAttributeDrawer<ContentReferenceAttribute,GameDefinitionAsset>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect=EditorGUILayout.GetControlRect();var field=label==null?rect:EditorGUI.PrefixLabel(rect,label);
            var select=new Rect(field.x,field.y,field.width-25,field.height);var ping=new Rect(field.xMax-23,field.y,23,field.height);
            var asset=ValueEntry.SmartValue;
            var caption=asset==null?(Attribute.Optional?"未指定（可选）":"请选择"):asset.Data.Name+" ["+asset.Data.Id+"]";
            if(GUI.Button(select,caption,EditorStyles.popup))
            {
                var owner=Property.Tree.WeakTargets[0] as UnityEngine.Object;var path=Property.UnityPropertyPath;
                var menu=new GenericMenu();menu.AddItem(new GUIContent("清空"),asset==null,()=>Assign(owner,path,null,Attribute.Kinds));
                foreach(var candidate in Candidates(Attribute.Kinds))
                {var choice=candidate;menu.AddItem(new GUIContent(choice.Data.Name+" ["+choice.Data.Id+"]"),asset==choice,()=>Assign(owner,path,choice,Attribute.Kinds));}
                menu.DropDown(select);
            }
            if(GUI.Button(ping,"↗")&&asset!=null)EditorGUIUtility.PingObject(asset);
            if((Event.current.type==EventType.DragUpdated||Event.current.type==EventType.DragPerform)&&field.Contains(Event.current.mousePosition))
            {
                var candidates=Candidates(Attribute.Kinds);var candidate=DragAndDrop.objectReferences.OfType<GameDefinitionAsset>().FirstOrDefault(candidates.Contains);
                DragAndDrop.visualMode=candidate!=null?DragAndDropVisualMode.Copy:DragAndDropVisualMode.Rejected;
                if(candidate!=null&&Event.current.type==EventType.DragPerform){DragAndDrop.AcceptDrag();ValueEntry.SmartValue=candidate;}Event.current.Use();
            }
        }
        public static GameDefinitionAsset[] Candidates(params ContentKind[] kinds)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            return catalog==null?Array.Empty<GameDefinitionAsset>():catalog.Definitions.Where(d=>d!=null&&d.Data!=null&&(kinds.Length==0||kinds.Contains(d.Data.Kind))).OrderBy(d=>d.Data.Name).ToArray();
        }
        public static void Assign(UnityEngine.Object owner,string path,GameDefinitionAsset value,params ContentKind[] kinds)
        {
            if(owner==null)return;
            if(value!=null&&!Candidates(kinds).Contains(value))throw new InvalidOperationException("只能选择当前目录中对应类型的内容");
            using var serialized=new SerializedObject(owner);serialized.Update();var property=serialized.FindProperty(path);
            if(property==null)return;property.objectReferenceValue=value;serialized.ApplyModifiedProperties();
        }
    }
}
#endif
