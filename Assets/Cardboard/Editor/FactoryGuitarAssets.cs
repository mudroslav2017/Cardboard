using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Cardboard.Editor
{
    public static class FactoryGuitarAssets
    {
        public const string Model="Assets/Game_Assets/Cardboard/Gitar/guitar_01 (2).fbx";
        const string Root="Assets/Cardboard/Resources/GuitarPerformance";
        public static void Audit()
        {
            var text=new StringBuilder();var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                text.AppendLine(filter.name+" bounds="+filter.sharedMesh.bounds+" world="+filter.GetComponent<Renderer>().bounds);
            Object.DestroyImmediate(root);
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(filter.sharedMesh && filter.sharedMesh.name.ToLowerInvariant().Contains("guitar"))text.AppendLine("SCENE "+filter.name+" "+filter.GetComponent<Renderer>().bounds+" position="+filter.transform.position);
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var material in renderer.sharedMaterials)if(material && material.mainTexture && material.mainTexture.name.ToLowerInvariant().Contains("guitar"))text.AppendLine("GUITAR TEXTURE "+renderer.name+" bounds="+renderer.bounds+" mesh="+(renderer.GetComponent<MeshFilter>()?renderer.GetComponent<MeshFilter>().sharedMesh.name:"none")+" parent="+renderer.transform.parent);
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))text.AppendLine("CAM "+camera.name+" mask="+camera.cullingMask+" enabled="+camera.enabled);
            File.WriteAllText("Library/FactoryGuitarAudit.txt",text.ToString());
        }
        [MenuItem("Cardboard/Prepare guitar recording assets")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                var filter=source.GetComponentInChildren<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);
                var vertices=mesh.vertices;var normals=mesh.normals;
                var pose=Matrix4x4.Rotate(Quaternion.Euler(90,0,0))*filter.transform.localToWorldMatrix;
                var normalPose=pose.inverse.transpose;Bounds bounds=default;
                for(int i=0;i<vertices.Length;i++)
                {vertices[i]=pose.MultiplyPoint3x4(vertices[i]);if(i==0)bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);}
                float top=0,bottom=0;
                foreach(var v in vertices)
                {if(v.y>bounds.max.y-bounds.size.y*.2f)top=Mathf.Max(top,Mathf.Abs(v.x-bounds.center.x));if(v.y<bounds.min.y+bounds.size.y*.2f)bottom=Mathf.Max(bottom,Mathf.Abs(v.x-bounds.center.x));}
                var upright=top>bottom?Quaternion.Euler(0,0,180):Quaternion.identity;
                Bounds stood=default;
                for(int i=0;i<vertices.Length;i++)
                {vertices[i]=upright*vertices[i];if(i==0)stood=new Bounds(vertices[i],Vector3.zero);else stood.Encapsulate(vertices[i]);}
                var origin=new Vector3(stood.center.x,stood.min.y,stood.center.z);float scale=1.4f/stood.size.y;
                for(int i=0;i<vertices.Length;i++)
                {vertices[i]=(vertices[i]-origin)*scale;if(normals.Length==vertices.Length)normals[i]=(upright*normalPose.MultiplyVector(normals[i])).normalized;}
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                mesh.name="Imported guitar, upright, original UV";
                string meshPath=Root+"/Guitar.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,meshPath);
                string matPath=Root+"/Guitar.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
                mat.name="Guitar original painted texture";mat.color=Color.white;
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Cardboard/Gitar/M_guitar_base color.png");
                mat.SetTexture("_BaseMap",mat.mainTexture);CardboardSurface.Configure(mat);EditorUtility.SetDirty(mat);
                var root=new GameObject("Guitar");
                try
                {root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=mat;PrefabUtility.SaveAsPrefabAsset(root,Root+"/Guitar.prefab");}
                finally{Object.DestroyImmediate(root);}
                var ink=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Ink.mat");
                if(!ink){ink=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(ink,Root+"/Ink.mat");}
                ink.color=new Color(.12f,.075f,.035f);CardboardSurface.Configure(ink);EditorUtility.SetDirty(ink);
                AssetDatabase.SaveAssets();
                File.WriteAllText("Library/FactoryGuitarAssets.txt","SUCCESS guitar prepared; bounds="+mesh.bounds+" neck flip="+(top>bottom));
            }
            finally{Object.DestroyImmediate(source);}
        }
    }
}