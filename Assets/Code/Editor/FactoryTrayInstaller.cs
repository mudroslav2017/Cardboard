using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryTrayInstaller
    {
        public const string ModelPath="Assets/Game_Assets/Cardboard/Tray/Tray.fbx";
        public const string TexturePath="Assets/Game_Assets/Cardboard/Tray/IMG_2286.PNG";
        public const string VisualName="Tray • textured model";
        static bool testing,oldBackground;
        static readonly List<string> report=new List<string>();
        static FactoryTrayInstaller(){EditorApplication.update+=TickTest;}
        public static void Visual(Transform tray)
        {
            var oldVisual=tray.Find(VisualName);if(oldVisual)Object.DestroyImmediate(oldVisual.gameObject);
            var old=tray.Find("podnos • normalized model");
            if(old)
                foreach(var renderer in old.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name=="tray of food")
                    {renderer.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);}
            var mesh=Bake(ModelPath,"TexturedLunchTray",0,Vector3.zero,true,.72f);
            var material=Paper("TexturedLunchTray",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            var visual=MeshObject(VisualName,tray,mesh,material,Vector3.up*-mesh.bounds.min.y);
            CardboardSurface.Configure(visual.GetComponent<Renderer>());
            // Keep existing dishes resting on the new tray, including inactive coworker meals.
            bool active=tray.gameObject.activeSelf;tray.gameObject.SetActive(true);
            try
            {
                if(old)foreach(var renderer in old.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name=="Apple" || renderer.name=="Bowl")
                    {
                        var center=tray.InverseTransformPoint(renderer.bounds.center);
                        float bottom=tray.InverseTransformPoint(renderer.bounds.center-Vector3.up*renderer.bounds.extents.y).y;
                        renderer.transform.position+=tray.up*(FactoryLunchProps.Surface(tray,center)-bottom);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.transform);
                    }
            }
            finally{tray.gameObject.SetActive(active);}
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update tray in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            EditorSceneManager.SaveScene(game.gameObject.scene);Directory.CreateDirectory("Backups/TrayUpdate");
            const string backup="Backups/TrayUpdate/FactoryShift-before-tray.unity";
            if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            Visual(game.lunch.tray);
            foreach(var meal in game.lunch.diners.meals)Visual(meal);
            FactoryLunchProps.Apply(game.lunch);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryTrayUpdate.txt","SUCCESS: all twelve trays use Tray.fbx and its original UVs, authored base map, full roughness and double-sided material; glass and dishes rest on new tray.\n");
            Preview(game);
        }
        static void Preview(CardboardFactoryGame game)
        {
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var tray=game.lunch.tray;
            try
            {
                var eye=tray.position+new Vector3(.4f,.5f,.65f);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(tray.position+Vector3.up*.07f-eye));
                CardboardFactorySceneBuilder.Capture("NewTexturedTray");
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
        static void Check(bool condition,string detail){report.Add((condition?"PASS ":"FAIL ")+detail);}
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();testing=true;oldBackground=Application.runInBackground;Application.runInBackground=true;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void TickTest()
        {
            if(!testing || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            var game=CardboardFactoryGame.Instance;
            if(!game || !CardboardRunner.Player || game.MenuActive || Time.time<.2f)return;
            testing=false;
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(FactoryPresentationInstaller.Generated+"TexturedLunchTray.asset");
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(mesh.uv.SequenceEqual(source.uv),"New tray preserves authored UV exactly");
                var trays=new[]{game.lunch.tray}.Concat(game.lunch.diners.meals).ToArray();
                foreach(var tray in trays)
                {
                    var visual=tray.Find(VisualName);var renderer=visual.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                    Check(visual && visual.GetComponent<MeshFilter>().sharedMesh==mesh,"New tray geometry: "+tray.name);
                    Check(material.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) &&
                        material.GetColor("_BaseColor")==Color.white && material.GetFloat("_Cull")==0 && material.GetFloat("_Smoothness")==0,
                        "Authored base map, no tint, two-sided and fully rough: "+tray.name);
                    Check(!tray.Find("podnos • normalized model").GetComponentsInChildren<Renderer>(true).Any(item=>item.name=="tray of food" && item.gameObject.activeSelf),
                        "Old tray is hidden: "+tray.name);
                    var cup=tray.Find(FactoryLunchProps.CupName);
                    bool active=tray.gameObject.activeSelf;tray.gameObject.SetActive(true);
                    try
                    {
                        float bottom=cup.localPosition.y+cup.GetComponent<MeshFilter>().sharedMesh.bounds.min.y;
                        Check(Mathf.Abs(bottom-FactoryLunchProps.Surface(tray,cup.localPosition))<.002f,"Glass rests on new tray surface: "+tray.name);
                    }
                    finally{tray.gameObject.SetActive(active);}
                }
                FactoryLunchVerification.Check(game,Check);
            }
            catch(Exception error){report.Add("FAIL "+error);}
            finally
            {
                File.WriteAllLines("Library/FactoryTrayVerification.txt",report);
                Application.runInBackground=oldBackground;EditorApplication.isPlaying=false;
            }
        }
    }
}
