using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Articy.Api;
using Articy.Api.Plugins;
using Articy.Api.ObjectCustomization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Narrative.Migration;

// This tool runs inside the supported MDK session. It never edits native partitions.
public sealed class Plugin : MacroPlugin
{
    public override string DisplayName => "Narrative migration";
    public override string ContextName => "Narrative migration";
    string Root => Session.GetProjectFolderPath().TrimEnd('\\','/');
    string ManifestFile => Path.Combine(Root,"..","Migration","NativeManifest.json");
    public override bool ProjectLoaded()
    {
        StartRequestPolling();
        QueueRequestProcessing();
        return true;
    }
    public override List<MacroCommandDescriptor> GetMenuEntries(ContextArgsBase args)
    {
        if (!Session.ProjectOpen) return new();
        return new()
        {
            new() { CaptionLid="Publish narrative to Unity", ModifiesData=false, Execute=Publish },
            new() { CaptionLid="Import native narrative", ModifiesData=true, Execute=Import },
            new() { CaptionLid="Export authored narrative", ModifiesData=false, Execute=Export },
            new() { CaptionLid="Connect biography flow", ModifiesData=true, Execute=ConnectFlow },
            new() { CaptionLid="Export official Unity package", ModifiesData=false, Execute=ExportUnity },
            new() { CaptionLid="Import native rules", ModifiesData=true, Execute=ImportRules }
        };
    }
    void SimplifyBiography()
    {
        // Archive current author values before removing fields through the supported MDK.
        var archive=new JArray();
        foreach(var record in (JArray)ReadManifest()["records"])
        {
            string schema=(string)record["schema"];
            if(schema!="Biography"&&schema!="Choice")continue;
            var obj=Session.GetObjectByTechName((string)record["name"]);
            if(obj==null||!obj.IsValid)throw new InvalidOperationException("Missing biography object "+record["name"]);
            var item=new JObject{["name"]=(string)record["name"],["title"]=obj.GetDisplayName(),["text"]=obj.GetText()};
            foreach(string field in schema=="Choice"?new[]{"F_sourceNote","F_consequence"}:new[]{"F_sourceNote"})
            {
                var raw=obj[schema+"Data."+field];item[field]=raw is TextProxy text?text.Text:Convert.ToString(raw);
            }
            archive.Add(item);
        }
        string path=Path.Combine(Root,"..","Migration","BiographyBeforeSimplification.json");
        if(!File.Exists(path))File.WriteAllText(path,archive.ToString(),new UTF8Encoding(false));
        foreach(string schema in new[]{"Biography","Choice"})
        {
            var descriptor=((JArray)ReadManifest()["schemas"]).First(x=>(string)x["name"]==schema);
            var authored=new List<(ObjectProxy obj,JObject values)>();
            foreach(var record in ((JArray)ReadManifest()["records"]).Where(x=>(string)x["schema"]==schema))
            {
                var obj=Session.GetObjectByTechName((string)record["name"]);var values=new JObject();
                foreach(var field in ((JObject)record["values"]).Properties())
                {
                    var raw=obj[schema+"Data."+field.Name];values[field.Name]=raw is TextProxy t?new JValue(t.Text):JToken.FromObject(raw??"");
                }
                authored.Add((obj,values));
            }
            string temporary=schema+"Clean";
            var f=Session.BeginNewFeature();f.TechnicalName=temporary+"Data";f.DisplayName=schema;
            foreach(var field in (JArray)descriptor["fields"])
            {
                string kind=(string)field["kind"],key=(string)field["key"],label=(string)field["label"];
                if(kind=="number"){var v=f.AddProperty<NumberPropertyBlueprint>();v.TechnicalName=key;v.DisplayName=label;v.MinimumValue=-9007199254740991;v.MaximumValue=9007199254740991;v.Precision=0;}
                else if(kind=="boolean"){var v=f.AddProperty<BooleanPropertyBlueprint>();v.TechnicalName=key;v.DisplayName=label;}
                else{var v=f.AddProperty<TextPropertyBlueprint>();v.TechnicalName=key;v.DisplayName=label;v.Size=TextPropertyBlueprint.TextPropertySizes.Large;}
            }
            var fresh=Session.EndFeature(f);var template=Session.BeginNewObjectTemplate(ObjectType.FlowFragment);template.TechnicalName=temporary;template.DisplayName=schema;template.AddFeature(fresh);Session.EndObjectTemplate(template);
            foreach(var item in authored)
            {
                item.obj.SetTemplate(temporary,false);
                foreach(var field in item.values.Properties())item.obj[temporary+"Data."+field.Name]=field.Value.Type==JTokenType.Integer?(object)(double)field.Value:(string)field.Value;
            }
            Session.DeleteObject(Session.GetTemplateByTechName(schema));Session.DeleteObject(Session.GetFeatureByTechName(schema+"Data"));
            var renamedFeature=Session.BeginModifyFeature(fresh);renamedFeature.TechnicalName=schema+"Data";Session.EndFeature(renamedFeature);
            var renamedTemplate=Session.BeginModifyObjectTemplate(Session.GetTemplateByTechName(temporary));renamedTemplate.TechnicalName=schema;Session.EndObjectTemplate(renamedTemplate);
        }
        Session.SaveProject();
        File.WriteAllText(Path.Combine(Root,"..","Migration","SimplifyBiographyResult.txt"),"Removed sourceNote and consequence; preserved author text, choice IDs, flow and metric values.");
    }
    void SimplifyPrologue()
    {
        // Preserve the author's current native Text and DisplayName before replacing object types.
        var records=(JArray)ReadManifest()["records"];
        var pages=new List<(string name,string id,string title,string body)>();
        foreach(var record in records.Where(r=>(string)r["schema"]=="ProloguePage"))
        {
            string name=(string)record["name"];
            var old=Session.GetObjectByTechName(name);
            if(old==null||!old.IsValid)throw new InvalidOperationException("Missing existing prologue page: "+name);
            string feature=old.ObjectType==ObjectType.FlowFragment?"PrologueData":"ProloguePageData";
            var raw=old[feature+".F_id"];
            string id=raw is TextProxy text?text.Text:Convert.ToString(raw);
            pages.Add((name,id,old.GetDisplayName(),old.GetText()));
        }
        string archive=Path.Combine(Root,"..","Migration","PrologueBeforeSimplification.json");
        if(!File.Exists(archive))File.WriteAllText(archive,JsonConvert.SerializeObject(pages,Formatting.Indented));
        if(!Session.TemplatePresent("ProloguePage"))
        {
            var blueprint=Session.BeginNewFeature();blueprint.TechnicalName="ProloguePageData";blueprint.DisplayName="Σελίδα προλόγου";
            foreach(string key in new[]{"id","title","body"})
            {
                var property=blueprint.AddProperty<TextPropertyBlueprint>();property.TechnicalName="F_"+key;property.DisplayName=key;property.Size=TextPropertyBlueprint.TextPropertySizes.Large;
            }
            var feature=Session.EndFeature(blueprint);
            var template=Session.BeginNewObjectTemplate(ObjectType.Entity);template.TechnicalName="ProloguePage";template.DisplayName="Σελίδα προλόγου";template.AddFeature(feature);Session.EndObjectTemplate(template);
        }
        var folder=Session.GetObjectByTechName("ProloguePages");
        if(folder==null||!folder.IsValid){folder=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Πρόλογος");folder.SetTechnicalName("ProloguePages");}
        foreach(var page in pages)
        {
            var old=Session.GetObjectByTechName(page.name);
            if(old.ObjectType==ObjectType.Entity){Session.MoveObject(old,folder);continue;}
            old.SetTechnicalName("Retired_"+page.name+"_"+DateTime.UtcNow.Ticks);Session.DeleteObject(old);
            var entity=Session.CreateEntity(folder,page.title,"ProloguePage");entity.SetTechnicalName(page.name);entity.SetText(page.body);
            entity["ProloguePageData.F_id"]=page.id;entity["ProloguePageData.F_title"]=page.title;entity["ProloguePageData.F_body"]=page.body;
        }
        var obsolete=Session.GetTemplateByTechName("Prologue");if(obsolete!=null&&obsolete.IsValid)Session.DeleteObject(obsolete);
        var obsoleteFeature=Session.GetFeatureByTechName("PrologueData");if(obsoleteFeature!=null&&obsoleteFeature.IsValid)Session.DeleteObject(obsoleteFeature);
        Session.SaveProject();
        File.WriteAllText(Path.Combine(Root,"..","Migration","SimplifyPrologueResult.txt"),"Four independent prologue entities; preserved native text; removed old flow template and note/background properties.");
    }
    void ApplyPoliticalCore()
    {
        string migration=Path.GetFullPath(Path.Combine(Root,"..","Migration"));
        var plan=JObject.Parse(File.ReadAllText(Path.Combine(migration,"PoliticalCorePlan.json"),Encoding.UTF8));
        if((int?)plan["schemaVersion"]!=1)throw new InvalidDataException("Unsupported political core plan.");
        string caseId=(string)plan["caseId"];
        string targetName="Case_"+caseId.Replace('-','_');
        var manifest=ReadManifest();
        var fields=(JArray)((JObject)((JArray)manifest["schemas"]).First(x=>(string)x["name"]=="Case"))["fields"];
        var original=new JArray(fields.Where(x=>(string)x["key"]!="F_politicsData"));
        var names=((JArray)manifest["records"]).Where(x=>(string)x["schema"]=="Case").Select(x=>(string)x["name"]).ToArray();
        if(caseId!="grain"||!names.Contains(targetName))throw new InvalidDataException("Political core first-case migration only accepts grain.");
        var replacement=(JObject)plan["caseReplacement"];
        if((string)replacement["id"]!=caseId)throw new InvalidDataException("Replacement changes the stable case ID.");
        var patchValues=new JObject();
        foreach(var field in original)
        {
            string key=(string)field["key"];JToken cursor=replacement;
            foreach(string part in key.Substring(2).Split('_'))
            {
                if(cursor==null)break;
                cursor=cursor is JArray array&&int.TryParse(part,out int index)?array[index]:cursor[part];
            }
            if(cursor!=null)patchValues[key]=cursor.DeepClone();
        }
        string politics=(string)plan["politicsData"];
        if((int?)JObject.Parse(politics)["version"]!=1)throw new InvalidDataException("Invalid politicsData version.");
        patchValues["F_politicsData"]=politics;
        var updates=new JArray(new JObject{["name"]=targetName,["values"]=patchValues,["title"]=(string)replacement["title"],["text"]=(string)replacement["brief"]});
        object Decode(JToken token)=>token.Type==JTokenType.Boolean?(object)(bool)token:token.Type==JTokenType.Integer||token.Type==JTokenType.Float?(object)(double)token:(object)(string)token;
        JToken ReadValue(ObjectProxy obj,string column)
        {
            var raw=obj[column];
            return raw is TextProxy text?new JValue(text.Text):JToken.FromObject(raw??"");
        }
        // Capture actual author values, never overwrite other cases from migration seed copies.
        var archive=new JArray();
        foreach(string name in names)
        {
            var obj=Session.GetObjectByTechName(name);
            if(obj==null||!obj.IsValid)throw new InvalidOperationException("Missing native case: "+name);
            var values=new JObject();
            foreach(var field in original)values[(string)field["key"]]=ReadValue(obj,"CaseData."+(string)field["key"]);
            archive.Add(new JObject{["name"]=name,["title"]=obj.GetDisplayName(),["text"]=obj.GetText(),["values"]=values});
        }
        string archivePath=Path.Combine(migration,"PoliticalCoreBefore-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff")+".json");
        File.WriteAllText(archivePath,archive.ToString(),new UTF8Encoding(false));

        bool hasPolitics=false;
        try{ReadValue(Session.GetObjectByTechName(names[0]),"CaseData.F_politicsData");hasPolitics=true;}
        catch(ArticyApiException){ }
        if(!hasPolitics)
        {
            // A temporary template is used because extending an existing MDK feature left
            // cached columns unavailable in the previous proven schema migration.
            const string temporary="CasePoliticalCore";
            if(Session.TemplatePresent(temporary))throw new InvalidOperationException("Unfinished political template migration. Restore the native project backup before retrying.");
            var feature=Session.BeginNewFeature();feature.TechnicalName=temporary+"Data";feature.DisplayName="Υπόθεση";
            foreach(var field in fields)
            {
                string key=(string)field["key"],kind=(string)field["kind"],label=(string)field["label"];
                if(kind=="number"){var p=feature.AddProperty<NumberPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=label;p.MinimumValue=-9007199254740991;p.MaximumValue=9007199254740991;p.Precision=0;}
                else if(kind=="boolean"){var p=feature.AddProperty<BooleanPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=label;}
                else{var p=feature.AddProperty<TextPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=label;p.Size=TextPropertyBlueprint.TextPropertySizes.Large;}
            }
            var fresh=Session.EndFeature(feature);
            var template=Session.BeginNewObjectTemplate(ObjectType.FlowFragment);template.TechnicalName=temporary;template.DisplayName="Υπόθεση";template.AddFeature(fresh);Session.EndObjectTemplate(template);
            foreach(var item in archive)
            {
                var obj=Session.GetObjectByTechName((string)item["name"]);obj.SetTemplate(temporary,false);
                foreach(var value in ((JObject)item["values"]).Properties())obj[temporary+"Data."+value.Name]=Decode(value.Value);
                obj[temporary+"Data.F_politicsData"]="";
                obj.SetDisplayName((string)item["title"]);obj.SetText((string)item["text"]);
            }
            Session.DeleteObject(Session.GetTemplateByTechName("Case"));Session.DeleteObject(Session.GetFeatureByTechName("CaseData"));
            var renamed=Session.BeginModifyFeature(fresh);renamed.TechnicalName="CaseData";Session.EndFeature(renamed);
            var renamedTemplate=Session.BeginModifyObjectTemplate(Session.GetTemplateByTechName(temporary));renamedTemplate.TechnicalName="Case";Session.EndObjectTemplate(renamedTemplate);
        }
        foreach(var item in updates)
        {
            var obj=Session.GetObjectByTechName((string)item["name"]);
            foreach(var value in ((JObject)item["values"]).Properties())obj["CaseData."+value.Name]=Decode(value.Value);
            if(item["title"]!=null)obj.SetDisplayName((string)item["title"]);
            if(item["text"]!=null)obj.SetText((string)item["text"]);
        }

        Session.SaveProject();
        File.WriteAllText(Path.Combine(migration,"PoliticalCoreResult.json"),new JObject{["completedUtc"]=DateTime.UtcNow.ToString("O"),["casesPreserved"]=names.Length,["casesUpdated"]=updates.Count,["contentVersion"]=(string)plan["contentVersion"],["beforeArchive"]=archivePath}.ToString(),new UTF8Encoding(false));
    }

