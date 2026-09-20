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

namespace LivingEmpires
{
    /// <summary>
    /// Opt-in QA for the real Input System -> camera/controller/UI path.
    /// Queues virtual Mouse events and lets normal player frames process them.
    /// Never calls InputSystem.Update or directly invokes pointer handlers.
    /// Replaces the unsaved QA world and finishes at a fresh paused charter.
    /// </summary>
    public static class NavigationVerification
    {
        public static bool Running { get; private set; }
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        public static string ReportPath=>Path.GetFullPath(Path.Combine(Application.dataPath,"..","Reports","navigation-tests.txt"));

        public static IEnumerator Run(GameController game)
        {
            if(Running)throw new InvalidOperationException("Navigation verification is already running.");
            if(game==null||game.BenchmarkRunning)throw new InvalidOperationException("A ready game outside the benchmark is required.");
            Running=true;Completed=false;Passed=false;
            var runner=new Runner(game);var tests=runner.Tests();
            try
            {
                while(true)
                {
                    bool more=false;object wait=null;
                    try{more=tests.MoveNext();if(more)wait=tests.Current;}
                    catch(Exception error){runner.Check(false,"Unexpected navigation failure: "+error);}
                    if(!more)break;
                    yield return wait;
                }
            }
            finally
            {
                runner.Finish();Passed=runner.Failures==0;Running=false;Completed=true;
            }
        }

        sealed class Runner
        {
            readonly GameController game;
            readonly StrategyCamera camera;
            readonly List<string> entries=new List<string>();
            readonly List<Mouse> disabledMice=new List<Mouse>();
            readonly Mouse originalMouse;
            readonly bool originalEdges;
            readonly string manualDigest, militaryDigest;
            Mouse mouse;Canvas canvas;Vector2 previous;
            int checks,events;
            public int Failures { get; private set; }
            public Runner(GameController instance)
            {game=instance;camera=game.CameraRig;originalMouse=Mouse.current;originalEdges=camera.EdgePanEnabled;manualDigest=Digest(game.CharterSavePath);militaryDigest=Digest(game.MilitarySavePath);}
            public void Check(bool ok,string message)
            {checks++;if(!ok)Failures++;entries.Add((ok?"PASS: ":"FAIL: ")+message);}
            void Require(bool ok,string message){Check(ok,message);if(!ok)throw new InvalidOperationException(message);}

