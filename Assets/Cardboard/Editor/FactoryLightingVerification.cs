using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryLightingVerification
    {
        static float Sample(Camera camera)
        {
            var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;var target=camera.targetTexture;
            var texture=new Texture2D(128,128,TextureFormat.RGB24,false,true);
            try
            {
                camera.targetTexture=rt;
                var request=new RenderPipeline.StandardRequest{destination=rt};
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,128,128),0,0);texture.Apply();
                float sum=0;
                for(int x=60;x<68;x++)for(int y=60;y<68;y++)sum+=texture.GetPixel(x,y).grayscale;
                return sum/64;
            }
            finally
            {
                camera.targetTexture=target;RenderTexture.active=previous;
                Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);
            }
        }
        static void RenderChecks(CardboardFactoryGame game,Action<bool,string> check)
        {
            var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var enabled=lights.Select(light=>light.enabled).ToArray();
            var ambient=RenderSettings.ambientLight;var ambientMode=RenderSettings.ambientMode;float intensity=RenderSettings.ambientIntensity;
            bool fog=RenderSettings.fog;var sun=RenderSettings.sun;
            var rig=new GameObject("Temporary cardboard lighting verification");
            var material=new Material(Shader.Find(CardboardSurface.ShaderName));CardboardSurface.Configure(material);
            material.SetColor("_BaseColor",Color.white);
            try
            {
                foreach(var light in lights)light.enabled=false;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.ambientIntensity=0;RenderSettings.fog=false;
                var plate=GameObject.CreatePrimitive(PrimitiveType.Quad);plate.transform.SetParent(rig.transform,false);plate.layer=30;
                plate.transform.rotation=Quaternion.Euler(90,0,0);plate.transform.localScale=Vector3.one*2;
                var renderer=plate.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;CardboardSurface.Configure(renderer);
                var camera=new GameObject("Test lens").AddComponent<Camera>();camera.transform.SetParent(rig.transform);
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.6f;camera.nearClipPlane=.02f;camera.farClipPlane=10;
                camera.allowHDR=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var lamp=new GameObject("Test lamp").AddComponent<Light>();lamp.transform.SetParent(rig.transform);
                lamp.type=LightType.Directional;lamp.intensity=1;lamp.color=Color.white;lamp.cullingMask=1<<30;lamp.shadows=LightShadows.None;
                lamp.transform.rotation=Quaternion.Euler(90,0,0);RenderSettings.sun=lamp;
                camera.transform.SetPositionAndRotation(Vector3.up*2,Quaternion.LookRotation(Vector3.down,Vector3.forward));
                float top=Sample(camera);
                camera.transform.SetPositionAndRotation(Vector3.down*2,Quaternion.LookRotation(Vector3.up,Vector3.forward));
                float bottom=Sample(camera);
                check(top>.1f && bottom<top*.15f,"GPU: upper light illuminates front, not reverse (front="+top.ToString("F3")+", back="+bottom.ToString("F3")+")");

                var quad=plate.GetComponent<MeshFilter>().sharedMesh;
                plate.GetComponent<MeshFilter>().sharedMesh=game.instruction.sheet.GetComponent<MeshFilter>().sharedMesh;
                camera.orthographicSize=.3f;
                camera.transform.SetPositionAndRotation(Vector3.up*2,Quaternion.LookRotation(Vector3.down,Vector3.forward));
                float paperTop=Sample(camera);
                camera.transform.SetPositionAndRotation(Vector3.down*2,Quaternion.LookRotation(Vector3.up,Vector3.forward));
                float paperBottom=Sample(camera);
                check(paperTop>.1f && paperBottom<paperTop*.15f,"GPU: actual instruction blocks back lighting (top="+paperTop.ToString("F3")+", underside="+paperBottom.ToString("F3")+")");
                plate.GetComponent<MeshFilter>().sharedMesh=quad;camera.orthographicSize=.6f;
                RenderSettings.sun=null;lamp.type=LightType.Spot;lamp.transform.position=Vector3.up*3;
                lamp.range=6;lamp.spotAngle=100;lamp.innerSpotAngle=80;lamp.intensity=15;
                lamp.shadows=LightShadows.Hard;lamp.shadowStrength=1;lamp.shadowBias=0;lamp.shadowNormalBias=0;lamp.shadowNearPlane=.05f;
                var data=lamp.GetUniversalAdditionalLightData();data.usePipelineSettings=false;var shadowData=new SerializedObject(data);shadowData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=2;shadowData.ApplyModifiedPropertiesWithoutUndo();
                camera.transform.SetPositionAndRotation(new Vector3(0,.35f,1.5f),Quaternion.LookRotation(new Vector3(0,-.35f,-1.5f)));
                float clear=Sample(camera);
                var blocker=Object.Instantiate(plate,rig.transform);blocker.name="Opaque thin cardboard";blocker.transform.position=Vector3.up;
                blocker.transform.localScale=Vector3.one*1.4f;
                float blocked=Sample(camera);
                blocker.transform.rotation=Quaternion.Euler(270,0,0);
                float reversed=Sample(camera);
                check(clear>.08f && blocked<clear*.35f && reversed<clear*.35f,
                    "GPU: thin cardboard blocks lamp from either winding (clear="+clear.ToString("F3")+", shadow="+blocked.ToString("F3")+", reversed="+reversed.ToString("F3")+")");
            }
            finally
            {
                Object.DestroyImmediate(rig);Object.DestroyImmediate(material);
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;RenderSettings.ambientIntensity=intensity;
                RenderSettings.fog=fog;RenderSettings.sun=sun;
                for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=enabled[i];
            }
        }
        public static void Check(CardboardFactoryGame game,Action<bool,string> check)
        {
            int count=0,bad=0;var materials=new HashSet<Material>();
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
                if(renderer.gameObject.layer==10 || renderer.GetComponent<TextMesh>() || renderer.GetComponentInParent<Camera>())continue;
                if(renderer.shadowCastingMode!=ShadowCastingMode.TwoSided || !renderer.receiveShadows)bad++;
                count++;
            }
            check(count>100 && bad==0,"World renderers receive shadows and cast from both sides");
            foreach(var material in materials)
                if(material.HasProperty("_Cull"))check(material.GetFloat("_Cull")==0 && (!material.HasProperty("_Smoothness") || material.GetFloat("_Smoothness")==0),"Two-sided matte material: "+material.name);
            check(!materials.Any(material=>material.shader.name=="Universal Render Pipeline/Lit"),"Lit surfaces use correct normals on their reverse sides");
            var shader=Shader.Find(CardboardSurface.ShaderName);
            check(shader && !ShaderUtil.ShaderHasError(shader),"Two-sided lit shader compiles successfully");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Cardboard/Generated/LunchGlassOfWater.asset");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(FactoryLunchProps.ModelPath).GetComponentInChildren<MeshFilter>().sharedMesh;
            check(mesh && original.uv.SequenceEqual(mesh.uv),"Glass keeps authored UVs unchanged");
            check(game.lunch.food.Length==1 && game.lunch.diners.bites.All(bite=>bite && bite.name==FactoryLunchProps.CupName),"Player and all eleven coworkers have glasses instead of food cubes");
            foreach(var meal in game.lunch.diners.meals)
            {
                check(!meal.Cast<Transform>().Any(child=>child.name.StartsWith("Food cube")),"No colored food cubes on "+meal.name);
                check(meal.GetComponentsInChildren<Renderer>(true).Count(item=>item.name==FactoryLunchProps.CupName && item.gameObject.activeSelf)==1,"New glass replaces the old embedded glass on "+meal.name);
            }
            int lamps=0;
            foreach(var lamp in game.GetComponentsInChildren<Light>(true))
                if(lamp.name=="Practical workshop lamp" || lamp.name=="Canteen warm light")
                {lamps++;check(lamp.type==LightType.Spot && lamp.shadows!=LightShadows.None && lamp.shadowStrength==1 && lamp.shadowNormalBias<.02f,"Ceiling lamp casts tight opaque shadows");}
            check(lamps>=10,"Workshop and canteen lamps are configured");
            RenderChecks(game,check);
        }
        public static void Preview(CardboardFactoryGame game)
        {
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var paper=game.instruction.sheet;var paperPosition=paper.position;var paperRotation=paper.rotation;
            try
            {
                var center=game.station.transform.TransformPoint(new Vector3(-.8f,1.2f,-3));
                paper.SetPositionAndRotation(center,Quaternion.Euler(90,0,0));
                camera.transform.SetPositionAndRotation(center+new Vector3(0,1.05f,-.7f),Quaternion.LookRotation(new Vector3(0,-1.05f,.7f)));
                CardboardFactorySceneBuilder.Capture("CardboardLitAbove");
                camera.transform.SetPositionAndRotation(center+new Vector3(0,-.65f,-.65f),Quaternion.LookRotation(new Vector3(0,.65f,.65f)));
                CardboardFactorySceneBuilder.Capture("CardboardShadedBelow");
                var tray=game.lunch.tray;
                camera.transform.SetPositionAndRotation(tray.position+new Vector3(.4f,.5f,.65f),Quaternion.LookRotation(tray.position+Vector3.up*.07f-(tray.position+new Vector3(.4f,.5f,.65f))));
                CardboardFactorySceneBuilder.Capture("TexturedGlassOnTray");
            }
            finally{paper.SetPositionAndRotation(paperPosition,paperRotation);camera.transform.SetPositionAndRotation(position,rotation);}
        }
        public static void RunCurrent()
        {
            var report=new List<string>();var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            try{Check(game,(passed,detail)=>report.Add((passed?"PASS ":"FAIL ")+detail));Preview(game);}
            catch(Exception error){report.Add("FAIL "+error);}
            foreach(var renderer in game.lunch.tray.Find("podnos • normalized model").GetComponentsInChildren<Renderer>(true))
                report.Add("TRAY "+renderer.name+" mesh="+renderer.GetComponent<MeshFilter>()?.sharedMesh.name+" bounds="+renderer.bounds+" local="+game.lunch.tray.InverseTransformPoint(renderer.bounds.center));
            File.WriteAllLines("Library/FactoryLightingVerification.txt",report);
        }
    }
}
