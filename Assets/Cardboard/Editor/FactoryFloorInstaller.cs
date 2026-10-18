using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryFloorInstaller
    {
        const string TexturePath="Assets/Game_Assets/Cardboard/Floor/Floor_1.png";
        const string MaterialPath="Assets/Game_Assets/Cardboard/Floor/Materials/Floor_1.mat";
        const float TileMetres=4;
        static readonly string[] Surfaces={"Main workshop floor surface","Canteen floor surface","Canteen doorway floor"};

        public static void Apply(Transform root)
        {
            var importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if(!importer) throw new InvalidOperationException("Floor texture is missing: "+TexturePath);
            if(importer.wrapMode!=TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.anisoLevel!=8 || importer.npotScale!=TextureImporterNPOTScale.None)
            {
                importer.wrapMode=TextureWrapMode.Repeat;
                importer.mipmapEnabled=true;importer.anisoLevel=8;importer.npotScale=TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                AssetDatabase.CreateAsset(material,MaterialPath);
            }
            material.SetTexture("_BaseMap",texture);material.SetTexture("_MainTex",texture);
            material.SetColor("_BaseColor",Color.white);material.SetColor("_Color",Color.white);
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",0);
            material.SetFloat("_EnvironmentReflections",0);material.SetFloat("_SpecularHighlights",0);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
            Directory.CreateDirectory("Assets/Cardboard/Generated");
            var report=new StringBuilder();
            foreach(string name in Surfaces)
            {
                Transform surface=null;
                foreach(var item in root.GetComponentsInChildren<Transform>(true))
                    if(item.name==name){surface=item;break;}
                if(!surface) throw new InvalidOperationException("Floor surface is missing: "+name);
                var filter=surface.GetComponent<MeshFilter>();
                var renderer=surface.GetComponent<MeshRenderer>();
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name=name+" tiled every "+TileMetres+" metres";
                var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 point=surface.TransformPoint(vertices[i]);
                    uv[i]=new Vector2(point.x/TileMetres,point.z/TileMetres);
                }
                mesh.uv=uv;mesh.RecalculateTangents();
                string path="Assets/Cardboard/Generated/"+name.Replace(' ','_')+"_Tiled.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
                Undo.RecordObjects(new UnityEngine.Object[]{filter,renderer},"Apply tiled floor texture");
                filter.sharedMesh=saved;renderer.sharedMaterial=material;
                EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
                var size=renderer.bounds.size;
                report.AppendLine(name+": "+size.x.ToString("F2")+" x "+size.z.ToString("F2")+" m; repeats="+(size.x/TileMetres).ToString("F2")+" x "+(size.z/TileMetres).ToString("F2"));
            }
            foreach(var item in root.GetComponentsInChildren<Transform>(true))
                if(item && item.name=="Floor fold") Undo.DestroyObjectImmediate(item.gameObject);
            report.AppendLine("SUCCESS: original floor texture, Repeat wrapping, planar world UVs at 4 x 4 metres, continuous scale and alignment through canteen doorway, matte white material.");
            File.WriteAllText("Library/FactoryFloorTexture.txt",report.ToString());
        }

        [MenuItem("Cardboard/Apply tiled floor texture")]
        public static void ApplyCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the floor in Edit Mode.");
            var world=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryWorld>();
            if(!world) throw new InvalidOperationException("Factory world is missing.");
            Apply(world.transform);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            try
            {
                var station=world.GetComponent<CardboardFactoryGame>().station.transform;
                camera.transform.position=new Vector3(station.position.x+3,2.2f,3.5f);
                camera.transform.rotation=Quaternion.LookRotation(new Vector3(station.position.x,0,8)-camera.transform.position);
                CardboardFactorySceneBuilder.Capture("TiledWorkshopFloor");
                camera.transform.position=new Vector3(-13,2,-19);
                camera.transform.rotation=Quaternion.LookRotation(new Vector3(-13,0,-24.5f)-camera.transform.position);
                CardboardFactorySceneBuilder.Capture("TiledCanteenFloor");
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
    }
}
