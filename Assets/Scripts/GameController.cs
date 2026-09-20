using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LivingEmpires
{
    public sealed class BuildingView : MonoBehaviour { public int Id; }
    public sealed class GameController : MonoBehaviour
    {
        public static GameController Instance;
        public Simulation Sim { get; private set; }
        public StrategyCamera CameraRig;
        public GameUI UI;
        public StoryMedia Media;
        public MilitaryController Military;
        public bool Paused=true, InMenu=true;
        public int Speed=1, Selected=-1;
        public string BuildType="", LastMessage="Welcome to Riverhold. Your first winter begins with a promise.";
        public float DayFraction;
        public float FPS;
        public string CharterSavePath => Path.Combine(Application.persistentDataPath,"first-winter-unity.json");
        public string MilitarySavePath => Path.Combine(Application.persistentDataPath,"toll-war-unity.json");
        public string SavePath => Sim != null && Sim.Military != null && Sim.Military.Enabled ? MilitarySavePath : CharterSavePath;
        public bool HasSave => File.Exists(SavePath);
        public bool HasMilitarySave => File.Exists(MilitarySavePath);
        public bool HasFirstWinterSave => File.Exists(CharterSavePath);
        public bool Modal => UI!=null&&UI.ModalOpen;
        public bool IsAdvancing => !Paused && !InMenu && !Modal;
        public bool BenchmarkRunning { get; private set; }
        public bool BenchmarkComplete { get; private set; }
        public bool BenchmarkPassed { get; private set; }
        public string BenchmarkError { get; private set; } = "";
        bool militaryBenchmark;
        public string BenchmarkReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,"..","Reports",militaryBenchmark?"military-performance.txt":"performance.txt"));
        public int RenderedBuildingCount => models.Count;
        public int RenderedWorkerCount => residents.Count;
        public int RenderedCaravanCount => carts.Count;
        public int TotalAssignedWorkers { get; private set; }
        public int FunctionalWorkplaceCount { get; private set; }
        const int MaxVisibleWorkers=128;
        sealed class CargoVisual { public GameObject Model; public Vector3[] Path; }
        sealed class CitizenVisual
        {
            public GameObject Model;
            public BuildingState Workplace;
            public Vector3 Rest, Work;
            public float Phase;
            public bool Productive;
        }
        Transform scenery, buildings, traffic, people;
        readonly Dictionary<int,GameObject> models=new Dictionary<int,GameObject>();
        readonly Dictionary<CaravanState,CargoVisual> carts=new Dictionary<CaravanState,CargoVisual>();
        readonly Dictionary<long,CitizenVisual> residents=new Dictionary<long,CitizenVisual>();
        readonly List<CaravanState> staleCarts=new List<CaravanState>();
        readonly List<long> staleResidents=new List<long>();
        readonly HashSet<long> activeResidents=new HashSet<long>();
        GameObject ferry, bridge, construction, preview, marker;
        Renderer previewRenderer;
        Light sunlight;
        string crossingKey="";
        bool? winterShown;
        double workerClock;
        float refreshTimer, fpsTimer; int fpsFrames;
        int graphicsProfile=1, renderWidth, renderHeight;
        Material validMat, invalidMat;
        void Awake()
        {
            Instance=this; Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
            Media=gameObject.AddComponent<StoryMedia>();
            Sim=new Simulation();
            var cameraGO=new GameObject("Strategy Camera",typeof(Camera),typeof(AudioListener));
            var cam=cameraGO.GetComponent<Camera>();cam.tag="MainCamera";cam.fieldOfView=42;cam.nearClipPlane=.15f;cam.farClipPlane=240;cam.backgroundColor=new Color(.56f,.7f,.75f);cam.clearFlags=CameraClearFlags.SolidColor;
            CameraRig=cameraGO.AddComponent<StrategyCamera>();
            // This strategy camera uses authored lighting and fog, without photographic lens effects.
            cameraGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
            var sunGO=new GameObject("Late afternoon sun"); sunlight=sunGO.AddComponent<Light>();sunlight.type=LightType.Directional;sunlight.color=new Color(1,.94f,.82f);sunlight.intensity=1.35f;sunlight.shadows=LightShadows.Soft;sunGO.transform.rotation=Quaternion.Euler(47,-35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.58f,.67f,.73f);RenderSettings.ambientEquatorColor=new Color(.43f,.50f,.46f);RenderSettings.ambientGroundColor=new Color(.25f,.30f,.25f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.57f,.68f,.69f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=150;
            scenery=WorldArt.BuildLandscape(transform);
            buildings=new GameObject("Settlements").transform;buildings.SetParent(transform);
            traffic=new GameObject("Traveling cargo").transform;traffic.SetParent(transform);
            people=new GameObject("Assigned workers").transform;people.SetParent(transform);
            validMat=MakeMat(new Color(.4f,.95f,.65f)); invalidMat=MakeMat(new Color(.95f,.27f,.2f));
            marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);marker.name="Selection ring";marker.transform.localScale=new Vector3(2.35f,.025f,2.35f);Destroy(marker.GetComponent<Collider>());marker.GetComponent<Renderer>().sharedMaterial=validMat;marker.SetActive(false);
            preview=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.name="Placement footprint";preview.transform.localScale=new Vector3(2.05f,.055f,2.05f);Destroy(preview.GetComponent<Collider>());previewRenderer=preview.GetComponent<Renderer>();preview.SetActive(false);
            Military=gameObject.AddComponent<MilitaryController>();Military.Initialize(this);
            UI=gameObject.AddComponent<GameUI>();UI.Initialize(this);
            var args=Environment.GetCommandLineArgs();
            ApplyQuality(args.Contains("--economy")?0:args.Contains("--balanced")?1:PlayerPrefs.GetInt("Quality",1),false); SyncWorld(); UI.ShowMain();
            if(args.Contains("--verify-military"))StartCoroutine(VerifyMilitary());
            else if(args.Contains("--verify-assets"))StartCoroutine(VerifyImportedAssets());
            else if(args.Contains("--verify-navigation"))StartCoroutine(VerifyNavigation());
            else if(args.Contains("--verify-integration"))StartCoroutine(VerifyIntegration());
            else if(args.Contains("--benchmark-war"))BeginBenchmark(true);
            else if(args.Contains("--benchmark"))BeginBenchmark();
        }
        IEnumerator VerifyMilitary()
        {
            yield return null;yield return new WaitForSecondsRealtime(.7f);
            yield return MilitaryRuntimeVerification.Run(this);
            if(!Application.isEditor)Application.Quit(MilitaryRuntimeVerification.Passed?0:2);
        }
        IEnumerator VerifyImportedAssets()
        {
            yield return null;yield return null;
            bool passed=true;
            try { Debug.Log(ImportedAssetVerification.Run(this)); }
            catch(Exception error) { passed=false;Debug.LogException(error); }
            yield return null;
            if(!Application.isEditor)Application.Quit(passed?0:2);
        }
        IEnumerator VerifyIntegration()
        {
            // Let Awake, the first layout pass and input modules settle before QA.
            yield return null;
            var verification=UnityIntegrationVerification.Run(this);bool failed=false;
            while(true)
            {
                bool more=false;object wait=null;
                try{more=verification.MoveNext();if(more)wait=verification.Current;}
                catch(Exception error){failed=true;Debug.LogException(error);}
                if(!more)break;
                yield return wait;
            }
            bool passed=!failed&&UnityIntegrationVerification.Completed&&UnityIntegrationVerification.Passed;
            Debug.Log(passed?"Standalone integration verification passed.":"Standalone integration verification failed.");
            if(!Application.isEditor)Application.Quit(passed?0:2);
        }
        IEnumerator VerifyNavigation()
        {
            yield return null;
            // Give the standalone window time to receive its initial focus.
            yield return new WaitForSecondsRealtime(.5f);
            yield return NavigationVerification.Run(this);
            bool passed=NavigationVerification.Completed&&NavigationVerification.Passed;
            if(passed)
            {
                yield return new WaitForSecondsRealtime(.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(NavigationVerification.ReportPath),"mouse-navigation.png"));
                yield return new WaitForSecondsRealtime(2f);
            }
            Debug.Log(passed?"Mouse navigation verification passed.":"Mouse navigation verification failed.");
            if(!Application.isEditor)Application.Quit(passed?0:2);
        }
        static Material MakeMat(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.color=color;return m;}
        void Update()
        {
            if(Screen.width!=renderWidth||Screen.height!=renderHeight)FitRenderResolution();
            if(Keyboard.current!=null&&Keyboard.current.f12Key.wasPressedThisFrame)CaptureGameplay();
            fpsTimer+=Time.unscaledDeltaTime;fpsFrames++;if(fpsTimer>=1){FPS=fpsFrames/fpsTimer;fpsTimer=0;fpsFrames=0;}
            CameraRig.ControlsEnabled=!InMenu&&!Modal&&!BenchmarkRunning;
            if(!InMenu&&!Modal)
            {
                var key=Keyboard.current; if(key!=null&&!BenchmarkRunning)
                {
                    if(key.spaceKey.wasPressedThisFrame)TogglePause();
                    if(key.tabKey.wasPressedThisFrame)Military.SetArmyMode(!Military.ArmyMode);
                    if(key.escapeKey.wasPressedThisFrame){if(BuildType!="")CancelBuild();else if(!Military.CancelOrder())UI.ShowPause();}
                    if(key.f5Key.wasPressedThisFrame)Save();
                    if(key.f9Key.wasPressedThisFrame)Load();
                }
                if(!BenchmarkRunning){if(Military.ArmyMode)Military.HandleInput();else PlacementAndSelection();}
                if(!Paused)
                {
                    Sim.SetMilitaryDayFraction(DayFraction);
                    float simulationDelta=Math.Min(Time.deltaTime,.25f)*Speed;
                    Sim.AdvanceMilitary(simulationDelta);
                    DayFraction+=simulationDelta/10f;
                    while(DayFraction>=1){DayFraction-=1;Sim.AdvanceDay();SyncWorld();UI.Refresh();}
                    Sim.SetMilitaryDayFraction(DayFraction);
                }
            }
            else preview.SetActive(false);
            WorldArt.SetAnimationSpeed(IsAdvancing?Speed:0);
            if(IsAdvancing)workerClock+=Time.deltaTime*Speed;
            AnimateCargo(); AnimateCitizens();
            refreshTimer+=Time.unscaledDeltaTime;if(refreshTimer>.45f){refreshTimer=0;UI.RefreshCounters();}
            if(!BenchmarkRunning&&!InMenu&&!Modal&&Sim.State.PendingStory.Count>0)
            {
                string id=Sim.State.PendingStory[0];Sim.AcknowledgeStory(id);
                if(id=="success"||id=="recovered")UI.ShowStory(id);
                else {LastMessage=Media.Get(id)?.title??id; UI.RefreshCounters();}
            }
        }
        public void NewGame()
        {
            if(BenchmarkRunning)return;
            Sim=new Simulation();Selected=-1;BuildType="";Paused=true;InMenu=false;Speed=1;DayFraction=0;workerClock=0;
            ClearWorldActors();
            CameraRig.Home();SyncWorld();UI.CloseModal();UI.Refresh();UI.ShowStory("opening");Sim.AcknowledgeStory("opening");
            LastMessage="Begin with a mill and bakery. Click Resume to let the town work; follow the guide.";
        }
        public void StartMilitaryChapter()
        {
            if(BenchmarkRunning)return;
            var chapter=new Simulation();string problem=chapter.StartMilitaryChapter();
            if(problem!=""){Act(problem);return;}
            Sim=chapter;Selected=-1;BuildType="";Paused=true;InMenu=false;Speed=1;DayFraction=0;workerClock=0;
            ClearWorldActors();Media.Stop();CameraRig.Home();CameraRig.Focus=new Vector3(-17,0,4);CameraRig.Distance=42;
            SyncWorld();UI.CloseModal();Military.SetArmyMode(true);
            LastMessage="THE TOLL WAR · An established Riverhold. Recruit your first squads, then Resume. Raiders approach in three minutes of game time.";
            UI.Refresh();
        }
        public void TogglePause(){if(BenchmarkRunning)return;Paused=!Paused;UI.RefreshCounters();}
        public void SetSpeed(int speed){if(BenchmarkRunning)return;Speed=Mathf.Clamp(speed,1,8);Paused=false;UI.RefreshCounters();}
        public void BeginBuild(string kind){if(Military.ArmyMode)Military.SetArmyMode(false);BuildType=kind;Selected=-1;marker.SetActive(false);LastMessage="Choose a free Riverhold tile. Right-click cancels.";UI.Refresh();}
        public void CancelBuild(){BuildType="";preview.SetActive(false);UI.Refresh();}
        public bool Act(string result,string success="Done.",string sound="click")
        {
            LastMessage=string.IsNullOrEmpty(result)?success:result;Media.Sound(string.IsNullOrEmpty(result)?sound:"error");SyncWorld();UI.Refresh();return string.IsNullOrEmpty(result);
        }
        void PlacementAndSelection()
        {
            if(Mouse.current==null)return;
            if(CameraRig.RightClickPressed&&BuildType!=""){CancelBuild();return;}
            Vector3 hit;
            if(CameraRig.PointerOverUI()||!CameraRig.Ground(out hit)){preview.SetActive(false);return;}
            Vector2Int tile=WorldArt.WorldToGrid(hit);int x=tile.x,z=tile.y;
            if(BuildType!="")
            {
                preview.SetActive(true);preview.transform.position=WorldArt.GridToWorld(x,z)+Vector3.up*.07f;
                string problem=Sim.PlacementProblem(BuildType,x,z);previewRenderer.sharedMaterial=problem==""?validMat:invalidMat;
                UI.PreviewMessage(problem==""?"Click to build "+Simulation.Definitions[BuildType].Name:problem);
                if(CameraRig.LeftClickReleased&&Act(Sim.PlaceBuilding(BuildType,x,z),"Construction complete. Review workers in the inspector.","build"))
                {
                    var placed=Sim.State.Buildings.Find(b=>b.Town==0&&b.X==x&&b.Z==z);
                    Selected=placed!=null?placed.Id:-1;
                    marker.SetActive(placed!=null);if(placed!=null)marker.transform.position=WorldArt.GridToWorld(x,z)+Vector3.up*.08f;
                    CancelBuild();
                }
            }
            else if(CameraRig.LeftClickReleased)
            {
                Ray r=CameraRig.View.ScreenPointToRay(Mouse.current.position.ReadValue()); RaycastHit info;
                var b=Physics.Raycast(r,out info,220)?info.collider.GetComponentInParent<BuildingView>():null;
                Selected=b!=null?b.Id:-1;
                var state=Sim.State.Buildings.Find(v=>v.Id==Selected);
                marker.SetActive(state!=null);if(state!=null)marker.transform.position=WorldArt.GridToWorld(state.X,state.Z)+Vector3.up*.08f;
                UI.RefreshInspector();
            }
        }
        public void CaptureGameplay()
        {
            try
            {
                string directory=Path.Combine(Application.persistentDataPath,"Screenshots");Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"Riverhold-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");
                ScreenCapture.CaptureScreenshot(path);LastMessage="Screenshot: "+path;
            }
            catch(Exception error){LastMessage="Screenshot could not be saved: "+error.Message;}
        }
        public void SyncWorld()
        {
            foreach(var b in Sim.State.Buildings)
            {
                if(!models.ContainsKey(b.Id))
                {
                    Color tint=b.Town==1?new Color(.65f,.3f,.22f):b.Town==2?new Color(.37f,.53f,.32f):new Color(.19f,.46f,.49f);
                    var model=WorldArt.CreateBuilding(b.Type,WorldArt.GridToWorld(b.X,b.Z),buildings,tint);model.AddComponent<BuildingView>().Id=b.Id;models[b.Id]=model;
                }
            }
            bool keepFerry=Sim.State.Crossing.Mode=="ferry";
            foreach(var cargo in Sim.State.Caravans)if(cargo.Route=="ferry")keepFerry=true;
            string key=Sim.State.Crossing.Mode+"/"+Sim.State.Crossing.Pending+"/"+keepFerry;
            if(key!=crossingKey)
            {
                crossingKey=key;
                if(ferry!=null)Destroy(ferry);if(bridge!=null)Destroy(bridge);if(construction!=null)Destroy(construction);
                ferry=keepFerry?WorldArt.CreateCrossing(false,WorldArt.FerryPosition,transform):null;
                bridge=Sim.State.Crossing.Mode=="bridge"?WorldArt.CreateCrossing(true,WorldArt.BridgePosition,transform):null;
                string pending=Sim.State.Crossing.Pending;
                construction=pending!=""?WorldArt.CreateConstruction(pending=="bridge",pending=="bridge"?WorldArt.BridgePosition:WorldArt.FerryPosition,transform):null;
            }
            if(construction!=null)construction.name=$"{Sim.State.Crossing.Pending} construction · {Sim.State.Crossing.DaysLeft} days remaining";
            staleCarts.Clear();foreach(var pair in carts)if(!Sim.State.Caravans.Contains(pair.Key))staleCarts.Add(pair.Key);
            foreach(var stale in staleCarts){Destroy(carts[stale].Model);carts.Remove(stale);}
            foreach(var c in Sim.State.Caravans)if(!carts.ContainsKey(c))
            {
                var path=Route(c.From,c.To,c.Route);
                var cart=WorldArt.CreateCart(path[0],traffic,c.PlayerOrder?new Color(.23f,.63f,.63f):new Color(.75f,.55f,.28f));
                cart.AddComponent<CargoView>().Id=c.Id;
                cart.name=$"{c.Quantity:0} {c.Good}: {Sim.State.Towns[c.From].Name} to {Sim.State.Towns[c.To].Name}";
                carts[c]=new CargoVisual{Model=cart,Path=path};
            }
            bool winter=Sim.State.Day>=Simulation.WinterDay;
            if(winterShown!=winter)
            {
                winterShown=winter;WorldArt.SetWinter(winter);
                sunlight.color=winter?new Color(.78f,.85f,1f):new Color(1,.94f,.82f);
                sunlight.intensity=winter?1.15f:1.35f;
                RenderSettings.fogColor=winter?new Color(.72f,.78f,.82f):new Color(.57f,.68f,.69f);
                CameraRig.View.backgroundColor=winter?new Color(.66f,.73f,.8f):new Color(.56f,.7f,.75f);
            }
            SyncWorkers();AnimateCargo();AnimateCitizens();
            var selected=Sim.State.Buildings.Find(b=>b.Id==Selected);
            marker.SetActive(selected!=null);if(selected!=null)marker.transform.position=WorldArt.GridToWorld(selected.X,selected.Z)+Vector3.up*.08f;
        }
        void AnimateCargo()
        {
            foreach(var pair in carts)
            {
                var c=pair.Key;float progress=Mathf.Clamp01((c.TotalDays-c.DaysLeft+DayFraction)/c.TotalDays);
                Vector3 pos=Along(pair.Value.Path,progress);Vector3 next=Along(pair.Value.Path,Mathf.Min(1,progress+.004f));
                pair.Value.Model.transform.position=pos;if((next-pos).sqrMagnitude>.000001f)pair.Value.Model.transform.rotation=Quaternion.LookRotation(next-pos);
            }
        }
        public static Vector3[] Route(int from,int to,string route)
        {
            return WorldArt.GetCargoWaypoints(from,to,route);
        }
        static Vector3 Along(Vector3[] points,float t)
        {
            float length=0;for(int i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);float distance=t*length;
            for(int i=1;i<points.Length;i++){float segment=Vector3.Distance(points[i-1],points[i]);if(segment<.0001f)continue;if(distance<=segment)return Vector3.Lerp(points[i-1],points[i],distance/segment);distance-=segment;}
            return points[points.Length-1];
        }
        void SyncWorkers()
        {
            activeResidents.Clear();TotalAssignedWorkers=0;FunctionalWorkplaceCount=0;
            foreach(var building in Sim.State.Buildings)
            {
                TotalAssignedWorkers+=building.Workers;
                bool productive=Sim.ProductionStatus(building.Id).StartsWith("Producing",StringComparison.Ordinal);
                if(productive)FunctionalWorkplaceCount++;
                for(int slot=0;slot<building.Workers&&activeResidents.Count<MaxVisibleWorkers;slot++)
                {
                    long key=((long)building.Id<<16)|((long)slot&0xffff);activeResidents.Add(key);
                    CitizenVisual citizen;
                    if(!residents.TryGetValue(key,out citizen))
                    {
                        float angle=(slot+.5f)*Mathf.PI*2f/Mathf.Max(1,building.Workers);
                        Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        Vector3 center=WorldArt.GridToWorld(building.X,building.Z)+Vector3.up*.04f;
                        var model=WorldArt.CreatePerson(center+direction*1.38f,people,
                            building.Town==1?new Color(.65f,.3f,.22f):building.Town==2?new Color(.37f,.53f,.32f):new Color(.19f,.46f,.49f),building.Id+slot);
                        model.name=$"{Sim.State.Towns[building.Town].Name} {building.Type} worker {slot+1}";
                        citizen=new CitizenVisual{Model=model,Workplace=building,Rest=center+direction*1.9f,Work=center+direction*1.38f,Phase=(building.Id*.71f+slot*2.3f)%14};
                        residents.Add(key,citizen);
                    }
                    citizen.Workplace=building;citizen.Productive=productive||Sim.ProductionStatus(building.Id)=="Ready";
                }
            }
            staleResidents.Clear();foreach(var pair in residents)if(!activeResidents.Contains(pair.Key))staleResidents.Add(pair.Key);
            foreach(long key in staleResidents){Destroy(residents[key].Model);residents.Remove(key);}
        }
        void AnimateCitizens()
        {
            foreach(var pair in residents)
            {
                var citizen=pair.Value;float phase=(float)((workerClock+citizen.Phase)%14.0);
                Vector3 position;bool walking=false,working=false;
                if(!citizen.Productive)position=citizen.Rest;
                else if(phase<3){position=Vector3.Lerp(citizen.Rest,citizen.Work,phase/3f);walking=true;}
                else if(phase<9){position=citizen.Work;working=true;}
                else if(phase<12){position=Vector3.Lerp(citizen.Work,citizen.Rest,(phase-9)/3f);walking=true;}
                else position=citizen.Rest;
                citizen.Model.transform.position=position;
                Vector3 direction=phase>=9&&phase<12?citizen.Rest-citizen.Work:citizen.Work-citizen.Rest;
                if(direction.sqrMagnitude>.0001f)citizen.Model.transform.rotation=Quaternion.LookRotation(direction);
                WorldArt.SetPersonActivity(citizen.Model,walking,working);
            }
        }
        void ClearWorldActors()
        {
            if(Military!=null)Military.ResetForWorld();
            foreach(var model in models.Values)Destroy(model);models.Clear();
            foreach(var cart in carts.Values)Destroy(cart.Model);carts.Clear();
            foreach(var citizen in residents.Values)Destroy(citizen.Model);residents.Clear();
            if(ferry!=null)Destroy(ferry);if(bridge!=null)Destroy(bridge);if(construction!=null)Destroy(construction);
            ferry=null;bridge=null;construction=null;crossingKey="";winterShown=null;
            marker.SetActive(false);preview.SetActive(false);
        }
        public void Save(){SaveTo(SavePath);}
        public bool SaveTo(string target)
        {
            if(BenchmarkRunning){LastMessage="The benchmark fixture cannot overwrite your charter save.";return false;}
            try
            {
                if(string.IsNullOrWhiteSpace(target))return Act("Choose a valid save path.");
                target=Path.GetFullPath(target);
                string json=JsonUtility.ToJson(Sim.Snapshot(),true);
                var parsed=JsonUtility.FromJson<SimulationState>(json);string problem=Simulation.Validate(parsed);
                if(problem!="")return Act("Save validation failed: "+problem);
                Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+".tmp";
                File.WriteAllText(temp,json);if(File.Exists(target))File.Replace(temp,target,target+".bak");else File.Move(temp,target);
                return Act("","Town saved, including construction and cargo in transit.");
            }
            catch(Exception e){return Act("Could not save: "+e.Message);}
        }
        public void Load(){LoadFrom(SavePath);}
        public void LoadMilitaryChapter(){if(LoadFrom(MilitarySavePath))Military.SetArmyMode(true);}
        public void LoadFirstWinterChapter(){LoadFrom(CharterSavePath);}
        public bool LoadFrom(string target)
        {
            if(BenchmarkRunning)return false;
            try
            {
                if(string.IsNullOrWhiteSpace(target))return Act("Choose a valid save path.");
                target=Path.GetFullPath(target);
                if(!File.Exists(target))return Act("No Unity save has been created at this path.");
                var state=JsonUtility.FromJson<SimulationState>(File.ReadAllText(target));string error=Sim.Restore(state);if(error!="")return Act(error);
                ClearWorldActors();
                Paused=true;InMenu=false;DayFraction=Sim.Military.Enabled?(float)Sim.Military.DayFraction:0;workerClock=0;Selected=-1;BuildType="";Media.Stop();UI.CloseModal();CameraRig.Home();return Act("","Save loaded. Time is paused.");
            }
            catch(Exception e){return Act("Save could not be loaded: "+e.Message);}
        }
        public void SetQuality(int level){ApplyQuality(level,true);}
        void ApplyQuality(int level,bool remember)
        {
            if(BenchmarkRunning)return;
            // Graphics profiles remain two choices even when this player build
            // includes only the template's PC quality tier.
            level=Mathf.Clamp(level,0,1);
            QualitySettings.SetQualityLevel(0,true);
            graphicsProfile=level;
            var p=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(p!=null){p.shadowDistance=level==0?55:70;p.msaaSampleCount=level==0?1:2;}
            FitRenderResolution();
            if(remember)PlayerPrefs.SetInt("Quality",level);
        }
        void FitRenderResolution()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null||Screen.width<1||Screen.height<1)return;
            renderWidth=Screen.width;renderHeight=Screen.height;
            float maximumScale=graphicsProfile==0?.8f:1f;
            // Keep the 3D workload near the tested laptop budget when Windows
            // maximizes a high-DPI window. Screen-space UI retains native pixels.
            float pixelBudget=1440f*900f*maximumScale*maximumScale;
            float scale=Mathf.Clamp(Mathf.Sqrt(pixelBudget/((float)renderWidth*renderHeight)),.25f,maximumScale);
            if(Mathf.Abs(pipeline.renderScale-scale)>.001f)pipeline.renderScale=scale;
        }
        public void BeginBenchmark(bool war=false)
        {
            if(BenchmarkRunning)return;
            militaryBenchmark=war;
            BenchmarkRunning=true;BenchmarkComplete=false;BenchmarkPassed=false;BenchmarkError="";
            StartCoroutine(Benchmark());
        }

        // A deliberately seeded performance fixture, not a claimed earned playthrough.
        // Every restore, purchase and promise is checked. The model harness separately
        // verifies unseeded scenario viability, accounting and malformed-save rejection.
        static Simulation BuildBenchmarkFixture(out string description)
        {
            var fixture=new Simulation();var seeded=fixture.Snapshot();
            seeded.Towns[0].Gold=100000;seeded.Towns[0].Population=160;
            foreach(var good in seeded.Towns[0].Stocks)good.Amount=75;
            Require(fixture.Restore(seeded),"initial benchmark stock and labor");
            foreach(var tile in new[]{new Vector2Int(5,15),new Vector2Int(5,18),new Vector2Int(5,21),new Vector2Int(8,21)})
                Require(fixture.PlaceBuilding("warehouse",tile.x,tile.y),"benchmark warehouse "+tile);
            seeded=fixture.Snapshot();foreach(var good in seeded.Towns[0].Stocks)good.Amount=475;
            Require(fixture.Restore(seeded),"warehouse-backed construction stock");
            for(int n=0;n<3;n++)Require(fixture.PlaceBuilding("mine",5,6+n*3),"benchmark mine "+n);
            var sites=new List<Vector2Int>();
            for(int z=6;z<=21;z+=3)for(int x=8;x<=17;x+=3)if(x!=8||z!=21)sites.Add(new Vector2Int(x,z));
            int index=0;
            foreach(string kind in new[]{"farm","lumberyard","mill","bakery","smelter","toolsmith"})
            {
                int count=kind=="smelter"||kind=="toolsmith"?3:4;
                for(int n=0;n<count;n++){var tile=sites[index++];Require(fixture.PlaceBuilding(kind,tile.x,tile.y),"benchmark "+kind+" "+tile);}
            }
            seeded=fixture.Snapshot();seeded.Towns[0].Gold=750;
            foreach(var good in seeded.Towns[0].Stocks)good.Amount=100;
            Require(fixture.Restore(seeded),"representative operating inventory");
            Require(fixture.ChooseCrossing("ferry"),"benchmark ferry");
            fixture.AdvanceDay();fixture.AdvanceDay();
            Require(fixture.ChooseCrossing("bridge"),"benchmark bridge works while ferry operates");
            int productive=0;
            foreach(var building in fixture.State.Buildings)
                if(building.Town==0&&Simulation.Definitions[building.Type].Workers>0)
                {
                    if(building.Workers!=Simulation.Definitions[building.Type].Workers)throw new InvalidOperationException("Benchmark workplace is understaffed: "+building.Type);
                    if(!fixture.ProductionStatus(building.Id).StartsWith("Producing",StringComparison.Ordinal))throw new InvalidOperationException("Benchmark workplace is blocked: "+building.Type);
                    productive++;
                }
            if(productive<24)throw new InvalidOperationException("Benchmark needs at least 24 operating workplaces; got "+productive);
            foreach(var offer in fixture.State.Contracts.ToArray())
            {Require(fixture.AcceptContract(offer.Id),"benchmark promise acceptance");Require(fixture.DispatchContract(offer.Id),"benchmark promised cargo");}
            Require(fixture.DispatchTrade(1,"stone",4,true),"benchmark stone import");
            Require(fixture.DispatchTrade(2,"wood",8,true),"benchmark timber import");
            Require(fixture.DispatchTrade(1,"bread",8,false),"benchmark bread export");
            // Validate the actual Unity serialization path, including construction,
            // reserved imports and contract escrow. Compare canonical field output.
            string json=JsonUtility.ToJson(fixture.Snapshot());
            var reloaded=JsonUtility.FromJson<SimulationState>(json);Require(Simulation.Validate(reloaded),"benchmark JSON validation");
            // The decoder may normalize the final bit of a double. Compare against
            // that parsed state, not the pre-decoding numeric spelling in the file.
            string normalizedJson=JsonUtility.ToJson(reloaded);
            var roundtrip=new Simulation();Require(roundtrip.Restore(reloaded),"benchmark JSON restore");
            if(JsonUtility.ToJson(roundtrip.Snapshot())!=normalizedJson)throw new InvalidOperationException("Benchmark restore changed the decoded persistent state.");
            description=$"Seeded benchmark fixture: {productive} fully staffed productive player workplaces, 4 warehouses, population160, finite inventory100 before transport construction, ferry operating and bridge under construction. {fixture.State.Caravans.Count} real shipments at setup. This is not an earned campaign save.";
            return fixture;
        }

        static void Require(string error,string step)
        {if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(step+": "+error);}

        string PrepareBenchmark(out Simulation fixture,out string description)
        {
            fixture=null;description="";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BenchmarkReportPath));
                var report=SimulationVerification.RunAll(s=>JsonUtility.ToJson(s),json=>JsonUtility.FromJson<SimulationState>(json),"Unity JsonUtility");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(BenchmarkReportPath),"simulation-tests.txt"),report.ToString());
                if(!report.Passed||!report.SerializationTested)return "Simulation or actual JSON verification failed; inspect simulation-tests.txt.";
                if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return "Rendering benchmark cannot run without a graphics device.";
                fixture=militaryBenchmark?BuildMilitaryBenchmarkFixture(out description):BuildBenchmarkFixture(out description);return "";
            }
            catch(Exception error){return error.ToString();}
        }

        static Simulation BuildMilitaryBenchmarkFixture(out string description)
        {
            var fixture=new Simulation();fixture.StartMilitaryChapter();
            var state=fixture.Snapshot();state.Towns[0].Population=96;state.Towns[0].Gold=1000;
            Require(fixture.Restore(state),"military benchmark population fixture");
            Require(fixture.UpgradeMilitary("barracks"),"military benchmark training upgrade");
            Require(fixture.UpgradeMilitary("equipment"),"military benchmark equipment upgrade");
            for(int i=0;i<6;i++)Require(fixture.RecruitSquad(i%2==0?"spearmen":"archers"),"military benchmark recruit "+i);
            fixture.AdvanceMilitary(105);
            int n=0;
            foreach(var squad in fixture.Military.Squads.Where(s=>!s.Hostile))
            {
                Require(fixture.OrderSquad(squad.Id,"move",-5.5-(n%3)*2.2,7.7+(n/3)*2.2),"military benchmark deployment");n++;
            }
            fixture.AdvanceMilitary(67);
            Require(fixture.DispatchTrade(1,"stone",4,true),"military benchmark caravan");
            Require(Simulation.Validate(fixture.Snapshot()),"military benchmark state");
            description="Seeded military performance fixture: established Toll War town, population96, six normally trained/equipped four-person squads, camp guard, active trade and next raiding party at sample start. Training/deployment clock fast-forwarded only for this disclosed QA fixture; no earned campaign or manual save claim.";
            return fixture;
        }

        IEnumerator Benchmark()
        {
            // Preserve the exact current model reference, including unsaved progress.
            var original=Sim;bool oldPaused=Paused,oldMenu=InMenu;int oldSpeed=Speed,oldSelected=Selected;
            string oldBuild=BuildType,oldMessage=LastMessage;float oldFraction=DayFraction;double oldClock=workerClock;
            var eventSystem=EventSystem.current;bool eventsEnabled=eventSystem!=null&&eventSystem.enabled;
            Vector3 oldFocus=CameraRig.Focus;float oldDistance=CameraRig.Distance,oldYaw=CameraRig.Yaw,oldPitch=CameraRig.Pitch;
            bool fixtureShown=false;
            try
            {
                yield return null;
                Simulation fixture;string description;
                BenchmarkError=PrepareBenchmark(out fixture,out description);
                if(BenchmarkError!="")
                {
                    File.WriteAllText(BenchmarkReportPath,"BENCHMARK FAILED\n"+BenchmarkError);Debug.LogError(BenchmarkError);yield break;
                }
                if(eventSystem!=null)eventSystem.enabled=false;
                Sim=fixture;fixtureShown=true;InMenu=false;Paused=false;Speed=1;Selected=-1;BuildType="";DayFraction=0;workerClock=0;
                ClearWorldActors();CameraRig.Focus=militaryBenchmark?new Vector3(-8,0,7.7f):WorldArt.GridToWorld(12,14);CameraRig.Distance=militaryBenchmark?49:59;CameraRig.Yaw=militaryBenchmark?-18:0;
                UI.CloseModal();Media.Stop();SyncWorld();UI.Refresh();
                LastMessage="Performance sample: seeded developed town; simulation and workers running at 1x.";
                float warmupUntil=Time.realtimeSinceStartup+8;
                while(Time.realtimeSinceStartup<warmupUntil)yield return null;
                var times=new List<double>(2400);var watch=System.Diagnostics.Stopwatch.StartNew();double previous=0;
                int startDay=Sim.State.Day;int startWorkers=RenderedWorkerCount,startCargo=RenderedCaravanCount;
                int maxSquads=Military.RenderedSquadCount;double warStart=Sim.Military.Clock;
                while(watch.Elapsed.TotalSeconds<30)
                {
                    yield return null;double now=watch.Elapsed.TotalSeconds;times.Add(now-previous);previous=now;maxSquads=Math.Max(maxSquads,Military.RenderedSquadCount);
                }
                watch.Stop();double elapsed=watch.Elapsed.TotalSeconds;
                if(times.Count<2){BenchmarkError="Too few rendered frames were sampled.";yield break;}
                times.Sort();double p95=times[Mathf.Clamp(Mathf.CeilToInt(times.Count*.95f)-1,0,times.Count-1)];
                var text=new StringBuilder();
                text.AppendLine((militaryBenchmark?"THE TOLL WAR":"FIRST WINTER DELIVERY")+" · UNITY PERFORMANCE SAMPLE");text.AppendLine(description);
                text.AppendLine($"UTC: {DateTime.UtcNow:O}\nUnity: {Application.unityVersion}\nEditor: {Application.isEditor}\nGPU: {SystemInfo.graphicsDeviceName}\nGraphics API: {SystemInfo.graphicsDeviceType}\nCPU: {SystemInfo.processorType}\nRAM: {SystemInfo.systemMemorySize} MiB");
                text.AppendLine($"Resolution: {Screen.width}x{Screen.height}; mode:{Screen.fullScreenMode}\nSample: {elapsed:F2}s / {times.Count} frames\nMean FPS: {times.Count/elapsed:F2}\n95th percentile frame: {p95*1000:F2} ms");
                text.AppendLine($"Simulation: 1x, active; day{startDay}→{Sim.State.Day}\nBuildings rendered: {RenderedBuildingCount}\nOperating workplaces at end: {FunctionalWorkplaceCount}\nWorker models: {startWorkers}→{RenderedWorkerCount} of {TotalAssignedWorkers} assigned; visual cap{MaxVisibleWorkers}\nCargo models: {startCargo}→{RenderedCaravanCount}");
                if(militaryBenchmark)text.AppendLine($"Maximum rendered squads: {maxSquads} / {maxSquads*4} soldiers; military time {warStart:F1}→{Sim.Military.Clock:F1}s; raiding parties spawned: {Sim.Military.RaidsSpawned}");
                text.AppendLine(RenderConfiguration());text.AppendLine("Timing uses real elapsed frame intervals and includes simulation/UI refresh. The current 60 FPS cap may limit measured FPS. No manual save was overwritten.");
                string measurements=text.ToString();File.WriteAllText(BenchmarkReportPath,measurements);
                string image=Path.Combine(Path.GetDirectoryName(BenchmarkReportPath),militaryBenchmark?"military-benchmark.png":"gameplay.png");ScreenCapture.CaptureScreenshot(image);
                float finish=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<finish)yield return null;
                if(!File.Exists(image)){BenchmarkError="Frame sample completed, but the requested screenshot was not written.";File.AppendAllText(BenchmarkReportPath,"\n"+BenchmarkError);}
                BenchmarkPassed=BenchmarkError=="";Debug.Log(measurements);
            }
            finally
            {
                if(eventSystem!=null)eventSystem.enabled=eventsEnabled;
                if(fixtureShown)
                {
                    Sim=original;Paused=oldPaused;InMenu=oldMenu;Speed=oldSpeed;Selected=oldSelected;BuildType=oldBuild;DayFraction=oldFraction;workerClock=oldClock;
                    LastMessage=oldMessage;ClearWorldActors();CameraRig.Focus=oldFocus;CameraRig.Distance=oldDistance;CameraRig.Yaw=oldYaw;CameraRig.Pitch=oldPitch;
                    SyncWorld();if(InMenu)UI.ShowMain();else UI.CloseModal();UI.Refresh();
                }
                BenchmarkRunning=false;BenchmarkComplete=true;
                if(!Application.isEditor&&(Environment.GetCommandLineArgs().Contains("--benchmark")||Environment.GetCommandLineArgs().Contains("--benchmark-war")))Application.Quit(BenchmarkPassed?0:2);
            }
        }

        string RenderConfiguration()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int level=QualitySettings.GetQualityLevel();
            string result=$"Quality: {QualitySettings.names[level]} ({level}); color space:{QualitySettings.activeColorSpace}\nTarget FPS:{Application.targetFrameRate}; vSync:{QualitySettings.vSyncCount}\nPipeline:{GraphicsSettings.currentRenderPipeline?.name??"Built-in"}\nFog:{RenderSettings.fog}; shadows:{QualitySettings.shadows}; shadow resolution:{QualitySettings.shadowResolution}";
            if(pipeline!=null)result+=$"\nProfile:{(graphicsProfile==0?"Economy":"Balanced")}; URP render scale:{pipeline.renderScale:F3}; approximate 3D size:{Mathf.RoundToInt(Screen.width*pipeline.renderScale)}x{Mathf.RoundToInt(Screen.height*pipeline.renderScale)}; MSAA:{pipeline.msaaSampleCount}; shadow distance:{pipeline.shadowDistance:F1}; HDR:{pipeline.supportsHDR}";
            if(CameraRig.View!=null)result+=$"\nCamera FOV:{CameraRig.View.fieldOfView:F1}; far clip:{CameraRig.View.farClipPlane:F1}; distance:{CameraRig.Distance:F1}";
            return result;
        }

        void OnDestroy()
        {
            if(Instance==this)Instance=null;
            if(validMat!=null)Destroy(validMat);if(invalidMat!=null)Destroy(invalidMat);
        }
    }
}
