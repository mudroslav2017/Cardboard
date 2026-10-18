using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class CardboardFactorySceneBuilder
    {
        public const string ScenePath="Assets/Scenes/FactoryShift.unity";
        const string ArtPath="Assets/Cardboard/Generated/FactoryWorkstationArt.asset";
        static bool stageAtBench;
        static CardboardFactorySceneBuilder()
        {
            EditorApplication.delayCall+=() =>
            {
                if(!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            };
            EditorApplication.update+=Commands;
        }
        static void Commands()
        {
            if(stageAtBench && EditorApplication.isPlaying && CardboardRunner.Player && CardboardFactoryGame.Instance)
            {
                var player=CardboardRunner.Player;var body=player.GetComponent<CharacterController>();
                body.enabled=false;player.transform.position=CardboardFactoryGame.Instance.station.transform.TransformPoint(new Vector3(0,0,-2.7f));body.enabled=true;
                Camera.main.transform.position=player.transform.position+Vector3.up*1.62f;stageAtBench=false;
            }
            const string request="Library/FactoryEditorCommand.txt";
            if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string command;
            try { command=File.ReadAllText(request).Trim();File.Delete(request); }
            catch(IOException) { return; }
            if(command=="build") Build();
            else if(command=="audit") WorkstationModelAudit.Write();
            else if(command=="audit-marker") WorkstationModelAudit.AuditMarkerFaces();
            else if(command=="refresh") AssetDatabase.Refresh();
            else if(command=="table-materials-audit") FactoryTableMaterials.Audit();
            else if(command=="table-materials") FactoryTableMaterials.Apply();
            else if(command=="table-materials-preview") {FactoryTableMaterials.Audit();FactoryTableMaterials.Preview();}
            else if(command=="table-leg-color") FactoryTableMaterials.FixLegColor();
            else if(command=="table-legs-rotate") FactoryTableMaterials.RotateVerticalLegs();
            else if(command=="floor-texture") FactoryFloorInstaller.ApplyCurrentScene();
            else if(command=="presentation-update") FactoryPresentationInstaller.ApplyCurrentScene();
            else if(command=="menu-rebuild") FactoryMenuBuilder.RebuildCurrent();
            else if(command=="lunch-install") FactoryLunchInstaller.InstallCurrent();
            else if(command=="import-people") ImportedCharacterInstaller.Install();
            else if(command=="verify") FactoryWorkstationVerification.Verify();
            else if(command=="play") { EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true; }
            else if(command=="bench-play") { stageAtBench=true;EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true; }
            else if(command=="stop") EditorApplication.isPlaying=false;
            else if(command=="preview") Capture("FactoryPreview");
            else if(command=="bench-preview")
            {
                var cam=Camera.main;var position=cam.transform.position;var rotation=cam.transform.rotation;
                var station=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>().station;
                cam.transform.position=station.transform.TransformPoint(new Vector3(0,2.9f,-6.7f));
                cam.transform.rotation=Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(.4f,1.4f,0))-cam.transform.position);
                Capture("WorkbenchPreview");cam.transform.SetPositionAndRotation(position,rotation);
            }
            else if(command=="layout-audit") FactoryLayoutAudit.Write();
            else if(command=="layout-preview")
            {
                var cam=Camera.main;var position=cam.transform.position;var rotation=cam.transform.rotation;
                bool ortho=cam.orthographic;float size=cam.orthographicSize;
                cam.transform.SetPositionAndRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX,65,-5),Quaternion.Euler(90,0,0));cam.orthographic=true;cam.orthographicSize=29;
                Capture("FactoryLayout");cam.orthographic=ortho;cam.orthographicSize=size;cam.transform.SetPositionAndRotation(position,rotation);
            }
        }
        [MenuItem("Cardboard/Create factory shift")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var previousScene=SceneManager.GetActiveScene();
            const string legacyScene="Assets/Scenes/LegacyFactoryShift.unity";
            if(previousScene.path==ScenePath && !File.Exists(legacyScene))
                EditorSceneManager.SaveScene(previousScene,legacyScene,true);
            else if(previousScene.isDirty && !string.IsNullOrEmpty(previousScene.path)) EditorSceneManager.SaveScene(previousScene);
            try
            {
                CardboardTypographyInstaller.Prepare();
                ImportedCharacterInstaller.Prepare();
                FactoryGearImporter.Prepare();
                Directory.CreateDirectory("Assets/Cardboard/Generated");Directory.CreateDirectory("Assets/Scenes");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("НЕ СВОЙ • cardboard factory shift");
                root.AddComponent<CardboardWorld>();root.AddComponent<CardboardFactoryGame>();
                root.AddComponent<CardboardFactoryWorld>().Build();
                FactoryFloorInstaller.Apply(root.transform);
                FactoryPresentationInstaller.Apply(root.transform);
                FactoryLunchInstaller.Create(root.transform);
                var container=AssetDatabase.LoadAssetAtPath<CardboardGeneratedAssets>(ArtPath);
                if(!container) { container=ScriptableObject.CreateInstance<CardboardGeneratedAssets>();AssetDatabase.CreateAsset(container,ArtPath); }
                else foreach(var old in AssetDatabase.LoadAllAssetsAtPath(ArtPath)) if(old && old!=container) UnityEngine.Object.DestroyImmediate(old,true);
                foreach(var asset in CardboardArt.Generated) if(asset && !AssetDatabase.Contains(asset)) AssetDatabase.AddObjectToAsset(asset,container);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
                EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/CardboardPlayground.unity",false)};
                Selection.activeGameObject=root;
                if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,-5),Quaternion.Euler(55,0,0),44);
                File.WriteAllText("Library/FactoryBuildReport.txt","SUCCESS: imported workstation models, animated guard, conveyor, numbered types, jump pump, wheel cutting, 3 finishing baths, punchcard orders; HUD, wall text and disguise locker removed.\n");
                Capture("FactoryPreview");
            }
            catch(Exception error) { File.WriteAllText("Library/FactoryBuildReport.txt",error.ToString());Debug.LogException(error); }
        }
        public static void Capture(string name)
        {
            var camera=Camera.main;if(!camera) return;
            Directory.CreateDirectory("Screenshots");
            var rt=new RenderTexture(1600,1000,24);
            var previous=camera.targetTexture;var active=RenderTexture.active;Texture2D tex=null;
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                var menu=UnityEngine.Object.FindFirstObjectByType<FactoryMainMenu>();
                if(menu && menu.uiCamera && menu.uiCamera.enabled){menu.uiCamera.aspect=camera.aspect;menu.uiCamera.fieldOfView=camera.fieldOfView;}
                var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest {destination=rt};
                if(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request))UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
                else camera.Render();
                RenderTexture.active=rt;
                tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
                // Image viewers may keep a memory map open on Windows. Each capture is immutable.
                string path="Screenshots/"+name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".png";
                File.WriteAllBytes(path,tex.EncodeToPNG());File.WriteAllText("Library/"+name+"Capture.txt",Path.GetFullPath(path));
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=active;
                if(tex) UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
