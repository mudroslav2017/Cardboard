using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class CardboardSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/CardboardPlayground.unity";
        const string AssetPath = "Assets/Cardboard/Generated/CardboardArt.asset";
        static CardboardSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            };
        }

        [MenuItem("Cardboard/Create playground")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) return;
            // Never discard unsaved edits to another scene.
            if (!Application.isBatchMode && SceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Directory.CreateDirectory("Assets/Cardboard/Generated");
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root = new GameObject("CARDBOARD • handmade running club");
                var world = root.AddComponent<CardboardWorld>(); world.Build();
                var container = AssetDatabase.LoadAssetAtPath<CardboardGeneratedAssets>(AssetPath);
                if (!container)
                {
                    container = ScriptableObject.CreateInstance<CardboardGeneratedAssets>();
                    AssetDatabase.CreateAsset(container,AssetPath);
                }
                else
                {
                    foreach(var old in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                        if(old && old!=container) UnityEngine.Object.DestroyImmediate(old,true);
                }
                foreach (var asset in CardboardArt.Generated)
                    if (asset && !AssetDatabase.Contains(asset)) AssetDatabase.AddObjectToAsset(asset,container);
                AssetDatabase.SaveAssets();
                PrefabUtility.SaveAsPrefabAsset(world.runners[1].gameObject,"Assets/Cardboard/Generated/CardboardRunner.prefab");
                EditorSceneManager.SaveScene(scene,ScenePath);
                EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath,true)};
                if (SceneView.lastActiveSceneView)
                    SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,0),Quaternion.Euler(38,215,0),24);
                Selection.activeGameObject = root;
                Preview();
                File.WriteAllText("Library/CardboardBuildReport.txt","SUCCESS: First-person scene, 11 single-sheet NPCs and player, simple whole-sheet sway.\n");
                Debug.Log("Cardboard playground ready. Press Play! WASD / Shift / Space / E / R.");
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/CardboardBuildReport.txt",error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Cardboard/Save preview")]
        public static void Preview()
        {
            var camera=Camera.main; if (!camera) return;
            Directory.CreateDirectory("Screenshots");
            var rt = new RenderTexture(1600,1000,24);
            var previous = camera.targetTexture; var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                var tex = new Texture2D(1600,1000,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply();
                File.WriteAllBytes("Screenshots/CardboardPreview.png",tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { camera.targetTexture=previous; RenderTexture.active=previousActive; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
