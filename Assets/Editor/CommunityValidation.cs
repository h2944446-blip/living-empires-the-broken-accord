using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LivingEmpires.EditorTools
{
    /// <summary>Community release entry points. No player save is read or written.</summary>
    public static class CommunityValidation
    {
        public const string ProductName="Living Empires Community Edition";
        public static readonly string[] ReleaseNotices={"LICENSE","Documentation/THIRD_PARTY_NOTICES.md"};

        [MenuItem("Living Empires/Community/Verify economy, military and narrative")]
        public static void Verify(){VerifyReports();}

        [MenuItem("Living Empires/Community/Build portable Windows release")]
        public static void BuildWindows(){BuildRelease();}

        public static void ConfigureIdentity()
        {
            PlayerSettings.companyName="Living Empires";
            PlayerSettings.productName=ProductName;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.livingempires.community");
            AssetDatabase.SaveAssets();
        }

        public static string VerifyReports()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before model verification.");
            ConfigureIdentity();
            Directory.CreateDirectory("Reports");
            Func<SimulationState,string> serialize=state=>JsonUtility.ToJson(state);
            Func<string,SimulationState> deserialize=json=>JsonUtility.FromJson<SimulationState>(json);
            var economy=SimulationVerification.RunAll(serialize,deserialize,"Unity JsonUtility");
            var military=MilitaryVerification.RunAll(serialize,deserialize);
            military.SerializationBackend="Unity JsonUtility";
            var narrative=VerifyNarrative();
            File.WriteAllText("Reports/simulation-tests.txt",economy.ToString());
            File.WriteAllText("Reports/military-tests.txt",military.ToString());
            File.WriteAllText("Reports/community-narrative-tests.txt",narrative.ToString());
            var report=new ReleaseVerification
            {
                utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,
                productName=PlayerSettings.productName,
                passed=economy.Passed&&military.Passed&&narrative.Passed,
                economy=new SuiteResult(economy),military=new SuiteResult(military),narrative=new SuiteResult(narrative)
            };
            File.WriteAllText("Reports/community-verification.json",JsonUtility.ToJson(report,true));
            string text="ECONOMY\n"+economy+"\nMILITARY\n"+military+"\nCOMMUNITY NARRATIVE\n"+narrative;
            Debug.Log(text);
            if(!report.passed)throw new InvalidOperationException("Community verification failed. See Reports/community-verification.json.");
            return text;
        }

        static VerificationReport VerifyNarrative()
        {
            var report=new VerificationReport{Groups=1,SerializationBackend="Unity JsonUtility narrative reader",SerializationTested=true};
            Action<bool,string> check=(ok,message)=>{report.Checks++;if(!ok)report.Failures.Add(message);};
            var source=Resources.Load<TextAsset>("Narrative/story");
            check(source!=null,"Narrative/story.json must be included.");
            if(source!=null)
            {
                var story=JsonUtility.FromJson<StoryCollection>(source.text);
                check(story!=null&&story.events!=null&&story.events.Length>0,"Council archive must contain story events.");
                if(story!=null&&story.events!=null)
                {
                    var ids=new HashSet<string>();
                    foreach(var entry in story.events)
                    {
                        check(entry!=null,"Story entries must not be null.");if(entry==null)continue;
                        check(!string.IsNullOrWhiteSpace(entry.id)&&ids.Add(entry.id),"Story IDs must be nonempty and unique: "+entry.id);
                        check(!string.IsNullOrWhiteSpace(entry.title)&&!string.IsNullOrWhiteSpace(entry.text),"Story title and full text are required: "+entry.id);
                        if(entry.lines!=null&&entry.lines.Length>0)
                            foreach(var line in entry.lines)
                                check(line!=null&&!string.IsNullOrWhiteSpace(line.speaker)&&!string.IsNullOrWhiteSpace(line.text)&&entry.text.Contains(line.text),"Dialogue must retain its speaker and full transcript text: "+entry.id);
                    }
                }
            }
            check(AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/Narrative"}).Length==0,"Community narrative must omit recordings with unverified rights.");
            check(AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Narrative"}).Length==0,"Community narrative must omit portraits with unverified rights.");
            check(PlayerSettings.productName==ProductName,"Community product name must isolate its persistent save directory.");
            report.Notes.Add("Narrative checks validate shipped text and media exclusions. Runtime integration verifies rendered speakers, scrolling and interaction separately.");
            return report;
        }

        public static void ValidateReleaseFiles()
        {
            foreach(string path in ReleaseNotices.Concat(new[]{"Documentation/PUBLIC_ASSET_INVENTORY.json","Documentation/WINDOWS_README.txt"}))
                if(!File.Exists(path))throw new FileNotFoundException("Required release file is missing.",path);
            if(!Directory.Exists("Documentation/Licenses"))throw new DirectoryNotFoundException("Documentation/Licenses is required for the portable build.");
        }

        public static string BuildRelease()
        {
            ConfigureIdentity();ValidateReleaseFiles();VerifyReports();
            return EditorSetup.BuildTo("Build/Windows");
        }

        public static void WriteBuildReport(BuildSummary summary,string directory)
        {
            var report=new ReleaseBuild
            {
                utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,
                productName=PlayerSettings.productName,result=summary.result.ToString(),
                output=Path.GetFullPath(Path.Combine(directory,"LivingEmpiresCommunity.exe")),
                bytes=summary.totalSize.ToString(),seconds=summary.totalTime.TotalSeconds,
                errors=summary.totalErrors,warnings=summary.totalWarnings
            };
            Directory.CreateDirectory("Reports");File.WriteAllText("Reports/community-build.json",JsonUtility.ToJson(report,true));
        }

        [Serializable] sealed class SuiteResult
        {
            public bool passed,serializationTested;public int checks,groups;
            public string serializationBackend;public string[] failures,scenarios,notes;
            public SuiteResult(VerificationReport report)
            {
                passed=report.Passed;checks=report.Checks;groups=report.Groups;
                serializationTested=report.SerializationTested;serializationBackend=report.SerializationBackend;
                failures=report.Failures.ToArray();scenarios=report.Scenarios.ToArray();notes=report.Notes.ToArray();
            }
        }
        [Serializable] sealed class ReleaseVerification
        {public string utc,unityVersion,productName;public bool passed;public SuiteResult economy,military,narrative;}
        [Serializable] sealed class ReleaseBuild
        {public string utc,unityVersion,productName,result,output,bytes;public double seconds;public int errors,warnings;}
    }
}
