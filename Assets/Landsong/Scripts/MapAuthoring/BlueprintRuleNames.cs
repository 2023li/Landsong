using System;
using System.Text.RegularExpressions;

namespace Landsong.GridSystem
{
    public static class BlueprintRuleNames
    {
        static readonly Regex Prefix=new Regex(@"^L(0|[1-9][0-9]*)_(.+)$",RegexOptions.CultureInvariant);
        public static string Key(string name,int? layer=null)
        {
            if(string.IsNullOrWhiteSpace(name) || name!=name.Trim())
                throw new InvalidOperationException("Blueprint / 规则名称不能为空或带有首尾空格。");
            var match=Prefix.Match(name);
            if(!match.Success)
            {
                if(Regex.IsMatch(name,@"^L(?:[0-9]|[-+_])"))
                    throw new InvalidOperationException("Blueprint 前缀必须为 L非负整数_名称："+name);
                return name;
            }
            if(!int.TryParse(match.Groups[1].Value,out int number) || number>=int.MaxValue)
                throw new InvalidOperationException("Blueprint 层编号无效："+name);
            if(layer.HasValue && number!=layer.Value)
                throw new InvalidOperationException(name+" 的名称编号与所属 Layer"+layer.Value+" 不一致。");
            var key=match.Groups[2].Value;
            if(string.IsNullOrWhiteSpace(key) || key!=key.Trim())throw new InvalidOperationException("Blueprint 地形名称无效："+name);
            return key;
        }
    }
}
