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
        string prologueRequest=Path.Combine(Root,"..","Migration","SimplifyPrologue.request");
        if(File.Exists(prologueRequest))
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>
            {
                try { SimplifyPrologue(); Publish(null!,new()); File.Move(prologueRequest,prologueRequest+".done",true); }
                catch(Exception error){File.WriteAllText(Path.Combine(Root,"..","Migration","SimplifyPrologueError.txt"),error.ToString());}
            }));
            return true;
        }

        string publication=Path.Combine(Root,"..","Migration","Publish.request");
        if(File.Exists(publication))
        {
            File.Move(publication,publication+".started",true);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>Publish(null!,new())));
        }
        string rules=Path.Combine(Root,"..","Migration","RulesImport.request");
        if(File.Exists(rules))
        {
            File.Move(rules,rules+".started",true);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>{ImportRules(null!,new());ExportUnity(null!,new());}));
        }
        string request=Path.Combine(Root,"..","Migration","UnityExport.request");
        if(!File.Exists(request))return true;
        File.Move(request,request+".started",true);
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>ExportUnity(null!,new())));
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
}



