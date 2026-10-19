using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryGuardLampInstaller
    {
        public const string ModelPath="Assets/Game_Assets/Cardboard/Zabralo/Zabralo.fbx";
        public const string TexturePath="Assets/Game_Assets/Cardboard/Zabralo/Texture/Zabralo.png";
        public static void Guards(Transform world)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var clip=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(item=>!item.name.StartsWith("__preview__"));
            if(!model || !clip)throw new InvalidOperationException("Animated Zabralo model is missing.");
            var material=FactoryPresentationInstaller.Paper("TexturedZabralo",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var wrapper=station.transform.Find("Animated Zabralo");
                if(!wrapper)
                {
                    wrapper=Group("Animated Zabralo",station.transform,Vector3.zero);
                    wrapper.localRotation=Quaternion.Euler(0,90,0);
                }
                while(wrapper.childCount>0)Object.DestroyImmediate(wrapper.GetChild(0).gameObject);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,wrapper);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                    CardboardSurface.Configure(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                foreach(var animator in instance.GetComponentsInChildren<Animator>(true))
                {animator.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(animator);}
                station.guardModel=instance.transform;station.guardClip=clip;station.RebindModels();
            }
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating lamps and guard.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Directory.CreateDirectory("Backups/GuardAndLamps");
            string backup="Backups/GuardAndLamps/FactoryShift-before-update.unity";
            if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            var report=new StringBuilder();
            foreach(var light in game.GetComponentsInChildren<Light>(true))
                if(light.name=="Practical workshop lamp")report.AppendLine("Before: enabled="+light.enabled+" active="+light.gameObject.activeInHierarchy+" intensity="+light.intensity);
            Guards(game.transform);FactorySurfaceInstaller.RestoreWorkshopLighting(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryGuardLampUpdate.txt","SUCCESS: factory lamps enabled, warm fixtures glow, all four animated guards updated with authored texture.\n"+report);
            Preview(game);
        }
        public static void Check(CardboardFactoryGame game,Action<bool,string> check)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponentsInChildren<MeshFilter>();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            foreach(var station in game.stations)
            {
                var meshes=station.guardModel.GetComponentsInChildren<MeshFilter>();
                check(meshes.Length==source.Length && meshes.All(filter=>source.Any(original=>original.sharedMesh==filter.sharedMesh)),
                    "New Zabralo geometry and authored UV installed: "+station.name);
                check(station.guardClip && AssetDatabase.GetAssetPath(station.guardClip)==ModelPath,
                    "Guard uses animation from new model: "+station.name);
                check(station.guardModel.GetComponentsInChildren<Renderer>().All(renderer=>renderer.sharedMaterials.All(material=>
                    material.GetTexture("_BaseMap")==texture && material.GetColor("_BaseColor")==Color.white &&
                    material.GetFloat("_Smoothness")==0 && material.GetFloat("_Cull")==0)),
                    "Guard texture is untinted, fully rough and double-sided: "+station.name);
            }
            var lamps=game.GetComponentsInChildren<Light>(true).Where(light=>light.name=="Practical workshop lamp").ToArray();
            check(lamps.Length==9 && lamps.All(light=>light.enabled && light.gameObject.activeInHierarchy && light.intensity>=8 && light.shadows!=LightShadows.None),
                "All nine compact workshop lamps are on, bright and shadowed");
            var fittings=game.GetComponentsInChildren<Renderer>(true).Where(renderer=>renderer.name=="Folded light fitting").ToArray();
            check(fittings.Length==lamps.Length && fittings.All(renderer=>renderer.enabled && renderer.sharedMaterial.IsKeywordEnabled("_EMISSION") &&
                renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent>1),"All factory fixtures visibly glow");
        }
        static void Preview(CardboardFactoryGame game)
        {
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var station=game.station;var guard=station.guardModel;
            var guardPosition=guard.localPosition;var guardRotation=guard.localRotation;var guardScale=guard.localScale;
            try
            {
                camera.fieldOfView=60;
                camera.transform.SetPositionAndRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX+10,2.1f,-5),
                    Quaternion.LookRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX-3,3,6)-new Vector3(CardboardFactoryWorld.WorkshopCenterX+10,2.1f,-5)));
                CardboardFactorySceneBuilder.Capture("FactoryLampsRestored");
                station.guardClip.SampleAnimation(guard.gameObject,station.guardClip.length);
                camera.transform.SetPositionAndRotation(station.transform.TransformPoint(new Vector3(.1f,1.6f,-2.5f)),
                    Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(0,1.25f,0))-station.transform.TransformPoint(new Vector3(.1f,1.6f,-2.5f))));
                CardboardFactorySceneBuilder.Capture("NewTexturedZabralo");
            }
            finally
            {
                guard.localPosition=guardPosition;guard.localRotation=guardRotation;guard.localScale=guardScale;
                camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);
            }
        }
    }
}
