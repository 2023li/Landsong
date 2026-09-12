using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    // Text only: no dynamic assembly, font, asset, remote path or executable expression loading.
    public sealed class ExternalLanguagePack
    {
        [Serializable] public sealed class Manifest { public int schemaVersion,targetKeysetVersion; public string packId,displayName,localeCode,fallbackLocaleCode; }
        public Manifest Info;public string DirectoryPath {get;private set;}public readonly Dictionary<string,string> Strings=new Dictionary<string,string>(StringComparer.Ordinal);
        public static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public static string[] Parameters(string text)=>Regex.Matches(text??"",@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})").Cast<Match>().Select(m=>m.Groups[1].Value).Distinct().OrderBy(s=>s).ToArray();
        public static bool Compatible(string source,string value)
        {
            if(value==null||value.Length>8192||!Parameters(source).SequenceEqual(Parameters(value)))return false;
            try{var ids=Parameters(value);if(ids.Any(id=>!int.TryParse(id,out int n)||n>15))return false;int count=ids.Select(int.Parse).DefaultIfEmpty(-1).Max()+1;string.Format(CultureInfo.InvariantCulture,value,Enumerable.Repeat<object>(1,count).ToArray());return true;}catch(FormatException){return false;}
        }
        public static List<string[]> Csv(string text)
        {
            var result=new List<string[]>();var row=new List<string>();var cell=new StringBuilder();bool quoted=false,closed=false;
            for(int i=0;i<=text.Length;i++)
            {
                char c=i==text.Length?'\n':text[i];
                if(quoted){if(c=='"'){if(i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}else cell.Append(c);continue;}
                if(c=='"'&&cell.Length==0&&!closed){quoted=true;continue;}
                if(c=='"')throw new InvalidDataException("CSV 非引号字段包含未转义引号。");
                if(c==','||c=='\r'||c=='\n')
                {row.Add(cell.ToString());cell.Clear();closed=false;if(c!=','){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;if(row.Count>1||row[0].Length>0)result.Add(row.ToArray());row.Clear();}if(result.Count>20000)throw new InvalidDataException("语言包行数过多。");continue;}
                if(closed)throw new InvalidDataException("CSV 引号后存在多余字符。");cell.Append(c);
            }if(quoted)throw new InvalidDataException("CSV 引号未闭合。");return result;
        }
        static string Read(string directory,string file,int max)
        {
            var info=new FileInfo(Path.Combine(directory,file));if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length>max)throw new InvalidDataException("语言包文件超限或使用链接。");return Utf8.GetString(File.ReadAllBytes(info.FullName));
        }
        public static ExternalLanguagePack Load(string directory,IReadOnlyDictionary<string,string> known)
        {
            if((new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("语言包不能使用链接目录。");
            var pack=new ExternalLanguagePack {DirectoryPath=Path.GetFullPath(directory),Info=JsonUtility.FromJson<Manifest>(Read(directory,"manifest.json",16384))};var m=pack.Info;
            if(m==null||m.schemaVersion!=1||!Regex.IsMatch(m.packId??"",@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,63}$")||string.IsNullOrWhiteSpace(m.displayName)||m.displayName.Length>64||(m.fallbackLocaleCode!="en"&&m.fallbackLocaleCode!="zh-Hans"))throw new InvalidDataException("语言包 manifest 无效。");
            if(string.IsNullOrWhiteSpace(m.localeCode)||m.localeCode.Length>32||m.packId=="en"||m.packId=="zh-Hans")throw new InvalidDataException("语言包语言标识无效或占用内置标识。");CultureInfo.GetCultureInfo(m.localeCode);var rows=Csv(Read(directory,"strings.csv",2*1024*1024).TrimStart('\uFEFF'));if(rows.Count==0||!rows[0].SequenceEqual(new[]{"Table","Key","Text"}))throw new InvalidDataException("语言包表头须为 Table,Key,Text。");
            var seen=new HashSet<string>();foreach(var row in rows.Skip(1))
            {
                if(row.Length!=3||!new[]{"UI","Content","Gameplay"}.Contains(row[0])||row[1].Length>256)throw new InvalidDataException("语言包条目无效。");
                string key=row[0]+"/"+row[1];if(!seen.Add(key))throw new InvalidDataException("语言包重复 Key："+row[1]);
                if(!known.TryGetValue(key,out var source))continue;if(!Compatible(source,row[2]))throw new InvalidDataException("语言包参数不匹配："+row[1]);
                pack.Strings.Add(key,Regex.Replace(row[2],"<[^>]*>",""));
            }return pack;
        }
    }
}
