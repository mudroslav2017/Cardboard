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
            else if(command=="web-fix-audit") FactoryWebFixVerification.Audit();
            else if(command=="web-fix-apply") FactoryWebFixVerification.Apply();
            else if(command=="web-fix-verify") FactoryWebFixVerification.Run();
            else if(command=="audit") WorkstationModelAudit.Write();
            else if(command=="editor-state") File.WriteAllText("Library/FactoryEditorState.txt","Playing="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+" timeScale="+Time.timeScale+" frame="+Time.frameCount);
            else if(command=="new-assets-audit") FactoryNewAssetsAudit.Write();
            else if(command=="audit-marker") WorkstationModelAudit.AuditMarkerFaces();
            else if(command=="refresh") AssetDatabase.Refresh();
            else if(command=="save" && !EditorApplication.isPlaying) {AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());}
            else if(command=="table-materials-audit") FactoryTableMaterials.Audit();
            else if(command=="table-materials") FactoryTableMaterials.Apply();
            else if(command=="table-materials-preview") {FactoryTableMaterials.Audit();FactoryTableMaterials.Preview();}
            else if(command=="table-leg-color") FactoryTableMaterials.FixLegColor();
            else if(command=="table-legs-rotate") FactoryTableMaterials.RotateVerticalLegs();
            else if(command=="floor-texture") FactoryFloorInstaller.ApplyCurrentScene();
            else if(command=="presentation-update") FactoryPresentationInstaller.ApplyCurrentScene();
            else if(command=="menu-rebuild") FactoryMenuBuilder.RebuildCurrent();
            else if(command=="opening-verify") FactoryOpeningVerification.Run();
            else if(command=="opening-update") FactoryOpeningInstaller.ApplyCurrent();
            else if(command=="audio-update") FactoryAudioInstaller.ApplyCurrent();
            else if(command=="audio-verify") FactoryAudioVerification.Run();
            else if(command=="repair-audit") FactoryRepairInstaller.Audit();
            else if(command=="repair-update") FactoryRepairInstaller.ApplyCurrent();
            else if(command=="repair-verify") FactoryRepairVerification.Run();
            else if(command=="lever-audit") FactoryLeverInstaller.Audit();
            else if(command=="lever-update") FactoryLeverInstaller.ApplyCurrent();
            else if(command=="lever-verify") FactoryLeverVerification.Run();
            else if(command=="floor-audio") FactoryFloorAudioInstaller.ApplyCurrent();
            else if(command=="floor-audio-verify") FactoryFloorAudioInstaller.Verify();
            else if(command=="webgl-build") FactoryWebGLBuilder.Request(File.ReadAllText("Library/FactoryWebGLBuildPath.txt").Trim());
            else if(command=="gear-placement-verify") FactoryGearPlacementVerification.Run();
            else if(command=="gear-placement") FactoryGearPlacementInstaller.ApplyCurrent();
            else if(command=="worker-visuals") FactoryWorkerVisualInstaller.ApplyCurrent();
            else if(command=="masha-turn") ImportedCharacterInstaller.TurnMashaCurrent();
            else if(command=="worker-verify") FactoryWorkerVisualVerification.Run();
            else if(command=="reaction-verify") FactoryReactionPresentationVerification.Run();
            else if(command=="reaction-presentation") FactoryReactionPresentationInstaller.ApplyCurrent();
            else if(command=="messages-height") FactoryShiftUpgrade.RaiseMessagesCurrent();
            else if(command=="shift-audio") FactoryShiftUpgrade.AudioCurrent();
            else if(command=="shift-verify") FactoryShiftVerification.Run();
            else if(command=="shift-preview") FactoryShiftVerification.Preview();
            else if(command=="shift-assets") FactoryShiftUpgrade.Inspect();
            else if(command=="shift-update") FactoryShiftUpgrade.ApplyCurrent();
            else if(command=="station-inspect") FactoryWorkstationUpgrade.Inspect();
            else if(command=="station-update") FactoryWorkstationUpgrade.ApplyCurrent();
            else if(command=="clean-menu-shot") FactoryWorkstationUpgrade.CaptureCleanMenu();
            else if(command=="people-restore") FactoryWorkstationUpgrade.RestorePeopleCurrent();
            else if(command=="station-upgrade-audit") FactoryWorkstationUpgrade.AuditCurrent();
            else if(command=="instruction-hints") FactoryPhysicsInstaller.InstructionHintsCurrent();
            else if(command=="physics-update") FactoryPhysicsInstaller.ApplyCurrent();
            else if(command=="physics-verify") FactoryPhysicsVerification.Run();
            else if(command=="cursor-verify") FactoryPhysicsVerification.RunCursor();
            else if(command=="storage-install") FactoryStorageInstaller.InstallCurrent();
            else if(command=="compact-audit") FactoryStorageInstaller.AuditCurrent();
            else if(command=="compact-preview") FactoryStorageInstaller.PreviewCurrent();
            else if(command=="new-assets") FactoryNewAssetsInstaller.ApplyCurrent();
            else if(command=="lunch-install") FactoryLunchInstaller.InstallCurrent();
            else if(command=="surfaces") FactorySurfaceInstaller.ApplyCurrent();
            else if(command=="guard-lamps") FactoryGuardLampInstaller.ApplyCurrent();
            else if(command=="tray-update") FactoryTrayInstaller.ApplyCurrent();
            else if(command=="tray-verify") FactoryTrayInstaller.Verify();
            else if(command=="walls-box") FactoryWallsBoxInstaller.ApplyCurrent();
            else if(command=="walls-box-preview") FactoryWallsBoxInstaller.Preview();
            else if(command=="environment-audit") FactoryEnvironmentInstaller.Audit();
            else if(command=="environment-update") FactoryEnvironmentInstaller.ApplyCurrent();
            else if(command=="environment-preview") FactoryEnvironmentInstaller.Preview();
            else if(command=="environment-verify") FactoryEnvironmentVerification.Run();
            else if(command=="feel-verify") FactoryFeelVerification.Run();
            else if(command=="feel-update") FactoryFeelInstaller.ApplyCurrent();
            else if(command=="inventory-palette") FactoryInventoryInstaller.PaletteCurrent();
            else if(command=="inventory-layout") FactoryInventoryInstaller.UpdateLayoutCurrent();
            else if(command=="inventory-style") FactoryInventoryInstaller.StyleCurrent();
            else if(command=="inventory-update") FactoryInventoryInstaller.ApplyCurrent();
            else if(command=="held-gear-verify") FactoryHeldGearVerification.Run();
            else if(command=="inventory-verify") FactoryInventoryVerification.Run();
            else if(command=="inventory-preview") FactoryInventoryInstaller.Preview();
            else if(command=="atmosphere-update") FactoryAtmosphereInstaller.ApplyCurrent();
            else if(command=="atmosphere-preview") FactoryAtmosphereInstaller.Preview();
            else if(command=="atmosphere-verify") FactoryAtmosphereVerification.Run();
            else if(command=="lighting-verify") FactoryLightingVerification.RunCurrent();
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
                var root=new GameObject("OUTSIDER • cardboard factory shift");
                root.AddComponent<CardboardWorld>();root.AddComponent<CardboardFactoryGame>();
                root.AddComponent<CardboardFactoryWorld>().Build();
                FactoryFloorInstaller.Apply(root.transform);
                FactoryPresentationInstaller.Apply(root.transform);
                FactoryNewAssetsInstaller.Presses(root.transform);FactoryNewAssetsInstaller.Instruction(root.transform);
                FactoryGuardLampInstaller.Guards(root.transform);
                FactoryLunchInstaller.Create(root.transform);
                FactorySurfaceInstaller.Apply(root.transform);
                FactoryEnvironmentInstaller.Geometry(root.transform);
                FactoryWallsBoxInstaller.Apply(root.transform);
                FactoryEnvironmentInstaller.Ground(root.transform);
                FactoryAtmosphereInstaller.Apply(root.transform);
                FactoryInventoryInstaller.Apply(root.transform);
                FactoryFeelInstaller.Apply(root.transform);
                root.GetComponent<CardboardFactoryWorld>().LayoutStations(root.GetComponent<CardboardFactoryGame>());
                FactoryStorageInstaller.Create(root.transform);
                FactoryPhysicsInstaller.Apply(root.transform);
                FactoryWorkstationUpgrade.Apply(root.transform);
                FactoryShiftUpgrade.Apply(root.transform);
                FactoryWorkerVisualInstaller.Apply(root.transform);
                FactoryAudioInstaller.Install(root.transform);
                FactoryFloorAudioInstaller.Install(root.transform);
                foreach(var station in root.GetComponentsInChildren<FactoryWorkstation>(true))FactoryLeverInstaller.Install(station);

                var container=AssetDatabase.LoadAssetAtPath<CardboardGeneratedAssets>(ArtPath);
                if(!container) { container=ScriptableObject.CreateInstance<CardboardGeneratedAssets>();AssetDatabase.CreateAsset(container,ArtPath); }
                else foreach(var old in AssetDatabase.LoadAllAssetsAtPath(ArtPath)) if(old && old!=container) UnityEngine.Object.DestroyImmediate(old,true);
                foreach(var asset in CardboardArt.Generated) if(asset && !AssetDatabase.Contains(asset)) AssetDatabase.AddObjectToAsset(asset,container);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
                EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/CardboardPlayground.unity",false)};
                Selection.activeGameObject=root;
                if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,-5),Quaternion.Euler(55,0,0),44);
                File.WriteAllText("Library/FactoryBuildReport.txt","SUCCESS: compact workshop "+CardboardFactoryWorld.WorkshopLength+" x "+CardboardFactoryWorld.WorkshopWidth+" metres, four workstations with left-side baths, 34 barrels and 18 empty boxes; authored models, textures and gameplay preserved.\n");
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
                var inventory=camera.GetComponent<FactoryInventorySlot>();if(inventory)inventory.Layout();
                var death=UnityEngine.Object.FindFirstObjectByType<FactoryDeathPresentation>();if(death)death.Layout();
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
