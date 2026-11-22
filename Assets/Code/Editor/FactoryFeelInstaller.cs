using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using static Cardboard.CardboardArt;

namespace Cardboard.Editor
{
    public static class FactoryFeelInstaller
    {
        public static void Apply(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();var lens=game.mainMenu.lens;var grade=game.captureSequence.grade;
            var old=grade.presentation;
            if(old)
            {
                if(old.overlay){lens.GetUniversalAdditionalCameraData().cameraStack.Remove(old.overlay);Object.DestroyImmediate(old.overlay.gameObject);}
                if(old.gearRoot)Object.DestroyImmediate(old.gearRoot.gameObject);
                if(old.fill)Object.DestroyImmediate(old.fill.gameObject);
                if(old.wave)Object.DestroyImmediate(old.wave.gameObject);
                Object.DestroyImmediate(old);
            }
            var presentation=grade.gameObject.AddComponent<FactoryDeathPresentation>();grade.presentation=presentation;presentation.lens=lens;
            lens.fieldOfView=CardboardCamera.GameplayFov;lens.cullingMask &= ~(1<<FactoryDeathPresentation.Layer);
            presentation.overlay=Group("Death • dimensional gear overlay camera",lens.transform,Vector3.zero).gameObject.AddComponent<Camera>();
            presentation.overlay.cullingMask=1<<FactoryDeathPresentation.Layer;presentation.overlay.fieldOfView=45;
            presentation.overlay.nearClipPlane=.01f;presentation.overlay.farClipPlane=8;presentation.overlay.clearFlags=CameraClearFlags.Depth;
            var data=presentation.overlay.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;
            lens.GetUniversalAdditionalCameraData().cameraStack.Add(presentation.overlay);
            presentation.gearRoot=Group("Death • radial cardboard gear corners",lens.transform,Vector3.zero);
            presentation.gears=new Transform[12];
            for(int corner=0;corner<4;corner++)for(int kind=0;kind<3;kind++)
            {
                int index=corner*3+kind;
                var pivot=Group("Corner "+(corner+1)+" • gear "+(kind+1),presentation.gearRoot,Vector3.zero);presentation.gears[index]=pivot;
                var prefab=Resources.Load<GameObject>("FactoryGears/Gear"+(kind+1));
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,pivot);
                var bounds=instance.GetComponent<MeshFilter>().sharedMesh.bounds;
                instance.transform.localScale=Vector3.one/Mathf.Max(bounds.size.x,bounds.size.y);
                instance.transform.localRotation=Quaternion.Euler(0,180,0);
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {renderer.gameObject.layer=FactoryDeathPresentation.Layer;CardboardSurface.Configure(renderer);}
            }
            presentation.fill=Group("Death • warm paper key light",lens.transform,new Vector3(-.5f,.7f,.15f)).gameObject.AddComponent<Light>();
            presentation.fill.type=LightType.Point;presentation.fill.range=5;presentation.fill.intensity=1.25f;
            presentation.fill.color=Hex("FFF0D5");presentation.fill.cullingMask=1<<FactoryDeathPresentation.Layer;
            string materialPath=FactoryPresentationInstaller.Generated+"CrushWave.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Cardboard/CrushWaveUI"));AssetDatabase.CreateAsset(material,materialPath);}
            CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
            var wave=new GameObject("Crush • glowing red radial wave",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect=wave.GetComponent<RectTransform>();rect.SetParent(grade.transform,false);rect.SetAsFirstSibling();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            presentation.wave=wave.GetComponent<Image>();presentation.wave.material=material;presentation.wave.raycastTarget=false;
            presentation.Layout();presentation.overlay.enabled=false;presentation.gearRoot.gameObject.SetActive(false);presentation.fill.enabled=false;
            grade.Hide();EditorUtility.SetDirty(grade);EditorUtility.SetDirty(presentation);EditorUtility.SetDirty(lens);
        }
        [MenuItem("Cardboard/Upgrade factory atmosphere, camera and crush presentation")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before upgrading the factory.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();EditorSceneManager.SaveScene(game.gameObject.scene);
            Directory.CreateDirectory("Backups/AtmosphereCameraCrush");
            string backup="Backups/AtmosphereCameraCrush/FactoryShift-before-feel-update.unity";if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            FactoryAtmosphereInstaller.Enhance(game.transform);Apply(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryFeelUpdate.txt","SUCCESS: denser air and floor debris, subtle depth-clipped haze, gameplay FOV 80, walking/running sway; facing workers and a red crush wave with all three gear models in every corner.");
        }
    }
}
