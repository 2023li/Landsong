using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    [Serializable]
    public sealed class InitialRoyalSource
    {
        [LabelText("姓名"), Required]
        public string Name;
        [LabelText("年龄"), MinValue(0)]
        public int Age = 20;
        [LabelText("王室角色")]
        public byte Role;
        [LabelText("性别")]
        public PersonGender Gender;
        [LabelText("初始特性")]
        public RoyalTraitDefinitionAsset[] Traits = Array.Empty<RoyalTraitDefinitionAsset>();
    }

    [DisallowMultipleComponent]
    public sealed class InitialRoyalFamilyAuthoring : MonoBehaviour
    {
        public RoyalTraitCatalogAsset Traits => GameContentSetAuthoring.Resolve<RoyalTraitCatalogAsset>(this);
        [LabelText("开局王室")]
        public InitialRoyalSource[] Family = Array.Empty<InitialRoyalSource>();
        public sealed class Baker : Baker<InitialRoyalFamilyAuthoring>
        {
            public override void Bake(InitialRoyalFamilyAuthoring authoring)
            {
                DependsOn(authoring.Traits);
                var traits = new RoyalTraitCatalogIndex(authoring.Traits);
                var monarchGender = PersonGender.Male;
                foreach (var person in authoring.Family)
                {
                    if (person == null || (byte)person.Gender > 2)
                        throw new InvalidOperationException("开局王室性别无效。");
                    if (person.Role == 0 && person.Gender != PersonGender.Unspecified)
                        monarchGender = person.Gender;
                }

                foreach (var person in authoring.Family)
                    if (person.Role == 1 && person.Gender != PersonGender.Unspecified && person.Gender == monarchGender)
                        throw new InvalidOperationException("开局配偶必须与君主异性。");
                var buffer = AddBuffer<InitialRoyal>(GetEntity(TransformUsageFlags.None));
                foreach (var person in authoring.Family)
                {
                    if (person == null || string.IsNullOrWhiteSpace(person.Name) || person.Age < 0)
                        throw new InvalidOperationException("开局王室姓名、年龄无效。");
                    var initial = new InitialRoyal
                    {
                        Name = new FixedString128Bytes(person.Name),
                        Age = person.Age,
                        Role = person.Role,
                        Gender = person.Gender
                    };
                    foreach (var trait in person.Traits)
                    {
                        DependsOn(trait);
                        initial.Traits.Add(traits.Resolve(trait));
                    }

                    buffer.Add(initial);
                }
            }
        }
    }
}
