using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public sealed class BuildingActionQuote
    {
        public ResultCode Code = ResultCode.Success;
        public string Reason = "";
        public List<BuildingCost> Costs = new List<BuildingCost>();
        public int ExperienceLoss;
        public bool Allowed => Code == ResultCode.Success;

        public BuildingActionQuote Fail(ResultCode code, string reason)
        {
            Code = code;
            Reason = reason;
            return this;
        }
    }
}
