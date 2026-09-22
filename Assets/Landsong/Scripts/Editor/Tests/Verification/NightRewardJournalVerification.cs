#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class NightRewardJournalVerification
    {
        [MenuItem("Landsong/ECS/Verification/Night reward identities")]
        public static string Run()
        {
            var report = new StringBuilder();
            int assertions = 0;
            void Check(bool value, string label)
            {
                if (!value)
                    throw new InvalidOperationException("FAIL " + label);
                assertions++;
                report.AppendLine("PASS " + label);
            }

            try
            {
                using var world = new World("Night reward entry identities");
                var em = world.EntityManager;
                var root = em.CreateEntity();
                using var itemsBuilder = new BlobBuilder(Allocator.Temp);
                ref var itemRoot = ref itemsBuilder.ConstructRoot<ItemCatalogBlob>();
                itemsBuilder.Allocate(ref itemRoot.Definitions, 1);
                using var items = itemsBuilder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
                using var buildingsBuilder = new BlobBuilder(Allocator.Temp);
                ref var buildingRoot = ref buildingsBuilder.ConstructRoot<BuildingCatalogBlob>();
                buildingsBuilder.Allocate(ref buildingRoot.Definitions, 1)[0].MaximumLevel = 3;
                using var buildings = buildingsBuilder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                using var buffsBuilder = new BlobBuilder(Allocator.Temp);
                ref var buffRoot = ref buffsBuilder.ConstructRoot<BuffCatalogBlob>();
                buffsBuilder.Allocate(ref buffRoot.Definitions, 1);
                using var buffs = buffsBuilder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent);
                using var featuresBuilder = new BlobBuilder(Allocator.Temp);
                ref var featureRoot = ref featuresBuilder.ConstructRoot<FeatureCatalogBlob>();
                featuresBuilder.Allocate(ref featureRoot.Definitions, 1);
                using var features = featuresBuilder.CreateBlobAssetReference<FeatureCatalogBlob>(Allocator.Persistent);
                using var opportunityBuilder = new BlobBuilder(Allocator.Temp);
                ref var opportunityRoot = ref opportunityBuilder.ConstructRoot<OpportunityCatalogBlob>();
                var opportunities = opportunityBuilder.Allocate(ref opportunityRoot.Definitions, 2);
                for (int i = 0; i < opportunities.Length; i++)
                {
                    ref var rewards = ref opportunities[i].Rewards;
                    var itemRewards = opportunityBuilder.Allocate(ref rewards.Items, 2);
                    itemRewards[0] = new ItemAmount
                    {
                        Item = ItemId.FromIndex(0),
                        Quantity = 2,
                        Order = i == 0 ? 0 : 3
                    };
                    itemRewards[1] = new ItemAmount
                    {
                        Item = ItemId.FromIndex(0),
                        Quantity = 3,
                        Order = 0
                    };
                    opportunityBuilder.Allocate(ref rewards.Blueprints, 1)[0] = new BlueprintReward
                    {
                        Building = BuildingId.FromIndex(0),
                        GrantedLevel = 1,
                        Order = 0
                    };
                    opportunityBuilder.Allocate(ref rewards.Buffs, 1)[0] = new BuffReward
                    {
                        Buff = BuffId.FromIndex(0),
                        GrantedLevel = 1,
                        Order = 0
                    };
                    opportunityBuilder.Allocate(ref rewards.Features, 1)[0] = new FeatureReward
                    {
                        Feature = FeatureId.FromIndex(0),
                        GrantedLevel = 1,
                        Order = 0
                    };
                }

                using var catalog = opportunityBuilder.CreateBlobAssetReference<OpportunityCatalogBlob>(Allocator.Persistent);
                em.AddComponentData(root, new ItemCatalog { Value = items });
                em.AddComponentData(root, new BuildingCatalog { Value = buildings });
                em.AddComponentData(root, new BuffCatalog { Value = buffs });
                em.AddComponentData(root, new FeatureCatalog { Value = features });
                em.AddComponentData(root, new OpportunityCatalog { Value = catalog });
                em.AddComponentData(root, new NightResultState { Turn = 1 });
                em.AddBuffer<NightItemReward>(root);
                em.AddBuffer<NightBlueprintReward>(root);
                em.AddBuffer<NightBuffReward>(root);
                em.AddBuffer<NightFeatureReward>(root);
                em.AddBuffer<BattleReportEntry>(root);
                NightResultOps.RecordOpportunityRewards(em, root, 41, OpportunityId.FromIndex(0));
                var recorded = Journal(em, root, 41);
                Check(recorded.Length == 5, "Equal authored order retains two item rewards plus blueprint, buff and feature rewards");
                Check(recorded.Select(row => row.Entry).Distinct().Count() == 5, "Every reward across typed arrays has its own stable entry identity");
                Check(recorded.Select(row => row.Entry).SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "Equal order preserves deterministic array and row order");
                Check(em.GetBuffer<NightItemReward>(root)[0].Quantity == 2 && em.GetBuffer<NightItemReward>(root)[1].Quantity == 3, "Equal order within the same item array does not merge or discard quantities");
                NightResultOps.RecordOpportunityRewards(em, root, 41, OpportunityId.FromIndex(0));
                Check(Journal(em, root, 41).SequenceEqual(recorded) && em.GetBuffer<BattleReportEntry>(root).Length == 5, "Reentry records no duplicate rewards or report entries");
                NightResultOps.RecordOpportunityRewards(em, root, 42, OpportunityId.FromIndex(1));
                var reordered = Journal(em, root, 42);
                Check(reordered.Select(row => row.Entry).SequenceEqual(new[] { 1, 2, 3, 4, 0 }), "Authored order changes delivery sequence without changing stable entry identity");
                NightResultOps.RecordOpportunityRewards(em, root, 42, OpportunityId.FromIndex(1));
                Check(Journal(em, root, 42).SequenceEqual(reordered) && em.GetBuffer<BattleReportEntry>(root).Length == 10, "A different source owns an independent idempotent reward set");
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/night-reward-journal-verification.txt", report.ToString());
            }
        }

        static (int Entry, int Sequence)[] Journal(EntityManager em, Entity root, ulong source)
        {
            var rows = new List<(int Entry, int Sequence)>();
            foreach (var reward in em.GetBuffer<NightItemReward>(root))
                if (reward.Source == source)
                    rows.Add((reward.Entry, reward.Sequence));
            foreach (var reward in em.GetBuffer<NightBlueprintReward>(root))
                if (reward.Source == source)
                    rows.Add((reward.Entry, reward.Sequence));
            foreach (var reward in em.GetBuffer<NightBuffReward>(root))
                if (reward.Source == source)
                    rows.Add((reward.Entry, reward.Sequence));
            foreach (var reward in em.GetBuffer<NightFeatureReward>(root))
                if (reward.Source == source)
                    rows.Add((reward.Entry, reward.Sequence));
            return rows.OrderBy(row => row.Sequence).ToArray();
        }
    }
}
#endif
