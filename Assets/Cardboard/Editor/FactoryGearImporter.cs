using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryGearImporter
    {
        public const string Root="Assets/Cardboard/Resources/FactoryGears";
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            for(int type=1;type<=3;type++)
            {
                string source="Assets/Game_Assets/Cardboard/gear wheel/"+type;
                string[] models=Directory.GetFiles(source,"*.fbx",SearchOption.AllDirectories);
                string[] textures=Directory.GetFiles(source,"*.png",SearchOption.AllDirectories);
                if(models.Length!=1 || textures.Length!=1) throw new InvalidOperationException("Expected one model and one texture for gear "+type+" in "+source);
                string modelPath=models[0].Replace('\\','/'),texturePath=textures[0].Replace('\\','/');
                var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
                if(!importer.isReadable) {importer.isReadable=true;importer.SaveAndReimport();}
                var textureImporter=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                if(!textureImporter.sRGBTexture || textureImporter.npotScale!=TextureImporterNPOTScale.None)
                {textureImporter.sRGBTexture=true;textureImporter.npotScale=TextureImporterNPOTScale.None;textureImporter.SaveAndReimport();}
                string materialPath=Root+"/Gear"+type+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
                material.name="Gear "+type+" • original atlas • both sides";
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.color=Color.white;
                material.SetFloat("_Smoothness",0);material.SetFloat("_Metallic",0);material.SetFloat("_Cull",0);material.doubleSidedGI=true;CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
                var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                try
                {
                    var filter=instance.GetComponentInChildren<MeshFilter>();
                    Mesh mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Imported gear "+type+" • authored UV";
                    var vertices=mesh.vertices;var normals=mesh.normals;
                    Matrix4x4 pose=Matrix4x4.Rotate(Quaternion.Euler(0,90,0))*filter.transform.localToWorldMatrix;
                    Matrix4x4 normalPose=pose.inverse.transpose;
                    Bounds bounds=default;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        vertices[i]=pose.MultiplyPoint3x4(vertices[i]);if(i==0) bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);
                        if(normals.Length==vertices.Length) normals[i]=normalPose.MultiplyVector(normals[i]).normalized;
                    }
                    float scale=2.16f/Mathf.Max(bounds.size.x,bounds.size.y);
                    for(int i=0;i<vertices.Length;i++) vertices[i]=(vertices[i]-bounds.center)*scale;
                    mesh.vertices=vertices;if(normals.Length==vertices.Length) mesh.normals=normals;else mesh.RecalculateNormals();
                    mesh.RecalculateBounds();mesh.RecalculateTangents();
                    string meshPath=Root+"/Gear"+type+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing) {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
                    var root=new GameObject("Imported gear "+type);
                    try
                    {
                        root.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++) materials[i]=material;
                        root.AddComponent<MeshRenderer>().sharedMaterials=materials;
                        PrefabUtility.SaveAsPrefabAsset(root,Root+"/Gear"+type+".prefab");
                    }
                    finally {UnityEngine.Object.DestroyImmediate(root);}
                }
                finally {UnityEngine.Object.DestroyImmediate(instance);}
            }
            AssetDatabase.SaveAssets();
        }
    }
}
