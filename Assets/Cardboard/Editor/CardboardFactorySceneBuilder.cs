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
        const string ArtPath="Assets/Cardboard/Generated/FactoryArt.asset";
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
                body.enabled=false;player.transform.position=new Vector3(-8,0,3.45f);body.enabled=true;
                Camera.main.transform.position=player.transform.position+Vector3.up*1.62f;stageAtBench=false;
            }
            const string request="Library/FactoryEditorCommand.txt";
            if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string command;
            try { command=File.ReadAllText(request).Trim();File.Delete(request); }
            catch(IOException) { return; }
            if(command=="build") Build();
            else if(command=="verify") FactoryVerification.Verify();
            else if(command=="play") { EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true; }
            else if(command=="bench-play") { stageAtBench=true;EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true; }
            else if(command=="stop") EditorApplication.isPlaying=false;
            else if(command=="preview") Capture("FactoryPreview");
            else if(command=="bench-preview")
            {
                var cam=Camera.main;var position=cam.transform.position;var rotation=cam.transform.rotation;
                cam.transform.position=new Vector3(-8.35f,1.95f,4.5f);
                cam.transform.rotation=Quaternion.LookRotation(new Vector3(-8.35f,1.63f,1)-cam.transform.position);
                Capture("WorkbenchPreview");cam.transform.SetPositionAndRotation(position,rotation);
            }
        }
        [MenuItem("Cardboard/Create factory shift")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Directory.CreateDirectory("Assets/Cardboard/Generated");Directory.CreateDirectory("Assets/Scenes");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("НЕ СВОЙ • cardboard factory shift");
                root.AddComponent<CardboardWorld>();root.AddComponent<CardboardFactoryGame>();
                root.AddComponent<CardboardFactoryWorld>().Build();
                var container=AssetDatabase.LoadAssetAtPath<CardboardGeneratedAssets>(ArtPath);
                if(!container) { container=ScriptableObject.CreateInstance<CardboardGeneratedAssets>();AssetDatabase.CreateAsset(container,ArtPath); }
                else foreach(var old in AssetDatabase.LoadAllAssetsAtPath(ArtPath)) if(old && old!=container) UnityEngine.Object.DestroyImmediate(old,true);
                foreach(var asset in CardboardArt.Generated) if(asset && !AssetDatabase.Contains(asset)) AssetDatabase.AddObjectToAsset(asset,container);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
                EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/CardboardPlayground.unity",false)};
                Selection.activeGameObject=root;
                if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,-1),Quaternion.Euler(35,205,0),24);
                File.WriteAllText("Library/FactoryBuildReport.txt","SUCCESS: FactoryShift scene, giant mechanism, 3 gear blueprints, assembly bench, disguise, suspicion, inspection, chase, 3 capture endings and shift completion.\n");
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
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
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