            public IEnumerator Tests()
            {
                Require(Application.isFocused,"QA game has focus; focus-gated input is testable.");
                game.NewGame();game.UI.CloseModal();game.Paused=true;camera.EdgePanEnabled=false;
                canvas=UnityEngine.Object.FindObjectsByType<Canvas>().Single(c=>c.name=="Riverhold interface");
                foreach(var device in InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray())
                {disabledMice.Add(device);InputSystem.DisableDevice(device);}
                mouse=InputSystem.AddDevice<Mouse>("Riverhold navigation QA mouse");mouse.MakeCurrent();
                previous=new Vector2(Screen.width*.5f,Screen.height*.45f);Queue(previous);
                yield return null;yield return null;
                Require(Mouse.current==mouse&&mouse.enabled,"Virtual Mouse is current and processed by the normal input loop.");
                yield return new WaitForSecondsRealtime(.6f);

                Vector2 ground=EmptyGround();Vector3 focus=camera.Focus;float yaw=camera.Yaw,distance=camera.Distance;
                Queue(ground,MouseButton.Right);yield return null;yield return null;
                Queue(ground+new Vector2(85,45),MouseButton.Right);yield return null;yield return null;
                Check(Vector3.Distance(camera.Focus,focus)>.05f&&Near(camera.Yaw,yaw)&&Near(camera.Distance,distance),"RMB drag pans the world without orbiting or zooming.");
                Check(camera.IsDragging&&!string.IsNullOrEmpty(camera.ActiveGesture),"RMB movement enters an active captured gesture.");
                Queue(previous);yield return null;yield return null;
                focus=camera.Focus;Queue(previous+new Vector2(55,25));yield return null;yield return null;
                Check(Vector3.Distance(camera.Focus,focus)<.001f&&!camera.IsDragging,"Release ends the drag; free pointer movement cannot keep panning.");

                camera.Home();yield return new WaitForSecondsRealtime(.6f);ground=EmptyGround();
                focus=camera.Focus;yaw=camera.Yaw;float pitch=camera.Pitch;
                Queue(ground,MouseButton.Middle);yield return null;yield return null;
                Queue(ground+new Vector2(75,50),MouseButton.Middle);yield return null;yield return null;
                Check(Mathf.Abs(Mathf.DeltaAngle(yaw,camera.Yaw))>.1f&&Mathf.Abs(camera.Pitch-pitch)>.1f&&Vector3.Distance(focus,camera.Focus)<.001f,"MMB drag changes yaw and pitch without panning focus.");
                Queue(new Vector2(ground.x,Screen.height*.78f),MouseButton.Middle);yield return null;yield return null;
                Check(camera.Pitch>=32&&camera.Pitch<=72,"Orbit clamps pitch at the first extreme.");
                Queue(new Vector2(ground.x,Screen.height*.20f),MouseButton.Middle);yield return null;yield return null;
                Check(camera.Pitch>=32&&camera.Pitch<=72,"Orbit clamps pitch at the opposite extreme.");
                Queue(previous);yield return null;yield return null;

                camera.Home();yield return new WaitForSecondsRealtime(.6f);ground=EmptyGround();distance=camera.Distance;
                Queue(ground,null,120);yield return null;yield return null;
                Check(camera.Distance<distance,"Wheel-up input zooms in over the world.");
                distance=camera.Distance;Queue(ground,null,-120);yield return null;yield return null;
                Check(camera.Distance>distance,"Wheel-down input zooms out over the world.");
                Queue(ground,null,100000);yield return null;yield return null;
                Check(camera.Distance>0&&!float.IsNaN(camera.Distance)&&!float.IsInfinity(camera.Distance),"Extreme inward wheel input leaves a finite positive camera distance.");
                Queue(ground,null,-100000);yield return null;yield return null;
                Check(camera.Distance>0&&camera.Distance<200&&!float.IsNaN(camera.Distance),"Extreme outward wheel input respects a bounded camera distance.");

                // The reset itself is a real InputSystemUIInputModule button click.
                Vector2 home=ButtonPoint("Home view");Queue(home);yield return null;yield return null;
                Queue(home,MouseButton.Left);yield return null;yield return null;
                Queue(home);yield return null;yield return null;
                Check(Vector3.Distance(camera.Focus,StrategyCamera.HomeFocus)<.001f
                    &&Near(Mathf.DeltaAngle(camera.Yaw,StrategyCamera.HomeYaw),0)
                    &&Near(camera.Pitch,StrategyCamera.HomePitch)&&Near(camera.Distance,StrategyCamera.HomeDistance),
                    "Queued mouse click on Home view resets focus, yaw, pitch and zoom to the current authored home view.");
                yield return new WaitForSecondsRealtime(.6f);
                Vector2 map=PanelPoint("Map surface");Require(OverUI(map),"Clickable valley map is reached by the actual UI raycaster.");
                int beforeMapBuildings=game.Sim.State.Buildings.Count;int beforeMapSelection=game.Selected;
                Queue(map);yield return null;yield return null;
                Queue(map,MouseButton.Left);yield return null;yield return null;
                Queue(map);yield return null;yield return null;
                Check(Vector3.Distance(camera.Focus,Vector3.zero)<.01f&&!camera.IsDragging
                    &&game.Sim.State.Buildings.Count==beforeMapBuildings&&game.Selected==beforeMapSelection,
                    "Queued click at the valley-map center focuses the center of the basin without selecting or building in the world.");
                camera.Home();
                yield return new WaitForSecondsRealtime(.6f);

                ground=EmptyGround();focus=camera.Focus;int buildingCount=game.Sim.State.Buildings.Count;
                Queue(ground,MouseButton.Left);yield return null;yield return null;
                Queue(ground+new Vector2(75,30),MouseButton.Left);yield return null;yield return null;
                Queue(previous);yield return null;yield return null;
                Check(Vector3.Distance(focus,camera.Focus)>.05f&&game.Sim.State.Buildings.Count==buildingCount&&game.Selected==-1,"LMB drag on empty ground pans without construction or a selection click.");

                camera.Home();yield return new WaitForSecondsRealtime(.6f);
                int buildingId;Vector2 buildingPoint=BuildingPoint(out buildingId);focus=camera.Focus;
                Queue(buildingPoint,MouseButton.Left);yield return null;yield return null;
                Check(game.Selected==-1,"Building selection is deferred until mouse release.");
                Queue(buildingPoint);yield return null;yield return null;
                Check(game.Selected==buildingId&&Vector3.Distance(focus,camera.Focus)<.001f,"A short building click selects the actual raycast target without panning.");
                Queue(buildingPoint,MouseButton.Left);yield return null;yield return null;
                Queue(buildingPoint+new Vector2(45,20),MouseButton.Left);yield return null;yield return null;
                Queue(previous);yield return null;yield return null;
                Check(game.Selected==buildingId&&Vector3.Distance(focus,camera.Focus)<.001f,"A drag begun on a building neither pans nor commits a new selection.");

                camera.Home();yield return new WaitForSecondsRealtime(.6f);game.BeginBuild("mill");
                int tileX,tileZ;Vector2 tile=BuildPoint("mill",out tileX,out tileZ);
                buildingCount=game.Sim.State.Buildings.Count;focus=camera.Focus;double gold=game.Sim.State.Towns[0].Gold;
                Queue(tile,MouseButton.Left);yield return null;yield return null;
                Check(game.Sim.State.Buildings.Count==buildingCount,"Placement press alone does not spend resources or construct.");
                Queue(tile+new Vector2(45,20),MouseButton.Left);yield return null;yield return null;
                Queue(previous);yield return null;yield return null;
                Check(game.Sim.State.Buildings.Count==buildingCount&&NearGold(game.Sim.State.Towns[0].Gold,gold)&&Vector3.Distance(focus,camera.Focus)<.001f,"Placement drag over six pixels suppresses construction and left-pan.");
                Queue(tile,MouseButton.Left);yield return null;yield return null;
                Queue(tile);yield return null;yield return null;
                var placed=game.Sim.State.Buildings.Find(b=>b.Town==0&&b.Type=="mill"&&b.X==tileX&&b.Z==tileZ);
                Check(placed!=null&&game.Sim.State.Buildings.Count==buildingCount+1&&NearGold(game.Sim.State.Towns[0].Gold,gold-Simulation.Definitions["mill"].GoldCost)
                    &&game.Selected==placed.Id&&game.BuildType=="","Fresh placement click builds exactly once, spends its cost and selects the new mill.");

                game.BeginBuild("house");ground=EmptyGround();focus=camera.Focus;
                Queue(ground,MouseButton.Right);yield return null;yield return null;
                Queue(ground+new Vector2(70,25),MouseButton.Right);yield return null;yield return null;
                Check(game.BuildType==""&&Vector3.Distance(focus,camera.Focus)<.001f&&!camera.IsDragging,"RMB while placing cancels construction without capturing a pan.");
                Queue(previous);yield return null;yield return null;

                Vector2 ui=PanelPoint("Resource ledger");game.BeginBuild("house");focus=camera.Focus;
                Queue(ui,MouseButton.Right);yield return null;yield return null;
                Queue(EmptyGround(),MouseButton.Right);yield return null;yield return null;
                Check(game.BuildType=="house"&&Vector3.Distance(focus,camera.Focus)<.001f&&!camera.IsDragging,"RMB begun on UI cannot cancel placement or start panning after entering the world.");
                Queue(previous);yield return null;yield return null;game.CancelBuild();
                Vector2 orders=ButtonPoint("Orders");Queue(orders);yield return null;yield return null;
                Queue(orders,MouseButton.Left);yield return null;yield return null;
                Queue(orders);yield return null;yield return null;
                Check(canvas.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Trade and council"),
                    "Actual queued Orders-tab click opens the on-demand management drawer before pointer-exclusion tests.");
                ui=PanelPoint("Trade and council");distance=camera.Distance;focus=camera.Focus;yaw=camera.Yaw;pitch=camera.Pitch;
                Queue(ui,null,120);yield return null;yield return null;
                Check(Near(camera.Distance,distance),"Wheel over the sidebar is consumed by UI rather than camera zoom.");
                Queue(ui,MouseButton.Middle);yield return null;yield return null;
                Queue(EmptyGround(),MouseButton.Middle);yield return null;yield return null;
                Check(Near(camera.Yaw,yaw)&&Near(camera.Pitch,pitch)&&Vector3.Distance(focus,camera.Focus)<.001f,"MMB begun over UI cannot orbit after crossing into the world.");
                Queue(previous);yield return null;yield return null;

                ground=EmptyGround();Queue(ground,MouseButton.Right);yield return null;yield return null;
                Queue(ground+new Vector2(45,20),MouseButton.Right);yield return null;yield return null;
                game.UI.ShowStory("opening");focus=camera.Focus;yaw=camera.Yaw;distance=camera.Distance;
                Queue(previous+new Vector2(60,15),MouseButton.Right,120);yield return null;yield return null;
                Check(Vector3.Distance(focus,camera.Focus)<.001f&&Near(yaw,camera.Yaw)&&Near(distance,camera.Distance)&&!camera.IsDragging,"Opening a story cancels captured navigation and blocks held drag/zoom.");
                game.UI.CloseModal();Queue(previous+new Vector2(25,10),MouseButton.Right);yield return null;yield return null;
                Check(Vector3.Distance(focus,camera.Focus)<.001f&&!camera.IsDragging,"A held button cannot resume a cancelled gesture after the story closes.");
                Queue(previous);yield return null;yield return null;
                ground=EmptyGround();Queue(ground,MouseButton.Right);yield return null;yield return null;
                Queue(ground+new Vector2(60,20),MouseButton.Right);yield return null;yield return null;
                Check(Vector3.Distance(focus,camera.Focus)>.05f,"A fresh world press starts panning normally after modal closure.");
                Queue(previous);yield return null;yield return null;
                game.UI.ShowMain();focus=camera.Focus;distance=camera.Distance;
                Queue(new Vector2(Screen.width*.5f,Screen.height*.45f),null,120);yield return null;yield return null;
                Check(Vector3.Distance(focus,camera.Focus)<.001f&&Near(distance,camera.Distance),"Main menu blocks world navigation.");
                game.InMenu=false;game.UI.CloseModal();camera.Home();yield return new WaitForSecondsRealtime(.6f);

                Vector2 edge=new Vector2(Screen.width-3,Screen.height*.5f);
                Require(!OverUI(edge),"The edge-pan test point is outside sidebar graphics.");
                camera.EdgePanEnabled=false;focus=camera.Focus;Queue(edge);
                float edgeUntil=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<edgeUntil)yield return null;
                Check(Vector3.Distance(focus,camera.Focus)<.001f,"Disabled edge panning leaves focus stationary.");
                camera.EdgePanEnabled=true;Queue(edge);
                edgeUntil=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<edgeUntil)yield return null;
                Check(Vector3.Distance(focus,camera.Focus)>.01f,"Enabled edge panning responds at the visible world edge.");
                camera.EdgePanEnabled=false;Queue(new Vector2(Screen.width*.5f,Screen.height*.45f));yield return null;yield return null;
                Check(game.Paused&&game.Sim.State.Day==1,"Mouse navigation and UI inspection never advance the paused simulation.");
                Check(events>50,events+" queued MouseState events exercised the actual Input System path.");
            }

