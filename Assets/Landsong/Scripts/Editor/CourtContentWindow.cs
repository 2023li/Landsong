#if UNITY_EDITOR
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public sealed class CourtContentWindow : EditorWindow
    {
        Vector2 scroll;
        [MenuItem("Landsong/ECS/Court content and rules")]
        static void Open()=>GetWindow<CourtContentWindow>("王室与人才配置");
        void OnGUI()
        {
            var c=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); if(c==null) return;
            EditorGUILayout.HelpBox("配置资产 → Baking → ECS。人才/基因/政策参数修改后需新开王朝。Court 配置年龄死亡概率、政治、交际与出访规则。",MessageType.Info);
            if(GUILayout.Button("选择王朝规则 GameCatalog")) Selection.activeObject=c;
            if(GUILayout.Button("校验人才 / 王室 / 政策")) { CourtContentValidation.Validate(c); Debug.Log("Court content validation PASS"); }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var d in c.Definitions) if(d.Data.Kind==ContentKind.Talent || d.Data.Kind==ContentKind.TalentSlot || d.Data.Kind==ContentKind.RoyalTrait || d.Data.Kind==ContentKind.Policy)
                if(GUILayout.Button(d.Data.Kind+" · "+d.Data.Name+" ["+d.Data.Id+"]")) Selection.activeObject=d;
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
