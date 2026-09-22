using System;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class OpportunityProfileValidation
    {
        public static void Validate(OpportunityProfile p)
        {
            if ((byte)p.Kind > 1 || p.Weight < 0 || p.Weight > 10000 || p.MaximumPerNight < 0 || p.MaximumPerNight > 32 || !math.all(math.isfinite(new float4(p.StartFraction, p.EndFraction, p.Speed, p.MinimumResponse))) || !math.all(math.isfinite(new float3(p.CaptureRadius, p.ResponseRadius, p.RouteLength))) || p.StartFraction < 0 || p.EndFraction > .5f || p.EndFraction <= p.StartFraction || p.Speed <= 0 || p.Speed > 10 || p.MinimumResponse < 2 || p.MinimumResponse > 30 || p.CaptureRadius < .5f || p.CaptureRadius > 3 || p.ResponseRadius < 1 || p.ResponseRadius > 100 || p.RouteLength < p.Speed * p.MinimumResponse || p.RouteLength > 100 || !p.Heroes && !p.Soldiers)
                throw new InvalidOperationException("访客时间窗口、路线或响应者配置无效。");
        }
    }
}
