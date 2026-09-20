using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LivingEmpires
{
    /// <summary>
    /// Destructive to the current unsaved QA session: ends at a fresh, paused charter.
    /// Never invokes the manual-slot Save/Load buttons or changes their files.
    /// Invoke with game.StartCoroutine(UnityIntegrationVerification.Run(game)).
    /// </summary>
    public static class UnityIntegrationVerification
    {
        public static bool Running { get; private set; }
        public static bool Completed { get; private set; }
        public static bool Passed { get; private set; }
        public static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,"..","Reports","integration-tests.txt"));

        public static IEnumerator Run(GameController game)
        {
            if(Running)throw new InvalidOperationException("Integration verification is already running.");
            if(game==null||game.BenchmarkRunning)throw new InvalidOperationException("A ready game outside the performance benchmark is required.");
            Running=true;Completed=false;Passed=false;
            var runner=new Runner(game);
            var tests=runner.Tests();
            try
            {
                while(true)
                {
                    object wait=null;bool more=false;
                    try{more=tests.MoveNext();if(more)wait=tests.Current;}
                    catch(Exception error){runner.Check(false,"Unexpected integration failure: "+error);}
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
            readonly List<string> entries=new List<string>();
            readonly string qaPath;
            readonly string manualDigest, militaryDigest;
            int checks,clicks;
            public int Failures { get; private set; }
            Canvas canvas;
            public Runner(GameController instance)
            {
                game=instance;
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                qaPath=Path.Combine(Path.GetDirectoryName(ReportPath),"qa-integration-"+Guid.NewGuid().ToString("N")+".json");
                manualDigest=Digest(game.CharterSavePath);militaryDigest=Digest(game.MilitarySavePath);
            }
            public void Check(bool ok,string description)
            {checks++;if(!ok)Failures++;entries.Add((ok?"PASS: ":"FAIL: ")+description);}
            void Require(bool ok,string description)
            {Check(ok,description);if(!ok)throw new InvalidOperationException(description);}

            public IEnumerator Tests()
            {
                game.UI.ShowMain();yield return null;yield return null;
                canvas=UnityEngine.Object.FindObjectsByType<Canvas>().SingleOrDefault(c=>c.name=="Riverhold interface");
                Require(canvas!=null,"Runtime Riverhold canvas exists.");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.isActiveAndEnabled)==1
                    &&canvas.GetComponents<UnityEngine.UI.GraphicRaycaster>().Count(r=>r.isActiveAndEnabled)==1,
                    "Exactly one active EventSystem and one interface GraphicRaycaster.");
                Click("Settings");yield return null;
                Check(HasText("SOUND & DISPLAY")&&canvas.GetComponentsInChildren<UnityEngine.UI.Slider>().Length==(game.Media.HasRecordings?3:2),"Settings exposes controls only for available sound channels.");
                var effects=canvas.GetComponentsInChildren<UnityEngine.UI.Slider>().Single(s=>s.name=="Effects");
                float oldVolume=effects.value;effects.value=oldVolume>.5f?.25f:.75f;
                Check(Mathf.Abs(game.Media.Effects.volume-effects.value)<.001f,"Settings slider changes the actual effects AudioSource volume.");
                effects.value=oldVolume;Click("Back");yield return null;
                Click("Begin a new charter");yield return null;yield return null;
                Check(!game.InMenu&&game.Paused&&game.Modal&&game.Sim.State.Day==1,"New charter button opens the paused opening story on day one.");

                var story=game.Media.Get("opening");
                Require(story!=null&&story.lines!=null&&story.lines.Length>0,"Opening narrative includes complete dialogue and named speakers.");
                if(game.Media.HasRecording(story.id))
                {
                    Check(game.Media.Voice.clip!=null&&game.Media.Voice.isPlaying,"Available opening recording plays through the actual voice AudioSource.");
                    var transcript=Texts().FirstOrDefault(t=>t.text==story.text);
                    Check(transcript!=null&&story.lines.All(l=>story.text.Contains(l.text)),"Visible transcript contains every opening spoken line in full.");
                    var caption=Field<TextMeshProUGUI>("captionText");var speaker=Field<TextMeshProUGUI>("speakerText");
                    Require(caption!=null&&speaker!=null,"Recorded story caption and speaker controls exist.");
                    game.Media.Voice.time=story.lines[0].start+.15f;yield return null;yield return null;
                    Check(caption.text==story.lines[0].text&&speaker.text==story.lines[0].speaker,"First timed caption uses the full line and the correct speaker.");
                    game.Media.Voice.time=story.lines[story.lines.Length-1].start+.15f;yield return null;yield return null;
                    Check(caption.text==story.lines[story.lines.Length-1].text,"Last opening cue remains reachable with its full caption.");
                    Canvas.ForceUpdateCanvases();
                    Check(transcript!=null&&transcript.font!=null&&transcript.rectTransform.rect.width>500
                        &&transcript.preferredHeight<=transcript.rectTransform.rect.height+2
                        &&caption.font!=null&&story.lines.All(l=>caption.GetPreferredValues(l.text,caption.rectTransform.rect.width,float.PositiveInfinity).y<=caption.rectTransform.rect.height+2),
                        "Transcript and all opening captions have fonts and sufficient text rectangles.");
                    var picture=canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().FirstOrDefault(i=>i.name=="Preserved artwork");
                    if(picture!=null)Check(picture.texture!=null&&Mathf.Abs(picture.rectTransform.rect.width/picture.rectTransform.rect.height-(float)picture.texture.width/picture.texture.height)<.02f,"Available story artwork retains its source aspect ratio.");
                    Click("Replay recording");yield return null;
                    Check(game.Media.Voice.isPlaying&&game.Media.Voice.time<2,"Replay button restarts the real recording.");
                }
                else
                {
                    Check(!game.Media.Voice.isPlaying&&game.Media.Voice.clip==null&&game.Media.Caption()==null,"Text story does not simulate voice playback or timed captions.");
                    Check(HasText("TEXT EDITION · Complete dialogue, at your own pace.")&&!canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="Replay recording"),"Text edition is identified and has no unavailable replay control.");
                    var transcript=Named("Council transcript");
                    var dialogue=transcript.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="Transcript dialogue").ToArray();
                    var speakers=transcript.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="Transcript speaker").ToArray();
                    Check(dialogue.Select(t=>t.text).SequenceEqual(story.lines.Select(l=>l.text))&&speakers.Select(t=>t.text).SequenceEqual(story.lines.Select(l=>l.speaker)),"Text transcript preserves every opening line and its speaker in story order.");
                    Canvas.ForceUpdateCanvases();
                    Check(dialogue.Length>0&&dialogue.All(t=>t.font!=null&&t.rectTransform.rect.width>900&&t.preferredHeight<=t.rectTransform.rect.height+2),"Text transcript uses the full dialog width and gives every line sufficient height.");
                    Check(!canvas.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Character study"),"Text dialog does not reserve an empty artwork column.");
                    var scroll=transcript.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                    Require(scroll!=null,"Complete dialogue is in a scrollable council record.");
                    scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                    Check(dialogue.Length>0&&Contained(dialogue[dialogue.Length-1].rectTransform,scroll.viewport),"Scrolling reaches the full final opening line.");
                }
                Click("Return to town");yield return null;
                Check(!game.Modal&&!game.Media.Voice.isPlaying&&game.Sim.State.StoryFlags.Contains("opening"),"Return closes the story, stops voice playback, and preserves opening acknowledgement.");

                var charter=Named("Charter");var drawer=Named("Trade and council",true);
                var rows=charter.GetComponentsInChildren<TextMeshProUGUI>().Skip(2).ToArray();
                Check(!drawer.gameObject.activeInHierarchy&&!Named("Inspector",true).gameObject.activeInHierarchy,"Fresh settlement keeps the management drawer closed until requested.");
                Check(rows.Length==6&&rows.All(t=>Contained(t.rectTransform,charter)),"All six compact charter rows fit inside their panel.");
                float clearFraction=UncoveredHudFraction();
                Check(clearFraction>=2f/3f,$"Default HUD leaves {clearFraction*100:0.00}% of the viewport outside its visible panel rectangles (target at least two thirds; decorative shadows excluded).");
                Check(Texts().All(t=>t.font!=null&&t.rectTransform.rect.width>0&&t.rectTransform.rect.height>0),"Active HUD labels have assigned fonts and nonzero rectangles.");
                Check(HasText("01  /  FLOUR BEFORE PROMISES",true),"Fresh tutorial asks for the mill first.");
                Click("Inspect");yield return null;
                var inspector=Named("Inspector");
                Check(drawer.gameObject.activeInHierarchy&&inspector.gameObject.activeInHierarchy
                    &&Contained(inspector,drawer)&&!ScreenRect(charter).Overlaps(ScreenRect(inspector)),
                    "Actual Inspect tab opens the contextual inspector within its drawer without covering the charter.");
                Click("Close drawer");yield return null;
                Check(!drawer.gameObject.activeInHierarchy,"Actual Close drawer button returns the space to the world.");
                Click("Mill\n65 gold");
                Check(game.BuildType=="mill","Mill build-ribbon button selects the correct construction type.");
                int count=game.Sim.State.Buildings.Count;double gold=game.Sim.State.Towns[0].Gold;
                Require(game.Act(game.Sim.PlaceBuilding("mill",11,12),"QA mill built."),"Mill placement succeeds through the normal simulation action.");
                game.CancelBuild();yield return null;
                Check(game.Sim.State.Buildings.Count==count+1&&Math.Abs(game.Sim.State.Towns[0].Gold-(gold-Simulation.Definitions["mill"].GoldCost))<.001
                    &&game.RenderedBuildingCount==game.Sim.State.Buildings.Count&&HasText("02  /  FEED THE TOWN",true),"Mill spends its cost, appears in the rendered world, and advances the tutorial.");
                Click("Bakery\n70 gold");Require(game.BuildType=="bakery"&&game.Act(game.Sim.PlaceBuilding("bakery",12,12),"QA bakery built."),"Bakery button and normal placement work.");
                game.CancelBuild();yield return null;
                Check(HasText("03  /  OPEN THE RIVER",true),"Tutorial advances from bakery to river crossing.");

                string[] tabs={"Orders","Market","Route","Council"};
                string[] headers={"A PROMISE HAS AN ARRIVAL DATE","MARKET & FREIGHT","THE RIVER CROSSING","THE COUNCIL LEDGER"};
                bool allTabs=true;
                for(int i=0;i<tabs.Length;i++){Click(tabs[i]);yield return null;allTabs&=HasText(headers[i]);}
                Check(allTabs,"All four tab buttons open their expected content.");
                Click("Market");yield return null;
                int reserve=game.Sim.State.ReserveDays;Click("Reserve +");yield return null;
                Check(game.Sim.State.ReserveDays==reserve+1,"Scrolled Market reserve control updates the simulation.");
                Click("Reserve −");yield return null;
                Click("Route");yield return null;Click("Commission ferry");yield return null;
                Check(game.Sim.State.Crossing.Pending=="ferry"&&game.Sim.State.Crossing.DaysLeft==2,"Route button commissions real ferry construction.");
                Click("Ask Pinewatch for relief");yield return null;
                Require(game.Sim.State.Caravans.Count>0&&game.RenderedCaravanCount==game.Sim.State.Caravans.Count,"Relief button creates delayed cargo and matching world models.");

                // Compare the disk/load boundary with the decoded JSON state.
                // JsonUtility can normalize the final bit of a double while
                // parsing (covered field-by-field in SimulationVerification).
                // No frame is yielded during save/mutate/load, so story Update
                // cannot account for a state discrepancy inside this block.
                string expected=JsonUtility.ToJson(JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(game.Sim.Snapshot())));
                Require(game.SaveTo(qaPath)&&File.Exists(qaPath),"Production SaveTo writes a unique QA file, outside the manual slot.");
                Require(game.SaveTo(qaPath)&&File.Exists(qaPath),"A second SaveTo safely replaces the existing QA file.");
                string saved=JsonUtility.ToJson(JsonUtility.FromJson<SimulationState>(File.ReadAllText(qaPath)));
                Check(saved==expected,"SaveTo preserves every decoded persistent field in the QA file."+JsonDifference(expected,saved));
                game.Sim.AdvanceDay();game.BeginBuild("house");game.Paused=false;game.DayFraction=.75f;
                Require(game.LoadFrom(qaPath),"Production LoadFrom reads the isolated QA save.");
                string loaded=JsonUtility.ToJson(game.Sim.Snapshot());
                Check(loaded==saved,"Load restores the decoded construction, cargo, and all persistent fields exactly."+JsonDifference(saved,loaded));
                Check(game.Paused&&!game.InMenu&&game.DayFraction==0&&game.BuildType==""&&game.Selected==-1,
                    "Load resets time and build selection. Paused="+game.Paused+", InMenu="+game.InMenu+", DayFraction="+game.DayFraction+", BuildType="+game.BuildType+", Selected="+game.Selected+".");
                string before=JsonUtility.ToJson(game.Sim.Snapshot());File.WriteAllText(qaPath,"{ broken QA JSON");
                Check(!game.LoadFrom(qaPath)&&JsonUtility.ToJson(game.Sim.Snapshot())==before,"Corrupted QA JSON is rejected without changing the world.");
                yield return null;

                Click("3x");Check(game.Speed==3&&!game.Paused,"Speed button selects 3x and resumes time.");
                Click("Pause");Check(game.Paused,"Pause button stops time.");
                Click("Menu");yield return null;
                Check(game.Modal&&HasText("THE STEWARD'S DESK"),"Menu button opens the paused steward desk.");
                Click("Settings");yield return null;Check(HasText("SOUND & DISPLAY"),"Pause-menu Settings button opens settings.");
                Click("Back");yield return null;Click("New charter");yield return null;
                Check(HasText("BEGIN AGAIN?")&&game.Modal,"New charter requires the existing in-game confirmation screen.");
                Click("Cancel");yield return null;Click("Main menu");yield return null;
                Check(game.InMenu&&game.Modal&&HasText("FIRST WINTER DELIVERY"),"Main-menu button returns to the title screen.");
                Check(clicks>=20,clicks+" button clicks passed actual center raycasts before pointer-click dispatch.");
            }

            T Field<T>(string name) where T:class => typeof(GameUI).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(game.UI) as T;
            TextMeshProUGUI[] Texts()=>canvas.GetComponentsInChildren<TextMeshProUGUI>();
            bool HasText(string text,bool prefix=false)=>Texts().Any(t=>prefix?t.text.StartsWith(text,StringComparison.Ordinal):t.text==text);
            RectTransform Named(string name,bool includeInactive=false)=>canvas.GetComponentsInChildren<RectTransform>(includeInactive).First(t=>t.name==name);
            float UncoveredHudFraction()
            {
                string[] panels={"Resource ledger","Charter","Management navigation","Trade and council","Build ribbon","Action feedback","Steward guide","Mouse camera controls","Valley map"};
                var rectangles=canvas.GetComponentsInChildren<RectTransform>().Where(r=>panels.Contains(r.name))
                    .Select(ScreenRect).Select(r=>Rect.MinMaxRect(Mathf.Clamp(r.xMin,0,Screen.width),Mathf.Clamp(r.yMin,0,Screen.height),Mathf.Clamp(r.xMax,0,Screen.width),Mathf.Clamp(r.yMax,0,Screen.height)))
                    .Where(r=>r.width>0&&r.height>0).ToArray();
                // Exact union of axis-aligned screen rectangles, rather than double-counting overlapping panels.
                var xs=rectangles.SelectMany(r=>new[]{r.xMin,r.xMax}).Concat(new[]{0f,(float)Screen.width}).Distinct().OrderBy(x=>x).ToArray();
                double occupied=0;
                for(int i=0;i<xs.Length-1;i++)
                {
                    float middle=(xs[i]+xs[i+1])*.5f;
                    var spans=rectangles.Where(r=>r.xMin<middle&&r.xMax>middle).OrderBy(r=>r.yMin).ToArray();
                    float bottom=0,top=0,coveredHeight=0;bool started=false;
                    foreach(var span in spans)
                    {
                        if(!started){bottom=span.yMin;top=span.yMax;started=true;}
                        else if(span.yMin>top){coveredHeight+=top-bottom;bottom=span.yMin;top=span.yMax;}
                        else top=Mathf.Max(top,span.yMax);
                    }
                    if(started)coveredHeight+=top-bottom;
                    occupied+=(xs[i+1]-xs[i])*coveredHeight;
                }
                return 1-(float)(occupied/((double)Screen.width*Screen.height));
            }
            static Rect ScreenRect(RectTransform rect)
            {
                var corners=new Vector3[4];rect.GetWorldCorners(corners);
                Vector2 a=RectTransformUtility.WorldToScreenPoint(null,corners[0]),b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
                return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
            }
            static bool Contained(RectTransform child,RectTransform parent)
            {var a=ScreenRect(child);var b=ScreenRect(parent);return a.xMin>=b.xMin-1&&a.xMax<=b.xMax+1&&a.yMin>=b.yMin-1&&a.yMax<=b.yMax+1;}
            void Click(string name)
            {
                Canvas.ForceUpdateCanvases();
                var button=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b=>b.name==name&&b.isActiveAndEnabled&&b.interactable);
                if(button==null)throw new InvalidOperationException("Active button missing: "+name);
                var scroll=button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                if(scroll!=null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();scroll.StopMovement();
                    var center=scroll.viewport.InverseTransformPoint(button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                    var offset=scroll.content.anchoredPosition;offset.y+=scroll.viewport.rect.center.y-center.y;
                    offset.y=Mathf.Clamp(offset.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));scroll.content.anchoredPosition=offset;
                    Canvas.ForceUpdateCanvases();
                }
                var rect=(RectTransform)button.transform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                if(point.x<0||point.y<0||point.x>Screen.width||point.y>Screen.height||hits.Count==0||hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=button)
                    throw new InvalidOperationException("Button center is not reachable by the actual raycaster: "+name+"; first hit="+(hits.Count>0?hits[0].gameObject.name:"none")+"; point="+point);
                clicks++;ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
            }
            static string Digest(string path)
            {
                if(!File.Exists(path))return "absent";
                using(var algorithm=SHA256.Create())return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(path)));
            }
            static string JsonDifference(string expected,string actual)
            {
                if(expected==actual)return "";
                int at=0;while(at<expected.Length&&at<actual.Length&&expected[at]==actual[at])at++;
                int start=Math.Max(0,at-45);
                return " First difference at character "+at+"; expected ..."+expected.Substring(start,Math.Min(120,expected.Length-start))+"; actual ..."+actual.Substring(Math.Min(start,actual.Length),Math.Min(120,Math.Max(0,actual.Length-start)))+".";
            }
            public void Finish()
            {
                try{game.NewGame();game.UI.CloseModal();game.Paused=true;Check(game.Sim.State.Day==1&&game.BuildType==""&&!game.Modal,"Cleanup leaves a fresh paused charter.");}
                catch(Exception error){Check(false,"Fresh-world cleanup: "+error.Message);}
                try
                {
                    foreach(string suffix in new[]{"",".tmp",".bak"})if(File.Exists(qaPath+suffix))File.Delete(qaPath+suffix);
                    Check(Digest(game.CharterSavePath)==manualDigest&&Digest(game.MilitarySavePath)==militaryDigest,"First Winter and Toll War manual saves both remain byte-for-byte unchanged (or absent).");
                }
                catch(Exception error){Check(false,"QA file cleanup/manual-slot check: "+error.Message);}
                var report=new StringBuilder();report.AppendLine((Failures==0?"PASS":"FAIL")+": "+checks+" integration checks; "+Failures+" failures.");
                report.AppendLine("UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\nResolution actually tested: "+Screen.width+"x"+Screen.height);
                foreach(var entry in entries)report.AppendLine(entry);
                report.AppendLine("Scope: real runtime canvas, GraphicRaycaster, pointer-click callbacks, audio transport/cue fields, measured text/image rectangles, and production SaveTo/LoadFrom. Construction uses normal model placement after UI selection; no synthetic world mouse click is claimed. Source playback is checked, not human listening or acoustic output. This run does not imply testing a different resolution.");
                File.WriteAllText(ReportPath,report.ToString());Debug.Log(report.ToString());
            }
        }
    }
}
