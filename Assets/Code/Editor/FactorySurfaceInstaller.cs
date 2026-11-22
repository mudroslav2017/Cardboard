using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactorySurfaceInstaller
    {
        public static void RestoreWorkshopLighting(Transform world)
        {
            var glow=FactoryPresentationInstaller.Paper("FactoryLampGlow",CardboardArt.Hex("FFF1CB"));
            glow.SetColor("_EmissionColor",CardboardArt.Hex("FFE1A3")*3);
            glow.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;glow.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(glow);
            foreach(var light in world.GetComponentsInChildren<Light>(true))
                if(light.name=="Practical workshop lamp")
                {
                    light.gameObject.SetActive(true);light.enabled=true;
                    light.intensity=12;light.range=12;light.color=CardboardArt.Hex("FFDEA2");
                }
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))
                if(renderer.name=="Folded light fitting")
                {
                    renderer.gameObject.SetActive(true);renderer.enabled=true;
                    renderer.sharedMaterial=glow;CardboardSurface.Configure(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
        }
        public static void Apply(Transform world)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=CardboardArt.Hex("8C9989");
            RenderSettings.ambientEquatorColor=CardboardArt.Hex("788574");
            RenderSettings.ambientGroundColor=CardboardArt.Hex("3E473A");
            var report=new StringBuilder();var materials=new HashSet<Material>();
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
                CardboardSurface.Configure(renderer);
            }
            // Include embedded generated materials, not just the first material in a .asset container.
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(AssetImporter.GetAtPath(path) is ModelImporter)continue;
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is Material material)materials.Add(material);
            }
            foreach(var material in materials)
            {
                CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
                report.AppendLine(material.name+": "+material.shader.name);
            }
            foreach(var light in world.GetComponentsInChildren<Light>(true))
            {
                if(light.name=="Practical workshop lamp" || light.name=="Canteen warm light")
                {
                    // A down-facing spot needs one shadow tile instead of six cubemap faces per ceiling lamp.
                    light.type=LightType.Spot;light.spotAngle=140;light.innerSpotAngle=100;
                    light.transform.rotation=Quaternion.Euler(90,0,0);
                    light.shadows=LightShadows.Soft;light.shadowStrength=1;
                    light.shadowBias=.02f;light.shadowNormalBias=.015f;light.shadowNearPlane=.05f;
                    light.shadowResolution=LightShadowResolution.Medium;
                    var data=light.GetUniversalAdditionalLightData();data.usePipelineSettings=false;var shadowData=new SerializedObject(data);shadowData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=1;shadowData.ApplyModifiedPropertiesWithoutUndo();
                }
                if(light.type==LightType.Directional)
                {
                    light.shadows=LightShadows.Soft;light.shadowStrength=1;
                    light.shadowBias=.02f;light.shadowNormalBias=.015f;
                }
            }
            RestoreWorkshopLighting(world);
            foreach(string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets/Settings"}))
            {
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var serialized=new SerializedObject(pipeline);
                void Set(string name,int value){var property=serialized.FindProperty(name);if(property!=null)property.intValue=value;}
                void Flag(string name){var property=serialized.FindProperty(name);if(property!=null)property.boolValue=true;}
                void Number(string name,float value){var property=serialized.FindProperty(name);if(property!=null)property.floatValue=value;}
                Flag("m_AdditionalLightShadowsSupported");Flag("m_MainLightShadowsSupported");
                Set("m_MainLightShadowmapResolution",4096);Set("m_AdditionalLightsShadowmapResolution",4096);
                Number("m_ShadowDepthBias",.02f);Number("m_ShadowNormalBias",.015f);Number("m_ShadowDistance",65);
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            }
            File.WriteAllText("Library/FactorySurfaceUpdate.txt","SUCCESS: two-sided materials with face-aware normals, two-sided shadow casters and shadowed ceiling lamps.\n"+report);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before applying surfaces.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Directory.CreateDirectory("Backups/TwoSidedLighting");
            string backup="Backups/TwoSidedLighting/FactoryShift-before-lighting.unity";
            if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            FactoryLunchProps.Apply(game.lunch);Apply(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
        }
    }
}
