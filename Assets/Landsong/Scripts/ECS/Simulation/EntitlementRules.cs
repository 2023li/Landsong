using System;

namespace Landsong.ECS
{
    // Shared by authored rewards, runtime issuance and snapshot import. No player state lives here.
    public static class EntitlementRules
    {
        public static string Error(ContentKind kind, string id, int maximumLevel, int flags, int level)
        {
            if (level <= 0) return "许可等级必须大于零";
            switch (kind)
            {
                case ContentKind.Building:
                    return level <= maximumLevel ? null : "蓝图等级超过建筑最高等级";
                case ContentKind.Buff:
                    return null; // Permanent ownership level is not a stack count or a building level.
                case ContentKind.Feature:
                    if (id != null && id.StartsWith("limit.", StringComparison.Ordinal)) return "建筑数量限制不能作为功能许可发放";
                    return level == 1 ? null : "功能许可等级必须为一";
                case ContentKind.Technology:
                    if (level == int.MaxValue) return "科技完成次数已超过可结算范围";
                    return (flags & 1) != 0 || level == 1 ? null : "不可重复科技只能完成一次";
                case ContentKind.Quest:
                case ContentKind.Expedition:
                    return level == 1 ? null : "完成标记必须为一";
                default:
                    return "该内容类型不能保存为许可或完成标记";
            }
        }
    }
}
