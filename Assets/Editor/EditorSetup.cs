using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LivingEmpires.EditorTools
{
    public static class EditorSetup
    {
        [MenuItem("Living Empires/Verify simulation")]
        public static void VerifyMenu(){Verify();}
        [MenuItem("Living Empires/Build Windows game")]
        public static void BuildMenu(){Build();}
        [MenuItem("Living Empires/Bake original art prefabs")]
        public static void BakeMenu(){BakeArt();}
        [MenuItem("Living Empires/Open playable chapter")]
        public static void OpenChapter(){EditorSceneManager.OpenScene("Assets/Scenes/FirstWinterDelivery.unity");}
        public static string Setup()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before setup.");
            Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Reports");
            if(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")==null)
                throw new InvalidOperationException("TMP font resources must be imported before configuring the chapter.");
            CommunityValidation.ConfigureIdentity();PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.colorSpace=ColorSpace.Linear;
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if(pipeline!=null){pipeline.renderScale=1;pipeline.shadowDistance=60;pipeline.msaaSampleCount=2;pipeline.supportsHDR=false;pipeline.useSRPBatcher=true;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;EditorUtility.SetDirty(pipeline);}
            QualitySettings.vSyncCount=0;
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Narrative"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
            foreach(string path in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/Narrative"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(importer==null)continue;var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.loadType=AudioClipLoadType.CompressedInMemory;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Living Empires Game",typeof(GameController));
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/FirstWinterDelivery.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/FirstWinterDelivery.unity",true)};
            AssetDatabase.SaveAssets();return "First Winter Delivery configured and saved.";
        }
        public static string Verify()
        {
            return CommunityValidation.VerifyReports();
        }
        public static string Build()
        {
            return CommunityValidation.BuildRelease();
        }
        public static string BuildTo(string directory)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before building.");
            CommunityValidation.ConfigureIdentity();
            CommunityValidation.ValidateReleaseFiles();
            ConfigureLaptop();
            Directory.CreateDirectory(directory);Directory.CreateDirectory("Reports");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/FirstWinterDelivery.unity"},locationPathName=Path.Combine(directory,"LivingEmpiresCommunity.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            var summary=result.summary;string text=$"Result: {summary.result}\nSize: {summary.totalSize} bytes\nTime: {summary.totalTime}\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}\n";File.WriteAllText("Reports/build.txt",text);
            CommunityValidation.WriteBuildReport(summary,directory);
            if(summary.result!=BuildResult.Succeeded)throw new Exception(text);
            // Ship creator attribution and existing package/font notices with every build.
            foreach(string source in Directory.GetFiles("Documentation/Licenses","*",SearchOption.AllDirectories))
            {
                if(source.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))continue;
                string relative=Path.GetRelativePath("Documentation/Licenses",source);
                string destination=Path.Combine(directory,"Licenses",relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination,true);
            }
            foreach(string notice in CommunityValidation.ReleaseNotices)File.Copy(notice,Path.Combine(directory,Path.GetFileName(notice)),true);
            File.Copy("Documentation/PUBLIC_ASSET_INVENTORY.json",Path.Combine(directory,"PUBLIC_ASSET_INVENTORY.json"),true);
            File.Copy("Documentation/WINDOWS_README.txt",Path.Combine(directory,"README.txt"),true);
            return text;
        }
        public static string BakeArt()
        {
            ConfigureLaptop();
            Directory.CreateDirectory("Assets/Resources/ArtLibrary");Directory.CreateDirectory("Assets/Art/Generated");
            var host=new GameObject("Art bake temporary");int count=0;
            foreach(string kind in Simulation.Definitions.Keys){var obj=WorldArt.CreateBuilding(kind,Vector3.zero,host.transform);Persist(obj,kind);count++;}
            Persist(WorldArt.CreateCart(Vector3.zero,host.transform),"Cart");
            for(int i=0;i<6;i++)Persist(WorldArt.CreatePerson(Vector3.zero,host.transform,null,i),"Person"+i);
            Persist(WorldArt.CreateCrossing(true,Vector3.zero,host.transform),"Bridge");Persist(WorldArt.CreateCrossing(false,Vector3.zero,host.transform),"Ferry");
            foreach(string faction in new[]{"riverhold","ironvale","pinewatch","tidemark","stonewake"})Persist(WorldArt.CreateBanner(faction,Vector3.zero,host.transform),"Banner_"+faction);
            Directory.CreateDirectory("Assets/Art/Icons");
            foreach(string id in Simulation.Goods.Concat(Simulation.Definitions.Keys)){var icon=WorldArt.Icon(id,64);File.WriteAllBytes("Assets/Art/Icons/"+id+".png",icon.texture.EncodeToPNG());}
            foreach(var renderer in host.GetComponentsInChildren<Renderer>(true))foreach(var mat in renderer.sharedMaterials){if(mat==null)continue;EditorUtility.SetDirty(mat);foreach(string property in mat.GetTexturePropertyNames()){var texture=mat.GetTexture(property);if(texture!=null)EditorUtility.SetDirty(texture);}}
            UnityEngine.Object.DestroyImmediate(host);AssetDatabase.SaveAssets();return $"Baked {count} building prefabs, cart, six citizens, bridge and ferry with original meshes/materials.";
        }
        public static void ConfigureLaptop()
        {
            EditorSettings.enterPlayModeOptionsEnabled=false;
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if(pipeline!=null){pipeline.shadowCascadeCount=2;pipeline.mainLightShadowmapResolution=1024;pipeline.supportsHDR=false;pipeline.useSRPBatcher=true;EditorUtility.SetDirty(pipeline);}
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            // Disabled SSAO still runs Create() in this URP version, while its
            // resources are stripped from players. Remove the unused feature
            // references so a standalone renderer never initializes it.
            if(renderer!=null){renderer.renderingMode=RenderingMode.Forward;renderer.rendererFeatures.Clear();EditorUtility.SetDirty(renderer);}
            Directory.CreateDirectory("Assets/Resources/ArtLibrary");
            const string indicatorPath="Assets/Resources/ArtLibrary/PlacementIndicator.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(indicatorPath)==null){var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("URP Unlit shader is unavailable.");AssetDatabase.CreateAsset(new Material(shader){name="Placement indicator",color=new Color(.4f,.95f,.65f)},indicatorPath);}
            AssetDatabase.SaveAssets();
        }
        static void Persist(GameObject obj,string name)
        {
            foreach(var mf in obj.GetComponentsInChildren<MeshFilter>(true))
                if(mf.sharedMesh!=null&&!AssetDatabase.Contains(mf.sharedMesh))AssetDatabase.CreateAsset(mf.sharedMesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Generated/"+Clean(mf.sharedMesh.name)+".asset"));
            foreach(var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.sharedMesh!=null&&!AssetDatabase.Contains(skin.sharedMesh))AssetDatabase.CreateAsset(skin.sharedMesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Generated/"+Clean(skin.sharedMesh.name)+".asset"));
            foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))foreach(var mat in renderer.sharedMaterials)
            {
                if(mat==null||AssetDatabase.Contains(mat))continue;
                foreach(string property in mat.GetTexturePropertyNames()){var texture=mat.GetTexture(property);if(texture!=null&&!AssetDatabase.Contains(texture))AssetDatabase.CreateAsset(texture,AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Generated/"+Clean(texture.name)+".asset"));}
                AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Generated/"+Clean(mat.name)+".mat"));
            }
            PrefabUtility.SaveAsPrefabAsset(obj,"Assets/Resources/ArtLibrary/"+name+".prefab");
        }
        static string Clean(string value){return string.IsNullOrWhiteSpace(value)?"OriginalAsset":string.Concat(value.Where(c=>char.IsLetterOrDigit(c)||c=='_'));}
    }
}
