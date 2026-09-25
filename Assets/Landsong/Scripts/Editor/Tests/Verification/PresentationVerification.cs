#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Landsong.Animation;
using Landsong.EditorTools;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class PresentationVerification
    {
        static StringBuilder log;
        static int count;
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException("FAIL " + name);
            count++;
            log.AppendLine("PASS " + name);
        }

        static void Reject(Action action, string name)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (Exception e)when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/Presentation")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before isolated verification.");
            log = new StringBuilder();
            count = 0;
            try
            {
                Assets();
                Languages();
                Simulation();
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/presentation-verification.txt", log.ToString());
            }
        }

        static void Assets()
        {
            var audio = AssetDatabase.LoadAssetAtPath<AudioCatalog>(ContentAssetPaths.Audio + "/LandsongAudio.asset");
            var visuals = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(ContentAssetPaths.LegacyPresentation + "/LandsongWorldVisuals.asset");
            var effects = AssetDatabase.LoadAssetAtPath<EffectCatalog>(ContentAssetPaths.Effects + "/LandsongEffects.asset");
            var portraits = AssetDatabase.LoadAssetAtPath<PortraitDisplayCatalog>(ContentAssetPaths.Portraits + "/LandsongPortraitDisplay.asset");
            var captions = AssetDatabase.LoadAssetAtPath<NightCaptionDefinition>(ContentAssetPaths.Presentation + "/Night/LandsongNightCaptions.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LanguageContentTools.Path);
            Check(audio != null && visuals != null && effects != null && portraits != null && captions != null && catalog != null, "Six independent presentation configuration assets exist");
            var application = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/UI/Prefabs/Bootstrap/UI_Root.prefab").GetComponent<ApplicationUiRoot>();
            Check(application.Audio.Configuration == audio && application.Localization.Configuration == catalog, "Audio and localization services own only their explicit catalog");
            var game = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab");
            var worldView = game.GetComponent<WorldPresentationView>();
            Check(worldView.Visuals == visuals && worldView.Effects == effects, "World view directly binds models and effects without a catalog facade");
            var firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/粒子/火焰/低模火焰.prefab");
            Check(firePrefab != null && worldView.FirePrefab == firePrefab && firePrefab.GetComponentInChildren<ParticleSystem>(true) != null, "Burning buildings use the authored fire particle prefab");
            var dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/粒子/烟雾/施工烟尘.prefab");
            Check(dustPrefab != null && worldView.ConstructionDustPrefab == dustPrefab && dustPrefab.GetComponentInChildren<ParticleSystem>(true) != null
                && Mathf.Approximately(worldView.ConstructionDustDuration, .5f), "Building view transitions use the authored dust prefab for 0.5 seconds");
            var dustInstance = UnityEngine.Object.Instantiate(dustPrefab);
            try
            {
                dustInstance.transform.localScale = Vector3.one * 1.25f;
                var particles = dustInstance.GetComponentInChildren<ParticleSystem>(true);
                WorldPresentationView.ConfigureConstructionDustFootprint(particles, new Unity.Mathematics.int2(2, 3), 1.5f);
                var area = particles.shape.scale;
                var scale = particles.transform.lossyScale;
                Check(particles.shape.shapeType == ParticleSystemShapeType.BoxEdge
                    && Mathf.Abs(area.x * scale.x - 3f) < .001f
                    && Mathf.Abs(area.z * scale.z - 4.5f) < .001f,
                    "Construction dust emitter covers the rotated building footprint in world units");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dustInstance);
            }
            var militiaView = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/ECSContent/Units/士兵/民兵/militiaView.prefab");
            var militiaAnimation = militiaView.GetComponent<Landsong.Animation.SoldierAnimationVisualAuthoring>();
            Check(militiaAnimation.TorchMount != null && militiaAnimation.TorchMount.name == "TorchMount"
                && militiaAnimation.TorchFlameParticles != null && militiaAnimation.TorchLight != null
                && militiaAnimation.TorchFlameParticles.transform.IsChildOf(militiaAnimation.TorchMount)
                && militiaAnimation.TorchLight.transform.IsChildOf(militiaAnimation.TorchMount)
                && PrefabUtility.GetCorrespondingObjectFromSource(militiaAnimation.TorchFlameParticles.gameObject) == firePrefab,
                "Militia torch socket embeds the authored flame prefab and point light");
            Check(game.GetComponentInChildren<UI_GamePanel_Hud>(true).NightPresentation == captions, "Night HUD owns the caption definition only");
            foreach (var binding in game.GetComponentsInChildren<UI_Common_PortraitImageBinding>(true))
                Check(binding.Portraits == portraits, "Portrait image explicitly binds its own display catalog: " + binding.name);
            WorldPresentationValidation.Verify(visuals, effects);
            Check(true, "All actor/effect templates have complete explicit serialized bindings");
            Check(audio.DayAmbient != null && audio.NightAmbient != null, "Existing licensed project ambient clips reused");
            Check(audio.Cues.Length == Enum.GetValues(typeof(PresentationCue)).Length && audio.Cues.Select(c => c.Id).Distinct().Count() == audio.Cues.Length, "One bounded cue binding for every semantic cue");
            foreach (var cue in audio.Cues)
                Check(cue.Clip != null && cue.Volume >= 0 && cue.Volume <= 1 && cue.Cooldown >= 0 && cue.Concurrency >= 1 && cue.Concurrency <= 16, "Valid audio cue " + cue.Id);
            Check(effects.Cues.Select(c => c.Id).Distinct().Count() == effects.Cues.Length, "Effect cue identifiers are unique");
            foreach (var cue in effects.Cues)
                if (cue.EffectPrefab != null)
                    Check(WorldPresentationValidation.PurePrefab(cue.EffectPrefab.gameObject) && cue.Lifetime > 0 && cue.Lifetime <= 10, "Pure bounded particle prefab " + cue.Id);
            var soldiers = AssetDatabase.LoadAssetAtPath<SoldierCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/SoldierCatalog.asset").Definitions;
            foreach (var definition in soldiers)
            {
                var model = visuals.Select(definition.Metadata.Id, LifeStage.Operational, 1, "");
                if (definition.Prefab != null && definition.Prefab.GetComponent<SoldierAnimationAuthoring>() != null)
                {
                    Check(model == null, "Native animated soldier owns its view without a global model mapping: " + definition.Metadata.Id);
                    continue;
                }

                Check(model != null && WorldPresentationValidation.PurePrefab(model.ActorPrefab.gameObject), "Replaceable pure actor " + definition.Metadata.Id);
                var animator = model.ActorPrefab.Animator;
                Check(animator != null && animator.runtimeAnimatorController != null, "Working placeholder Animator controller " + definition.Metadata.Id);
            }

            var externallyPresented = AssetDatabase.LoadAssetAtPath<HeroCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/HeroCatalog.asset").Definitions.Cast<ScriptableObject>()
                .Concat(AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/EnemyCatalog.asset").Definitions);
            foreach (var definition in externallyPresented)
            {
                var id = Landsong.EditorTools.UnitAuthoringWorkflow.Metadata(definition).Id;
                var prefab = Landsong.EditorTools.UnitAuthoringWorkflow.Prefab(definition);
                var model = visuals.Select(id, LifeStage.Operational, 1, "");
                if (prefab != null && prefab.GetComponent<SoldierAnimationAuthoring>() != null)
                {
                    Landsong.EditorTools.UnitAuthoringWorkflow.Validate(definition);
                    Check(model == null, "Animated hero/enemy owns a valid independent View without a legacy mapping: " + id);
                }
                else
                    Check(model != null && WorldPresentationValidation.PurePrefab(model.ActorPrefab.gameObject), "Legacy external actor remains explicit until animation migration: " + id);
            }

            Check(visuals.Select("missing", LifeStage.Operational, 1, "") == null, "Missing model leaves existing ECS renderer in charge");
            Check(catalog.Text.Select(t => t.Table + "/" + t.Key).Distinct().Count() == catalog.Text.Length, "Semantic language keys unique");
            void Names(System.Collections.Generic.IEnumerable<string> ids)
            {
                foreach (var id in ids)
                    Check(catalog.Text.Any(t => t.Table == "Content" && t.Key == "content." + id + ".name"), "Stable name key " + id);
            }

            Names(AssetDatabase.LoadAssetAtPath<BuffCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuffCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<BuildingLimitGroupCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingLimitGroupCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<CropCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/CropCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/EnemyCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<ExpeditionCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ExpeditionCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<FeatureCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/FeatureCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<HeroCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/HeroCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<ItemGroupCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemGroupCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<LootCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/LootCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<OpportunityCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/OpportunityCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<PolicyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/PolicyCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<PolicyGroupCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/PolicyGroupCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<ProjectileCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ProjectileCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<RoyalTraitCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/RoyalTraitCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<SoldierCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/SoldierCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<StorageSlotCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/StorageSlotCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<TalentCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TalentCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<TalentSlotCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TalentSlotCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            Names(AssetDatabase.LoadAssetAtPath<TechnologyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TechnologyCatalog.asset").Definitions.Select(d => d.Metadata.Id));
            foreach (var table in new[]
            {
                "UI",
                "Content",
                "Gameplay"
            }

            )
                Check(catalog.Text.Any(t => t.Table == table && !string.IsNullOrEmpty(t.En)), "Retained bilingual table " + table);
            var go = new GameObject("Owned unsafe model");
            try
            {
                go.AddComponent<AudioListener>();
                Check(!WorldPresentationValidation.PurePrefab(go), "Listener/gameplay components cannot become model authority");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            var p = InterfaceSettings.Decode("{\"Muted\":true,\"Language\":\"en\"}");
            Check(p.Muted && p.Language == "en", "Language/mute preferences roundtrip without save mutation");
            p.Language = "../outside";
            p.Validate();
            Check(p.Language == "zh-Hans", "Invalid preference language identifier normalized");
        }

        static void Languages()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(LanguageContentTools.Path);
            PresentationText.Initialize(catalog);
            PresentationText.SetLanguage("en");
            Check(PresentationText.Source("暂停菜单") == "Pause", "Fixed text resolves semantic alias");
            Check(PresentationText.Source("主音量  0.42") == "Master volume  0.42", "Dynamic text resolves stable template, not generated value key: " + PresentationText.Source("主音量  0.42"));
            Check(PresentationText.Source("玩家自定义国库乙") == "玩家自定义国库乙", "Unknown player name preserved verbatim");
            Check(PresentationText.Get("missing", "缺失 {0}", 12) == "缺失 12", "Missing key has formatted Chinese fallback");
            Check(!ExternalLanguagePack.Compatible("数量 {0}", "Count {1}") && !ExternalLanguagePack.Compatible("{99999999999999999}", "{99999999999999999}"), "Missing/oversized parameter IDs rejected");
            Check(ExternalLanguagePack.Compatible("数 {0:0.00}", "Amount {0:0.0}") && ExternalLanguagePack.Compatible("括号 {{0}}", "Braces {{0}}"), "Numeric formats and escaped braces accepted");
            var csv = ExternalLanguagePack.Csv("Table,Key,Text\r\nUI,k,\"comma, and \"\"quote\"\"\nnew line\"\r\n");
            Check(csv.Count == 2 && csv[1][2] == "comma, and \"quote\"\nnew line", "CSV quoted commas, multiline, escaped quotes and CRLF");
            Reject(() => ExternalLanguagePack.Csv("a,b,\"unterminated"), "Unclosed CSV quote rejected");
            Reject(() => ExternalLanguagePack.Csv("a,b,\"x\"z"), "Trailing quoted cell junk rejected");
            string directory = Path.Combine(Path.GetTempPath(), "Landsong-15-Language-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string folder = Path.Combine(directory, "pack");
            Directory.CreateDirectory(folder);
            var manifest = new ExternalLanguagePack.Manifest
            {
                schemaVersion = 1,
                targetKeysetVersion = PresentationText.KeysetVersion,
                packId = "owned-test",
                displayName = "Owned",
                localeCode = "en",
                fallbackLocaleCode = "en"
            };
            void Write(string text)
            {
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest), ExternalLanguagePack.Utf8);
                File.WriteAllText(Path.Combine(folder, "strings.csv"), text, ExternalLanguagePack.Utf8);
            }

            try
            {
                const string valid = "Table,Key,Text\nUI,ui.ecs.pause,<b>Owned pause</b>\nUI,ui.ecs.cancel,\nUI,unknown,ignored\n";
                Write(valid);
                LanguageContentTools.ExportTemplate();
                File.Copy("Library/LandsongEcs/language-template.csv", Path.Combine(folder, "strings.csv"), true);
                Check(ExternalLanguagePack.Load(folder, PresentationText.Keyset).Strings.Count == catalog.Text.Length, "Exported full language template is reloadable");
                Write(valid);
                var pack = ExternalLanguagePack.Load(folder, PresentationText.Keyset);
                Check(pack.Strings["UI/ui.ecs.pause"] == "Owned pause" && pack.Strings["UI/ui.ecs.cancel"] == "" && !pack.Strings.ContainsKey("UI/unknown"), "Text-only pack strips markup, permits empty static text, ignores unknown keys");
                PresentationText.Discover(directory);
                PresentationText.SetLanguage("owned-test");
                Check(PresentationText.Source("暂停菜单") == "Owned pause" && PresentationText.Get("UI/ui.ecs.resume", "回到游戏") == "Resume", "External text with built-in English fallback");
                Write("Table,Key,Text\nUI,ui.ecs.pause,x\nUI,ui.ecs.pause,y\n");
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "Duplicate key rejects entire pack");
                PresentationText.Discover(directory);
                Check(PresentationText.Source("暂停菜单") == "Owned pause" && PresentationText.Diagnostics.Contains("保留"), "Invalid active reload preserves last successful text atomically");
                Write("Table,Key,Text\nGameplay,gameplay.ecs.turn,Turn {1}\n");
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "External parameter mismatch rejected");
                Write(valid);
                File.WriteAllBytes(Path.Combine(folder, "strings.csv"), new byte[] { 0xff, 0xfe, 0xff });
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "Invalid UTF-8 rejected");
                Write(valid);
                File.WriteAllText(Path.Combine(folder, "strings.csv"), new string ('a', 2 * 1024 * 1024 + 1));
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "Oversize pack rejected before parsing");
                Write(valid);
                string duplicate = Path.Combine(directory, "duplicate");
                Directory.CreateDirectory(duplicate);
                File.Copy(Path.Combine(folder, "manifest.json"), Path.Combine(duplicate, "manifest.json"));
                File.Copy(Path.Combine(folder, "strings.csv"), Path.Combine(duplicate, "strings.csv"));
                PresentationText.Discover(directory);
                Check(!PresentationText.Packs.ContainsKey("owned-test") && PresentationText.Diagnostics.Contains("重复"), "Duplicate pack IDs disable both instead of directory-order selection");
                manifest.packId = "en";
                Write(valid);
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "External pack cannot shadow built-in locale");
                manifest.packId = "../outside";
                Write(valid);
                Reject(() => ExternalLanguagePack.Load(folder, PresentationText.Keyset), "Manifest path-like ID rejected");
            }
            finally
            {
                Directory.Delete(directory, true);
                PresentationText.Initialize(catalog);
                PresentationText.SetLanguage("zh-Hans");
            }
        }

        static void Simulation()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Wave fifteen isolated", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var s = em.GetComponentData<Session>(root);
                PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                sPersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sPersistence);
                }

                var before = SnapshotCodec.Capture(em, root);
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var entity in buildings)
                        EntityState.Set(em, entity, new ExternalVisual { Active = 1 });
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Renderer ownership is transient, never a snapshot/gameplay field");
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                {
                    var e = buildings[0];
                    var original = em.GetComponentData<Building>(e);
                    BuildingMaintenanceState originalMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
                    foreach (var pair in new[]
                    {
                        (LifeStage.Construction, ActorPose.Construction),
                        (LifeStage.Repairing, ActorPose.Repairing),
                        (LifeStage.Ruined, ActorPose.Ruined),
                        (LifeStage.Operational, ActorPose.Working)
                    }

                    )
                    {
                        var b = original;
                        BuildingMaintenanceState bMaintenance = originalMaintenance;
                        b.Stage = pair.Item1;
                        bMaintenance.Maintained = 1;
                        {
                            em.SetComponentData(e, b);
                            em.SetComponentData(e, bMaintenance);
                        }

                        Check(WorldPresentationView.Pose(em, e, 0) == pair.Item2, "Building presentation pose " + pair.Item1);
                    }

                    {
                        em.SetComponentData(e, original);
                        em.SetComponentData(e, originalMaintenance);
                    }
                }

                var visual = new GameObject("Owned presentation actor");
                try
                {
                    var actor = visual.AddComponent<PresentationActor>();
                    var model = new GameObject("Model");
                    model.transform.SetParent(visual.transform, false);
                    actor.MovingPart = model.transform;
                    actor.Apply(ActorPose.Moving, 1, false, false, .1f);
                    var position = model.transform.position;
                    actor.Apply(ActorPose.Moving, 1, true, false, .5f);
                    Check(model.transform.position == position, "Paused actor keeps presentation clock frozen");
                    actor.Apply(ActorPose.Moving, 1, false, true, .5f);
                    Check(model.transform.localPosition == Vector3.zero, "Reduced motion resets bob offset");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(visual);
                }

                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Pose and model inspection never changes authority");
                Entity target;
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    target = buildings[0];
                em.GetBuffer<GameEvent>(root).Clear();
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = target, Amount = 1 });
                Check(em.GetBuffer<GameEvent>(root).Length == 1 && em.GetBuffer<GameEvent>(root)[0].Kind == EventKind.Damage && em.GetBuffer<GameEvent>(root)[0].Position.Equals(EntityState.Position(em, target)), "Actual mitigated damage publishes position without text/reward payload");
                int events = em.GetBuffer<GameEvent>(root).Length;
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = target, Amount = 0 });
                Check(em.GetBuffer<GameEvent>(root).Length == events, "Rejected zero damage has no phantom Hit cue");
                var enemy = EnemyEntities.Spawn(em, root, EnemyDefinitions.Find(em, root, "raider"), EntityState.Position(em, target), false);
                EnemyCombatants.Configure(em, root, enemy, true, em.GetComponentData<Identity>(target).Id, EntityState.Position(em, target));
                var actorData = em.GetComponentData<Combatant>(enemy);
                actorData.ProtectedUntil = 0;
                em.SetComponentData(enemy, actorData);
                em.SetComponentData(enemy, new Health { Maximum = 10, Current = 10 });
                em.GetBuffer<GameEvent>(root).Clear();
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = enemy, Amount = 9999 });
                int deaths = 0;
                foreach (var e in em.GetBuffer<GameEvent>(root))
                    if (e.Kind == EventKind.Death)
                        deaths++;
                Check(deaths == 1 && WorldPresentationView.Pose(em, enemy, 0) == ActorPose.Dead, "Ordinary death publishes once and drives a dead pose from health");
                CombatOps.Death(em, root, enemy, Entity.Null);
                deaths = 0;
                foreach (var e in em.GetBuffer<GameEvent>(root))
                    if (e.Kind == EventKind.Death)
                        deaths++;
                Check(deaths == 1, "Repeated death cannot replay death cue or rewards");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
