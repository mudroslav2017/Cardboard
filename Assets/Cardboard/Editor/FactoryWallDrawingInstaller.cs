using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard.Editor
{
    public static class FactoryWallDrawingInstaller
    {
        public static bool IsWall(MeshRenderer renderer)
        {
            return renderer.GetComponent<MeshFilter>() && renderer.GetComponent<BoxCollider>() &&
                ((renderer.name.IndexOf("wall",StringComparison.OrdinalIgnoreCase)>=0 && renderer.name!="Wall corrugation") || renderer.name=="Doorway infill");
        }
        public static int Install(Transform world)
        {
            const string root="Assets/Cardboard/Generated/";
            var blank=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"WallMarkerBlank.asset");
            if(!blank)
            {
                blank=new Texture2D(1,1,TextureFormat.RGBA32,false){name="Empty wall ink",wrapMode=TextureWrapMode.Clamp};
                blank.SetPixel(0,0,Color.clear);blank.Apply();AssetDatabase.CreateAsset(blank,root+"WallMarkerBlank.asset");
            }
            var material=FactoryPresentationInstaller.Paper("WallMarkerInk",Color.white,blank);
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster",false);material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormals",false);
            EditorUtility.SetDirty(material);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(root+"WallMarkerFace.asset");
            if(!mesh)
            {
                mesh=new Mesh{name="Wall marker canvas"};
                mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,root+"WallMarkerFace.asset");
            }
            int count=0;
            foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!IsWall(renderer))continue;
                var wall=renderer.GetComponent<CardboardWallDrawing>();if(!wall)wall=renderer.gameObject.AddComponent<CardboardWallDrawing>();
                wall.inkMaterial=material;wall.faceMesh=mesh;EditorUtility.SetDirty(wall);count++;
            }
            return count;
        }
        [MenuItem("Cardboard/Enable marker drawing on walls")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install wall drawing in Edit Mode.");
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();int count=Install(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryWallDrawingInstall.txt","Installed marker canvases on "+count+" walls; original wall textures and collisions preserved.");
        }
    }
}
