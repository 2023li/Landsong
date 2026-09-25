using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct RecruitSoldiersRequest : IGameRequest
    {
        public ulong Garrison;
        public SoldierId Soldier;
        public int Quantity;
        public bool Pending;
        public CommandKind Kind => CommandKind.RecruitSoldier;
        public ulong Target => Garrison;
    }

    public struct RecruitHeroRequest : IGameRequest
    {
        public ulong Sanctum;
        public CommandKind Kind => CommandKind.RecruitHero;
        public ulong Target => Sanctum;
    }

    public struct AssignSoldierRequest : IGameRequest
    {
        public ulong Soldier;
        public ulong Garrison;
        public int Slot;
        public CommandKind Kind => CommandKind.AssignSoldier;
        public ulong Target => Soldier;
    }

    public struct UnassignSoldierRequest : IGameRequest
    {
        public ulong Soldier;
        public CommandKind Kind => CommandKind.UnassignSoldier;
        public ulong Target => Soldier;
    }

    public struct SwapSoldiersRequest : IGameRequest
    {
        public ulong FirstSoldier;
        public ulong SecondSoldier;
        public CommandKind Kind => CommandKind.SwapSoldiers;
        public ulong Target => FirstSoldier;
    }

    public struct RenameSoldierRequest : IGameRequest
    {
        public ulong Soldier;
        public FixedString128Bytes Name;
        public CommandKind Kind => CommandKind.RenameSoldier;
        public ulong Target => Soldier;
    }

    public struct DismissSoldierRequest : IGameRequest
    {
        public ulong Soldier;
        public bool Confirmed;
        public CommandKind Kind => CommandKind.DismissSoldier;
        public ulong Target => Soldier;
    }

    public struct SetSoldierAttentionRequest : IGameRequest
    {
        public ulong Soldier;
        public bool Watched;
        public CommandKind Kind => CommandKind.SetSoldierAttention;
        public ulong Target => Soldier;
    }

    public struct EquipSoldierWeaponRequest : IGameRequest
    {
        public ulong Soldier;
        public SoldierWeaponKind Weapon;
        public CommandKind Kind => CommandKind.EquipSoldierWeapon;
        public ulong Target => Soldier;
    }

    public struct FillGarrisonRequest : IGameRequest
    {
        public ulong Garrison;
        public CommandKind Kind => CommandKind.FillGarrison;
        public ulong Target => Garrison;
    }

    public struct RecallGarrisonRequest : IGameRequest
    {
        public ulong Garrison;
        public bool Cancel;
        public CommandKind Kind => CommandKind.RecallGarrison;
        public ulong Target => Garrison;
    }

    public struct RingBellRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.Bell;
        public ulong Target => Building;
    }

    public struct WakeHeroRequest : IGameRequest
    {
        public ulong Sanctum;
        public CommandKind Kind => CommandKind.WakeHero;
        public ulong Target => Sanctum;
    }

    public struct SelectHeroRequest : IGameRequest
    {
        public ulong Hero;
        public CommandKind Kind => CommandKind.SelectHero;
        public ulong Target => Hero;
    }

    public struct MoveHeroRequest : IGameRequest
    {
        public float3 Destination;
        public CommandKind Kind => CommandKind.MoveHero;
        public ulong Target => 0;
    }

    public struct FocusHeroRequest : IGameRequest
    {
        public ulong Enemy;
        public CommandKind Kind => CommandKind.FocusHero;
        public ulong Target => Enemy;
    }

    public struct RecallHeroRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.Recall;
        public ulong Target => 0;
    }
}
