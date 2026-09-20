using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace LivingEmpires
{
    /// <summary>Opt-in runtime QA. Preserves both manual saves; replaces only the unsaved QA session.</summary>
    public static class MilitaryRuntimeVerification
    {
        public static bool Completed, Passed, Running;
        public static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,"..","Reports","military-runtime-tests.txt"));
        public static IEnumerator Run(GameController game)
        {
            if(Running)throw new InvalidOperationException("Military QA is already running.");
            Running=true;Completed=Passed=false;
            var runner=new Runner(game);var tests=runner.Tests();
            try
            {
                while(true)
                {
                    bool more=false;object next=null;
                    try{more=tests.MoveNext();if(more)next=tests.Current;}
                    catch(Exception error){runner.Check(false,"Unexpected runtime failure: "+error);}
                    if(!more)break;yield return next;
                }
            }
            finally{runner.Finish();Passed=runner.Failures==0;Completed=true;Running=false;}
        }
        sealed class Runner
        {
            readonly GameController g;
            readonly List<string> lines=new List<string>();
            readonly List<Mouse> disabled=new List<Mouse>();
            readonly string charterDigest,warDigest,qaPath;
            readonly Mouse original=Mouse.current;
            readonly bool oldEdges;
            Canvas canvas;Mouse mouse;Vector2 previous;
            int checks,clicks,events;
            public int Failures;
            public Runner(GameController game)
            {
                g=game;oldEdges=g.CameraRig.EdgePanEnabled;
                charterDigest=Digest(g.CharterSavePath);warDigest=Digest(g.MilitarySavePath);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                qaPath=Path.Combine(Path.GetDirectoryName(ReportPath),"qa-military-"+Guid.NewGuid().ToString("N")+".json");
            }
            public void Check(bool ok,string text){checks++;if(!ok)Failures++;lines.Add((ok?"PASS: ":"FAIL: ")+text);}
            void Require(bool ok,string text){Check(ok,text);if(!ok)throw new InvalidOperationException(text);}
            public IEnumerator Tests()
            {
                Require(Application.isFocused,"Window has focus for real focus-gated mouse controls.");
                g.UI.ShowMain();yield return null;yield return null;
                canvas=UnityEngine.Object.FindObjectsByType<Canvas>().Single(c=>c.name=="Riverhold interface");
                Click("The Toll War");yield return null;yield return null;
                Check(g.Sim.Military.Enabled&&g.Military.ArmyMode&&g.Paused&&!g.Modal&&!g.InMenu,"Menu opens paused military chapter with Army controls.");
                Check(g.SavePath==g.MilitarySavePath&&g.SavePath!=g.CharterSavePath,"Military and First Winter use distinct save files.");
                Check(g.Sim.State.Buildings.Any(b=>b.Town==0&&b.Type=="barracks")&&g.Sim.State.Towns[0].Population==72,"Chapter starts with disclosed established settlement and barracks.");
                g.CameraRig.EdgePanEnabled=false;
                foreach(var d in InputSystem.devices.OfType<Mouse>().Where(d=>d.enabled).ToArray()){disabled.Add(d);InputSystem.DisableDevice(d);}
                mouse=InputSystem.AddDevice<Mouse>("Riverhold military QA mouse");mouse.MakeCurrent();
                previous=new Vector2(Screen.width*.48f,Screen.height*.45f);Queue(previous);yield return null;yield return null;

                Click("Army tab");yield return null;
                double gold=g.Sim.State.Towns[0].Gold;
                int workers=g.Sim.WorkforceTotal();
                Click("Recruit spearmen");yield return null;
                Check(g.Sim.Military.Queue.Count==1&&g.Sim.MilitaryReservedWorkers()==4&&g.Sim.WorkforceTotal()==workers-4,"Recruitment reserves four real citizens immediately.");
                Check(Math.Abs(g.Sim.State.Towns[0].Gold-gold+Simulation.RecruitmentGold("spearmen"))<.0001,"Recruitment debits the displayed cost exactly once.");
                int recruit=g.Sim.Military.Queue[0].Id;
                Click("Cancel recruitment "+recruit);yield return null;
                Check(g.Sim.Military.Queue.Count==0&&g.Sim.WorkforceTotal()==workers&&Math.Abs(g.Sim.State.Towns[0].Gold-gold)<.0001,"Cancel returns recruitment money, equipment and reserved workers.");
                Click("Upgrade barracks");yield return null;
                Click("Upgrade equipment");yield return null;
                Check(g.Sim.Military.BarracksLevel==2&&g.Sim.Military.EquipmentLevel==1,"Both upgrade buttons change the actual military model.");
                Click("Recruit spearmen");yield return null;
                Click("Recruit archers");yield return null;
                Check(g.Sim.Military.Queue.Count==2&&g.Sim.MilitaryReservedWorkers()==8&&g.Sim.Military.Queue[0].TotalSeconds==15,"Queue is serial, reserves eight people and applies faster barracks training.");
                double clock=g.Sim.Military.Clock;yield return new WaitForSecondsRealtime(.3f);
                Check(g.Sim.Military.Clock==clock,"Pause stops training and raid time.");
                Click("8x");
                float until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until)yield return null;
                Click("Pause");
                Require(g.Sim.Military.Queue.Count>0&&g.Sim.Military.Queue[0].SecondsLeft<15,"Training advances through normal game frames at selected speed.");
                Require(g.Sim.DispatchTrade(1,"stone",4,true)=="","Real bridge trade creates cargo during training.");g.SyncWorld();
                string normalized=JsonUtility.ToJson(JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(g.Sim.Snapshot())));
                Require(g.SaveTo(qaPath),"Production save writes isolated QA slot during training and shipping.");
                g.Sim.AdvanceMilitary(2);
                Require(g.LoadFrom(qaPath),"Production load restores military chapter.");
                Check(JsonUtility.ToJson(g.Sim.Snapshot())==normalized,"Military queue, guard, cargo, costs and clock survive decoded JSON exactly.");
                Check(Math.Abs(g.DayFraction-g.Sim.Military.DayFraction)<.00001,"Load restores intra-day shipping progress without a backward time jump.");
                g.Military.SetArmyMode(true);g.UI.OpenMilitaryPanel();yield return null;
                Click("8x");until=Time.realtimeSinceStartup+12;
                while(g.Sim.Military.Queue.Count>0&&Time.realtimeSinceStartup<until)yield return null;
                // Let the last recruits clear their shared barracks doorway before selection.
                yield return new WaitForSecondsRealtime(.45f);
                Click("Pause");yield return null;yield return null;
                var squads=g.Sim.Military.Squads.Where(s=>!s.Hostile).ToArray();
                Require(squads.Length==2&&g.Sim.Military.Queue.Count==0,"Both squads finish recruitment through normal runtime frames.");
                var views=UnityEngine.Object.FindObjectsByType<SquadView>().Where(v=>!v.Hostile).ToArray();
                Check(views.Length==2&&views.All(v=>v.GetComponentsInChildren<SkinnedMeshRenderer>().Length==4&&v.GetComponents<Collider>().Length==1),"Two physical squad roots render eight animated soldiers with one selection collider each.");
                Check(UnityEngine.Object.FindObjectsByType<MilitaryCampView>().Length==1&&g.Sim.Military.Squads.Any(s=>s.Hostile),"A guarded enemy camp is visible in the playable world.");
                Click("Close drawer");g.CameraRig.FocusOn(new Vector3((float)squads[0].X,0,(float)squads[0].Z));g.CameraRig.Distance=27;
                yield return new WaitForSecondsRealtime(.7f);Physics.SyncTransforms();
                var points=squads.Select(s=>Point(new Vector3((float)s.X,.5f,(float)s.Z))).ToArray();
                Vector2 boxA=new Vector2(points.Min(p=>p.x)-24,points.Min(p=>p.y)-24),boxB=new Vector2(points.Max(p=>p.x)+24,points.Max(p=>p.y)+24);
                Require(!OverUI(boxA)&&!OverUI(boxB),"Group selection fits in the visible world.");
                var boxFocus=g.CameraRig.Focus;
                Queue(boxA,MouseButton.Left);yield return null;yield return null;
                Queue(boxB,MouseButton.Left);yield return null;yield return null;
                Queue(boxB);yield return null;yield return null;
                Check(g.Military.SelectedSquads.Count==2&&Vector3.Distance(boxFocus,g.CameraRig.Focus)<.001f,"Left-drag selects both squads without panning the army camera.");
                if(canvas.GetComponentsInChildren<Button>().Any(b=>b.name=="Close drawer"))Click("Close drawer");
                var view=views.First(v=>v.Id==squads[0].Id);
                Vector2 unit=Point(view.GetComponent<Collider>().bounds.center);
                Require(!OverUI(unit),"Recruit is visible outside interface graphics.");
                RaycastHit hit;Require(Physics.Raycast(g.CameraRig.View.ScreenPointToRay(unit),out hit,260)&&hit.collider.GetComponentInParent<SquadView>()==view,"Squad selection point reaches its actual collider.");
                Queue(unit,MouseButton.Left);yield return null;yield return null;
                Queue(unit);yield return null;yield return null;
                Check(g.Military.SelectedSquads.Count==1&&g.Military.SelectedSquads[0]==view.Id,"Short world click selects the rendered squad through camera and physics.");
                if(canvas.GetComponentsInChildren<Button>().Any(b=>b.name=="Close drawer"))Click("Close drawer");
                Vector2 destination=FindDestination(squads[0]);
                var focus=g.CameraRig.Focus;string order=squads[0].Order;
                Queue(destination,MouseButton.Right);yield return null;yield return null;
                Queue(destination+new Vector2(65,25),MouseButton.Right);yield return null;yield return null;
                Queue(previous);yield return null;yield return null;
                Check(Vector3.Distance(g.CameraRig.Focus,focus)>.05f&&squads[0].Order==order,"Right-drag pans without issuing an accidental unit order.");
                g.CameraRig.FocusOn(new Vector3((float)squads[0].X,0,(float)squads[0].Z));yield return new WaitForSecondsRealtime(.6f);
                destination=FindDestination(squads[0]);
                Queue(destination,MouseButton.Right);yield return null;yield return null;Queue(destination);yield return null;yield return null;
                Check(squads[0].Order=="move"&&squads[0].Path.Count>0,"Short right-click assigns a genuine traversable movement path.");
                var start=new Vector2((float)squads[0].X,(float)squads[0].Z);Click("3x");
                yield return new WaitForSecondsRealtime(.6f);Click("Pause");yield return null;
                Check(Vector2.Distance(start,new Vector2((float)squads[0].X,(float)squads[0].Z))>.2f,"Squad moves in the authoritative model and rendered scene during play.");
                Check(Vector3.Distance(view.transform.position,new Vector3((float)squads[0].X,view.transform.position.y,(float)squads[0].Z))<.7f,"Visible unit position follows the simulation rather than a decorative animation.");
                normalized=JsonUtility.ToJson(JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(g.Sim.Snapshot())));
                Require(g.SaveTo(qaPath)&&g.LoadFrom(qaPath),"Save/load works during an active movement order.");
                Check(JsonUtility.ToJson(g.Sim.Snapshot())==normalized,"Movement path and progress survive the production save boundary.");
                g.Military.SetArmyMode(true);g.UI.OpenMilitaryPanel();yield return null;
                Click("Select all squads");yield return null;
                Check(g.Military.SelectedSquads.Count==2,"Select-all command selects friendly squads only.");
                Click("Hold position");yield return null;
                Check(g.Sim.Military.Squads.Where(s=>!s.Hostile).All(s=>s.Order=="hold"),"Hold position button stops both squads.");
                Click("Patrol");Check(g.Military.PendingOrder=="patrol","Patrol button waits for a world destination.");g.Military.CancelOrder();
                Click("Escort");Check(g.Military.PendingOrder=="escort","Escort button waits for a real cargo target.");g.Military.CancelOrder();
                Click("Retreat");yield return null;
                Check(g.Sim.Military.Squads.Where(s=>!s.Hostile).All(s=>s.Order=="retreat"),"Retreat issues return-to-barracks orders.");
                Click("Close drawer");Click("Settlement");yield return null;
                Check(!g.Military.ArmyMode&&!g.CameraRig.ArmyControls,"Settlement mode releases army mouse ownership.");
                Click("Mill\n65 gold");Check(g.BuildType=="mill","Construction remains available in military chapter.");g.CancelBuild();
                Click("Army");yield return null;Click("Close drawer");
                g.CameraRig.FocusOn(new Vector3(-10,0,6));g.CameraRig.Distance=32;
                yield return new WaitForSecondsRealtime(.7f);
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(ReportPath),"toll-war-gameplay.png"));
                yield return new WaitForSecondsRealtime(1);
                Check(clicks>=20&&events>=11,$"Runtime exercised {clicks} raycast-verified UI clicks and {events} queued MouseState events.");
            }
            Vector2 Point(Vector3 p)=>g.CameraRig.View.WorldToScreenPoint(p);
            Vector2 FindDestination(SquadState s)
            {
                for(int z=12;z<=22;z++)for(int x=13;x<=17;x++)
                {
                    var p=WorldArt.GridToWorld(x,z);var screen=Point(p);
                    if(Vector2.Distance(new Vector2((float)s.X,(float)s.Z),new Vector2(p.x,p.z))<4)continue;
                    if(screen.x<Screen.width*.23f||screen.x>Screen.width*.70f||screen.y<Screen.height*.28f||screen.y>Screen.height*.70f||OverUI(screen))continue;
                    if(g.Sim.MilitaryRouteAvailable(s.X,s.Z,p.x,p.z))return screen;
                }
                throw new InvalidOperationException("No visible reachable movement destination.");
            }
            bool OverUI(Vector2 p){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);return hits.Any(h=>h.module is GraphicRaycaster);}
            void Queue(Vector2 p,MouseButton? button=null)
            {
                mouse.MakeCurrent();var s=new MouseState{position=p,delta=p-previous};if(button.HasValue)s=s.WithButton(button.Value);
                InputSystem.QueueStateEvent(mouse,s);previous=p;events++;
            }
            void Click(string name)
            {
                Canvas.ForceUpdateCanvases();var b=canvas.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name==name&&x.interactable);
                if(b==null)throw new InvalidOperationException("Missing active button: "+name);
                var scroll=b.GetComponentInParent<ScrollRect>();
                if(scroll!=null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();scroll.StopMovement();
                    var center=scroll.viewport.InverseTransformPoint(b.transform.TransformPoint(((RectTransform)b.transform).rect.center));
                    var offset=scroll.content.anchoredPosition;offset.y+=scroll.viewport.rect.center.y-center.y;
                    offset.y=Mathf.Clamp(offset.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));scroll.content.anchoredPosition=offset;Canvas.ForceUpdateCanvases();
                }
                var rect=(RectTransform)b.transform;Vector2 p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var data=new PointerEventData(EventSystem.current){position=p,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b)throw new InvalidOperationException("Button obscured: "+name);
                ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerClickHandler);clicks++;
            }
            static string Digest(string path){if(!File.Exists(path))return "absent";using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(path)));}
            public void Finish()
            {
                try
                {
                    if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                    foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);
                    if(original!=null&&original.added)original.MakeCurrent();
                    foreach(string suffix in new[]{"",".tmp",".bak"})if(File.Exists(qaPath+suffix))File.Delete(qaPath+suffix);
                    Check(Digest(g.CharterSavePath)==charterDigest&&Digest(g.MilitarySavePath)==warDigest,"Both manual campaign saves remain unchanged.");
                    g.StartMilitaryChapter();g.CameraRig.EdgePanEnabled=oldEdges;g.UI.ShowMain();
                }
                catch(Exception error){Check(false,"Cleanup failed: "+error.Message);}
                var report=new StringBuilder();report.AppendLine($"{(Failures==0?"PASS":"FAIL")}: {checks} military runtime checks; {Failures} failures.");
                report.AppendLine($"UTC: {DateTime.UtcNow:O}\nUnity: {Application.unityVersion}\nEditor: {Application.isEditor}\nResolution: {Screen.width}x{Screen.height}");
                foreach(var line in lines)report.AppendLine(line);
                report.AppendLine("Scope: live rendered models, actual GraphicRaycaster/UI callbacks, queued mouse input through normal frames, normal timed recruitment, production disk saves. Model combat outcomes are verified separately by MilitaryVerification. Manual save buttons are deliberately not clicked.");
                File.WriteAllText(ReportPath,report.ToString());Debug.Log(report.ToString());
            }
        }
    }
}
