using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace LivingEmpires
{
    public sealed class GameUI : MonoBehaviour
    {
        GameController game;
        RectTransform root, hud, modal, right, inspector, objectives, content, navigation, buildItems, armyItems, guideBox, valleyMap, mapCamp, mapCamera;
        TextMeshProUGUI dayText, statusText, guideText, captionText, speakerText, inspectorText, edgePanText, pauseText, wealthText, drawerTitle;
        readonly Dictionary<string,TextMeshProUGUI> counters=new Dictionary<string,TextMeshProUGUI>();
        readonly Dictionary<string,UnityEngine.UI.Button> tabButtons=new Dictionary<string,UnityEngine.UI.Button>();
        readonly List<Action> armyBindings=new List<Action>();
        UnityEngine.UI.Button settlementButton, armyButton;
        TMP_FontAsset interfaceFont;
        string tab="", storyId="", objectiveKey="", inspectorKey="", armyKey="", armySelectionKey="";
        int town=1,good=2,quantity=6, lastSelection=-1;
        bool buy=true, guide=true, lastArmyMode;
        float armyRefresh;
        const float DrawerWidth=370;
        readonly Color ink=new Color(.055f,.095f,.145f,.98f), panel=new Color(.085f,.135f,.19f,.97f),
            parchment=new Color(.95f,.925f,.86f), gold=new Color(.79f,.63f,.34f),
            muted=new Color(.69f,.75f,.77f), teal=new Color(.16f,.23f,.29f), paperInk=new Color(.16f,.20f,.24f);
        public bool ModalOpen=>modal!=null&&modal.gameObject.activeSelf;
        bool MilitaryEnabled=>game.Sim.Military!=null&&game.Sim.Military.Enabled;
        bool ArmyMode=>game.Military!=null&&game.Military.ArmyMode;
        public void Initialize(GameController controller)
        {
            game=controller;
            interfaceFont=Resources.Load<TMP_FontAsset>("Fonts/Interface") ?? TMP_Settings.defaultFontAsset ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if(interfaceFont==null)interfaceFont=TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var canvasGO=new GameObject("Riverhold interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var scaler=canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;
            root=canvasGO.GetComponent<RectTransform>();
            if(EventSystem.current==null)new GameObject("UI Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            hud=Box(root,"HUD",Color.clear);Stretch(hud,0,0,0,0);hud.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;

            var top=Box(hud,"Resource ledger",ink);Anchor(top,0,1,1,1,0,-78,0,0);
            Rule(top,0,76,0,2);
            var crest=Box(top,"Riverhold seal",gold);Place(crest,new Rect(16,14,34,34));crest.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var seal=Label(crest,"R",24,ink,new Rect(0,1,34,32));seal.alignment=TextAlignmentOptions.Center;
            var brand=Label(top,"LIVING EMPIRES",15,parchment,new Rect(62,12,183,24));brand.characterSpacing=1.6f;
            Label(top,"THE BROKEN ACCORD",9,gold,new Rect(63,39,174,16));
            dayText=Label(top,"",15,parchment,new Rect(252,12,590,28));
            var controls=Box(top,"Time and charter controls",Color.clear);Anchor(controls,1,1,1,1,-438,-42,-12,-10);controls.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            pauseText=Button(controls,"Pause",new Rect(0,0,76,30),()=>game.TogglePause()).GetComponentInChildren<TextMeshProUGUI>();
            Button(controls,"1x",new Rect(83,0,36,30),()=>game.SetSpeed(1));Button(controls,"3x",new Rect(123,0,36,30),()=>game.SetSpeed(3));Button(controls,"8x",new Rect(163,0,36,30),()=>game.SetSpeed(8));
            Button(controls,"Save",new Rect(208,0,62,30),()=>game.Save());Button(controls,"Load",new Rect(275,0,62,30),()=>game.Load());Button(controls,"Menu",new Rect(342,0,82,30),ShowPause);
            int n=0;foreach(string g in Simulation.Goods)
            {
                var chip=Box(top,"Resource "+g,Color.clear);Place(chip,new Rect(252+n++*102,47,99,24));chip.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
                Icon(chip,g,new Rect(0,0,21,21));counters[g]=Label(chip,"",11,parchment,new Rect(26,0,73,22));
            }
            wealthText=Label(top,"",12,muted,new Rect());Anchor(wealthText.rectTransform,1,1,1,1,-514,-73,-15,-45);wealthText.alignment=TextAlignmentOptions.Right;

            objectives=Box(hud,"Charter",parchment);Anchor(objectives,0,1,0,1,16,-332,282,-94);Border(objectives);
            navigation=Box(hud,"Management navigation",ink);Anchor(navigation,1,1,1,1,-386,-132,-16,-94);Border(navigation);
            string[] tabs={"Inspect","Orders","Market","Route","Council","Army"};
            for(int i=0;i<tabs.Length;i++)
            {
                string captured=tabs[i];var button=Button(navigation,captured,new Rect(4+i*61,4,59,30),()=>OpenTab(captured));
                button.GetComponentInChildren<TextMeshProUGUI>().fontSize=12;if(captured=="Army")button.name="Army tab";tabButtons[captured]=button;
            }
            right=Box(hud,"Trade and council",panel);Anchor(right,1,0,1,1,-386,170,-16,-140);Border(right);
            drawerTitle=Label(right,"",12,gold,new Rect(15,12,262,27));drawerTitle.characterSpacing=1.6f;
            Button(right,"Close drawer",new Rect(289,8,70,29),()=>OpenTab("")).GetComponentInChildren<TextMeshProUGUI>().text="Close";
            Rule(right,12,42,12,1);
            content=Scroll(right,52,12,12,12);
            inspector=Box(right,"Inspector",Color.clear);Stretch(inspector,12,52,12,12);inspector.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;

            var bottom=Box(hud,"Build ribbon",parchment);Anchor(bottom,0,0,1,0,16,16,-16,118);Border(bottom);
            Label(bottom,"RIVERHOLD",12,paperInk,new Rect(16,10,170,21)).characterSpacing=2;
            settlementButton=Button(bottom,"Settlement",new Rect(13,38,98,43),()=>SetMode(false));
            armyButton=Button(bottom,"Army",new Rect(116,38,82,43),()=>SetMode(true));
            buildItems=Box(bottom,"Settlement construction",Color.clear);Anchor(buildItems,0,0,1,1,216,12,-226,-10);buildItems.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var definitions=Simulation.Definitions.Values.ToArray();
            for(int i=0;i<definitions.Length;i++)
            {
                var def=definitions[i];string kind=def.Kind;var b=Button(buildItems,def.Name+"\n"+def.GoldCost.ToString("0")+" gold",new Rect(),()=>{SetMode(false,false);game.BeginBuild(kind);});
                Anchor((RectTransform)b.transform,i/(float)definitions.Length,0,(i+1)/(float)definitions.Length,1,0,0,-7,0);
                Icon(b.transform,kind,new Rect(0,0,30,30));var icon=(RectTransform)b.transform.Find(kind+" icon");icon.anchorMin=icon.anchorMax=new Vector2(.5f,1);icon.pivot=new Vector2(.5f,1);icon.anchoredPosition=new Vector2(0,-6);
                var label=b.GetComponentInChildren<TextMeshProUGUI>();label.fontSize=11;Stretch(label.rectTransform,3,40,3,3);
            }
            armyItems=Box(bottom,"Army command ribbon",Color.clear);Anchor(armyItems,0,0,1,1,216,12,-226,-10);armyItems.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            string[] commands={"Select all squads","Move","Attack","Patrol","Escort","Rally","Hold position","Retreat"};
            for(int i=0;i<commands.Length;i++)
            {
                string command=commands[i];var b=Button(armyItems,"Ribbon "+command,new Rect(),()=>ArmyCommand(command));b.GetComponentInChildren<TextMeshProUGUI>().text=command;
                Anchor((RectTransform)b.transform,i/(float)commands.Length,0,(i+1)/(float)commands.Length,1,0,0,-6,0);
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=12;
            }
            var utilities=Box(bottom,"View utilities",Color.clear);Anchor(utilities,1,0,1,1,-211,12,-12,-10);utilities.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            Button(utilities,"Home view",new Rect(0,0,195,31),()=>game.CameraRig.Home());
            Button(utilities,"Guide on/off",new Rect(0,39,195,31),()=>{guide=!guide;RefreshCounters();});

            var messageBar=Box(hud,"Action feedback",ink);Anchor(messageBar,0,0,1,0,16,125,-16,157);messageBar.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            statusText=Label(messageBar,"",12,parchment,new Rect());Stretch(statusText.rectTransform,13,4,13,4);
            guideBox=Box(hud,"Steward guide",new Color(.055f,.095f,.145f,.86f));Anchor(guideBox,0,1,0,1,16,-416,282,-343);
            guideBox.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;guideText=Label(guideBox,"",11,parchment,new Rect());Stretch(guideText.rectTransform,12,8,12,8);
            CreateMouseToolbar();
            CreateValleyMap();
            modal=Box(root,"Modal",new Color(.025f,.035f,.055f,.72f));Stretch(modal,0,0,0,0);modal.gameObject.SetActive(false);
            Refresh();
        }
        void Rule(RectTransform parent,float left,float top,float rightOffset,float height)
        {
            var line=Box(parent,"Brass rule",gold);Anchor(line,0,1,1,1,left,-top-height,-rightOffset,-top);line.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
        }
        void Border(RectTransform parent)
        {
            var outline=parent.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.49f,.39f,.23f,.6f);outline.effectDistance=new Vector2(1,-1);
            var shadow=parent.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.25f);shadow.effectDistance=new Vector2(2,-3);
        }
        void CreateMouseToolbar()
        {
            var toolbar=Box(hud,"Mouse camera controls",ink);Anchor(toolbar,0,0,0,0,16,170,326,207);Border(toolbar);
            string[] labels={"Zoom in","Zoom out","Turn left","Turn right","Edge pan"};
            string[] captions={"+","−","<",">","Edges"};
            Action[] actions={()=>game.CameraRig.ZoomBy(-4f),()=>game.CameraRig.ZoomBy(4f),()=>game.CameraRig.RotateBy(-15f),()=>game.CameraRig.RotateBy(15f),()=>{game.CameraRig.SetEdgePanning(!game.CameraRig.EdgePanEnabled);RefreshCounters();}};
            for(int i=0;i<labels.Length;i++)
            {
                var b=Button(toolbar,labels[i],new Rect(5+i*60,5,56,27),actions[i]);b.GetComponentInChildren<TextMeshProUGUI>().text=captions[i];
                if(i==4){edgePanText=b.GetComponentInChildren<TextMeshProUGUI>();edgePanText.fontSize=10;}
            }
        }
        void CreateValleyMap()
        {
            var frame=Box(hud,"Valley map",ink);Anchor(frame,0,0,0,0,16,218,208,350);Border(frame);
            Label(frame,"THE BASIN  /  CLICK TO VIEW",9,gold,new Rect(9,7,177,18)).characterSpacing=.6f;
            valleyMap=Box(frame,"Map surface",new Color(.32f,.40f,.34f));Place(valleyMap,new Rect(8,30,176,94));
            var river=Box(valleyMap,"River",new Color(.26f,.48f,.54f));Anchor(river,.47f,0,.53f,1,0,0,0,0);river.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            for(int i=0;i<WorldArt.TownAnchors.Length;i++)
            {
                var world=WorldArt.TownAnchors[i];var dot=Box(valleyMap,"Town map marker "+i,i==0?parchment:gold);MapPosition(dot,world,7);
                string title=i==0?"Riverhold":i==1?"Ironvale":"Pinewatch";
                var label=Label(dot,title,8,parchment,new Rect(-27,9,62,13));label.alignment=TextAlignmentOptions.Center;
            }
            mapCamp=Box(valleyMap,"Raider camp map marker",new Color(.85f,.34f,.22f));MapPosition(mapCamp,new Vector3(40.7f,0,12.1f),6);
            mapCamera=Box(valleyMap,"Camera map marker",new Color(.95f,.89f,.67f));MapPosition(mapCamera,game.CameraRig.Focus,4);
            var trigger=valleyMap.gameObject.AddComponent<EventTrigger>();var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerClick};
            entry.callback.AddListener(data=>
            {
                var pointer=data as PointerEventData;if(pointer==null||pointer.button!=PointerEventData.InputButton.Left)return;
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(valleyMap,pointer.position,pointer.pressEventCamera,out var local))
                {
                    float x=Mathf.InverseLerp(valleyMap.rect.xMin,valleyMap.rect.xMax,local.x);
                    float z=Mathf.InverseLerp(valleyMap.rect.yMin,valleyMap.rect.yMax,local.y);
                    game.CameraRig.FocusOn(new Vector3(Mathf.Lerp(-44,44,x),0,Mathf.Lerp(-31,31,z)));
                }
            });trigger.triggers.Add(entry);
        }
        void MapPosition(RectTransform dot,Vector3 world,float size)
        {
            dot.anchorMin=dot.anchorMax=new Vector2(Mathf.InverseLerp(-44,44,world.x),Mathf.InverseLerp(-31,31,world.z));
            dot.pivot=new Vector2(.5f,.5f);dot.anchoredPosition=Vector2.zero;dot.sizeDelta=new Vector2(size,size);
            dot.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
        }
        void Update()
        {
            if(captionText!=null&&ModalOpen&&storyId!="")
            {
                var line=game.Media.Caption();captionText.text=line?.text??(game.Media.Voice.isPlaying?"":"Recording finished. You may replay it or return to the town.");
                if(speakerText!=null)speakerText.text=line?.speaker??game.Media.Get(storyId)?.speaker??"";
            }
            if(game==null||game.InMenu)return;
            armyRefresh+=Time.unscaledDeltaTime;
            if(armyRefresh>=.3f)
            {
                armyRefresh=0;
                if(mapCamera!=null)MapPosition(mapCamera,game.CameraRig.Focus,4);
                if(mapCamp!=null){mapCamp.gameObject.SetActive(MilitaryEnabled&&game.Sim.Military.CampHP>0);if(MilitaryEnabled)MapPosition(mapCamp,new Vector3((float)game.Sim.Military.CampX,0,(float)game.Sim.Military.CampZ),6);}
                if(lastArmyMode!=ArmyMode){lastArmyMode=ArmyMode;RefreshMode();}
                if(tab=="Army")
                {
                    string key=ArmyStructureKey();
                    if(key!=armyKey)RefreshPanel();
                    else foreach(var bind in armyBindings)bind();
                }
                if(MilitaryEnabled)RefreshObjectives();
            }
        }
        public void Refresh()
        {
            string selection=game.Military==null?"":string.Join(",",game.Military.SelectedSquads);
            if(selection!=armySelectionKey){armySelectionKey=selection;if(ArmyMode&&selection!="")tab="Army";}
            RefreshCounters();RefreshObjectives();RefreshInspector();RefreshMode();RefreshPanel();
        }
        public void RefreshCounters()
        {
            if(dayText==null)return;var s=game.Sim.State;var t=s.Towns[0];
            pauseText.text=game.Paused?"Resume":"Pause";
            dayText.text=$"DAY {s.Day}   /   {(MilitaryEnabled?"THE TOLL WAR":s.Day>=120?"WINTER":(120-s.Day)+" DAYS TO WINTER")}   ·   {(game.Paused?"PAUSED":game.Speed+"x")}";
            wealthText.text=$"{t.Gold:0} gold   ·   {game.Sim.WorkersUsed()}/{game.Sim.WorkforceTotal()} workers   ·   {game.Sim.FoodDays():0.0} food days";
            foreach(var item in counters)item.Value.text=$"{GoodName(item.Key)} {t.Stock(item.Key):0.#}";
            statusText.text=game.LastMessage;
            guideText.text=guide?Tutorial():"Drag ground / right-drag to pan\nWheel zoom · middle-drag orbit\nSpace pause · F5 save · F9 load";
            guideBox.gameObject.SetActive(guide);
            if(edgePanText!=null)edgePanText.text=game.CameraRig.EdgePanEnabled?"Edges on":"Edges off";
        }
        string Tutorial()
        {
            if(MilitaryEnabled)return ArmyMode?"ARMY COMMAND\nSelect a squad or drag a selection. Choose an order, then its world target. Right-click gives a contextual command.":"THE TOLL WAR\nBuild barracks, recruit a guard and protect the river. Army opens recruitment and squad orders.";
            var sim=game.Sim;var b=sim.State.Buildings;
            if(!b.Any(v=>v.Town==0&&v.Type=="mill"))return "01  /  FLOUR BEFORE PROMISES\nChoose Mill below, then click a free western tile.";
            if(!b.Any(v=>v.Town==0&&v.Type=="bakery"))return "02  /  FEED THE TOWN\nA bakery turns flour and timber into bread. Assign three workers.";
            if(sim.State.Crossing.Mode=="none")return "03  /  OPEN THE RIVER\nRoute → ferry or bridge. Resume time to finish the works.";
            if(!b.Any(v=>v.Town==0&&v.Type=="warehouse"))return "04  /  ROOM FOR WINTER\nAdd a warehouse to protect the coming harvest.";
            if(sim.State.Scenario.Delivered<2)return "05  /  KEEP TWO PROMISES\nOrders → accept, then dispatch. Payment follows arrival.";
            return "06  /  A TOWN THAT ENDURES\nKeep seven days of food. Council records your promises.";
        }
        void RefreshObjectives()
        {
            if(objectives==null)return;
            string heading,subtitle;var rows=new List<(string text,bool done)>();
            if(MilitaryEnabled)
            {
                var m=game.Sim.Military;
                heading="THE TOLL WAR";subtitle=m.Status=="won"?"The river is free again":m.Status=="recovery"?"Recover. Rebuild. Return.":"Defeat raids or clear their camp";
                rows.Add(("Establish the barracks",game.Sim.State.Buildings.Any(b=>b.Town==0&&b.Type=="barracks")));
                rows.Add(("Field a Riverhold squad",m.Squads.Any(s=>!s.Hostile&&s.HP>0)));
                rows.Add(($"Defeat raiding parties ({m.RaidsDefeated}/3)",m.RaidsDefeated>=3));
                rows.Add(($"OR clear camp ({Math.Max(0,m.CampHP):0} HP)",m.CampHP<=0));
                rows.Add(($"Food supply: {game.Sim.FoodDays():0.0} days",game.Sim.FoodDays()>=3));
                rows.Add((m.Status=="won"?"The Toll War is won":m.RaidWarning?"Raiders are approaching":"Scout, escort and defend",m.Status=="won"));
            }
            else {heading="FIRST WINTER DELIVERY";subtitle="The First Steward's charter";foreach(var r in game.Sim.ObjectiveRows())rows.Add((r.Text,r.Done));}
            string key=heading+subtitle+string.Join("|",rows.Select(r=>r.text+":"+r.done));if(key==objectiveKey)return;objectiveKey=key;
            Clear(objectives);Label(objectives,heading,14,paperInk,new Rect(14,13,240,25)).characterSpacing=.8f;
            Label(objectives,subtitle,11,new Color(.39f,.41f,.39f),new Rect(14,40,240,20));
            for(int i=0;i<rows.Count;i++)
            {
                var r=rows[i];Label(objectives,(r.done?"•  ":"—  ")+r.text,12,r.done?new Color(.23f,.43f,.34f):paperInk,new Rect(14,67+i*27,240,26));
            }
        }
        public void RefreshInspector()
        {
            if(inspector==null)return;
            bool selectionChanged=lastSelection!=game.Selected;lastSelection=game.Selected;
            if(game.BuildType!=""||(selectionChanged&&game.Selected>=0))
            {
                tab="Inspect";right.gameObject.SetActive(true);
            }
            var b=game.Sim.State.Buildings.Find(v=>v.Id==game.Selected);
            string key=game.BuildType+"|"+game.Selected+"|"+(b==null?"":b.Workers+"|"+game.Sim.ProductionStatus(b.Id));if(key==inspectorKey)return;inspectorKey=key;
            Clear(inspector);
            if(game.BuildType!="")
            {
                var def=Simulation.Definitions[game.BuildType];
                Icon(inspector,def.Kind,new Rect(10,5,45,45));Label(inspector,def.Name.ToUpperInvariant(),20,gold,new Rect(65,8,269,33));
                Label(inspector,def.Description,15,parchment,new Rect(10,69,322,88));
                Label(inspector,"CONSTRUCTION",11,gold,new Rect(10,171,322,22));
                Label(inspector,Cost(def),15,parchment,new Rect(10,202,322,63));
                Label(inspector,$"Workforce  {def.Workers}\n{Recipe(def)}",14,muted,new Rect(10,282,322,84));
                Button(inspector,"Cancel placement",new Rect(10,384,322,39),game.CancelBuild);return;
            }
            if(b==null)
            {
                Label(inspector,"THE VEYRAN BASIN",20,gold,new Rect(10,6,322,35));
                Label(inspector,"Select a building in the world to inspect production, assign workers and find shortages.\n\nTwelve years after the River Crown fell, each town must choose what its promises are worth.",15,parchment,new Rect(10,62,322,200));return;
            }
            var d=Simulation.Definitions[b.Type];Icon(inspector,b.Type,new Rect(10,4,45,45));
            Label(inspector,d.Name.ToUpperInvariant(),20,gold,new Rect(65,5,269,35));
            Label(inspector,game.Sim.State.Towns[b.Town].Name,12,muted,new Rect(66,41,269,22));
            inspectorText=Label(inspector,game.Sim.ProductionStatus(b.Id)+"\n\n"+Recipe(d),15,parchment,new Rect(10,92,322,169));
            Label(inspector,$"WORKFORCE   {b.Workers} / {d.Workers}",13,gold,new Rect(10,280,322,29));
            if(b.Town==0&&d.Workers>0)
            {
                Button(inspector,"− worker",new Rect(10,325,153,39),()=>game.Act(game.Sim.SetWorkers(b.Id,b.Workers-1),"Worker reassigned."));
                Button(inspector,"+ worker",new Rect(171,325,161,39),()=>game.Act(game.Sim.SetWorkers(b.Id,b.Workers+1),"Worker assigned."));
            }
        }
        public void PreviewMessage(string message){if(game.BuildType!=""&&statusText!=null)statusText.text=message;}
        public void OpenMilitaryPanel(){SetMode(true);}
        void SetMode(bool army,bool openDrawer=true)
        {
            if(game.Military!=null)game.Military.SetArmyMode(army&&MilitaryEnabled);
            if(army)game.CancelBuild();
            lastArmyMode=ArmyMode;RefreshMode();
            if(openDrawer)OpenTab(army?"Army":"");else RefreshCounters();
        }
        void RefreshMode()
        {
            if(buildItems==null)return;
            buildItems.gameObject.SetActive(!ArmyMode);armyItems.gameObject.SetActive(ArmyMode);
            StyleSelected(settlementButton,!ArmyMode);StyleSelected(armyButton,ArmyMode);
        }
        void OpenTab(string value)
        {
            tab=value;right.gameObject.SetActive(tab!="");content.anchoredPosition=Vector2.zero;
            if(value=="Inspect"){inspectorKey="";RefreshInspector();}
            RefreshPanel();
        }
        void RefreshPanel()
        {
            if(content==null)return;
            right.gameObject.SetActive(tab!="");drawerTitle.text=tab=="Army"?"RIVERHOLD COMMAND":tab.ToUpperInvariant()+" / RIVERHOLD";
            foreach(var item in tabButtons)StyleSelected(item.Value,item.Key==tab);
            bool inspect=tab=="Inspect";inspector.gameObject.SetActive(inspect);content.parent.parent.gameObject.SetActive(!inspect);
            if(tab==""||inspect)return;
            armyBindings.Clear();Clear(content);
            if(tab=="Orders")Orders();else if(tab=="Market")Market();else if(tab=="Route")Crossing();else if(tab=="Army")Army();else Council();
        }
        void StyleSelected(UnityEngine.UI.Button button,bool selected)
        {
            if(button==null)return;button.GetComponent<UnityEngine.UI.Image>().color=selected?gold:teal;button.GetComponentInChildren<TextMeshProUGUI>().color=selected?ink:parchment;
        }
        string ArmyStructureKey()
        {
            if(!MilitaryEnabled)return "inactive";
            var m=game.Sim.Military;
            return m.BarracksLevel+"|"+m.EquipmentLevel+"|"+string.Join(",",m.Squads.Where(s=>!s.Hostile&&s.HP>0).Select(s=>s.Id))+"|"+string.Join(",",m.Queue.Select(q=>q.Id));
        }
        void ArmyCommand(string command)
        {
            if(game.Military==null||!MilitaryEnabled){OpenTab("Army");return;}
            if(command=="Select all squads")game.Military.SelectAll();
            else if(command=="Hold position")game.Military.HoldSelected();
            else if(command=="Retreat")game.Military.RetreatSelected();
            else if(command=="Demobilize")game.Military.DemobilizeSelected();
            else game.Military.SetOrderMode(command.ToLowerInvariant());
            RefreshCounters();
        }
        TextMeshProUGUI BoundArmyLabel(Func<string> value,int size,Color color,float height)
        {
            var t=Label(content,value(),size,color,new Rect());t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;
            armyBindings.Add(()=>{if(t!=null)t.text=value();});return t;
        }
        void Army()
        {
            armyKey=ArmyStructureKey();
            RowLabel("THE TOLL WAR",20,gold,32);
            if(!MilitaryEnabled)
            {
                RowLabel("The military chapter opens a contested river, recruitment and squad orders.",15,parchment,76);
                RowLabel("Save your charter, then choose The Toll War from the main menu to begin its prepared scenario.",14,muted,90);
                RowButton("Open steward's desk",ShowPause,40);return;
            }
            var m=game.Sim.Military;
            BoundArmyLabel(()=>m.LastEvent,12,muted,60);
            BoundArmyLabel(()=>m.Status=="won"?"Riverhold holds the road. The Toll War is won.":m.RaidsSpawned>=3?"All three parties have arrived. No further raids.\nRegroup and clear the camp if the road is not secure.":$"Next raid in {Math.Max(0,m.NextRaidAt-m.Clock):0} game seconds\nTraining and raids follow game speed and pause.",12,gold,45);
            BoundArmyLabel(()=>$"{game.Sim.MilitaryReservedWorkers()} citizens in service\nDaily upkeep: {game.Sim.DailyMilitaryGold():0.00} gold + {game.Sim.DailyMilitaryFood():0.00} extra food",13,parchment,48);
            RowLabel("RECRUITMENT",12,gold,25);
            RowLabel("Each squad immediately reserves 4 workers. Six squads including recruits is the limit. One squad trains at a time; the rest wait in queue.",13,muted,52);
            RowLabel($"Spearmen · {Simulation.RecruitmentSeconds("spearmen")*(m.BarracksLevel>=2 ? .75 : 1):0} game seconds\n{Simulation.RecruitmentGold("spearmen"):0} gold · {Simulation.RecruitmentWood("spearmen"):0} timber · {Simulation.RecruitmentIron("spearmen"):0} iron · 4 workers",14,parchment,48);
            RowButton("Recruit spearmen",()=>game.Act(game.Sim.RecruitSquad("spearmen"),"Spearmen joined the training queue."),36);
            RowLabel($"Archers · {Simulation.RecruitmentSeconds("archers")*(m.BarracksLevel>=2 ? .75 : 1):0} game seconds\n{Simulation.RecruitmentGold("archers"):0} gold · {Simulation.RecruitmentWood("archers"):0} timber · {Simulation.RecruitmentIron("archers"):0} iron · 4 workers",14,parchment,48);
            RowButton("Recruit archers",()=>game.Act(game.Sim.RecruitSquad("archers"),"Archers joined the training queue."),36);
            foreach(var queued in m.Queue)
            {
                int id=queued.Id;var card=RowBox(86);
                var title=Label(card,"",13,parchment,new Rect(10,8,320,29));
                var track=Box(card,"Training progress",ink);Place(track,new Rect(10,45,202,9));track.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
                var fill=Box(track,"Training fill",gold);fill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
                var cancel=Button(card,"Cancel recruitment "+id,new Rect(225,39,99,31),()=>game.Act(game.Sim.CancelRecruitment(id),"Recruitment cancelled. Reserved workers released."));
                cancel.GetComponentInChildren<TextMeshProUGUI>().text="Cancel";
                Action update=()=>
                {
                    var current=game.Sim.Military.Queue.Find(q=>q.Id==id);if(current==null||title==null)return;
                    bool training=game.Sim.Military.Queue.Count>0&&game.Sim.Military.Queue[0].Id==id;
                    title.text=$"#{id} {GoodName(current.Kind)} · {(training?"training":"queued")} · {Math.Ceiling(current.SecondsLeft):0}s";
                    float progress=current.TotalSeconds>0?(float)(1-current.SecondsLeft/current.TotalSeconds):0;
                    Anchor(fill,0,0,Mathf.Clamp01(progress),1,0,0,0,0);
                };armyBindings.Add(update);update();
            }
            RowLabel("SQUAD ORDERS",12,gold,24);
            BoundArmyLabel(()=>game.Military==null?"Select a squad.":game.Military.Status,13,parchment,53);
            RowButtons(new[]{"Move","Attack","Patrol"},new Action[]{()=>ArmyCommand("Move"),()=>ArmyCommand("Attack"),()=>ArmyCommand("Patrol")});
            RowButtons(new[]{"Escort","Rally","Hold position"},new Action[]{()=>ArmyCommand("Escort"),()=>ArmyCommand("Rally"),()=>ArmyCommand("Hold position")});
            RowButtons(new[]{"Retreat","Demobilize"},new Action[]{()=>ArmyCommand("Retreat"),()=>ArmyCommand("Demobilize")});
            RowButton("Select all squads",()=>ArmyCommand("Select all squads"),36);
            foreach(var squad in m.Squads.Where(s=>!s.Hostile&&s.HP>0))
            {
                int id=squad.Id;var button=Button(content,"Squad "+id,new Rect(),()=>{game.Military?.SelectSquad(id);RefreshCounters();});
                button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=70;
                var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=13;text.alignment=TextAlignmentOptions.Left;
                Action update=()=>
                {
                    if(text==null)return;var current=game.Sim.Military.Squads.Find(s=>s.Id==id);if(current==null)return;
                    bool selected=game.Military!=null&&game.Military.SelectedSquads.Contains(id);
                    text.text=$"{(selected?"› ":"")}#{id} {GoodName(current.Kind)}   {current.Members} soldiers\nHP {current.HP:0}/{current.MaxHP:0} · Morale {current.Morale:0} · XP {current.Experience:0}\n{current.Order.Replace('_',' ')}{(current.Recovering?" · recovering":"")}";
                    StyleSelected(button,selected);
                };armyBindings.Add(update);update();
            }
            if(!m.Squads.Any(s=>!s.Hostile&&s.HP>0))RowLabel("No field squads. Train your first guard above.",13,muted,48);
            RowLabel("MILITARY IMPROVEMENTS",12,gold,27);
            RowLabel($"Barracks level {m.BarracksLevel}/2\n100 gold · 24 timber · 12 stone\nLevel 2 trains recruits 25% faster.",13,parchment,77);
            RowButton("Upgrade barracks",()=>game.Act(game.Sim.UpgradeMilitary("barracks"),"Barracks upgraded."),38);
            RowLabel($"Equipment level {m.EquipmentLevel}/1\n90 gold · 12 iron · 4 tools\nAll militia must be unwounded and within 5m of the Muster Yard.",13,parchment,91);
            RowButton("Upgrade equipment",()=>game.Act(game.Sim.UpgradeMilitary("equipment"),"Military equipment upgraded."),38);
        }
        void Orders()
        {
            RowLabel("A PROMISE HAS AN ARRIVAL DATE",16,gold,32);
            RowLabel("Accepted orders reserve the buyer's gold. Keep food at home; cargo is paid when it arrives.",13,muted,55);
            var orders=game.Sim.State.Contracts.OrderBy(c=>c.Status=="active"?0:c.Status=="in_transit"?1:c.Status=="offered"?2:3).ThenByDescending(c=>c.Id);
            foreach(var c in orders)
            {
                bool offered=c.Status=="offered";
                var card=RowBox(offered?284:172);
                Label(card,$"#{c.Id} {game.Sim.State.Towns[c.Town].Name} · {c.Status.Replace('_',' ').ToUpperInvariant()}",15,gold,new Rect(12,10,318,27));
                if(c.Status=="offered")
                {
                    double trust=game.Sim.Relation(c.Town).Trust;
                    int arrivalDeadline=game.Sim.State.Day+(trust<40?20:28);
                    // Use the same cent rounding and deadline rules as AcceptContract.
                    double normalPrice=Math.Floor(c.UnitPrice/.01+.5)*.01;
                    double negotiatedPrice=Math.Floor(c.UnitPrice*1.08/.01+.5)*.01;
                    string terms=$"{c.Quantity:0.##} {GoodName(c.Good)} · Offer expires day {c.Deadline}\n\nAccept: {normalPrice*c.Quantity:0.00} gold + {c.Reward:0.00} bonus\nArrive by day {arrivalDeadline} if accepted now.\n\n";
                    terms+=trust>=40
                        ?$"Negotiate: {negotiatedPrice*c.Quantity:0.00} gold + {c.Reward:0.00} bonus\nArrive by day {arrivalDeadline-3} if negotiated now.\nNegotiating accepts this promise."
                        :"Negotiation needs at least 40 trust.\nKeep a delivery to rebuild relations.";
                    Label(card,terms,13,parchment,new Rect(12,42,318,172));
                    Label(card,"Bonus requires arrival on or before the deadline.",11,muted,new Rect(12,221,318,20));
                    var accept=Button(card,"Accept",new Rect(12,241,140,34),()=>game.Act(game.Sim.AcceptContract(c.Id),"Promise accepted. Dispatch when the route and cargo are ready."));
                    var negotiate=Button(card,"Negotiate",new Rect(163,241,151,34),()=>game.Act(game.Sim.AcceptContract(c.Id,true),"Promise accepted at the higher price and earlier deadline shown."));
                    accept.interactable=game.Sim.State.Day<=c.Deadline;
                    negotiate.interactable=accept.interactable&&trust>=40;
                }
                else
                {
                    Label(card,$"{c.Quantity:0.##} {GoodName(c.Good)}\n{c.UnitPrice*c.Quantity:0.00} gold + {c.Reward:0.00} on-time bonus\nArrival deadline: day {c.Deadline}",14,parchment,new Rect(12,42,318,75));
                    if(c.Status=="active")Button(card,"Dispatch cargo",new Rect(12,125,302,34),()=>game.Act(game.Sim.DispatchContract(c.Id),"Cargo dispatched. Payment follows arrival.","trade"));
                    else Label(card,c.Status=="in_transit"?"See On the Road below for this shipment's ETA.":c.Status=="fulfilled"?"Recorded in the public ledger.":"A missed promise can be followed by a kept one.",12,muted,new Rect(12,123,302,40));
                }
            }
            RowLabel($"ON THE ROAD · {game.Sim.State.Caravans.Count}",16,gold,32);
            if(game.Sim.State.Caravans.Count==0)RowLabel("No cargo traveling. Market shipments, contract cargo and relief will appear here.",13,muted,60);
            foreach(var cargo in game.Sim.State.Caravans.OrderBy(c=>c.DaysLeft))
            {
                string purpose=cargo.ContractId>=1?"Contract #"+cargo.ContractId:cargo.PlayerOrder?"Market shipment":cargo.From==0||cargo.To==0?"Relief delivery":"Town trade";
                string routeName=cargo.Route=="ferry"?"Ferry":cargo.Route=="bridge"?"Shared bridge":cargo.Route=="lower_ridge_road"?"Lower Ridge Road":cargo.Route.Replace('_',' ');
                var shipment=RowBox(100);
                Label(shipment,$"{cargo.Quantity:0.##} {GoodName(cargo.Good)} · {purpose}\n{game.Sim.State.Towns[cargo.From].Name} → {game.Sim.State.Towns[cargo.To].Name}\nRoute: {routeName}\nETA day {game.Sim.State.Day+cargo.DaysLeft} · {cargo.DaysLeft} days left",13,parchment,new Rect(12,10,318,80));
            }
        }
        void Market()
        {
            RowLabel("MARKET & FREIGHT",18,gold,32);RowLabel("Every town has its own stock and treasury. Prices follow local scarcity.",13,muted,50);
            RowButtons(new[]{"Ironvale","Pinewatch"},new Action[]{()=>{town=1;RefreshPanel();},()=>{town=2;RefreshPanel();}});
            var t=game.Sim.State.Towns[town];RowLabel($"{t.Name}   ·   Treasury {t.Gold:0} gold",16,parchment,36);
            for(int i=0;i<Simulation.Goods.Length;i++){int g=i;string id=Simulation.Goods[i];RowButton($"{(good==i?"› ":"")}{GoodName(id)}    stock {t.Stock(id):0.#}    ask {game.Sim.TradeQuote(town,id,true):0.00}",()=>{good=g;RefreshPanel();},34);}
            RowButtons(new[]{"Buy imports","Sell exports"},new Action[]{()=>{buy=true;RefreshPanel();},()=>{buy=false;RefreshPanel();}});
            RowLabel($"{(buy?"Buy":"Sell")} {quantity} {GoodName(Simulation.Goods[good])}",17,gold,38);
            RowButtons(new[]{"−","+","Fill route"},new Action[]{()=>{quantity=Math.Max(1,quantity-1);RefreshPanel();},()=>{quantity=Math.Min(40,quantity+1);RefreshPanel();},()=>{quantity=Math.Max(1,(int)game.Sim.RouteInfo().Capacity);RefreshPanel();}});
            RowLabel($"Riverhold: {game.Sim.State.Towns[0].Stock(Simulation.Goods[good]):0.##} in store\nIncoming: {game.Sim.IncomingQuantity(0,Simulation.Goods[good]):0.##} · Capacity: {game.Sim.StorageCapacity():0}",13,muted,55);
            double price=game.Sim.TradeQuote(town,Simulation.Goods[good],buy)*quantity;var route=game.Sim.RouteInfo();
            RowLabel($"Goods {price:0.00} gold  ·  Freight {route.Fee:0}\nJourney {route.Days} days · Limit {route.Capacity:0} per cart",14,parchment,59);
            RowButton(buy?"Buy & send cargo":"Sell & send cargo",()=>game.Act(game.Sim.DispatchTrade(town,Simulation.Goods[good],quantity,buy),"Trade settled. Follow the cargo on the road.","trade"),42);
            RowLabel($"Protected food: {game.Sim.State.ReserveDays} days\nExports cannot consume this reserve.",13,muted,57);
            RowButtons(new[]{"Reserve −","Reserve +"},new Action[]{()=>game.Act(game.Sim.SetReserveDays(game.Sim.State.ReserveDays-1),"Food reserve updated."),()=>game.Act(game.Sim.SetReserveDays(game.Sim.State.ReserveDays+1),"Food reserve updated.")});
        }
        void Crossing()
        {
            var r=game.Sim.RouteInfo();RowLabel("THE RIVER CROSSING",18,gold,32);RowLabel($"{r.Name}\n{(r.Ready?$"{r.Capacity:0} cargo · {r.Days} days · {r.Fee:0} gold freight":"No regular freight yet")}",15,parchment,69);
            if(r.DaysLeft>0)RowLabel($"Building {r.Pending}: {r.DaysLeft} days remaining",16,gold,44);
            RowLabel("HIRED FERRY\n45 gold · 10 timber · 2 tools\nReady in 2 days. 12 cargo per cart; 3 gold freight. A modest start that leaves money for trade.",14,parchment,112);
            RowButton("Commission ferry",()=>game.Act(game.Sim.ChooseCrossing("ferry"),"Ferry works commissioned.","build"),42);
            RowLabel("STONE BRIDGE\n120 gold · 30 timber · 12 stone · 6 tools\nReady in 8 days. 40 cargo per cart; no freight fee. The ferry continues during reconstruction.",14,parchment,120);
            RowButton("Reconstruct bridge",()=>game.Act(game.Sim.ChooseCrossing("bridge"),"Bridge foundations under reconstruction.","build"),42);
            RowLabel("THE WINTER ROAD\nThe High Pass closes on day 120. River traffic slows and harvests shrink. The Lower Ridge remains a relief route.",14,muted,106);
            RowButton("Ask Pinewatch for relief",()=>game.Act(game.Sim.RequestRelief(),"Pinewatch has answered. Any donated cargo still needs time to arrive."),42);
        }
        void Council()
        {
            RowLabel("THE COUNCIL LEDGER",18,gold,32);
            foreach(var r in game.Sim.State.Relations)RowLabel($"{game.Sim.State.Towns[r.Town].Name} · trust {r.Trust:0}\n{r.Fulfilled} promises kept / {r.Missed} missed",15,parchment,66);
            RowLabel("Their offers reflect supplies, money and trust. Early arrival earns a bonus; a missed deadline is remembered.",13,muted,72);
            foreach(var entry in game.Media.All)
                if(entry.id!="route_report"&&(entry.id=="opening"||entry.id=="prologue"||game.Sim.State.StoryFlags.Contains(entry.id))){string id=entry.id;RowButton(entry.title,()=>ShowStory(id),42);}
            RowLabel("RECENT TOWN HISTORY",15,gold,35);
            foreach(string e in game.Sim.State.Events.Take(16))RowLabel(e,13,parchment,61);
        }
        public void ShowMain()
        {
            game.InMenu=true;hud.gameObject.SetActive(false);var box=OpenModal(1000,650);
            Label(box,"THE VEYRAN BASIN",12,gold,new Rect(45,40,460,26)).characterSpacing=3;
            Label(box,"LIVING\nEMPIRES",50,parchment,new Rect(42,99,470,135)).characterSpacing=3;
            Label(box,"THE BROKEN ACCORD",16,gold,new Rect(46,252,455,35)).characterSpacing=2;
            Label(box,"A town to build.\nA river to cross.\nA promise worth defending.",22,parchment,new Rect(46,320,450,116));
            var creed=Box(box,"Steward's charter",parchment);Place(creed,new Rect(45,482,435,113));
            Label(creed,"RIVERHOLD / FIRST STEWARD",11,paperInk,new Rect(18,15,397,25)).characterSpacing=1;
            Label(creed,"Twelve years after the River Crown fell,\nthe valley's future belongs to those who stay.",16,paperInk,new Rect(18,48,397,53));
            var choices=Box(box,"Choose a chapter",panel);Place(choices,new Rect(527,39,429,557));Border(choices);
            Label(choices,"CHOOSE YOUR CHAPTER",12,gold,new Rect(24,20,381,27)).characterSpacing=1.7f;
            Label(choices,"FIRST WINTER DELIVERY",19,parchment,new Rect(24,72,381,31));
            Label(choices,"Build the supply chain, restore a crossing\nand keep the valley fed through winter.",14,muted,new Rect(24,112,381,55));
            Button(choices,"Begin a new charter",new Rect(24,178,381,44),()=>{tab="";objectiveKey="";game.NewGame();});
            Label(choices,"THE TOLL WAR",19,parchment,new Rect(24,249,381,31));
            Label(choices,"An established river town faces three raiding\nparties. Raise a guard and defend its trade.",14,muted,new Rect(24,289,381,55));
            Button(choices,"The Toll War",new Rect(24,351,381,44),()=>{tab="";objectiveKey="";game.StartMilitaryChapter();SetMode(true,false);OpenTab("");});
            var resume=Button(choices,"Continue saved charter",new Rect(24,421,183,47),game.LoadFirstWinterChapter);resume.interactable=game.HasFirstWinterSave;resume.GetComponentInChildren<TextMeshProUGUI>().text="Continue\nFirst Winter";
            var warResume=Button(choices,"Continue The Toll War",new Rect(217,421,188,47),game.LoadMilitaryChapter);warResume.interactable=game.HasMilitarySave;warResume.GetComponentInChildren<TextMeshProUGUI>().text="Continue\nThe Toll War";
            Button(choices,"Settings",new Rect(24,490,183,39),()=>ShowSettings(true));
            Button(choices,"Quit",new Rect(217,490,188,39),Quit);
        }
        public void ShowPause()
        {
            game.Paused=true;var box=OpenModal(560,485);Label(box,"THE STEWARD'S DESK",25,gold,new Rect(35,30,495,49));
            Button(box,"Return to town",new Rect(35,104,490,46),CloseModal);Button(box,"Save charter",new Rect(35,162,490,46),()=>game.Save());
            Button(box,"Settings",new Rect(35,220,490,46),()=>ShowSettings(false));Button(box,"New charter",new Rect(35,278,490,46),ConfirmNew);
            Button(box,"Main menu",new Rect(35,336,490,46),ShowMain);Button(box,"Quit",new Rect(35,394,490,46),Quit);
        }
        void ConfirmNew(){var b=OpenModal(570,305);Label(b,"BEGIN AGAIN?",26,gold,new Rect(30,30,510,47));Label(b,"Your current unsaved progress will be replaced. Your manual save stays available until you save again.",17,parchment,new Rect(30,95,510,90));Button(b,"Begin",new Rect(30,222,240,45),game.NewGame);Button(b,"Cancel",new Rect(285,222,255,45),ShowPause);}
        void ShowSettings(bool fromMain)
        {
            var b=OpenModal(650,575);Label(b,"SOUND & DISPLAY",26,gold,new Rect(35,30,580,50));
            if(game.Media.HasRecordings)Volume(b,"Council voices",110,game.Media.VoiceVolume,v=>game.Media.VoiceVolume=v);
            else Label(b,"COUNCIL ARCHIVE\nText edition · Read every conversation in the council ledger.",16,muted,new Rect(35,110,580,65));
            Volume(b,"Effects",193,game.Media.EffectsVolume,v=>game.Media.EffectsVolume=v);
            Volume(b,"River ambience",276,game.Media.AmbienceVolume,v=>game.Media.AmbienceVolume=v);
            Label(b,"Graphics",17,parchment,new Rect(35,365,580,25));Button(b,"Economy",new Rect(35,403,270,44),()=>{game.SetQuality(0);game.LastMessage="Economy graphics enabled.";});Button(b,"Balanced",new Rect(320,403,295,44),()=>{game.SetQuality(1);game.LastMessage="Balanced graphics enabled.";});
            Button(b,"Back",new Rect(35,493,580,44),()=>{PlayerPrefs.Save();if(fromMain)ShowMain();else ShowPause();});
        }
        void Volume(RectTransform b,string title,float y,float value,Action<float> set)
        {
            var label=Label(b,$"{title} · {value*100:0}%",17,parchment,new Rect(35,y,580,30));
            var track=Box(b,title,teal);Place(track,new Rect(35,y+39,580,10));var slider=track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var handle=Box(track,"Handle",gold);Place(handle,new Rect(0,-6,18,23));slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();slider.minValue=0;slider.maxValue=1;slider.value=value;
            slider.onValueChanged.AddListener(v=>{set(v);game.Media.ApplyVolumes();label.text=$"{title} · {v*100:0}%";});
        }
        public void ShowStory(string id)
        {
            var e=game.Media.Get(id);if(e==null)return;
            if(!game.Media.HasRecording(id)){ShowTextStory(e);return;}
            game.Paused=true;hud.gameObject.SetActive(true);var b=OpenModal(1130,760);storyId=id;
            Label(b,"RIVERHOLD COUNCIL",13,gold,new Rect(32,23,1000,25));Label(b,e.title,29,parchment,new Rect(32,62,1040,47));
            var texture=string.IsNullOrEmpty(e.image)?null:Resources.Load<Texture2D>(e.image);if(texture!=null)
            {
                var image=Box(b,"Character study",new Color(.12f,.17f,.18f));Place(image,new Rect(32,133,348,358));
                var picture=new GameObject("Preserved artwork",typeof(RectTransform),typeof(UnityEngine.UI.RawImage),typeof(UnityEngine.UI.AspectRatioFitter));picture.transform.SetParent(image,false);
                var raw=picture.GetComponent<UnityEngine.UI.RawImage>();raw.texture=texture;raw.raycastTarget=false;
                var fit=picture.GetComponent<UnityEngine.UI.AspectRatioFitter>();fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)texture.width/texture.height;
            }
            if(texture!=null)Label(b,"From the Riverhold council archive",12,muted,new Rect(32,505,348,24));
            var transcript=Scroll(b,132,200,texture!=null?410:32,32);var text=Label(transcript,e.text,17,parchment,new Rect());text.overflowMode=TextOverflowModes.Overflow;var le=text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();le.preferredHeight=Math.Max(180,text.GetPreferredValues(e.text,texture!=null?688:1066,float.PositiveInfinity).y+12);le.flexibleWidth=1;
            speakerText=Label(b,e.speaker,17,gold,new Rect(32,581,1060,28));captionText=Label(b,"",17,parchment,new Rect(32,616,1060,67));
            Button(b,"Replay recording",new Rect(32,700,243,39),()=>game.Media.Play(id));
            Button(b,"Return to town",new Rect(820,700,278,39),CloseModal);
            if(id=="opening")Label(b,"The ferry crew is ready; regular freight starts after landing construction.",12,muted,new Rect(32,539,1060,30));
            game.Media.Play(id);
        }
        void ShowTextStory(StoryEvent entry)
        {
            game.Paused=true;hud.gameObject.SetActive(true);var box=OpenModal(1040,760);storyId=entry.id;
            Label(box,"RIVERHOLD / COUNCIL ARCHIVE",12,gold,new Rect(44,29,952,25)).characterSpacing=2;
            Label(box,entry.title,29,parchment,new Rect(44,70,952,48));
            Label(box,"TEXT EDITION · Complete dialogue, at your own pace.",13,muted,new Rect(44,128,952,27));
            Rule(box,44,173,44,1);
            var transcript=Scroll(box,195,126,44,44);transcript.name="Council transcript";
            if(entry.lines!=null&&entry.lines.Length>0)
            {
                foreach(var line in entry.lines)
                {
                    TranscriptLabel(transcript,line.speaker??entry.speaker,"Transcript speaker",13,gold,28);
                    TranscriptLabel(transcript,line.text,"Transcript dialogue",18,parchment,24);
                }
            }
            else
            {
                TranscriptLabel(transcript,entry.speaker,"Transcript speaker",13,gold,28);
                TranscriptLabel(transcript,entry.text,"Transcript dialogue",18,parchment,24);
            }
            Rule(box,44,652,44,1);
            Label(box,entry.id=="opening"?"The ferry crew is ready. Build the landing to open regular freight.":"Scroll to read the complete council record.",12,muted,new Rect(44,674,640,52));
            Button(box,"Return to town",new Rect(718,694,278,42),CloseModal);
            game.Media.Play(entry.id);
        }
        void TranscriptLabel(RectTransform parent,string value,string name,int size,Color color,float minimumHeight)
        {
            var label=Label(parent,value??"",size,color,new Rect());label.name=name;label.richText=false;
            label.overflowMode=TextOverflowModes.Overflow;
            var layout=label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight=Math.Max(minimumHeight,label.GetPreferredValues(label.text,952,float.PositiveInfinity).y+12);
            layout.flexibleWidth=1;
        }
        public void CloseModal(){game.Media.Stop();storyId="";captionText=null;speakerText=null;modal.gameObject.SetActive(false);hud.gameObject.SetActive(!game.InMenu);Refresh();}
        RectTransform OpenModal(float width,float height)
        {
            game.Media.Stop();storyId="";captionText=null;speakerText=null;Clear(modal);modal.gameObject.SetActive(true);
            var b=Box(modal,"Dialog",ink);b.anchorMin=b.anchorMax=b.pivot=new Vector2(.5f,.5f);b.sizeDelta=new Vector2(width,height);b.anchoredPosition=Vector2.zero;Border(b);Rule(b,0,0,0,3);return b;
        }
        void Quit(){if(Application.isEditor){game.LastMessage="Stop Play Mode in Unity to close the preview.";CloseModal();}else Application.Quit();}
        static string GoodName(string g)=>g=="wood"?"Timber":char.ToUpperInvariant(g[0])+g.Substring(1);
        static string Cost(BuildingDefinition d)=>d.GoldCost+" gold, "+string.Join(", ",d.Cost.Select(x=>$"{x.Amount:0} {GoodName(x.Good)}"));
        static string Recipe(BuildingDefinition d)=>string.Join(" + ",d.Inputs.Select(x=>$"{x.Amount:0.#} {x.Good}"))+(d.Outputs.Count>0?" → "+string.Join(" + ",d.Outputs.Select(x=>$"{x.Amount:0.#} {x.Good}"))+" / day":"");
        RectTransform Box(Transform parent,string name,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var img=go.GetComponent<UnityEngine.UI.Image>();img.color=color;return go.GetComponent<RectTransform>();}
        void Icon(Transform parent,string id,Rect rect){var icon=Box(parent,id+" icon",Color.white);Place(icon,rect);var image=icon.GetComponent<UnityEngine.UI.Image>();image.sprite=WorldArt.Icon(id);image.preserveAspect=true;image.raycastTarget=false;}
        TextMeshProUGUI Label(Transform parent,string text,int size,Color color,Rect rect)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.font=interfaceFont;t.text=text;t.fontSize=size;t.color=color;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;Place(go.GetComponent<RectTransform>(),rect);return t;
        }
        UnityEngine.UI.Button Button(Transform p,string text,Rect rect,Action click)
        {
            var r=Box(p,text,teal);Place(r,rect);var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();var colors=button.colors;colors.highlightedColor=new Color(1.3f,1.3f,1.2f);colors.pressedColor=new Color(.76f,.81f,.84f);colors.disabledColor=new Color(.47f,.5f,.52f,.65f);colors.fadeDuration=.1f;button.colors=colors;
            var label=Label(r,text,14,parchment,new Rect());Stretch(label.rectTransform,6,3,6,3);label.alignment=TextAlignmentOptions.Center;
            button.onClick.AddListener(()=>{game.Media.Sound("click");click();EventSystem.current?.SetSelectedGameObject(null);});return button;
        }
        RectTransform Scroll(Transform p,float top,float bottom,float left,float rightOffset)
        {
            var r=Box(p,"Scroll",Color.clear);Stretch(r,left,top,rightOffset,bottom);var scroll=r.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            var view=Box(r,"Viewport",Color.white);Stretch(view,0,0,0,0);view.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var c=new GameObject("Content",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup),typeof(UnityEngine.UI.ContentSizeFitter)).GetComponent<RectTransform>();c.SetParent(view,false);c.anchorMin=new Vector2(0,1);c.anchorMax=new Vector2(1,1);c.pivot=new Vector2(.5f,1);c.anchoredPosition=Vector2.zero;c.sizeDelta=Vector2.zero;
            var layout=c.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=8;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;c.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=view;scroll.content=c;return c;
        }
        void RowLabel(string text,int size,Color color,float height){var t=Label(content,text,size,color,new Rect());t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;}
        RectTransform RowBox(float height){var r=Box(content,"Entry",new Color(.115f,.175f,.23f));r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;return r;}
        void RowButton(string text,Action a,float height){var b=Button(content,text,new Rect(),a);b.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;}
        void RowButtons(string[] labels,Action[] actions){var r=RowBox(40);for(int i=0;i<labels.Length;i++){float width=326f/labels.Length;Button(r,labels[i],new Rect(i*width,0,width-5,39),actions[i]);}}
        static void Clear(Transform t){for(int i=t.childCount-1;i>=0;i--){t.GetChild(i).gameObject.SetActive(false);Destroy(t.GetChild(i).gameObject);}}
        static void Place(RectTransform r,Rect rect){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(rect.x,-rect.y);r.sizeDelta=new Vector2(rect.width,rect.height);}
        static void Stretch(RectTransform r,float left,float top,float right,float bottom){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
        static void Anchor(RectTransform r,float ax0,float ay0,float ax1,float ay1,float x0,float y0,float x1,float y1){r.anchorMin=new Vector2(ax0,ay0);r.anchorMax=new Vector2(ax1,ay1);r.offsetMin=new Vector2(x0,y0);r.offsetMax=new Vector2(x1,y1);}
    }
}
