#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Landsong.ECS.Editor
{
    public static class LanguageContentTools
    {
        public const string Path="Assets/Landsong/ECSContent/Resources/LandsongPresentation.asset";
        [MenuItem("Landsong/ECS/Presentation/Sync language entries (preserve edits)")]
        public static string SyncText()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(Path);if(catalog==null)throw new InvalidOperationException("Missing presentation catalog at Assets/Landsong/ECSContent/Resources/LandsongPresentation.asset");var result=catalog.Text.ToDictionary(e=>e.Table+"/"+e.Key,e=>e);
            void Add(string table,string key,string zh,string en=""){if(zh==null||result.ContainsKey(table+"/"+key))return;result.Add(table+"/"+key,new GamePresentationCatalog.Translation{Table=table,Key=key,Zh=zh,En=en??""});}
            foreach(var table in new[]{"UI","Content","Gameplay"})
            {
                var zh=AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Landsong/Objects/本地化/Tables/"+table+"_zh-Hans.asset");var en=AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Landsong/Objects/本地化/Tables/"+table+"_en.asset");if(zh==null)continue;
                foreach(var shared in zh.SharedData.Entries){var source=zh.GetEntry(shared.Id);if(source!=null)Add(table,shared.Key,source.Value,en?.GetEntry(shared.Id)?.Value);}
            }
            foreach(var entry in NativeText())Add(entry.Table,entry.Key,entry.Zh,entry.En);
            var content=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");foreach(var definition in content.Definitions)
            {var data=definition.Data;var previous=result.Values.FirstOrDefault(e=>e.Table=="Content"&&e.Zh==data.Name);Add("Content","content."+data.Id+".name",data.Name,previous?.En);Add("Content","content."+data.Id+".description",data.Description??"");}
            catalog.Text=result.Values.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();ExportTemplate();return "Text entries: "+catalog.Text.Length;
        }
        public static IEnumerable<GamePresentationCatalog.Translation> NativeText()
        {
            var rows=new[]{
                ("ui.ecs.quests.window","任务与邀约","Tasks and invitations"),
                ("ui.ecs.quests.resume_tracking","恢复自动追踪","Resume automatic tracking"),
                ("ui.ecs.quests.no_tracking","暂无可追踪任务","No accepted tasks to track"),
                ("ui.ecs.quests.claim","领取奖励","Claim rewards"),
                ("ui.ecs.quests.invitations","邀约面板","Invitations"),
                ("ui.ecs.quests.empty","空闲任务槽","Empty task slot"),
                ("ui.ecs.quests.sources","全部来源","All sources"),
                ("ui.ecs.quests.shared_slots","主线与一般任务共用槽位","Mainline and ordinary tasks share slots"),
                ("ui.ecs.quests.waiting_prerequisites","等待前置","Waiting for prerequisites"),
                ("ui.ecs.quests.reserve_step","等待前置条件，原承接槽位保留","Waiting for prerequisites; original task slot retained"),
                ("ui.ecs.quests.chain_slot","有后续步骤时沿用原承接槽位，任务链结束后才释放。","Next steps keep the original slot until the task chain ends."),
                ("ui.ecs.quests.trade","贸易","Trade"),
                ("ui.ecs.quests.construction","建设","Construction"),
                ("ui.ecs.quests.civil","民生","Livelihood"),
                ("ui.ecs.quests.exploration","探索","Exploration"),
                ("ui.ecs.quests.track_mainline","自动追踪","Track automatically"),
                ("ui.ecs.portrait.confirm","确定容貌（仅一次）","Confirm appearance (once)"),("ui.ecs.portrait.later","稍后再定","Decide later"),
                ("ui.ecs.portrait.open","塑造容貌","Customize appearance"),("ui.ecs.portrait.title","丽质 · 为{0}塑造容貌","Beauty · Customize {0}"),
                ("ui.ecs.portrait.event","丽质初成：{0}（待塑容 {1}）","Beauty: {0} ({1} awaiting customization)"),
                ("ui.ecs.portrait.rules","仅有一次机会。确认后基础容貌固定，仍会自然衰老。修改后的颜色可由之后出生的子女继承。","This can be done once. Appearance is fixed after confirmation and will age naturally. Future children can inherit the edited colors."),
                ("ui.ecs.portrait.announce","丽质初成，可在处理请求中塑造一次容貌","Beauty has blossomed. Customize once via Handle requests."),
                ("ui.ecs.portrait.done","丽质容貌已定，此后只能自然衰老","Appearance is set and will age naturally."),
                ("ui.ecs.portrait.part","{0}：款式 {1}（点击切换）","{0}: style {1} (click to change)"),
                ("ui.ecs.portrait.empty","{0}：无（点击切换）","{0}: none (click to change)"),
                ("ui.ecs.soldier.memorial.natural","{0}自然死亡。遗言：愿后来的人守住家园。","{0} passed away. Last words: May those who follow protect our home."),
                ("ui.ecs.soldier.memorial.battle","{0}阵亡。遗愿：替我看看太平的日子。","{0} fell in battle. Last wish: See the peaceful days for me."),
                ("ui.ecs.build.skin.choose","选择建筑皮肤","Choose building style"),("ui.ecs.build.crop.choose","选择作物","Choose a crop"),
                ("ui.ecs.build.crop.empty","种植 · 点击圆钮选择作物","Planting · Select the circle to choose a crop"),("ui.ecs.build.output.none","无人口 / 科研 / 驻兵基础产出","No population, research or garrison output"),
                ("ui.ecs.royal.requests","处理请求","Handle requests"),("ui.ecs.royal.requests.empty","暂无待处理请求。","No outstanding requests."),
                ("ui.ecs.royal.requests.close","关闭（稍后处理）","Close (decide later)"),("ui.ecs.royal.requests.marriage","查看赐婚请求","Review marriage request"),
                ("ui.ecs.royal.requests.expedition","安排远征","Arrange expedition"),("ui.ecs.royal.requests.captain_unavailable","暂时无法担任远征队长","Unavailable as expedition captain"),
                ("ui.ecs.royal.requests.refuse_expedition","拒绝远征请求","Refuse expedition request"),("ui.ecs.royal.requests.submit","提交委托物品","Submit requested items"),
                ("ui.ecs.royal.requests.insufficient","委托物品不足","Insufficient requested items"),
                ("ui.ecs.royal.details","人物详情","Person details"),("ui.ecs.royal.affairs","王朝事务","Court affairs"),("ui.ecs.royal.choose","点击肖像查看人物","Select a portrait"),
                ("ui.ecs.royal.designate","立为储君","Designate heir"),("ui.ecs.royal.execute","赐死","Execute"),("ui.ecs.royal.arrange","赐婚","Arrange marriage"),
                ("ui.ecs.marriage.confirm","确认赐婚","Confirm marriage"),("ui.ecs.marriage.reselect","重新选择","Choose another"),("ui.ecs.royal.no_ambition","未表现出野心","No ambition shown"),
                ("ui.ecs.marriage.approve","同意赐婚","Approve marriage"),("ui.ecs.marriage.refuse","不同意","Refuse"),("ui.ecs.marriage.later","稍后处理","Decide later"),
                ("ui.ecs.marriage.risk","拒绝可能让请求人记恨，增加弑君或夺位风险。","Refusal may cause resentment, increasing the risk of regicide or usurpation."),("ui.ecs.marriage.no_traits","暂无已知特性","No known traits"),
                ("ui.ecs.family.title","王室家谱 · 金线标记曾登基的子代","Royal family · gold lines mark children who ascended the throne"),("ui.ecs.family.crown","王","K"),
                ("ui.ecs.research.empty","尚未选择科技","No research selected"),("ui.ecs.research.choose","点击选择研究项目","Select a research project"),("ui.ecs.research.empty_hint","选择科技，规划王朝的发展方向。","Choose a technology to shape your dynasty."),("ui.ecs.research.details","点击查看科技详情。","Open technology details."),("ui.ecs.research.placeholder","研","R"),("ui.ecs.military.available","可用军队","Available troops"),
                ("ui.ecs.main.continue","继续游戏","Continue"),("ui.ecs.main.new","开始新王朝","New dynasty"),("ui.ecs.main.load","加载游戏","Load game"),("ui.ecs.main.quit","退出到桌面","Exit to desktop"),
                ("ui.ecs.main.welcome","建立新的王朝，或重返你的山河。","Build a dynasty, or return to your realm."),("ui.ecs.main.quit_confirm","确定退出到桌面？","Exit to desktop?"),("ui.ecs.main.quit_hint","已有王朝和存档将保留。","Your dynasties and saves will be kept."),("ui.ecs.main.quit_accept","确认退出","Confirm exit"),
                ("ui.ecs.new.name","王朝名称","Dynasty name"),("ui.ecs.new.map","选择地图","Choose a map"),("ui.ecs.new.difficulty","难度","Difficulty"),("ui.ecs.new.easy","简单","Easy"),("ui.ecs.new.normal","普通","Normal"),("ui.ecs.new.hard","困难","Hard"),
                ("ui.ecs.new.difficulty_hint","难度数值效果待策划定义，当前选择暂不改变游戏规则。","Difficulty effects are awaiting design. This selection does not change gameplay yet."),("ui.ecs.new.rules","特殊规则","Special rules"),("ui.ecs.new.no_thieves","不刷新小偷（暂未开放）","Disable thieves (unavailable)"),("ui.ecs.new.rules_hint","特殊规则待策划完成后开放配置。","Special rules will become available after design is complete."),
                ("ui.ecs.new.create","建立王朝","Found dynasty"),("ui.ecs.new.back","返回主菜单","Back to main menu"),("ui.ecs.new.no_maps","未配置地图，请先添加可用地图。","No maps configured. Add an available map first."),
                ("ui.ecs.pause","暂停菜单","Pause"),("ui.ecs.next_stage","下一阶段","Next phase"),("ui.ecs.resume","回到游戏","Resume"),("ui.ecs.settings","设置","Settings"),("ui.ecs.save","保存","Save"),("ui.ecs.quick_save","快速保存","Quick save"),("ui.ecs.menu","回到主菜单","Main menu"),("ui.ecs.quit","退出游戏","Quit"),("ui.ecs.confirm","确认","Confirm"),("ui.ecs.cancel","取消","Cancel"),
                ("ui.ecs.navigation.back","返回面板","Previous panel"),("ui.ecs.navigation.history","消息 / 历史","Messages / History"),("ui.ecs.archive.open","王朝 / 存档 / 覆灭记录","Dynasties / Saves / Records"),("ui.ecs.archive.new_name","新王朝名称（可留空）","New dynasty name (optional)"),("ui.ecs.archive.load","载入此记录","Load this save"),("ui.ecs.archive.rename","重命名","Rename"),("ui.ecs.archive.list","返回列表","Back to list"),("ui.ecs.archive.back","关闭 / 返回","Close / Back"),
                ("ui.ecs.settings.apply","应用 / 保留画面","Apply / Keep display"),("ui.ecs.settings.default","恢复默认（待应用）","Defaults (apply to save)"),("ui.ecs.settings.back","返回（丢弃未应用）","Back (discard edits)"),("ui.ecs.settings.saved","设置已保存。","Settings saved."),("ui.ecs.settings.master","主音量","Master volume"),("ui.ecs.settings.music","音乐音量","Music volume"),("ui.ecs.settings.effects","音效 / UI 音量","Effects / UI volume"),("ui.ecs.settings.ambient","环境音量","Ambient volume"),("ui.ecs.settings.scale","界面缩放","UI scale"),("ui.ecs.settings.camera","镜头速度","Camera speed"),("ui.ecs.settings.mute_on","静音：开","Mute: On"),("ui.ecs.settings.mute_off","静音：关","Mute: Off"),("ui.ecs.settings.pack_scan","重新扫描外部语言包","Refresh language packs"),
                ("ui.ecs.panel.buildings","建筑","Buildings"),("ui.ecs.panel.inventory","库存","Inventory"),("ui.ecs.panel.military","驻军","Garrison"),("ui.ecs.panel.tech","科技","Technology"),("ui.ecs.panel.quests","任务","Quests"),("ui.ecs.panel.talents","人才","Talents"),("ui.ecs.panel.royal","王室","Royal family"),("ui.ecs.panel.policy","政策","Policies"),("ui.ecs.panel.intel","情报","Intelligence"),("ui.ecs.panel.report","战报","Night report"),
                ("ui.ecs.royal.graph","王室家谱 · 青线双亲，金线配偶；点击查看详情","Royal family · Cyan: parents; gold: spouse. Select for details."),("ui.ecs.royal.portraits","人物画像 · 点击查看交际 / 任职","People · Select for social / appointment details"),("ui.ecs.policy.graph","政策 · 分层展示；左侧操作沿用原规则","Policies · Tiered view; actions on the left"),("ui.ecs.day","白天建造","Day construction"),("ui.ecs.night","夜晚","Night"),("ui.ecs.paused","已暂停","Paused")
            };
            foreach(var row in rows)yield return new GamePresentationCatalog.Translation{Table="UI",Key=row.Item1,Zh=row.Item2,En=row.Item3};
            foreach(var row in new[]{
                ("ui.ecs.cleanup.retry","重试清理并返回","Retry cleanup and return"),("ui.ecs.cleanup.pending","正在清理","Cleaning up"),
                ("ui.ecs.settings.apply_short","应用","Apply"),("ui.ecs.settings.defaults_short","恢复默认","Restore defaults"),("ui.ecs.archive.title","存档记录","Save records"),
                ("ui.ecs.settings.bus_hint","分类音量即时作用于表现音源；主音量只在唯一监听器上应用。","Bus volumes apply to presentation sources; master volume is applied once."),
                ("ui.ecs.settings.apply_hint","修改后点击应用。显示变更需在 15 秒内确认，否则恢复。","Apply to save. Confirm display changes within 15 seconds or they revert."),
                ("ui.ecs.settings.important_hint","重要消息不静音；筛选不删除历史。情报仍为独立按钮。","Important messages remain visible. Filters keep history; intelligence is separate."),
                ("ui.ecs.phase.deploy","出勤","Deployment"),("ui.ecs.phase.retreat","敌军撤离","Enemy retreat"),("ui.ecs.phase.closure","战后收尾","Aftermath"),("ui.ecs.phase.report","今晚战报","Tonight's report"),("ui.ecs.phase.end","王朝终局","Dynasty ended"),("ui.ecs.phase.settle","结算","Settlement"),("ui.ecs.moon","月亮进度","Night progress"),("ui.ecs.pause_hotkey","暂停 / Esc","Pause / Esc"),("ui.ecs.intel_unread","情报 •","Intelligence •"),("ui.ecs.build.all","全部","All"),("ui.ecs.build.industry","工业","Industry"),("ui.ecs.build.market","市场","Market"),("ui.ecs.build.military","军事","Military"),("ui.ecs.build.decoration","装饰","Decoration"),("ui.ecs.build.collapse","收起","Collapse"),("ui.ecs.build.economy","经济总览","Economy")
            })yield return new GamePresentationCatalog.Translation{Table="UI",Key=row.Item1,Zh=row.Item2,En=row.Item3};
            yield return new GamePresentationCatalog.Translation{Table="Gameplay",Key="gameplay.ecs.turn_status",Zh="{0}　白天 {1} / 夜晚 {1}　{2}　人口 {3}（空闲 {4}）",En="{0}  Day {1} / Night {1}  {2}  Population {3} (Free {4})"};
            foreach(var row in new[]{("gameplay.ecs.turn","回合 {0}","Turn {0}"),("gameplay.ecs.day_number","白天 {0}","Day {0}"),("gameplay.ecs.volume.master","主音量  {0}","Master volume  {0}"),("gameplay.ecs.volume.music","音乐音量  {0}","Music volume  {0}"),("gameplay.ecs.volume.effects","音效 / UI 音量  {0}","Effects / UI volume  {0}"),("gameplay.ecs.volume.ambient","环境音量  {0}","Ambient volume  {0}"),("gameplay.ecs.display_confirm","保留画面设置？{0} 秒后恢复。点击“应用 / 保留画面”。","Keep display settings? Reverting in {0}s. Select Apply / Keep display."),("gameplay.ecs.restore_notice","白天进度已载入","Day progress loaded"),("gameplay.ecs.report.dawn","确认战报 · 下一回合","Confirm report · Next turn")})yield return new GamePresentationCatalog.Translation{Table="Gameplay",Key=row.Item1,Zh=row.Item2,En=row.Item3};
        }
        [MenuItem("Landsong/ECS/Presentation/Export language pack template")]
        public static void ExportTemplate()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(Path);if(catalog==null)return;string Escape(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";var text=new StringBuilder("Table,Key,Text\n");foreach(var entry in catalog.Text)text.AppendLine(Escape(entry.Table)+","+Escape(entry.Key)+","+Escape(entry.Zh));Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/language-template.csv",text.ToString(),new UTF8Encoding(false));
        }
    }
}
#endif
