using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryMarkerWebFix
    {
        const string Active="Cardboard.MarkerWebVerification";
        static readonly List<string> report=new List<string>();
        static double started;
        static bool background;
        static FactoryMarkerWebFix(){EditorApplication.update+=Tick;}
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply hand fix in Edit Mode.");
            try
            {
                var lens=Camera.main;var hand=lens.GetComponent<CardboardMarkerHand>();
                var shader=Shader.Find(CardboardMarkerHand.ShaderName);if(!shader)throw new InvalidOperationException("Dedicated hand shader is missing.");
                hand.paperTemplate.shader=shader;hand.paperTemplate.SetFloat("_Cull",2);hand.paperTemplate.renderQueue=3100;hand.paperTemplate.enableInstancing=false;
                lens.cullingMask|=1<<CardboardMarkerHand.HandLayer;
                if(hand.viewCamera){hand.viewCamera.enabled=false;lens.GetUniversalAdditionalCameraData().cameraStack.Remove(hand.viewCamera);EditorUtility.SetDirty(hand.viewCamera);}
                EditorUtility.SetDirty(hand.paperTemplate);EditorUtility.SetDirty(lens);EditorUtility.SetDirty(lens.GetUniversalAdditionalCameraData());
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(lens.gameObject.scene);EditorSceneManager.SaveScene(lens.gameObject.scene);
                File.WriteAllText("Library/FactoryMarkerWebFix.txt","SUCCESS: first-person hand rendered by the base camera, dedicated serialized lit shader, outward faces, no world-depth occlusion.\nMaterial: "+AssetDatabase.GetAssetPath(hand.paperTemplate));
            }
            catch(Exception error){File.WriteAllText("Library/FactoryMarkerWebFix.txt",error.ToString());Debug.LogException(error);}
        }
        static void Check(bool ok,string description){report.Add((ok?"PASS ":"FAIL ")+description);}
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static Color32[] Frame(Camera camera)
        {
            var rt=new RenderTexture(640,400,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(640,400,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,640,400),0,0);image.Apply();return image.GetPixels32();
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Finish()
        {
            SessionState.SetBool(Active,false);Application.runInBackground=background;
            File.WriteAllLines("Library/FactoryMarkerWebVerification.txt",new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>35){report.Add("FAIL verification timeout");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player || Time.time<.4f)return;
            try
            {
                var game=CardboardFactoryGame.Instance;game.enabled=false;game.station.enabled=false;game.mainMenu.enabled=false;
                foreach(var worker in game.crowd.runners)worker.enabled=false;
                var camera=Camera.main;camera.GetComponent<CardboardCamera>().enabled=false;
                var marker=camera.GetComponent<CardboardMarker>();var hand=camera.GetComponent<CardboardMarkerHand>();
                marker.SetDrawingMode(true,false);marker.TracePointer(new Vector2(Screen.width*.52f,Screen.height*.5f),false);hand.RefreshPose(1);
                Check(hand.Visible && hand.Palm.gameObject.activeInHierarchy,"Drawing mode displays the marker and palm");
                Check((camera.cullingMask & (1<<CardboardMarkerHand.HandLayer))!=0 && (!hand.viewCamera || !hand.viewCamera.enabled),"Hand is rendered by the base camera without the additional camera");
                Check(hand.paperTemplate.shader.name==CardboardMarkerHand.ShaderName && hand.paperTemplate.shader.isSupported && AssetDatabase.Contains(hand.paperTemplate),"Supported dedicated hand shader is referenced by a serialized material for the build");
                Check(hand.Tip.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.shader.name==CardboardMarkerHand.ShaderName && r.sharedMaterial.GetFloat("_Cull")==2 && r.sharedMaterial.renderQueue==3100),"Every hand primitive uses outward faces and the late first-person rendering pass");
                var shown=Frame(camera);marker.SetDrawingMode(false,false);hand.RefreshPose(1);var hidden=Frame(camera);
                int changed=0;for(int i=0;i<shown.Length;i++)if(Math.Abs(shown[i].r-hidden[i].r)+Math.Abs(shown[i].g-hidden[i].g)+Math.Abs(shown[i].b-hidden[i].b)>24)changed++;
                Check(changed>500,"GPU: hand produces visible pixels in the final image: "+changed);
                Check(!hand.Visible,"Leaving drawing mode hides the hand");
                marker.SetDrawingMode(true,false);marker.TracePointer(new Vector2(Screen.width*.52f,Screen.height*.5f),false);hand.RefreshPose(1);
                CardboardFactorySceneBuilder.Capture("WebSafeMarkerHand");
                hand.enabled=false;Check(!hand.Visible,"Disabling the hand component removes its geometry from the base camera");
                Finish();
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
