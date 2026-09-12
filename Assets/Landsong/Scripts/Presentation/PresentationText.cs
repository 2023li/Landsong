using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public static class PresentationText
    {
        public const int KeysetVersion=5;
        static readonly Dictionary<string,GamePresentationCatalog.Translation> entries=new Dictionary<string,GamePresentationCatalog.Translation>();
        static readonly Dictionary<string,string> aliases=new Dictionary<string,string>();
        static readonly Dictionary<string,string> cache=new Dictionary<string,string>();
        static readonly List<(string key,Regex pattern)> patterns=new List<(string,Regex)>();
        public static readonly Dictionary<string,ExternalLanguagePack> Packs=new Dictionary<string,ExternalLanguagePack>();
        public static string Diagnostics {get;private set;}="";
        public static int Revision {get;private set;}
        public static string Language {get;private set;}="zh-Hans";
        public static IReadOnlyDictionary<string,string> Keyset=>entries.ToDictionary(p=>p.Key,p=>p.Value.Zh);
        public static void Initialize(GamePresentationCatalog catalog)
        {
            entries.Clear();aliases.Clear();patterns.Clear();cache.Clear();Packs.Clear();
            if(catalog!=null)foreach(var entry in catalog.Text)
            {
                if(entry==null||string.IsNullOrWhiteSpace(entry.Key)||entry.Zh==null)continue;string key=entry.Table+"/"+entry.Key;if(!entries.TryAdd(key,entry))continue;
                if(!aliases.ContainsKey(entry.Zh))aliases.Add(entry.Zh,key);
                // Legacy source adapters retain semantic table keys; no path/hash/runtime-value keys are generated.
                if(ExternalLanguagePack.Parameters(entry.Zh).Length>0&&ExternalLanguagePack.Compatible(entry.Zh,entry.Zh))
                {
                    var pattern=new StringBuilder("^");int offset=0;
                    foreach(Match part in Regex.Matches(entry.Zh,@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")){pattern.Append(Regex.Escape(entry.Zh.Substring(offset,part.Index-offset))).Append("(.*?)");offset=part.Index+part.Length;}
                    pattern.Append(Regex.Escape(entry.Zh.Substring(offset))).Append('$');
                    if(LiteralLength(entry.Zh)>0)patterns.Add((key,new Regex(pattern.ToString(),RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(10))));
                }
            }
            // Specific templates precede legacy catch-all templates; naked placeholders are not source aliases.
            patterns.Sort((a,b)=>LiteralLength(entries[b.key].Zh).CompareTo(LiteralLength(entries[a.key].Zh)));Revision++;
        }
        static int LiteralLength(string value)=>Regex.Replace(value,@"(?<!\{)\{\d+(?:[^{}]*)\}(?!\})","").Length;
        public static void Discover(string root)
        {
            var candidates=new List<ExternalLanguagePack>();var messages=new List<string>();var failed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var duplicate=new HashSet<string>();Packs.TryGetValue(Language,out var active);var keyset=Keyset;
            try{if(Directory.Exists(root))
            {
                if((new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)!=0){Diagnostics="语言包根目录不能为链接。";return;}
                foreach(var folder in Directory.GetDirectories(root).OrderBy(p=>p,StringComparer.Ordinal).Take(32))try{candidates.Add(ExternalLanguagePack.Load(folder,keyset));}catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException||e is UnauthorizedAccessException){failed.Add(Path.GetFullPath(folder));messages.Add(Path.GetFileName(folder)+"："+e.Message);}
            }}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){Diagnostics="读取语言包目录失败："+e.Message;return;}
            Packs.Clear();foreach(var group in candidates.GroupBy(p=>p.Info.packId)){if(group.Count()!=1){duplicate.Add(group.Key);messages.Add("重复语言包 ID，均已禁用："+group.Key);continue;}var pack=group.First();Packs.Add(group.Key,pack);if(pack.Info.targetKeysetVersion!=KeysetVersion)messages.Add(pack.Info.displayName+"：Keyset 版本不同，缺项使用内置回退。");}
            if(active!=null&&!Packs.ContainsKey(active.Info.packId)&&!duplicate.Contains(active.Info.packId)&&failed.Contains(active.DirectoryPath)){Packs.Add(active.Info.packId,active);messages.Add("当前语言包更新无效，保留上次成功加载的文本。");}
            Diagnostics=string.Join("\n",messages);cache.Clear();Revision++;
        }
        public static void SetLanguage(string language){Language=language=="en"||language=="zh-Hans"||Packs.ContainsKey(language)?language:"zh-Hans";cache.Clear();Revision++;}
        public static string Get(string key,string fallback,params object[] args)
        {
            string value=fallback;entries.TryGetValue(key,out var entry);if(entry!=null)value=entry.Zh;
            if(Packs.TryGetValue(Language,out var pack)){if(pack.Strings.TryGetValue(key,out var external))value=external;else if(pack.Info.fallbackLocaleCode=="en"&&entry!=null&&!string.IsNullOrEmpty(entry.En))value=entry.En;}
            else if(Language=="en"&&entry!=null&&!string.IsNullOrEmpty(entry.En))value=entry.En;
            if(!ExternalLanguagePack.Compatible(fallback,value))value=fallback;
            try{return args.Length==0?value:string.Format(CultureInfo.InvariantCulture,value,args);}catch(FormatException){try{return args.Length==0?fallback:string.Format(CultureInfo.InvariantCulture,fallback,args);}catch(FormatException){return fallback;}}
        }
        public static string Source(string text)
        {
            if(Language=="zh-Hans"||string.IsNullOrEmpty(text)||text.Length>8192)return text;if(cache.TryGetValue(text,out var cached))return cached;
            string translated=text;
            if(aliases.TryGetValue(text,out var key))translated=Get(key,text);
            else foreach(var pair in patterns)
            {
                Match match;try{match=pair.pattern.Match(text);}catch(RegexMatchTimeoutException){continue;}if(!match.Success)continue;
                var entry=entries[pair.key];var parameterIds=ExternalLanguagePack.Parameters(entry.Zh);int maximum=parameterIds.Select(int.Parse).DefaultIfEmpty(-1).Max();var args=new object[maximum+1];
                var order=Regex.Matches(entry.Zh,@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})");for(int i=0;i<order.Count&&i+1<match.Groups.Count;i++)args[int.Parse(order[i].Groups[1].Value)]=match.Groups[i+1].Value;
                translated=Get(pair.key,entry.Zh,args);break;
            }
            if(cache.Count>=1024)cache.Clear();cache[text]=translated;return translated;
        }
    }
    // Changes the rendered text only. Command routing, player names and raw source snapshots remain untouched.
}