    void RefreshPoliticalCoreRules()
    {
        var rules=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","RulesManifest.json"),Encoding.UTF8));
        foreach(var node in (JArray)rules["nodes"])
        {
            string name=(string)node["name"],kind=(string)node["type"];
            if(!(name.StartsWith("Rule_PoliticalSpeech_",StringComparison.Ordinal)||name.StartsWith("Rule_PoliticalVote_",StringComparison.Ordinal)))continue;
            if(kind!="instruction"&&kind!="condition")continue;
            var obj=Session.GetObjectByTechName(name);
            if(obj==null||!obj.IsValid)throw new InvalidOperationException("New political operation node was not imported: "+name);
            obj[ObjectPropertyNames.Expression]=(string)node["script"];
        }
        Session.SaveProject();
    }

    JObject ReadManifest() => JObject.Parse(File.ReadAllText(ManifestFile,Encoding.UTF8));
    void Publish(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        try
        {
            ExportCore(false);ExportUnity(null!,selected);
            string path=Path.GetFullPath(Path.Combine(Root,"..","Exports"));
            string bundle=Path.Combine(path,"UnityContentBundle.json");
            string unity=Path.GetFullPath(Path.Combine(Root,"..","..","Assets","ArticyContent","Game.articyu3d"));
            string Hash(string file)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            File.WriteAllText(Path.Combine(path,"Publication.json"),new JObject{["schemaVersion"]=1,["exportedUtc"]=DateTime.UtcNow.ToString("O"),["bundleSha256"]=Hash(bundle),["unitySha256"]=Hash(unity)}.ToString());
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Root,"..","Migration","PublishError.txt"),e.ToString());if(command!=null)ShowMessageBox(e.Message,"Publication failed",MessageBoxButton.OK,MessageBoxImage.Error);else throw;}
    }
    void ApplyGameplayRevision()
    {
        var plan=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","GameplayRevisionPlan.json")));
        const string temp="CaseRevision";
        var feature=Session.BeginNewFeature();feature.TechnicalName=temp+"Data";feature.DisplayName="Υπόθεση";
        foreach(var field in (JArray)plan["caseFields"])
        {
            string key=(string)field["key"],kind=(string)field["kind"];
            if(kind=="number"){var prop=feature.AddProperty<NumberPropertyBlueprint>();prop.TechnicalName=key;prop.DisplayName=(string)field["label"];prop.MinimumValue=-9007199254740991;prop.MaximumValue=9007199254740991;prop.Precision=0;}
            else if(kind=="boolean"){var prop=feature.AddProperty<BooleanPropertyBlueprint>();prop.TechnicalName=key;prop.DisplayName=(string)field["label"];}
            else{var prop=feature.AddProperty<TextPropertyBlueprint>();prop.TechnicalName=key;prop.DisplayName=(string)field["label"];prop.Size=TextPropertyBlueprint.TextPropertySizes.Large;}
        }
        var fresh=Session.EndFeature(feature);var blueprint=Session.BeginNewObjectTemplate(ObjectType.FlowFragment);blueprint.TechnicalName=temp;blueprint.DisplayName="Υπόθεση";blueprint.AddFeature(fresh);Session.EndObjectTemplate(blueprint);
        foreach(var record in (JArray)plan["records"])
        {
            string name=(string)record["name"],schema=(string)record["schema"];
            var obj=Session.GetObjectByTechName(name);
            if(obj==null||!obj.IsValid){obj=Session.CreateFlowFragment(Session.GetObjectByTechName("NarrativeFlow"),(string)record["label"],temp);obj.SetTechnicalName(name);obj.SetFlowPosition((double?)record["x"]??0,(double?)record["y"]??0);}
            if(schema=="Case"){obj.SetTemplate(temp,false);obj.SetText((string)record["text"]);}
            string prefix=schema=="Case"?temp:schema;
            foreach(var f in ((JObject)record["values"]).Properties())obj[prefix+"Data."+f.Name]=f.Value.Type==JTokenType.Boolean?(object)(bool)f.Value:f.Value.Type==JTokenType.Integer?(object)(double)f.Value:(object)(string)f.Value;
        }
        Session.DeleteObject(Session.GetTemplateByTechName("Case"));Session.DeleteObject(Session.GetFeatureByTechName("CaseData"));
        var renamed=Session.BeginModifyFeature(fresh);renamed.TechnicalName="CaseData";Session.EndFeature(renamed);
        var renamedTemplate=Session.BeginModifyObjectTemplate(Session.GetTemplateByTechName(temp));renamedTemplate.TechnicalName="Case";Session.EndObjectTemplate(renamedTemplate);
        var rules=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","RulesManifest.json")));
        foreach(var n in (JArray)rules["nodes"]){var obj=Session.GetObjectByTechName((string)n["name"]);if(obj!=null&&obj.IsValid&&((string)n["type"]=="instruction"||(string)n["type"]=="condition"))obj[ObjectPropertyNames.Expression]=(string)n["script"];}
        Session.SaveProject();
    }
    void ImportRules(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        try
        {
            var m=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","RulesManifest.json")));
            var set=Session.GetVariableSetByName("Story");
            if(set==null||!set.IsValid)set=Session.CreateVariableSet("Story","Narrative operation state");
            var names=set.GetChildren().Select(x=>x.GetDisplayName()).ToHashSet();
            foreach(var v in ((JObject)m["variables"]).Properties())
            {
                if(names.Contains(v.Name))continue;
                if(v.Value.Type==JTokenType.Boolean)Session.CreateGlobalVariable(set,v.Name,(bool)v.Value,"Runtime narrative input/output");
                else if(v.Value.Type==JTokenType.Integer)Session.CreateGlobalVariable(set,v.Name,(int)v.Value,"Runtime narrative input/output");
                else Session.CreateGlobalVariable(set,v.Name,(string)v.Value,"Runtime narrative input/output");
            }
            foreach(var group in (JArray)m["groups"])
            {
                string name=(string)group["name"];
                var obj=Session.GetObjectByTechName(name);
                if(obj!=null&&obj.IsValid)continue;
                obj=Session.CreateFlowFragment(Session.GetSystemFolder(SystemFolderNames.Flow),(string)group["label"]);obj.SetTechnicalName(name);
            }
            var copyFolder=Session.GetObjectByTechName("NarrativeCopy");
            if(copyFolder==null||!copyFolder.IsValid){copyFolder=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Κείμενα αφήγησης και διεπαφής");copyFolder.SetTechnicalName("NarrativeCopy");}
            foreach(var text in (JArray)m["copy"])
            {
                string name=(string)text["name"];var obj=Session.GetObjectByTechName(name);
                if(obj!=null&&obj.IsValid)continue;
                obj=Session.CreateEntity(copyFolder,(string)text["label"]);obj.SetTechnicalName(name);obj.SetText((string)text["text"]);
            }
            foreach(var n in (JArray)m["nodes"])
            {
                string name=(string)n["name"],kind=(string)n["type"];
                var obj=Session.GetObjectByTechName(name);
                if(obj!=null&&obj.IsValid)continue;
                var parent=Session.GetObjectByTechName((string)n["parent"]);
                obj=kind=="instruction"?Session.CreateInstruction(parent,(string)n["label"]):kind=="condition"?Session.CreateCondition(parent,(string)n["label"]):kind=="hub"?Session.CreateHub(parent,(string)n["label"]):Session.CreateFlowFragment(parent,(string)n["label"]);
                obj.SetTechnicalName(name);
                obj.SetFlowPosition((double)n["x"],(double)n["y"]);
                if(kind=="instruction"||kind=="condition")obj[ObjectPropertyNames.Expression]=(string)n["script"];
            }
            foreach(var e in (JArray)m["edges"])
            {
                var a=Session.GetObjectByTechName((string)e["from"]);var b=Session.GetObjectByTechName((string)e["to"]);
                var pin=a.GetOutputPin((int)e["pin"]);
                if(!pin.GetOutgoingConnections(null).Any(c=>((ObjectProxy)c[ObjectPropertyNames.TargetPin]).GetParent()==b))Session.ConnectPins(pin,b.GetInputPin(0));
            }
            string upgrade=Path.Combine(Root,"..","Migration","BribePriceUpgrade.done");
            if(!File.Exists(upgrade))
            {
                var price=Session.GetObjectByTechName("Rule_Official_6");
                price[ObjectPropertyNames.Expression]="Story.PersonalDelta = 0 - Story.BribePrice; Story.RiskDelta = 2;";
                File.WriteAllText(upgrade,DateTime.UtcNow.ToString("O"));
            }
            var summary=Session.GetObjectByTechName("BioSummary");
            if(summary==null||!summary.IsValid){summary=Session.CreateFlowFragment(Session.GetObjectByTechName("NarrativeFlow"),"Σύνοψη βιογραφίας");summary.SetTechnicalName("BioSummary");summary.SetFlowPosition(8500,0);}
            var bindings=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","ContentBindings.json")));
            var last=(JObject)bindings["baseline"]["configuration"]["biography"].Last;
            foreach(var choice in (JArray)last["choices"])
            {
                var node=Session.GetObjectByTechName("Bio_"+((string)last["id"]).Replace('-','_')+"_"+((string)choice["id"]).Replace('-','_'));
                var pin=node.GetOutputPin(0);
                if(!pin.GetOutgoingConnections(null).Any(c=>((ObjectProxy)c[ObjectPropertyNames.TargetPin]).GetParent()==summary))Session.ConnectPins(pin,summary.GetInputPin(0));
            }
            Session.SaveProject();
            File.WriteAllText(Path.Combine(Root,"..","Migration","RulesImportResult.txt"),"Imported "+((JArray)m["nodes"]).Count+" native rules; "+DateTime.UtcNow.ToString("O"));
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Root,"..","Migration","RulesImportError.txt"),e.ToString());throw;}
    }
    void ExportUnity(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        try
        {
            string target=Path.GetFullPath(Path.Combine(Root,"..","..","Assets","ArticyContent","Game.articyu3d"));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var args=new UnityExportArgs();
            args.SelectedRuleSet=Session.GetObjectsByType(ObjectType.RuleSet).First();
            foreach(var package in Session.GetObjectsByType(ObjectType.RuleSetPackage))args.SetPackageState(package,true);
            args.Filename=target;args.IncludeAssets=false;
            File.WriteAllText(Path.Combine(Root,"..","Migration","UnityExportSettings.xml"),Session.GetExportSettings(args));
            Session.ExportData(args);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Root,"..","Migration","UnityExportError.txt"),e.ToString());if(command!=null)ShowMessageBox(e.Message,"Unity export failed",MessageBoxButton.OK,MessageBoxImage.Error);else throw;}
    }
    void Import(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        try
        {
            var manifest=ReadManifest();
            foreach(var schema in (JArray)manifest["schemas"])
            {
                string name=(string)schema["name"];
                if(Session.TemplatePresent(name))
                {
                    // Older prototypes used a budget-oriented range, unsuitable for Unity local file IDs.
                    // Widen metadata without replacing any author's values.
                    var existing=Session.BeginModifyFeature(Session.GetFeatureByTechName(name+"Data"));
                    foreach(var field in (JArray)schema["fields"])
                        if((string)field["kind"]=="number")
                        {
                            var number=existing.Property<NumberPropertyBlueprint>((string)field["key"]);
                            number.MinimumValue=-9007199254740991;number.MaximumValue=9007199254740991;
                        }
                    Session.EndFeature(existing);
                    continue;
                }
                var feature=Session.BeginNewFeature(); feature.TechnicalName=name+"Data"; feature.DisplayName=(string)schema["label"];
                foreach(var field in (JArray)schema["fields"])
                {
                    string key=(string)field["key"],kind=(string)field["kind"];
                    if(kind=="number") {var p=feature.AddProperty<NumberPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=(string)field["label"];p.MinimumValue=-9007199254740991;p.MaximumValue=9007199254740991;p.Precision=0;}
                    else if(kind=="boolean") {var p=feature.AddProperty<BooleanPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=(string)field["label"];}
                    else {var p=feature.AddProperty<TextPropertyBlueprint>();p.TechnicalName=key;p.DisplayName=(string)field["label"];p.Size=TextPropertyBlueprint.TextPropertySizes.Large;}
                }
                var f=Session.EndFeature(feature);
                var t=Session.BeginNewObjectTemplate((string)schema["type"]=="flow"?ObjectType.FlowFragment:ObjectType.Entity);
                t.TechnicalName=name;t.DisplayName=(string)schema["label"];t.AddFeature(f);Session.EndObjectTemplate(t);
            }
            var folder=Session.GetObjectByTechName("NarrativeContent");
            if(folder==null || !folder.IsValid){folder=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Περιεχόμενο παιχνιδιού");folder.SetTechnicalName("NarrativeContent");}
            var flow=Session.GetObjectByTechName("NarrativeFlow");
            if(flow==null || !flow.IsValid){flow=Session.CreateFlowFragment(Session.GetSystemFolder(SystemFolderNames.Flow),"Αφηγηματική ροή");flow.SetTechnicalName("NarrativeFlow");}
            int created=0,kept=0;
            foreach(var record in (JArray)manifest["records"])
            {
                string tech=(string)record["name"],template=(string)record["schema"];
                var obj=Session.GetObjectByTechName(tech);
                if(obj!=null && obj.IsValid){kept++;continue;} // Never overwrite an author's subsequent edits.
                var parent=folder;
                if((string)record["parent"]=="ProloguePages")
                {
                    parent=Session.GetObjectByTechName("ProloguePages");
                    if(parent==null||!parent.IsValid){parent=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Πρόλογος");parent.SetTechnicalName("ProloguePages");}
                }
                obj=(string)record["type"]=="flow"?Session.CreateFlowFragment(flow,(string)record["label"],template):Session.CreateEntity(parent,(string)record["label"],template);
                obj.SetTechnicalName(tech);
                if((string)record["type"]=="flow") obj.SetFlowPosition((double?)record["x"]??0,(double?)record["y"]??0);
                if(record["text"]!=null)obj["Text"]=(string)record["text"];
                foreach(var field in ((JObject)record["values"]).Properties())
                {
                    object value=field.Value.Type==JTokenType.Integer?(object)(double)field.Value:field.Value.Type==JTokenType.Boolean?(bool)field.Value:(string)field.Value;
                    obj[template+"Data."+field.Name]=value;
                }
                created++;
            }
            ConnectFlow(null,new());
            Session.SaveProject();
            File.WriteAllText(Path.Combine(Root,"..","Migration","NativeImportResult.json"),new JObject{["created"]=created,["preserved"]=kept,["utc"]=DateTime.UtcNow.ToString("O")}.ToString());
            ShowMessageBox($"Created {created} native records; preserved {kept} existing records.","Narrative migration",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Root,"..","Migration","NativeImportError.txt"),e.ToString());ShowMessageBox(e.Message,"Migration failed",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
    void ConnectFlow(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        foreach(var edge in (JArray)ReadManifest()["edges"])
        {
            string from=(string)edge["from"],to=(string)edge["to"],key=from+"->"+to;
            var a=Session.GetObjectByTechName(from);var b=Session.GetObjectByTechName(to);
            if(a==null||!a.IsValid||b==null||!b.IsValid)throw new InvalidOperationException("Missing flow endpoint: "+key);
            var pin=a.GetOutputPin(0);
            bool exists=pin.GetOutgoingConnections(null).Any(c=>((ObjectProxy)c[ObjectPropertyNames.TargetPin]).GetParent()==b);
            if(!exists)Session.ConnectPins(pin,b.GetInputPin(0));
        }
        Session.SaveProject();
    }
    void Export(MacroCommandDescriptor command,List<ObjectProxy> selected)
    {
        ExportCore(true);
    }
    void ExportCore(bool notify)
    {
        try
        {
            var manifest=ReadManifest();var result=new JArray();
            foreach(var record in (JArray)manifest["records"])
            {
                string name=(string)record["name"],schema=(string)record["schema"];
                var obj=Session.GetObjectByTechName(name);
                if(obj==null||!obj.IsValid)throw new InvalidOperationException("Missing object: "+name);
                var values=new JObject();
                foreach(var field in ((JObject)record["values"]).Properties())
                {
                    var raw=obj[schema+"Data."+field.Name];
                    values[field.Name]=raw is TextProxy text ? new JValue(text.Text.Replace("\r\n","\n")) : JToken.FromObject(raw??"");
                }
                // Use articy's visible text/title as the single authoring source for narrative copy.
                // Template fields retain metrics, sources and structured metadata.
                if(values.ContainsKey("F_body")) values["F_body"]=obj.GetText().Replace("\r\n","\n");
                else if(values.ContainsKey("F_text")) values["F_text"]=obj.GetText().Replace("\r\n","\n");
                if(values.ContainsKey("F_title") && schema!="news")values["F_title"]=obj.GetDisplayName();
                else if(values.ContainsKey("F_name"))values["F_name"]=obj.GetDisplayName();
                result.Add(new JObject{["name"]=name,["label"]=obj.GetDisplayName(),["values"]=values});
            }
            string path=Path.Combine(Root,"..","Exports");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"AuthoredContent.json"),new JObject{["schemaVersion"]=1,["projectGuid"]=Session.CurrentProjectGuid.ToString(),["exportedUtc"]=DateTime.UtcNow.ToString("O"),["records"]=result}.ToString(),new UTF8Encoding(false));
            ExportUnityBundle(result,path);
            Session.SaveProject();
            if(notify)ShowMessageBox("Native content exported.","Narrative export",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Root,"..","Migration","NativeExportError.txt"),e.ToString());if(notify)ShowMessageBox(e.Message,"Export failed",MessageBoxButton.OK,MessageBoxImage.Error);else throw;}
    }
    void ExportUnityBundle(JArray records,string path)
    {
        var binding=JObject.Parse(File.ReadAllText(Path.Combine(Root,"..","Migration","ContentBindings.json")));
        var data=(JObject)binding["baseline"].DeepClone();
        var objects=records.ToDictionary(x=>(string)x["name"],x=>(JObject)x["values"]);
        foreach(var b in (JArray)binding["bindings"])
        {
            string name=(string)b["object"],field=((string)b["field"]).Split('.')[1];
            if(!objects.TryGetValue(name,out var values)||!values.TryGetValue(field,out var value))throw new InvalidOperationException("Missing exported field "+name+"."+field);
            var parts=(JArray)b["path"];JToken cursor=data;
            for(int i=0;i<parts.Count-1;i++)cursor=parts[i].Type==JTokenType.Integer?cursor[(int)parts[i]]:cursor[(string)parts[i]];
            JToken typed=(string)b["kind"]=="number"?new JValue(checked((long)Math.Round((double)value))):(string)b["kind"]=="boolean"?new JValue((bool)value):new JValue((string)value);
            if(parts.Last.Type==JTokenType.Integer)cursor[(int)parts.Last]=typed;else cursor[(string)parts.Last]=typed;
        }
        var references=new JArray();
        void ResolveReferences(JToken token,string propertyPath)
        {
            if(token is JObject o)
            {
                if(o["fileID"]!=null)
                {
                    references.Add(new JObject{["propertyPath"]=propertyPath,["guid"]=(string)o["guid"]??"",["fileId"]=(long?)o["fileID"]??0});
                    o.RemoveAll();o["instanceID"]=0;return;
                }
                foreach(var p in o.Properties().ToArray())ResolveReferences(p.Value,string.IsNullOrEmpty(propertyPath)?p.Name:propertyPath+"."+p.Name);
            }
            else if(token is JArray a)for(int i=0;i<a.Count;i++)ResolveReferences(a[i],propertyPath+".Array.data["+i+"]");
        }
        ResolveReferences(data["configuration"],"");
        var bundle=new JObject{["schemaVersion"]=1,["projectGuid"]=Session.CurrentProjectGuid.ToString(),["configJson"]=data["configuration"].ToString(Formatting.None),["campaignJson"]=data["campaign"].ToString(Formatting.None),["cityJson"]=data["city"].ToString(Formatting.None),["lexiconJson"]=data["lexicon"].ToString(Formatting.None),["references"]=references};
        File.WriteAllText(Path.Combine(path,"UnityContentBundle.json"),bundle.ToString(),new UTF8Encoding(false));
    }
    // All MDK calls stay on Articy's UI dispatcher. File/protocol requests contain
    // reviewed migration plans, never arbitrary code or arbitrary filesystem paths.
    System.Windows.Threading.DispatcherTimer requestTimer;
    System.Windows.Threading.Dispatcher requestDispatcher;
    bool requestQueued, requestBusy;
    public override string Initialize()
    {
        StartRequestPolling();
        return null;
    }
    public override bool Deinitialize()
    {
        requestTimer?.Stop();
        requestTimer=null;
        return true;
    }
    void StartRequestPolling()
    {
        if(requestTimer!=null)return;
        requestDispatcher=Application.Current?.Dispatcher??System.Windows.Threading.Dispatcher.CurrentDispatcher;
        requestTimer=new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(2),System.Windows.Threading.DispatcherPriority.Background,
            (_,__)=>QueueRequestProcessing(),requestDispatcher);
        requestTimer.Start();
    }
    public override bool ExecuteProtocol(string command,Dictionary<string,string> parameters)
    {
        if(command!="process"&&command!="publish")return false;
        StartRequestPolling();
        requestDispatcher.BeginInvoke(new Action(()=>
        {
            if(!Session.ProjectOpen)return;
            if(command=="publish")
            {
                string marker=Path.Combine(Root,"..","Migration","Publish.request");
                if(!File.Exists(marker))File.WriteAllText(marker,"{\"schemaVersion\":1}",new UTF8Encoding(false));
            }
            QueueRequestProcessing();
        }));
        return true;
    }
    void QueueRequestProcessing()
    {
        if(requestQueued||requestBusy||Session==null||!Session.ProjectOpen)return;
        requestQueued=true;
        requestDispatcher.BeginInvoke(new Action(()=>
        {
            requestQueued=false;
            if(Session==null||!Session.ProjectOpen||requestBusy)return;
            ProcessOneRequest();
        }),System.Windows.Threading.DispatcherPriority.Background);
    }
    void ProcessOneRequest()
    {
        string migration=Path.GetFullPath(Path.Combine(Root,"..","Migration"));
        string[] operations={"FirstPrytanyRevision","PoliticalCore","GameplayRevision","SimplifyBiography","SimplifyPrologue","RulesImport","Publish","UnityExport"};
        string operation=operations.FirstOrDefault(x=>File.Exists(Path.Combine(migration,x+".request")));
        if(operation==null)return;
        string request=Path.Combine(migration,operation+".request");
        // A failed operation must be reviewed before retrying. Never silently run
        // another content mutation after a partially completed schema change.
        if(Directory.GetFiles(migration,"*.request.failed").Length>0)return;
        requestBusy=true;
        try
        {
            string body=File.ReadAllText(request,Encoding.UTF8).Trim();
            var envelope=body.StartsWith("{",StringComparison.Ordinal)?JObject.Parse(body):new JObject();
            if(envelope["schemaVersion"]!=null&&(int)envelope["schemaVersion"]!=1)throw new InvalidDataException("Unsupported request envelope.");
            File.Move(request,request+".started",true);
            switch(operation)
            {
                case "FirstPrytanyRevision":ApplyFirstPrytanyRevision((string)envelope["planFile"]??"FirstPrytanyRevisionPlan.json");break;
                case "PoliticalCore":ApplyPoliticalCore();ImportRules(null!,new());RefreshPoliticalCoreRules();Publish(null!,new());break;
                case "GameplayRevision":ApplyGameplayRevision();ImportRules(null!,new());Publish(null!,new());break;
                case "SimplifyBiography":SimplifyBiography();Publish(null!,new());break;
                case "SimplifyPrologue":SimplifyPrologue();Publish(null!,new());break;
                case "RulesImport":ImportRules(null!,new());ExportUnity(null!,new());break;
                case "Publish":Publish(null!,new());break;
                case "UnityExport":ExportUnity(null!,new());break;
            }
            File.WriteAllText(request+".result.json",new JObject{["operation"]=operation,["status"]="completed",["projectGuid"]=Session.CurrentProjectGuid.ToString(),["utc"]=DateTime.UtcNow.ToString("O")}.ToString(),new UTF8Encoding(false));
            File.Move(request+".started",request+".done",true);
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(migration,operation+"Error.txt"),error.ToString(),new UTF8Encoding(false));
            string source=File.Exists(request+".started")?request+".started":request;
            if(File.Exists(source))File.Move(source,request+".failed",true);
        }
        finally{requestBusy=false;}
    }
    string MigrationPath(string filename)
    {
        if(string.IsNullOrWhiteSpace(filename)||filename!=Path.GetFileName(filename))throw new InvalidDataException("Migration filenames must be simple local filenames.");
        return Path.Combine(Root,"..","Migration",filename);
    }
    static object NativeValue(JToken token)
    {
        if(token==null||token.Type==JTokenType.Null)return "";
        if(token.Type==JTokenType.Boolean)return (bool)token;
        if(token.Type==JTokenType.Integer||token.Type==JTokenType.Float)return (double)token;
        if(token.Type==JTokenType.String)return (string)token;
        throw new InvalidDataException("Native field values must be strings, numbers or booleans. Serialize structured JSON as a string.");
    }
    static JToken CaptureValue(ObjectProxy obj,string property)
    {
        var raw=obj[property];
        return raw is TextProxy text?new JValue(text.Text):JToken.FromObject(raw??"");
    }
    void DefineFeatureAndTemplate(JObject descriptor,string technicalName)
    {
        var feature=Session.BeginNewFeature();feature.TechnicalName=technicalName+"Data";feature.DisplayName=(string)descriptor["label"];
        foreach(var field in (JArray)descriptor["fields"])
        {
            string key=(string)field["key"],label=(string)field["label"]??key;
            switch((string)field["kind"])
            {
                case "number":var number=feature.AddProperty<NumberPropertyBlueprint>();number.TechnicalName=key;number.DisplayName=label;number.MinimumValue=-9007199254740991;number.MaximumValue=9007199254740991;number.Precision=0;break;
                case "boolean":var boolean=feature.AddProperty<BooleanPropertyBlueprint>();boolean.TechnicalName=key;boolean.DisplayName=label;break;
                case "text":var text=feature.AddProperty<TextPropertyBlueprint>();text.TechnicalName=key;text.DisplayName=label;text.Size=TextPropertyBlueprint.TextPropertySizes.Large;break;
                default:throw new InvalidDataException("Unknown native field kind: "+field["kind"]);
            }
        }
        var fresh=Session.EndFeature(feature);
        var template=Session.BeginNewObjectTemplate((string)descriptor["type"]=="flow"?ObjectType.FlowFragment:ObjectType.Entity);
        template.TechnicalName=technicalName;template.DisplayName=(string)descriptor["label"];template.AddFeature(fresh);Session.EndObjectTemplate(template);
    }
    void ApplyFirstPrytanyRevision(string planFilename)
    {
        var plan=JObject.Parse(File.ReadAllText(MigrationPath(planFilename),Encoding.UTF8));
        if((int?)plan["schemaVersion"]!=1)throw new InvalidDataException("Unsupported first-prytany migration plan.");
        string revision=(string)plan["revisionId"];
        if(string.IsNullOrWhiteSpace(revision)||!Regex.IsMatch(revision,"^[a-zA-Z0-9_-]+$"))throw new InvalidDataException("A safe revisionId is required.");
        if(plan["projectGuid"]!=null&&!string.Equals((string)plan["projectGuid"],Session.CurrentProjectGuid.ToString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The migration targets another Articy project.");
        foreach(var digest in ((JObject)plan["sha256"]??new JObject()).Properties())
        {
            string actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(MigrationPath(digest.Name)))).ToLowerInvariant();
            if(!string.Equals(actual,(string)digest.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Migration digest mismatch: "+digest.Name);
        }
        var manifest=ReadManifest();
        var previous=JObject.Parse(File.ReadAllText(MigrationPath((string)plan["previousManifestFile"]??"PreviousNativeManifest.json"),Encoding.UTF8));
        var schemas=((JArray)manifest["schemas"]).Cast<JObject>().ToDictionary(x=>(string)x["name"]);
        var oldSchemas=((JArray)previous["schemas"]).Cast<JObject>().ToDictionary(x=>(string)x["name"]);
        var records=((JArray)manifest["records"]).Cast<JObject>().ToDictionary(x=>(string)x["name"]);
        var schemaUpdates=((JArray)plan["schemaUpdates"]??new JArray()).Select(x=>(string)x).ToHashSet();
        var recordUpdates=((JArray)plan["recordUpdates"]??new JArray()).Cast<JObject>().ToArray();
        var expressionUpdates=((JArray)plan["expressionUpdates"]??new JArray()).Select(x=>(string)x).ToHashSet();
        var replaceEdges=((JArray)plan["replaceOutgoingEdgesFrom"]??new JArray()).Select(x=>(string)x).ToHashSet();
        var rules=JObject.Parse(File.ReadAllText(MigrationPath("RulesManifest.json"),Encoding.UTF8));
        var ruleNodes=((JArray)rules["nodes"]).Cast<JObject>().ToDictionary(x=>(string)x["name"]);

        // Complete validation precedes any native mutation.
        foreach(var update in recordUpdates)
        {
            string name=(string)update["name"];
            if(!records.TryGetValue(name,out var record))throw new InvalidDataException("Unknown record update: "+name);
            foreach(string key in ((JArray)update["fields"]??new JArray()).Select(x=>(string)x))
            {
                if(((JObject)record["values"])[key]==null)throw new InvalidDataException("Missing explicit record value: "+name+"."+key);
                NativeValue(record["values"][key]);
            }
            if((bool?)update["updateText"]==true&&record["text"]==null)throw new InvalidDataException("No native Text for "+name);
        }
        foreach(string name in expressionUpdates)
            if(!ruleNodes.TryGetValue(name,out var node)||((string)node["type"]!="instruction"&&(string)node["type"]!="condition"))throw new InvalidDataException("Unknown expression update: "+name);
        foreach(string schema in schemaUpdates)
            if(!schemas.ContainsKey(schema))throw new InvalidDataException("Unknown schema update: "+schema);
        foreach(var pair in schemas)
        {
            var fields=((JArray)pair.Value["fields"]).Cast<JObject>().ToDictionary(x=>(string)x["key"]);
            foreach(var field in fields.Values)
                if((string)field["kind"]!="number"&&(string)field["kind"]!="boolean"&&(string)field["kind"]!="text")throw new InvalidDataException("Unsupported field kind: "+pair.Key+"."+field["key"]);
            if(oldSchemas.TryGetValue(pair.Key,out var previousSchema))
                foreach(var oldField in (JArray)previousSchema["fields"])
                    if(fields.TryGetValue((string)oldField["key"],out var target)&&(string)target["kind"]!=(string)oldField["kind"])throw new InvalidDataException("Changing a native field kind requires a separate reviewed migration: "+pair.Key+"."+oldField["key"]);
            foreach(var record in records.Values.Where(x=>(string)x["schema"]==pair.Key))
                foreach(var value in ((JObject)record["values"]).Properties())
                {
                    if(!fields.ContainsKey(value.Name))throw new InvalidDataException("Record references an unknown native field: "+record["name"]+"."+value.Name);
                    NativeValue(value.Value);
                }
        }

        var archive=new JArray();
        var schemaObjects=new Dictionary<string,List<ObjectProxy>>();
        foreach(var pair in schemas)
        {
            string schema=pair.Key;
            if(!Session.TemplatePresent(schema)){schemaObjects[schema]=new();continue;}
            var objects=Session.GetObjectsWithFeature(schema+"Data").ToList();schemaObjects[schema]=objects;
            var fields=((JArray)pair.Value["fields"]).Concat(oldSchemas.TryGetValue(schema,out var old)?(JArray)old["fields"]:new JArray()).GroupBy(x=>(string)x["key"]).Select(g=>g.First());
            foreach(var obj in objects)
            {
                var values=new JObject();
                foreach(var field in fields)
                {
                    string key=(string)field["key"];
                    try{values[key]=CaptureValue(obj,schema+"Data."+key);}
                    catch(ArticyApiException)
                    {
                        bool formerlyKnown=oldSchemas.TryGetValue(schema,out var descriptor)&&((JArray)descriptor["fields"]).Any(x=>(string)x["key"]==key);
                        if(formerlyKnown)throw;
                    }
                }
                archive.Add(new JObject{["name"]=obj.GetTechnicalName(),["schema"]=schema,["title"]=obj.GetDisplayName(),["text"]=obj.GetText(),["values"]=values});
            }
        }
        foreach(var pair in schemaObjects)
        {
            if(pair.Value.Count==0||schemaUpdates.Contains(pair.Key))continue;
            var required=((JArray)schemas[pair.Key]["fields"]).Select(x=>(string)x["key"]);
            if(archive.Where(x=>(string)x["schema"]==pair.Key).Any(x=>required.Any(key=>!((JObject)x["values"]).ContainsKey(key))))
                throw new InvalidDataException("A schema changed without schemaUpdates permission: "+pair.Key);
        }
        // Also capture every explicitly overwritten native expression.
        var expressions=new JArray();
        foreach(string name in expressionUpdates)
        {
            var obj=Session.GetObjectByTechName(name);
            if(obj!=null&&obj.IsValid)expressions.Add(new JObject{["name"]=name,["script"]=CaptureValue(obj,ObjectPropertyNames.Expression)});
        }
        string archiveFile=MigrationPath("FirstPrytanyBefore-"+revision+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff")+".json");
        File.WriteAllText(archiveFile,new JObject{["projectGuid"]=Session.CurrentProjectGuid.ToString(),["records"]=archive,["expressions"]=expressions}.ToString(),new UTF8Encoding(false));

        int changedSchemas=0,created=0,updated=0;
        foreach(var pair in schemas)
        {
            string schema=pair.Key;var descriptor=pair.Value;
            if(!Session.TemplatePresent(schema)){DefineFeatureAndTemplate(descriptor,schema);continue;}
            if(!schemaUpdates.Contains(schema))continue;
            var snapshots=archive.Where(x=>(string)x["schema"]==schema).ToArray();
            var desiredKeys=((JArray)descriptor["fields"]).Select(x=>(string)x["key"]).ToArray();
            bool complete=snapshots.Length>0&&snapshots.All(x=>desiredKeys.All(key=>((JObject)x["values"]).ContainsKey(key)));
            if(complete)continue; // Idempotent retry after an already completed schema migration.
            string temporary=schema+"Revision";
            if(Session.TemplatePresent(temporary))throw new InvalidOperationException("An unfinished schema migration exists: "+temporary+". Restore the saved native backup before retrying.");
            DefineFeatureAndTemplate(descriptor,temporary);
            foreach(var snapshot in snapshots)
            {
                string name=(string)snapshot["name"];var obj=Session.GetObjectByTechName(name);obj.SetTemplate(temporary,false);
                foreach(var field in (JArray)descriptor["fields"])
                {
                    string key=(string)field["key"];var value=snapshot["values"][key];
                    // Initialize only absent fields. Existing author values are preserved.
                    if(value==null&&records.TryGetValue(name,out var seed))value=seed["values"][key];
                    if(value==null)value=(string)field["kind"]=="number"?new JValue(0):(string)field["kind"]=="boolean"?new JValue(false):new JValue("");
                    obj[temporary+"Data."+key]=NativeValue(value);
                }
                obj.SetDisplayName((string)snapshot["title"]);obj.SetText((string)snapshot["text"]);
            }
            Session.DeleteObject(Session.GetTemplateByTechName(schema));Session.DeleteObject(Session.GetFeatureByTechName(schema+"Data"));
            var feature=Session.BeginModifyFeature(Session.GetFeatureByTechName(temporary+"Data"));feature.TechnicalName=schema+"Data";Session.EndFeature(feature);
            var template=Session.BeginModifyObjectTemplate(Session.GetTemplateByTechName(temporary));template.TechnicalName=schema;Session.EndObjectTemplate(template);
            changedSchemas++;
        }
        var folder=Session.GetObjectByTechName("NarrativeContent");
        if(folder==null||!folder.IsValid){folder=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Περιεχόμενο παιχνιδιού");folder.SetTechnicalName("NarrativeContent");}
        var flow=Session.GetObjectByTechName("NarrativeFlow");
        if(flow==null||!flow.IsValid){flow=Session.CreateFlowFragment(Session.GetSystemFolder(SystemFolderNames.Flow),"Αφηγηματική ροή");flow.SetTechnicalName("NarrativeFlow");}
        foreach(var record in records.Values)
        {
            string name=(string)record["name"],schema=(string)record["schema"];var obj=Session.GetObjectByTechName(name);
            if(obj!=null&&obj.IsValid)continue;
            bool isFlow=(string)record["type"]=="flow";var parent=isFlow?flow:folder;
            string specifiedParent=(string)record["parent"];
            if(!string.IsNullOrWhiteSpace(specifiedParent))
            {
                var custom=Session.GetObjectByTechName(specifiedParent);
                if(custom==null||!custom.IsValid)
                {
                    if(specifiedParent!="ProloguePages")throw new InvalidDataException("Missing record parent: "+specifiedParent);
                    custom=Session.CreateFolder(Session.GetSystemFolder(SystemFolderNames.Entities),"Πρόλογος");custom.SetTechnicalName(specifiedParent);
                }
                parent=custom;
            }
            obj=isFlow?Session.CreateFlowFragment(parent,(string)record["label"],schema):Session.CreateEntity(parent,(string)record["label"],schema);
            obj.SetTechnicalName(name);if(isFlow)obj.SetFlowPosition((double?)record["x"]??0,(double?)record["y"]??0);
            if(record["text"]!=null)obj.SetText((string)record["text"]);
            foreach(var field in ((JObject)record["values"]).Properties())obj[schema+"Data."+field.Name]=NativeValue(field.Value);
            created++;
        }
        foreach(var update in recordUpdates)
        {
            string name=(string)update["name"];var record=records[name];string schema=(string)record["schema"];var obj=Session.GetObjectByTechName(name);
            foreach(string key in ((JArray)update["fields"]??new JArray()).Select(x=>(string)x))obj[schema+"Data."+key]=NativeValue(record["values"][key]);
            if((bool?)update["updateText"]==true)obj.SetText((string)record["text"]);
            if((bool?)update["updateDisplayName"]==true)obj.SetDisplayName((string)record["label"]);
            if((bool?)update["updatePosition"]==true)obj.SetFlowPosition((double?)record["x"]??0,(double?)record["y"]??0);
            updated++;
        }
        // Only explicit graph endpoints may lose old outgoing edges; all legacy
        // groups/objects and other author's connections remain in the native project.
        foreach(string name in replaceEdges)
        {
            var obj=Session.GetObjectByTechName(name);
            if(obj==null||!obj.IsValid)throw new InvalidDataException("Cannot replace edges for missing node: "+name);
            foreach(var pin in obj.GetOutputPins())foreach(var edge in pin.GetOutgoingConnections(null).ToArray())Session.DeleteObject(edge);
        }
        ConnectFlow(null!,new());
        ImportRules(null!,new());
        foreach(string name in expressionUpdates)
        {
            var obj=Session.GetObjectByTechName(name);
            if(obj==null||!obj.IsValid)throw new InvalidOperationException("Missing imported expression: "+name);
            obj[ObjectPropertyNames.Expression]=(string)ruleNodes[name]["script"];
        }
        Session.SaveProject();
        Publish(null!,new());
        File.WriteAllText(MigrationPath("FirstPrytanyRevisionResult.json"),new JObject{["revisionId"]=revision,["completedUtc"]=DateTime.UtcNow.ToString("O"),["contentVersion"]=(string)plan["contentVersion"],["schemaReplacements"]=changedSchemas,["nativeRecordsCaptured"]=archive.Count,["recordsCreated"]=created,["recordsUpdated"]=updated,["expressionsUpdated"]=expressionUpdates.Count,["beforeArchive"]=archiveFile}.ToString(),new UTF8Encoding(false));
    }

}



