using System;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Common runtime scene shell. SubScene references also declare the entity scenes for player builds.
    public sealed class EcsGameHost : MonoBehaviour
    {
        [LabelText("地图目录"), Required] public EcsMapMenuCatalog Catalog;
        [LabelText("地图实体子场景"), Required] public SubScene[] Maps;
        [LabelText("世界相机"), Required] public Camera Camera;
        [LabelText("场景主光源"), Required] public Light Sun;
        public bool Visible { get; private set; }
        void Awake()
        {
            if(Catalog==null||Maps==null||Maps.Length==0||Camera==null||Sun==null)throw new InvalidOperationException("Game 场景宿主检查器引用不完整。");
            SetVisible(false);
        }
        public Entity LoadMap(string id, World world)
        {
            if (Catalog == null || Maps == null || Catalog.Maps.Length != Maps.Length) throw new InvalidOperationException("地图目录与 SubScene 配置不一致。");
            for (var i = 0; i < Maps.Length; i++)
            {
                if (Maps[i] == null || Maps[i].AutoLoadScene) throw new InvalidOperationException("Game 的地图 SubScene 必须配置引用并关闭 Auto Load Scene。");
                for (var j = 0; j < i; j++)
                    if (Catalog.Maps[i].Id == Catalog.Maps[j].Id || Maps[i].SceneGUID == Maps[j].SceneGUID) throw new InvalidOperationException("地图 ID 或 SubScene 重复配置。");
            }
            var index = Array.FindIndex(Catalog.Maps, m => m.Id == id);
            if (index < 0 || Maps[index] == null || !Maps[index].SceneGUID.IsValid) throw new InvalidOperationException("找不到地图：" + id);
            return SceneSystem.LoadSceneAsync(world.Unmanaged, Maps[index].SceneGUID);
        }
        public void FocusCore(EntityManager em, Entity root)
        {
            using var buildings = Sim.Entities<Building>(em);
            foreach (var entity in buildings)
                if (em.GetComponentData<BuildingStats>(entity).IsCore != 0)
                { Camera.transform.position = (Vector3)Sim.Position(em, entity) + new Vector3(0, 28, -20); break; }
        }
        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (Camera != null) Camera.gameObject.SetActive(visible);
            if (Sun != null) Sun.gameObject.SetActive(visible);
        }
    }
}