            void Queue(Vector2 position,MouseButton? button=null,float scroll=0)
            {
                if(mouse==null)throw new InvalidOperationException("Virtual Mouse is unavailable.");
                mouse.MakeCurrent();var state=new MouseState{position=position,delta=position-previous,scroll=new Vector2(0,scroll)};
                if(button.HasValue)state=state.WithButton(button.Value);
                InputSystem.QueueStateEvent(mouse,state);previous=position;events++;
            }
            static bool Near(float a,float b)=>Mathf.Abs(a-b)<.001f;
            static bool NearGold(double a,double b)=>Math.Abs(a-b)<1e-8;
            static string Digest(string path)
            {if(!File.Exists(path))return "absent";using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));}
            bool OverUI(Vector2 position)
            {
                Canvas.ForceUpdateCanvases();var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
                return hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster);
            }
            bool Usable(Vector2 point)=>point.x>Screen.width*.21f&&point.x<Screen.width*.71f&&point.y>Screen.height*.24f&&point.y<Screen.height*.74f&&!OverUI(point);
            Vector2 EmptyGround()
            {
                Physics.SyncTransforms();
                for(int iy=0;iy<6;iy++)for(int ix=0;ix<7;ix++)
                {
                    var p=new Vector2(Screen.width*(.31f+ix*.05f),Screen.height*(.34f+iy*.055f));
                    if(!Usable(p))continue;RaycastHit hit;
                    if(Physics.Raycast(camera.View.ScreenPointToRay(p),out hit,250)&&hit.collider is MeshCollider&&hit.collider.GetComponentInParent<BuildingView>()==null)return p;
                }
                throw new InvalidOperationException("No unoccluded empty ground test point is visible.");
            }
            Vector2 BuildingPoint(out int id)
            {
                Physics.SyncTransforms();
                foreach(var view in UnityEngine.Object.FindObjectsByType<BuildingView>())
                {
                    var state=game.Sim.State.Buildings.Find(b=>b.Id==view.Id);if(state==null||state.Town!=0)continue;
                    var collider=view.GetComponent<Collider>();if(collider==null)continue;
                    foreach(float offset in new[]{.35f,0f,-.2f})
                    {
                        Vector3 screen=camera.View.WorldToScreenPoint(collider.bounds.center+Vector3.up*collider.bounds.extents.y*offset);if(screen.z<=0||!Usable(screen))continue;
                        RaycastHit hit;if(Physics.Raycast(camera.View.ScreenPointToRay(screen),out hit,250)&&hit.collider.GetComponentInParent<BuildingView>()==view){id=view.Id;return screen;}
                    }
                }
                throw new InvalidOperationException("No selectable player building has an unoccluded test point.");
            }
            Vector2 BuildPoint(string kind,out int x,out int z)
            {
                for(int candidateZ=5;candidateZ<=23;candidateZ++)for(int candidateX=4;candidateX<=17;candidateX++)
                {
                    if(game.Sim.PlacementProblem(kind,candidateX,candidateZ)!="")continue;
                    Vector3 screen=camera.View.WorldToScreenPoint(WorldArt.GridToWorld(candidateX,candidateZ));
                    if(screen.z>0&&Usable(screen)){x=candidateX;z=candidateZ;return screen;}
                }
                throw new InvalidOperationException("No valid construction tile is visible outside UI.");
            }
            Vector2 PanelPoint(string name)
            {
                var rect=canvas.GetComponentsInChildren<RectTransform>().First(t=>t.name==name);
                return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            }
            Vector2 ButtonPoint(string name)
            {
                var button=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name==name&&b.interactable);
                var rect=(RectTransform)button.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,name+" button center is reachable by the real GraphicRaycaster.");
                return point;
            }
            public void Finish()
            {
                try
                {
                    if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                    foreach(var device in disabledMice)if(device.added)InputSystem.EnableDevice(device);
                    if(originalMouse!=null&&originalMouse.added)originalMouse.MakeCurrent();
                    Check(disabledMice.All(m=>!m.added||m.enabled)&&(mouse==null||!mouse.added),"QA Mouse removed and original mouse devices reenabled.");
                }
                catch(Exception error){Check(false,"Mouse cleanup: "+error.Message);}
                try
                {
                    game.NewGame();game.UI.CloseModal();game.Paused=true;camera.EdgePanEnabled=originalEdges;camera.Home();
                    Check(game.Sim.State.Day==1&&game.BuildType==""&&!game.Modal&&!camera.IsDragging,"Cleanup leaves a fresh paused charter with no captured gesture.");
                    Check(Digest(game.CharterSavePath)==manualDigest&&Digest(game.MilitarySavePath)==militaryDigest,"First Winter and Toll War manual saves both remain byte-for-byte unchanged or absent.");
                }
                catch(Exception error){Check(false,"World cleanup: "+error.Message);}
                var report=new StringBuilder();report.AppendLine((Failures==0?"PASS":"FAIL")+": "+checks+" navigation checks; "+Failures+" failures.");
                report.AppendLine("UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\nResolution actually tested: "+Screen.width+"x"+Screen.height+"\nMouseState events: "+events);
                foreach(string entry in entries)report.AppendLine(entry);
                report.AppendLine("Scope: queued virtual Mouse input processed in ordinary Update/LateUpdate frames; UI clicks go through InputSystemUIInputModule. No direct pointer-handler invocation, forced InputSystem.Update, OS pointer warp, or manual-slot save/load. Other Mouse devices are temporarily isolated only within this QA process. Camera Home and direct edge flag changes are fixture setup except the explicitly tested Home UI click. This does not test OS focus loss/reentry or other screen sizes.");
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));File.WriteAllText(ReportPath,report.ToString());Debug.Log(report.ToString());
            }
        }
    }
}
