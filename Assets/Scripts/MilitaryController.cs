using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace LivingEmpires
{
    public sealed class SquadView : MonoBehaviour { public int Id; public bool Hostile; }
    public sealed class MilitaryCampView : MonoBehaviour { }
    public sealed class CargoView : MonoBehaviour { public int Id; }

    /// <summary>Mouse command ownership and presentation; the serializable simulation owns all movement and combat.</summary>
    public sealed class MilitaryController : MonoBehaviour
    {
        public bool ArmyMode { get; private set; }
        public IReadOnlyList<int> SelectedSquads => selected;
        public string PendingOrder { get; private set; } = "";
        public string Status { get; private set; } = "Select squads, then right-click to move or attack.";
        public int RenderedSquadCount => actors.Count;
        public int RenderedSoldierCount => actors.Count * 4;
        GameController game;
        Transform host;
        GameObject camp;
        readonly List<int> selected = new List<int>();
        readonly Dictionary<int, Actor> actors = new Dictionary<int, Actor>();
        readonly List<int> stale = new List<int>();
        Material green, red, dark, gold;
        LineRenderer destination, commandPath;
        RectTransform selectionRect;
        float destinationTime;
        SimulationState observedState;
        string lastEvent = "";
        sealed class Actor
        {
            public GameObject Root;
            public readonly List<GameObject> Soldiers = new List<GameObject>();
            public LineRenderer Ring, Health, HealthBack;
            public Transform Bar;
            public double LastHP;
            public float HitFlash;
        }

        public void Initialize(GameController owner)
        {
            game = owner;
            host = new GameObject("Militia and raiders").transform; host.SetParent(transform, false);
            green = ColorMaterial(new Color(.35f, .86f, .71f)); red = ColorMaterial(new Color(.90f, .28f, .17f));
            dark = ColorMaterial(new Color(.06f, .10f, .13f)); gold = ColorMaterial(new Color(1f, .77f, .36f));
            destination = Ring("Command destination", host, gold, .62f, .035f); destination.gameObject.SetActive(false);
            commandPath = Line("Selected movement route", host, green, .028f, true); commandPath.gameObject.SetActive(false);
        }
        public void SetArmyMode(bool enabled)
        {
            if (game == null) return;
            if (enabled && !game.Sim.Military.Enabled)
            { Status = "Military commands are available in The Toll War chapter."; game.LastMessage = Status; game.UI.RefreshCounters(); return; }
            ArmyMode = enabled; PendingOrder = "";
            game.CameraRig.ArmyControls = enabled; game.CameraRig.CancelGesture();
            if (enabled) { game.CancelBuild(); game.Selected = -1; }
            else selected.Clear();
            Status = enabled ? "Army: drag a box to select. Short right-click moves or attacks; right-drag pans." : "Settlement: choose buildings and manage production.";
            game.LastMessage = Status; game.UI.Refresh();
        }
        public void ResetForWorld()
        {
            selected.Clear(); PendingOrder = ""; ArmyMode = false;
            if (game != null) { game.CameraRig.ArmyControls = false; game.CameraRig.CancelGesture(); }
            foreach (var actor in actors.Values) if (actor.Root != null) Destroy(actor.Root);
            actors.Clear(); if (camp != null) Destroy(camp); camp = null;
            destinationTime = 0; if (commandPath != null) commandPath.gameObject.SetActive(false);
            if (destination != null) destination.gameObject.SetActive(false);
            observedState = game != null ? game.Sim.State : null; lastEvent = "";
        }
        public void SelectSquad(int id, bool additive = false)
        {
            var squad = game.Sim.Military.Squads.FirstOrDefault(s => s.Id == id && !s.Hostile);
            if (squad == null) return;
            if (!ArmyMode) SetArmyMode(true);
            if (!additive) selected.Clear();
            if (additive && selected.Contains(id)) selected.Remove(id); else if (!selected.Contains(id)) selected.Add(id);
            PendingOrder = ""; game.UI.Refresh();
        }
        public void SelectAll()
        {
            SetArmyMode(true); selected.Clear(); selected.AddRange(game.Sim.Military.Squads.Where(s => !s.Hostile).Select(s => s.Id));
            Status = selected.Count + " squads selected."; game.LastMessage = Status; game.UI.Refresh();
        }
        public void SetOrderMode(string order)
        {
            if (!ArmyMode) SetArmyMode(true);
            if (!ArmyMode) return;
            if (order != "rally" && selected.Count == 0) { Report("Select a squad first."); return; }
            PendingOrder = order;
            Status = order == "escort" ? "Click a traveling caravan to escort it." : order == "attack" ? "Click a hostile squad or the raider camp." : "Click the ground for " + order + ". Escape cancels.";
            game.LastMessage = Status; game.UI.RefreshCounters();
        }
        public bool CancelOrder()
        { if (PendingOrder == "") return false; PendingOrder = ""; Status = "Command cancelled."; game.LastMessage = Status; game.UI.RefreshCounters(); return true; }
        public void HoldSelected() { IssueAt(Vector3.zero, "hold"); }
        public void RetreatSelected() { IssueAt(Vector3.zero, "retreat"); }
        public void DemobilizeSelected()
        {
            if (selected.Count == 0) { Report("Select a squad first."); return; }
            string failure = "";
            foreach (int id in selected.ToArray()) { string result = game.Sim.DemobilizeSquad(id); if (result != "") failure = result; }
            selected.RemoveAll(id => !game.Sim.Military.Squads.Any(s => s.Id == id));
            game.Act(failure, "Squads are returning to the Muster Yard. Workers are released only on arrival.");
        }
        public bool IssueAt(Vector3 point, string order, int targetId = -1)
        {
            if (!game.Sim.Military.Enabled) return false;
            string failure = ""; int issued = 0;
            if (order == "rally") { failure = game.Sim.SetRally(point.x, point.z); if (failure == "") issued = 1; }
            else
            {
                if (selected.Count == 0) { Report("Select a squad first."); return false; }
                var reserved = new HashSet<Vector2Int>();
                foreach (int id in selected.ToArray())
                {
                    Vector3 destinationPoint = point;
                    var squad = game.Sim.Military.Squads.FirstOrDefault(s => s.Id == id);
                    if ((order == "move" || order == "patrol") && squad != null)
                    {
                        bool found=false;
                        var offsets=new List<Vector2Int>();
                        for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)offsets.Add(new Vector2Int(x,z));
                        foreach(var offset in offsets.OrderBy(v=>v.sqrMagnitude))
                        {
                            var candidate=point+new Vector3(offset.x*2.2f,0,offset.y*2.2f);
                            var tile=new Vector2Int(Mathf.FloorToInt(candidate.x/2.2f+20),Mathf.FloorToInt(candidate.z/2.2f+14));
                            if(reserved.Contains(tile)||!game.Sim.MilitaryRouteAvailable(squad.X,squad.Z,candidate.x,candidate.z))continue;
                            reserved.Add(tile);destinationPoint=candidate;found=true;break;
                        }
                        if(!found){failure="No separate reachable formation space for squad "+id+".";continue;}
                    }
                    string result = game.Sim.OrderSquad(id, order, destinationPoint.x, destinationPoint.z, targetId);
                    if (result == "") issued++; else failure = result;
                }
            }
            PendingOrder = "";
            if (issued > 0)
            {
                Status = order == "rally" ? "Rally point updated. Trained squads will gather here." : issued + " squad(s): " + order + ".";
                if (failure != "") Status += " " + failure;
                if(order!="hold"&&order!="retreat")
                {destination.transform.position = new Vector3(point.x, .11f, point.z); destinationTime = 1.7f;}
                game.Media.Sound("click"); game.LastMessage = Status; game.UI.Refresh(); return true;
            }
            Report(failure); return false;
        }
        void Report(string message) { Status = message; game.Act(message); }

        public void HandleInput()
        {
            if (game == null || !ArmyMode || game.InMenu || game.Modal || game.BenchmarkRunning || Mouse.current == null) return;
            var camera = game.CameraRig;
            bool additive = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            if (camera.SelectionBoxReleased)
            {
                Vector2 a = camera.SelectionStart, b = camera.SelectionEnd;
                Rect rect = Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
                if (!additive) selected.Clear();
                foreach (var squad in game.Sim.Military.Squads.Where(s => !s.Hostile))
                {
                    Vector3 screen = camera.View.WorldToScreenPoint(new Vector3((float)squad.X, .5f, (float)squad.Z));
                    if (screen.z > 0 && rect.Contains(screen) && !selected.Contains(squad.Id)) selected.Add(squad.Id);
                }
                PendingOrder = ""; game.UI.Refresh(); return;
            }
            if ((!camera.LeftClickReleased && !camera.RightClickReleased) || camera.PointerOverUI()) return;
            Vector3 point; if (!camera.Ground(out point)) return;
            RaycastHit hit;
            bool found = Physics.Raycast(camera.View.ScreenPointToRay(Mouse.current.position.ReadValue()), out hit, 260);
            SquadView squadView = found ? hit.collider.GetComponentInParent<SquadView>() : null;
            bool campHit = found && hit.collider.GetComponentInParent<MilitaryCampView>() != null;
            CargoView cargo = found ? hit.collider.GetComponentInParent<CargoView>() : null;
            if (camera.LeftClickReleased && PendingOrder == "")
            {
                if (squadView != null && !squadView.Hostile) SelectSquad(squadView.Id, additive);
                else if (!additive) { selected.Clear(); game.UI.Refresh(); }
                return;
            }
            string order = PendingOrder;
            if (order == "") order = squadView != null && squadView.Hostile || campHit ? "attack" : "move";
            int target = -1;
            if (order == "attack")
            {
                if (squadView != null && squadView.Hostile) target = squadView.Id;
                else if (campHit) target = -2;
                else { Report("Choose an enemy squad or the raider camp."); return; }
            }
            if (order == "escort")
            {
                if (cargo == null) { Report("Choose a traveling caravan. Use Orders to dispatch one if the road is empty."); return; }
                target = cargo.Id;
            }
            IssueAt(point, order, target);
        }
        void LateUpdate()
        {
            if (game == null) return;
            if (observedState != game.Sim.State) ResetForWorld();
            if (game.Sim.Military.Enabled) SyncActors();
            else if (actors.Count > 0 || camp != null) ResetForWorld();
            DrawSelectionBox();
            destinationTime -= Time.unscaledDeltaTime;
            destination.gameObject.SetActive(destinationTime > 0 && !game.InMenu && !game.Modal);
            if (destinationTime > 0) destination.transform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 9) * .12f);
        }
        void SyncActors()
        {
            var state = game.Sim.Military;
            if (camp == null && state.CampHP > 0)
            {
                camp = MilitaryArt.CreateCamp(host); camp.name = "Raider camp - command target";
                camp.transform.position = new Vector3((float)state.CampX, .04f, (float)state.CampZ);
                camp.AddComponent<MilitaryCampView>(); var box = camp.AddComponent<BoxCollider>(); box.center = Vector3.up * .8f; box.size = new Vector3(3.6f, 1.8f, 3.6f);
            }
            if (camp != null && state.CampHP <= 0) { Destroy(camp); camp = null; }
            stale.Clear(); foreach (int id in actors.Keys) if (!state.Squads.Any(s => s.Id == id)) stale.Add(id);
            foreach (int id in stale) { Destroy(actors[id].Root); actors.Remove(id); selected.Remove(id); }
            foreach (var squad in state.Squads)
            {
                Actor actor;
                Vector3 position = new Vector3((float)squad.X, Surface((float)squad.X, (float)squad.Z), (float)squad.Z);
                if (!actors.TryGetValue(squad.Id, out actor))
                {
                    actor = new Actor { Root = new GameObject((squad.Hostile ? "Raider" : "Riverhold") + " squad " + squad.Id), LastHP = squad.HP };
                    actor.Root.transform.SetParent(host, false); actor.Root.transform.position = position;
                    var view = actor.Root.AddComponent<SquadView>(); view.Id = squad.Id; view.Hostile = squad.Hostile;
                    var collider = actor.Root.AddComponent<CapsuleCollider>(); collider.center = new Vector3(0,.6f,0); collider.radius = .64f; collider.height = 1.45f;
                    for (int i = 0; i < 4; i++)
                    {
                        var model = MilitaryArt.CreateSoldier(squad.Kind, squad.Hostile, actor.Root.transform);
                        model.transform.localPosition = new Vector3((i % 2 == 0 ? -.29f : .29f), 0, i < 2 ? .28f : -.28f); actor.Soldiers.Add(model);
                    }
                    actor.Ring = Ring("Squad selection", actor.Root.transform, squad.Hostile ? red : green, .85f, .045f);
                    actor.Bar = new GameObject("Squad health").transform; actor.Bar.SetParent(actor.Root.transform,false); actor.Bar.localPosition = new Vector3(0,1.24f,0);
                    actor.HealthBack = Line("Health background", actor.Bar, dark, .12f, false); actor.HealthBack.positionCount = 2;
                    actor.HealthBack.SetPositions(new[] { new Vector3(-.57f,0,.012f), new Vector3(.57f,0,.012f) });
                    actor.Health = Line("Health", actor.Bar, squad.Hostile ? red : green, .065f, false); actor.Health.positionCount = 2;
                    actors.Add(squad.Id, actor);
                }
                Vector3 delta = position - actor.Root.transform.position;
                bool walking = delta.sqrMagnitude > .00008f;
                actor.Root.transform.position = Vector3.Lerp(actor.Root.transform.position, position, 1-Mathf.Exp(-Time.unscaledDeltaTime*22));
                if (walking) actor.Root.transform.rotation = Quaternion.Slerp(actor.Root.transform.rotation, Quaternion.LookRotation(new Vector3(delta.x,0,delta.z)), 1-Mathf.Exp(-Time.unscaledDeltaTime*14));
                bool fighting = game.IsAdvancing && !squad.Recovering && squad.HP>0 && squad.AttackCooldown>0;
                if (squad.HP < actor.LastHP) actor.HitFlash = .24f;
                actor.LastHP = squad.HP; actor.HitFlash = Mathf.Max(0, actor.HitFlash-Time.unscaledDeltaTime);
                foreach (var soldier in actor.Soldiers) WorldArt.SetPersonActivity(soldier, walking && game.IsAdvancing, fighting);
                actor.Ring.gameObject.SetActive(selected.Contains(squad.Id) || squad.Hostile || actor.HitFlash > 0);
                actor.Ring.sharedMaterial = actor.HitFlash > 0 ? gold : squad.Hostile ? red : green;
                actor.Bar.rotation = game.CameraRig.View.transform.rotation;
                float hp = Mathf.Clamp01((float)(squad.HP / Math.Max(1,squad.MaxHP)));
                actor.Health.SetPositions(new[] { new Vector3(-.55f,0,0), new Vector3(-.55f + hp*1.1f,0,0) });
            }
            if (state.LastEvent != lastEvent) { lastEvent = state.LastEvent; if (!string.IsNullOrEmpty(lastEvent)) { game.LastMessage = lastEvent; game.UI.RefreshCounters(); } }
            var leader = state.Squads.FirstOrDefault(s => selected.Contains(s.Id));
            commandPath.gameObject.SetActive(ArmyMode && leader != null && leader.Path != null && leader.PathIndex < leader.Path.Count);
            if (commandPath.gameObject.activeSelf)
            {
                int start = leader.PathIndex, count = Math.Min(80, leader.Path.Count-start);
                commandPath.positionCount = count+1; commandPath.SetPosition(0,new Vector3((float)leader.X,.14f,(float)leader.Z));
                for(int i=0;i<count;i++) { var p=leader.Path[start+i]; commandPath.SetPosition(i+1,new Vector3((float)p.X,Surface((float)p.X,(float)p.Z)+.10f,(float)p.Z)); }
            }
        }
        float Surface(float x,float z)
        {
            if (Mathf.Abs(x)<3.4f && Mathf.Abs(z-WorldArt.BridgePosition.z)<1.2f && game.Sim.State.Crossing.Mode=="bridge") return .44f;
            if (Mathf.Abs(x)<3.4f && Mathf.Abs(z-WorldArt.FerryPosition.z)<1.2f && game.Sim.State.Crossing.Mode=="ferry") return .25f;
            return WorldArt.GroundHeight(x,z)+.045f;
        }
        void DrawSelectionBox()
        {
            if (selectionRect == null)
            {
                var canvas = FindObjectsByType<Canvas>().FirstOrDefault(c=>c.name=="Riverhold interface");
                if (canvas == null) return;
                var go = new GameObject("Army selection rectangle",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                selectionRect = go.GetComponent<RectTransform>(); selectionRect.SetParent(canvas.transform,false);
                selectionRect.anchorMin = selectionRect.anchorMax = Vector2.zero; selectionRect.pivot = Vector2.zero;
                var picture = go.GetComponent<UnityEngine.UI.Image>(); picture.color = new Color(.34f,.77f,.65f,.15f); picture.raycastTarget=false;
                var outline = go.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.72f,.93f,.81f,.8f); outline.effectDistance=new Vector2(1,-1);
            }
            bool shown = ArmyMode && game.CameraRig.SelectionBoxActive && !game.InMenu && !game.Modal;
            selectionRect.gameObject.SetActive(shown); if (!shown) return;
            var parent = (RectTransform)selectionRect.parent; Vector2 a,b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,game.CameraRig.SelectionStart,null,out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,game.CameraRig.SelectionEnd,null,out b);
            selectionRect.anchoredPosition = Vector2.Min(a,b)-parent.rect.min; selectionRect.sizeDelta = new Vector2(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y));
        }
        static Material ColorMaterial(Color color)
        { var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){enableInstancing=true}; material.SetColor("_BaseColor",color);return material; }
        static LineRenderer Line(string name,Transform parent,Material material,float width,bool world)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();
            line.useWorldSpace=world;line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=2;return line;
        }
        static LineRenderer Ring(string name,Transform parent,Material material,float radius,float width)
        {
            var line=Line(name,parent,material,width,false);line.loop=true;line.positionCount=40;
            for(int i=0;i<40;i++){float angle=i*Mathf.PI*2/40;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,.055f,Mathf.Sin(angle)*radius));}return line;
        }
        void OnDestroy(){foreach(var m in new[]{green,red,dark,gold})if(m!=null)Destroy(m);}
    }
}
