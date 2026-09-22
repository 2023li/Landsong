using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    internal static class SnapshotContentFingerprint
    {
        internal static string Compute(EntityManager em, Entity root)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("Item");
            writer.Write(ItemDefinitions.Count(em, root));
            for (int i = 0; i < ItemDefinitions.Count(em, root); i++)
            {
                ref var definition = ref ItemDefinitions.Get(em, root, ItemId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("ItemGroup");
            writer.Write(ItemGroupDefinitions.Count(em, root));
            for (int i = 0; i < ItemGroupDefinitions.Count(em, root); i++)
            {
                ref var definition = ref ItemGroupDefinitions.Get(em, root, ItemGroupId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("StorageSlot");
            writer.Write(StorageSlotDefinitions.Count(em, root));
            for (int i = 0; i < StorageSlotDefinitions.Count(em, root); i++)
            {
                ref var definition = ref StorageSlotDefinitions.Get(em, root, StorageSlotId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Building");
            writer.Write(BuildingDefinitions.Count(em, root));
            for (int i = 0; i < BuildingDefinitions.Count(em, root); i++)
            {
                ref var definition = ref BuildingDefinitions.Get(em, root, BuildingId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("BuildingLimitGroup");
            writer.Write(BuildingLimitGroupDefinitions.Count(em, root));
            for (int i = 0; i < BuildingLimitGroupDefinitions.Count(em, root); i++)
            {
                ref var definition = ref BuildingLimitGroupDefinitions.Get(em, root, BuildingLimitGroupId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Soldier");
            writer.Write(SoldierDefinitions.Count(em, root));
            for (int i = 0; i < SoldierDefinitions.Count(em, root); i++)
            {
                ref var definition = ref SoldierDefinitions.Get(em, root, SoldierId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Enemy");
            writer.Write(EnemyDefinitions.Count(em, root));
            for (int i = 0; i < EnemyDefinitions.Count(em, root); i++)
            {
                ref var definition = ref EnemyDefinitions.Get(em, root, EnemyId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Hero");
            writer.Write(HeroDefinitions.Count(em, root));
            for (int i = 0; i < HeroDefinitions.Count(em, root); i++)
            {
                ref var definition = ref HeroDefinitions.Get(em, root, HeroId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Technology");
            writer.Write(TechnologyDefinitions.Count(em, root));
            for (int i = 0; i < TechnologyDefinitions.Count(em, root); i++)
            {
                ref var definition = ref TechnologyDefinitions.Get(em, root, TechnologyId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Quest");
            writer.Write(QuestDefinitions.Count(em, root));
            for (int i = 0; i < QuestDefinitions.Count(em, root); i++)
            {
                ref var definition = ref QuestDefinitions.Get(em, root, QuestId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Buff");
            writer.Write(BuffDefinitions.Count(em, root));
            for (int i = 0; i < BuffDefinitions.Count(em, root); i++)
            {
                ref var definition = ref BuffDefinitions.Get(em, root, BuffId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Policy");
            writer.Write(PolicyDefinitions.Count(em, root));
            for (int i = 0; i < PolicyDefinitions.Count(em, root); i++)
            {
                ref var definition = ref PolicyDefinitions.Get(em, root, PolicyId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("PolicyGroup");
            writer.Write(PolicyGroupDefinitions.Count(em, root));
            for (int i = 0; i < PolicyGroupDefinitions.Count(em, root); i++)
            {
                ref var definition = ref PolicyGroupDefinitions.Get(em, root, PolicyGroupId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Expedition");
            writer.Write(ExpeditionDefinitions.Count(em, root));
            for (int i = 0; i < ExpeditionDefinitions.Count(em, root); i++)
            {
                ref var definition = ref ExpeditionDefinitions.Get(em, root, ExpeditionId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Talent");
            writer.Write(TalentDefinitions.Count(em, root));
            for (int i = 0; i < TalentDefinitions.Count(em, root); i++)
            {
                ref var definition = ref TalentDefinitions.Get(em, root, TalentId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("TalentSlot");
            writer.Write(TalentSlotDefinitions.Count(em, root));
            for (int i = 0; i < TalentSlotDefinitions.Count(em, root); i++)
            {
                ref var definition = ref TalentSlotDefinitions.Get(em, root, TalentSlotId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("RoyalTrait");
            writer.Write(RoyalTraitDefinitions.Count(em, root));
            for (int i = 0; i < RoyalTraitDefinitions.Count(em, root); i++)
            {
                ref var definition = ref RoyalTraitDefinitions.Get(em, root, RoyalTraitId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Feature");
            writer.Write(FeatureDefinitions.Count(em, root));
            for (int i = 0; i < FeatureDefinitions.Count(em, root); i++)
            {
                ref var definition = ref FeatureDefinitions.Get(em, root, FeatureId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Crop");
            writer.Write(CropDefinitions.Count(em, root));
            for (int i = 0; i < CropDefinitions.Count(em, root); i++)
            {
                ref var definition = ref CropDefinitions.Get(em, root, CropId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Projectile");
            writer.Write(ProjectileDefinitions.Count(em, root));
            for (int i = 0; i < ProjectileDefinitions.Count(em, root); i++)
            {
                ref var definition = ref ProjectileDefinitions.Get(em, root, ProjectileId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Opportunity");
            writer.Write(OpportunityDefinitions.Count(em, root));
            for (int i = 0; i < OpportunityDefinitions.Count(em, root); i++)
            {
                ref var definition = ref OpportunityDefinitions.Get(em, root, OpportunityId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            writer.Write("Loot");
            writer.Write(LootDefinitions.Count(em, root));
            for (int i = 0; i < LootDefinitions.Count(em, root); i++)
            {
                ref var definition = ref LootDefinitions.Get(em, root, LootId.FromIndex(i));
                DefinitionFingerprint.Write(writer, ref definition);
            }

            var grid = em.GetComponentData<GridData>(root);
            DefinitionFingerprint.Write(writer, ref grid.Value.Value);
            SnapshotBinary.Write(writer, grid.Origin);
            writer.Write(grid.CellSize);
            var events = em.GetComponentData<NightEventCatalog>(root).Value;
            writer.Write(events.Value.Events.Length);
            for (int i = 0; i < events.Value.Events.Length; i++)
                DefinitionFingerprint.Write(writer, ref events.Value.Events[i]);
            SnapshotBinary.Write(writer, em.GetComponentData<NightSettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<IntelligenceSettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<CurrencySettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<RoyalFamilySettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<TalentSettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<QuestGenerationSettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<NightRules>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<PeacefulRules>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<ExpeditionSettings>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<CourtSettings>(root));
            var royals = SnapshotBuffers.Capture<InitialRoyal>(em, root);
            writer.Write(royals.Length);
            foreach (var royal in royals)
            {
                var value = royal;
                value.Name = default;
                SnapshotBinary.Write(writer, value);
            }

            var buildings = SnapshotBuffers.Capture<InitialBuilding>(em, root);
            writer.Write(buildings.Length);
            foreach (var building in buildings)
            {
                var value = building;
                value.Name = default;
                SnapshotBinary.Write(writer, value);
            }

            bool portraits = PortraitOps.Ready(em, root);
            writer.Write(portraits);
            if (portraits)
                SnapshotBinary.Write(writer, em.GetComponentData<PortraitLibrary>(root).Value.Value.Settings);
            var rewards = em.GetComponentData<StartingRewards>(root).Value;
            writer.Write(rewards.IsCreated);
            if (rewards.IsCreated)
                DefinitionFingerprint.Write(writer, ref rewards.Value.Rewards);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
    }
}
