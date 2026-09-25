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
    public static partial class SnapshotBinary
    {
        static void WriteRoyalFamilySettings(BinaryWriter writer, RoyalFamilySettings value)
        {
            Write(writer, value.MaxChildren);
            Write(writer, value.BirthChance);
            Write(writer, value.MutationChance);
        }

        static RoyalFamilySettings ReadRoyalFamilySettings(BinaryReader reader)
        {
            return new RoyalFamilySettings
            {
                MaxChildren = Read<int>(reader),
                BirthChance = Read<float>(reader),
                MutationChance = Read<float>(reader),
            };
        }

        static void WriteSession(BinaryWriter writer, Session value)
        {
            Write(writer, value.Phase);
            Write(writer, value.Initialized);
        }

        static Session ReadSession(BinaryReader reader)
        {
            return new Session
            {
                Phase = Read<Phase>(reader),
                Initialized = Read<byte>(reader),
            };
        }

        static void WriteSimulationControl(BinaryWriter writer, SimulationControl value)
        {
            Write(writer, value.Paused);
        }

        static SimulationControl ReadSimulationControl(BinaryReader reader)
        {
            return new SimulationControl
            {
                Paused = Read<byte>(reader),
            };
        }

        static void WriteSimulationRandomState(BinaryWriter writer, SimulationRandomState value)
        {
            Write(writer, value.State);
        }

        static SimulationRandomState ReadSimulationRandomState(BinaryReader reader)
        {
            return new SimulationRandomState
            {
                State = Read<uint>(reader),
            };
        }

        static void WriteSoldier(BinaryWriter writer, Soldier value)
        {
            Write(writer, value.Garrison);
            Write(writer, value.Slot);
            Write(writer, value.PopulationCost);
            Write(writer, value.PendingSince);
            Write(writer, value.Experience);
            Write(writer, value.LastExperienceTurn);
            Write(writer, value.RecallState);
            Write(writer, (byte)value.Weapon);
        }

        static Soldier ReadSoldier(BinaryReader reader)
        {
            return new Soldier
            {
                Garrison = Read<ulong>(reader),
                Slot = Read<int>(reader),
                PopulationCost = Read<int>(reader),
                PendingSince = Read<int>(reader),
                Experience = Read<int>(reader),
                LastExperienceTurn = Read<int>(reader),
                RecallState = Read<byte>(reader),
                Weapon = (SoldierWeaponKind)Read<byte>(reader),
            };
        }

        static void WriteSoldierPerson(BinaryWriter writer, SoldierPerson value)
        {
            Write(writer, value.Age);
            Write(writer, value.Lifespan);
            Write(writer, value.LastAgeTurn);
            Write(writer, value.Incarnation);
            Write(writer, value.Gender);
            Write(writer, value.SpecialAttention);
            Write(writer, value.DeathNotified);
        }

        static SoldierPerson ReadSoldierPerson(BinaryReader reader)
        {
            return new SoldierPerson
            {
                Age = Read<int>(reader),
                Lifespan = Read<int>(reader),
                LastAgeTurn = Read<int>(reader),
                Incarnation = Read<int>(reader),
                Gender = Read<PersonGender>(reader),
                SpecialAttention = Read<byte>(reader),
                DeathNotified = Read<byte>(reader),
            };
        }

        static void WriteSpawnRegion(BinaryWriter writer, SpawnRegion value)
        {
            Write(writer, value.Direction);
            Write(writer, value.Center);
            Write(writer, value.Size);
            Write(writer, value.EdgeOnly);
        }

        static SpawnRegion ReadSpawnRegion(BinaryReader reader)
        {
            return new SpawnRegion
            {
                Direction = Read<int>(reader),
                Center = Read<float3>(reader),
                Size = Read<float3>(reader),
                EdgeOnly = Read<byte>(reader),
            };
        }

        static void WriteTalent(BinaryWriter writer, Talent value)
        {
            Write(writer, value.Slot);
            Write(writer, value.Experience);
            Write(writer, value.Level);
            Write(writer, value.AssignedTurns);
            Write(writer, value.WageTurn);
            Write(writer, value.LastBenefitTurn);
            Write(writer, value.Recruited);
            Write(writer, value.Paid);
        }

        static Talent ReadTalent(BinaryReader reader)
        {
            return new Talent
            {
                Slot = Read<Landsong.ECS.Definitions.TalentSlotId>(reader),
                Experience = Read<int>(reader),
                Level = Read<int>(reader),
                AssignedTurns = Read<int>(reader),
                WageTurn = Read<int>(reader),
                LastBenefitTurn = Read<int>(reader),
                Recruited = Read<byte>(reader),
                Paid = Read<byte>(reader),
            };
        }

        static void WriteTalentSettings(BinaryWriter writer, TalentSettings value)
        {
            Write(writer, value.TalentCapacity);
            Write(writer, value.TalentRecruitCost);
            Write(writer, value.TalentExperience);
        }

        static TalentSettings ReadTalentSettings(BinaryReader reader)
        {
            return new TalentSettings
            {
                TalentCapacity = Read<int>(reader),
                TalentRecruitCost = Read<int>(reader),
                TalentExperience = Read<int>(reader),
            };
        }

        static void WriteTechnologyProgress(BinaryWriter writer, TechnologyProgress value)
        {
            Write(writer, value.Technology);
            Write(writer, value.ResearchPoints);
            Write(writer, value.Completions);
            Write(writer, value.QueueOrder);
        }

        static TechnologyProgress ReadTechnologyProgress(BinaryReader reader)
        {
            return new TechnologyProgress
            {
                Technology = Read<Landsong.ECS.Definitions.TechnologyId>(reader),
                ResearchPoints = Read<int>(reader),
                Completions = Read<int>(reader),
                QueueOrder = Read<int>(reader),
            };
        }

        static void WriteTraitEntry(BinaryWriter writer, TraitEntry value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Revealed);
            Write(writer, value.Active);
        }

        static TraitEntry ReadTraitEntry(BinaryReader reader)
        {
            return new TraitEntry
            {
                Definition = Read<Landsong.ECS.Definitions.RoyalTraitId>(reader),
                Revealed = Read<byte>(reader),
                Active = Read<byte>(reader),
            };
        }

        static void WriteUnlockedFeature(BinaryWriter writer, UnlockedFeature value)
        {
            Write(writer, value.Feature);
        }

        static UnlockedFeature ReadUnlockedFeature(BinaryReader reader)
        {
            return new UnlockedFeature
            {
                Feature = Read<Landsong.ECS.Definitions.FeatureId>(reader),
            };
        }

        static void WriteUnresolvedBoss(BinaryWriter writer, UnresolvedBoss value)
        {
            Write(writer, value.Event);
            Write(writer, value.Definition);
            Write(writer, value.DueTurn);
        }

        static UnresolvedBoss ReadUnresolvedBoss(BinaryReader reader)
        {
            return new UnresolvedBoss
            {
                Event = Read<FixedString64Bytes>(reader),
                Definition = Read<Landsong.ECS.Definitions.EnemyId>(reader),
                DueTurn = Read<int>(reader),
            };
        }

        static void WriteFixedList128Bytes_Landsong_ECS_Definitions_RoyalTraitId_(BinaryWriter writer, FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
                Write(writer, value[i]);
        }

        static FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId> ReadFixedList128Bytes_Landsong_ECS_Definitions_RoyalTraitId_(BinaryReader reader)
        {
            var value = new FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId>();
            int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity)
                throw new InvalidDataException("Invalid fixed list count");
            for (int i = 0; i < count; i++)
                value.Add(Read<Landsong.ECS.Definitions.RoyalTraitId>(reader));
            return value;
        }

        static void WriteFixedList128Bytes_int_(BinaryWriter writer, FixedList128Bytes<int> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
                Write(writer, value[i]);
        }

        static FixedList128Bytes<int> ReadFixedList128Bytes_int_(BinaryReader reader)
        {
            var value = new FixedList128Bytes<int>();
            int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity)
                throw new InvalidDataException("Invalid fixed list count");
            for (int i = 0; i < count; i++)
                value.Add(Read<int>(reader));
            return value;
        }

        static void WriteFixedList512Bytes_float_(BinaryWriter writer, FixedList512Bytes<float> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
                Write(writer, value[i]);
        }

        static FixedList512Bytes<float> ReadFixedList512Bytes_float_(BinaryReader reader)
        {
            var value = new FixedList512Bytes<float>();
            int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity)
                throw new InvalidDataException("Invalid fixed list count");
            for (int i = 0; i < count; i++)
                value.Add(Read<float>(reader));
            return value;
        }

        static void WriteFixedList64Bytes_int_(BinaryWriter writer, FixedList64Bytes<int> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
                Write(writer, value[i]);
        }

        static FixedList64Bytes<int> ReadFixedList64Bytes_int_(BinaryReader reader)
        {
            var value = new FixedList64Bytes<int>();
            int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity)
                throw new InvalidDataException("Invalid fixed list count");
            for (int i = 0; i < count; i++)
                value.Add(Read<int>(reader));
            return value;
        }

        static void WriteEntity(BinaryWriter writer, Entity value)
        {
            Write(writer, value.Index);
            Write(writer, value.Version);
        }

        static Entity ReadEntity(BinaryReader reader)
        {
            return new Entity
            {
                Index = Read<int>(reader),
                Version = Read<int>(reader),
            };
        }

        static void Writefloat3(BinaryWriter writer, float3 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
        }

        static float3 Readfloat3(BinaryReader reader)
        {
            return new float3
            {
                x = Read<float>(reader),
                y = Read<float>(reader),
                z = Read<float>(reader),
            };
        }

        static void Writefloat4(BinaryWriter writer, float4 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
            Write(writer, value.w);
        }

        static float4 Readfloat4(BinaryReader reader)
        {
            return new float4
            {
                x = Read<float>(reader),
                y = Read<float>(reader),
                z = Read<float>(reader),
                w = Read<float>(reader),
            };
        }

        static void Writeint2(BinaryWriter writer, int2 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
        }

        static int2 Readint2(BinaryReader reader)
        {
            return new int2
            {
                x = Read<int>(reader),
                y = Read<int>(reader),
            };
        }

        static void Writeint4(BinaryWriter writer, int4 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
            Write(writer, value.w);
        }

        static int4 Readint4(BinaryReader reader)
        {
            return new int4
            {
                x = Read<int>(reader),
                y = Read<int>(reader),
                z = Read<int>(reader),
                w = Read<int>(reader),
            };
        }

        static void Writequaternion(BinaryWriter writer, quaternion value)
        {
            Write(writer, value.value);
        }

        static quaternion Readquaternion(BinaryReader reader)
        {
            return new quaternion
            {
                value = Read<float4>(reader),
            };
        }

        static void WriteLocalTransform(BinaryWriter writer, LocalTransform value)
        {
            Write(writer, value.Position);
            Write(writer, value.Scale);
            Write(writer, value.Rotation);
        }

        static LocalTransform ReadLocalTransform(BinaryReader reader)
        {
            return new LocalTransform
            {
                Position = Read<float3>(reader),
                Scale = Read<float>(reader),
                Rotation = Read<quaternion>(reader),
            };
        }
    }
}
