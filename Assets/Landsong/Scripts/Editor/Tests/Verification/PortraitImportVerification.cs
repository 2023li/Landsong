#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class PortraitImportVerification
    {
        static StringBuilder log; static int count;
        static void Check(bool ok,string name) { if(!ok)throw new InvalidOperationException("FAIL "+name); count++; log.AppendLine("PASS "+name); }
        [MenuItem("Landsong/ECS/Verification/Portrait part importer")]
        public static string Run()
        {
            log=new StringBuilder(); count=0;
            try { Verify(); log.AppendLine("Assertions: "+count); return log.ToString(); }
            catch(Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/portrait-import-verification.txt",log.ToString()); }
        }
        static void Verify()
        {
            string unique=Guid.NewGuid().ToString("N"), fixture="Assets/Landsong/ECSContent/PortraitImportVerification_"+unique;
            string sources=Path.GetFullPath("Library/LandsongEcs/PortraitImportSources_"+unique);
            Directory.CreateDirectory(sources); AssetDatabase.CreateFolder("Assets/Landsong/ECSContent","PortraitImportVerification_"+unique);
            var original=AssetDatabase.LoadAssetAtPath<PortraitConfig>("Assets/Landsong/ECSContent/PortraitConfig.asset");
            string sourceAsset=File.ReadAllText(AssetDatabase.GetAssetPath(original));
            var config=UnityEngine.Object.Instantiate(original); config.Resolution=32; config.Placeholders=true; config.Parts=Array.Empty<PortraitPartSource>();
            AssetDatabase.CreateAsset(config,fixture+"/Config.asset");
            var created=new System.Collections.Generic.List<string>();
            string Png(string name,int w,int h)
            {
                var image=new Texture2D(w,h,TextureFormat.RGBA32,false);
                try { var pixels=new Color32[w*h];for(int i=0;i<pixels.Length;i++)pixels[i]=i%3==0?new Color32(255,255,255,255):new Color32(0,0,0,0);image.SetPixels32(pixels);image.Apply();string path=Path.Combine(sources,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());return path; }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            var front=Png("front",32,32);var back=Png("back",32,32);var wrong=Png("large",64,64);var rectangle=Png("rectangle",32,64);
            var draft=new PortraitImportDraft {Id="verify_"+unique,Type=PortraitPartType.Hair};draft.ResetLayers();
            PortraitImportLayer Layer(PortraitLayer layer)=>draft.Layers.Single(l=>l.Layer==layer);
            void Invalid(string label)
            {
                int before=config.Parts.Length;string path=PortraitPartImporter.Destination(draft);bool failed=false;
                try { PortraitPartImporter.Import(config,draft); }catch(InvalidOperationException){failed=true;}
                Check(failed&&config.Parts.Length==before&&!Directory.Exists(path),label);
            }
            try
            {
                Invalid("Empty logical part rejected before project files exist");
                draft.AddLayer(PortraitLayer.HairFront);Layer(PortraitLayer.HairFront).File=front;
                Check(PortraitPartImporter.Validate(config,draft)=="","Front hair alone is valid without back hair");
                Check(!draft.AddLayer(PortraitLayer.HairFront)&&!draft.AddLayer(PortraitLayer.ClothesFront)&&draft.Layers.Count==1,"Adding layers excludes duplicates and layers belonging to other part types");
                draft.AddLayer(PortraitLayer.HairBack);
                Check(PortraitPartImporter.Validate(config,draft)=="","Unfilled optional layer does not block a valid single-layer part");
                Layer(PortraitLayer.HairBack).File=back;draft.Male=draft.Female=false;Invalid("No gender selection rejected");draft.Male=true;
                Layer(PortraitLayer.HairBack).File=wrong;Invalid("Raw 64 PNG rejected for a 32 project");
                Layer(PortraitLayer.HairBack).File=rectangle;Invalid("Non-square source rejected");
                Layer(PortraitLayer.HairBack).File=front;Invalid("Same PNG cannot fill both hair slots");Layer(PortraitLayer.HairBack).File=back;
                draft.Weight=float.NaN;Invalid("Nonfinite random weight rejected");draft.Weight=1;
                Layer(PortraitLayer.HairFront).Tint=PortraitTint.None;Invalid("Uncolored hair cannot bypass natural aging");Layer(PortraitLayer.HairFront).Tint=PortraitTint.Hair;
                var validId=draft.Id;foreach(string bad in new[]{"../escape","CON","test.","placeholder.new","a/b"}) { draft.Id=bad;bool rejected=false;try{PortraitPartImporter.ValidateId(bad);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Invalid output identifier rejected: "+bad); }draft.Id=validId;
                foreach(string step in new[]{"file-imported","validated","registered"})
                {
                    string before=File.ReadAllText(AssetDatabase.GetAssetPath(config));bool rolledBack=false;
                    try { PortraitPartImporter.Import(config,draft,at=>{if(at==step)throw new IOException("Owned import rollback fixture");}); }catch(IOException){rolledBack=true;}
                    Check(rolledBack&&config.Parts.Length==0&&!Directory.Exists(PortraitPartImporter.Destination(draft))&&before==File.ReadAllText(AssetDatabase.GetAssetPath(config)),"Import rollback restores files and catalog: "+step);
                }
                string folder=PortraitPartImporter.Import(config,draft);created.Add(folder);
                Check(config.Parts.Length==1&&config.Parts[0].Genders==1&&config.Parts[0].Renders.Length==2,"Paired male hairstyle registered once");
                foreach(var r in config.Parts[0].Renders)
                {
                    string path=AssetDatabase.GetAssetPath(r.Sprite);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    Check(r.Sprite.texture.width==32&&r.Sprite.texture.height==32&&r.Sprite.pivot==new Vector2(16,16)&&importer.spriteImportMode==SpriteImportMode.Single&&importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.isReadable&&importer.textureCompression==TextureImporterCompression.Uncompressed,"Imported sprite has exact pixel-safe settings: "+r.Layer);
                    Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(r.Layer==PortraitLayer.HairBack?back:front)),"Source bytes copied without resizing: "+r.Layer);
                }
                using(var library=PortraitLibraryBuilder.Build(config))
                {
                    int id=PortraitLibraryBuilder.StableId(draft.Id),at=PortraitOps.Find(ref library.Value,id);
                    Check(at>=0&&library.Value.Parts.Length>1,"Imported art is present while procedural placeholders remain enabled");
                    Check(PortraitOps.Compatible(ref library.Value,id,PortraitPartType.Hair,PersonGender.Male)&&!PortraitOps.Compatible(ref library.Value,id,PortraitPartType.Hair,PersonGender.Female),"Male-only import respects runtime gender filtering");
                }
                var beforeDuplicate=File.ReadAllBytes(folder+"/HairFront.png");bool duplicate=false;try{PortraitPartImporter.Import(config,draft);}catch(InvalidOperationException){duplicate=true;}
                Check(duplicate&&config.Parts.Length==1&&beforeDuplicate.SequenceEqual(File.ReadAllBytes(folder+"/HairFront.png")),"Duplicate import cannot overwrite existing art or add a second entry");
                draft.Id=validId+"_existing";string existingFolder=PortraitPartImporter.Destination(draft);AssetDatabase.CreateFolder(PortraitPartImporter.OutputRoot+"/Hair",draft.Id);created.Add(existingFolder);
                bool collision=false;try{PortraitPartImporter.Import(config,draft);}catch(InvalidOperationException){collision=true;}
                Check(collision&&AssetDatabase.IsValidFolder(existingFolder)&&config.Parts.Length==1,"Pre-existing destination folder is preserved rather than overwritten or deleted");
                foreach(byte gender in new byte[]{2,3})
                {
                    draft.Id=validId+"_"+gender;draft.Male=(gender&1)!=0;draft.Female=(gender&2)!=0;created.Add(PortraitPartImporter.Import(config,draft));
                    Check(config.Parts.Last().Genders==gender,"Female/shared choice persists bitmask "+gender);
                }
                foreach(var single in new[]{PortraitLayer.HairFront,PortraitLayer.HairBack})
                {
                    var singleDraft=new PortraitImportDraft {Id=validId+"_single_"+single,Type=PortraitPartType.Hair};singleDraft.AddLayer(single);singleDraft.Layers[0].File=single==PortraitLayer.HairFront?front:back;
                    created.Add(PortraitPartImporter.Import(config,singleDraft));
                    using var library=PortraitLibraryBuilder.Build(config);
                    int id=PortraitLibraryBuilder.StableId(singleDraft.Id),at=PortraitOps.Find(ref library.Value,id);
                    var dna=new PortraitDNA {Hair=new Color32(255,255,255,255)};dna.Parts.Add(id);
                    using var pixels=new NativeArray<Color32>(32*32,Allocator.Temp);PortraitPixels.Compose(ref library.Value,dna,32,PersonGender.Male,pixels);
                    Check(config.Parts.Last().Renders.Length==1&&library.Value.Parts[at].Count==1&&pixels[0].a==255,"Single hair layer imports, bakes and composes without a companion: "+single);
                }
                var bodyDraft=new PortraitImportDraft {Id=validId+"_body",Type=PortraitPartType.Body};
                foreach(var layer in PortraitPartRules.Allowed(bodyDraft.Type)) {bodyDraft.AddLayer(layer);bodyDraft.Layers.Last().File=Png("body_"+layer,32,32);}
                created.Add(PortraitPartImporter.Import(config,bodyDraft));
                Check(config.Parts.Last().Renders.Length==3,"One logical body part imports all three selected program layers");
                var backClothes=new PortraitImportDraft {Id=validId+"_clothes",Type=PortraitPartType.Clothes};backClothes.AddLayer(PortraitLayer.ClothesBack);backClothes.Layers[0].File=back;
                created.Add(PortraitPartImporter.Import(config,backClothes));
                Check(config.Parts.Last().Renders.Single().Layer==PortraitLayer.ClothesBack,"Back clothes can be imported without a front layer");
                var imported=config.Parts[0];var layers=imported.Renders;
                try
                {
                    foreach(var single in layers) {imported.Renders=new[]{single};using var valid=PortraitLibraryBuilder.Build(config);}
                    Check(true,"Direct Inspector configuration also permits either individual hair layer");
                }
                finally{imported.Renders=layers;}
                imported.Renders=new[]{layers[0],layers[0]};bool repeated=false;
                try{using var invalid=PortraitLibraryBuilder.Build(config);}catch(InvalidOperationException){repeated=true;}finally{imported.Renders=layers;}
                Check(repeated,"Baker still rejects duplicate program layers");
                var wrongLayer=layers[0].Layer;layers[0].Layer=PortraitLayer.ClothesFront;bool mismatched=false;
                try{using var invalid=PortraitLibraryBuilder.Build(config);}catch(InvalidOperationException){mismatched=true;}finally{layers[0].Layer=wrongLayer;}
                Check(mismatched,"Baker rejects a render layer belonging to another body part");
                config.Resolution=64;bool mixed=false;try{using var invalid=PortraitLibraryBuilder.Build(config);}catch(InvalidOperationException){mixed=true;}
                Check(mixed,"Changing project size catches incompatible existing art");config.Parts=Array.Empty<PortraitPartSource>();
                draft.Id=validId+"_64";Layer(PortraitLayer.HairFront).File=wrong;Layer(PortraitLayer.HairBack).File=Png("back64",64,64);
                created.Add(PortraitPartImporter.Import(config,draft));Check(config.Parts.Single().Renders.All(r=>r.Sprite.rect.width==64),"A 64 project accepts paired 64 art");
                var window=ScriptableObject.CreateInstance<PortraitImportWindow>();
                try
                {
                    var form=window.Draft;form.Id="window_"+unique;form.Type=PortraitPartType.Hair;form.ResetLayers();
                    window.Recheck();window.ImportCurrent();
                    Check(!window.CanImport&&window.ValidationError.Contains("至少")&&!Directory.Exists(PortraitPartImporter.Destination(form)),"Window validation and submit block an empty part");
                    form.AddLayer(PortraitLayer.HairBack);form.Layers.Single(l=>l.Layer==PortraitLayer.HairBack).File=Png("window_back",original.Resolution,original.Resolution);
                    window.Recheck();Check(window.CanImport,"Window permits back hair without requiring front hair");
                    form.AddLayer(PortraitLayer.HairFront);
                    window.Recheck();Check(window.CanImport,"Adding an unfilled optional window layer does not block submission");
                    var windowFront=form.Layers.Single(l=>l.Layer==PortraitLayer.HairFront);windowFront.File=Png("window_front",original.Resolution,original.Resolution);
                    window.Recheck();Check(window.CanImport,"Window enables valid paired draft using actual project resolution");
                    form.Layers.RemoveAt(0);window.Recheck();Check(window.CanImport&&form.Layers.Single().Layer==PortraitLayer.HairFront,"Removing back hair keeps the remaining front layer importable");
                    form.Male=form.Female=false;window.Recheck();Check(!window.CanImport&&window.ValidationError.Contains("男用"),"Window requires at least one gender checkbox");form.Male=form.Female=true;
                    int incompatible=original.Resolution==32?64:32;windowFront.File=Png("window_bad",incompatible,incompatible);window.Recheck();window.ImportCurrent();
                    Check(!window.CanImport&&window.ValidationError.Contains("原始尺寸")&&!Directory.Exists(PortraitPartImporter.Destination(form)),"Window refuses wrong raw resolution on submission");
                }
                finally { UnityEngine.Object.DestroyImmediate(window); }
                Check(File.ReadAllText(AssetDatabase.GetAssetPath(original))==sourceAsset,"Importer tests leave actual project portrait config untouched");
            }
            finally
            {
                AssetDatabase.DeleteAsset(fixture);
                foreach(var folder in created)if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                foreach(var file in Directory.GetFiles(sources))File.Delete(file);Directory.Delete(sources);
            }
        }
    }
}
#endif
