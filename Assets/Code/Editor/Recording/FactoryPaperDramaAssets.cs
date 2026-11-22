using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Cardboard.Editor
{
    public static class FactoryPaperDramaAssets
    {
        public static void Audit()
        {
            var report=new StringBuilder();
            foreach(string part in new[]{"House 1/house_01.fbx","House 2/house_02.fbx","Sofa 2/sofa_02.fbx","Flowers/flowers.fbx","Badge/badge.fbx"})
            {
                var path=Source+part;var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));report.AppendLine("ASSET "+part);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())report.AppendLine(renderer.name+" bounds="+renderer.bounds+" rot="+renderer.transform.rotation.eulerAngles+" scale="+renderer.transform.lossyScale);
                Object.DestroyImmediate(root);
            }
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(filter.name.ToLowerInvariant().Contains("house") || filter.name.ToLowerInvariant().Contains("sofa"))
                    report.AppendLine("SCENE "+filter.name+" bounds="+filter.GetComponent<Renderer>().bounds+" pos="+filter.transform.position+" rot="+filter.transform.eulerAngles+" scale="+filter.transform.lossyScale+" mesh="+filter.sharedMesh.name+" asset="+AssetDatabase.GetAssetPath(filter.sharedMesh));
            }
            File.WriteAllText("Library/FactoryPaperDramaAudit.txt",report.ToString());
        }
        const string Source="Assets/Game_Assets/Tik Tok Trends/PaperDrama/Source/";
        public const string Root="Assets/Game_Assets/Tik Tok Trends/PaperDrama/Resources/PaperDrama";
        static Material Material(string name,Color color,Texture texture=null)
        {
            string path=Root+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;if(texture){mat.mainTexture=texture;mat.SetTexture("_BaseMap",texture);}CardboardSurface.Configure(mat);EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Model(string name,string sourcePath,string texturePath,Quaternion turn,float size=0,bool width=false)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(sourcePath);if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));var root=new GameObject(name);
            try
            {
                var filter=source.GetComponentInChildren<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;
                var pose=Matrix4x4.Rotate(turn)*filter.transform.localToWorldMatrix;var normalPose=pose.inverse.transpose;Bounds bounds=default;
                for(int j=0;j<vertices.Length;j++){vertices[j]=pose.MultiplyPoint3x4(vertices[j]);if(j==0)bounds=new Bounds(vertices[j],Vector3.zero);else bounds.Encapsulate(vertices[j]);}
                float scale=size>0?size/(width?bounds.size.x:bounds.size.y):1;var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                for(int j=0;j<vertices.Length;j++){vertices[j]=(vertices[j]-origin)*scale;if(normals.Length==vertices.Length)normals[j]=normalPose.MultiplyVector(normals[j]).normalized;}
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();mesh.name=name+" original UV";
                string meshPath=Root+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,meshPath);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=Material(name,Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                return PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(root);}
        }
        static AudioClip Sound(string name,float seconds,int kind)
        {
            string path=Root+"/"+name+".wav";const int rate=22050;var data=new float[Mathf.CeilToInt(seconds*rate)];var random=new System.Random(905+kind);float smooth=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,u=t/seconds,noise=(float)random.NextDouble()*2-1;smooth=Mathf.Lerp(smooth,noise,kind==1?.035f:.17f);
                float envelope=kind==0?Mathf.Pow(Mathf.Sin(u*Mathf.PI),1.7f):kind==1?Mathf.Exp(-u*4)*Mathf.Min(1,u*50):1;
                float wave=kind==0?(noise*.22f+Mathf.Sin(t*(410-220*u)*Mathf.PI*2)*.12f):kind==1?(smooth*2.5f+Mathf.Sin(t*42)*.12f):noise*.12f;
                data[i]=Mathf.Clamp(wave*envelope,-1,1);
            }
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*2);foreach(float sample in data)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            string stale=Root+"/"+name+".asset";if(File.Exists(stale))AssetDatabase.DeleteAsset(stale);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        [MenuItem("Cardboard/Prepare paper drama assets")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);
            var config=AssetDatabase.LoadAssetAtPath<FactoryPaperDramaSettings>(Root+"/Settings.asset");
            if(!config){config=ScriptableObject.CreateInstance<FactoryPaperDramaSettings>();AssetDatabase.CreateAsset(config,Root+"/Settings.asset");}
            config.house1=Model("House1",Source+"House 1/house_01.fbx",Source+"House 1/M_house_01_base color.png",Quaternion.identity);
            config.house2=Model("House2",Source+"House 2/house_02.fbx",Source+"House 2/M_house_02_base color.png",Quaternion.identity);
            config.sofa=Model("Sofa2",Source+"Sofa 2/sofa_02.fbx",Source+"Sofa 2/M_sofa_02_base color.png",Quaternion.Euler(0,90,0),3.8f,true);
            config.flowers=Model("Flowers",Source+"Flowers/flowers.fbx",Source+"M_items_base color.png",Quaternion.Euler(0,90,0),.8f);
            config.badge=Model("Badge",Source+"Badge/badge.fbx",Source+"M_items_base color.png",Quaternion.Euler(0,90,0),.2f);
            config.boss=Material("KolyaBoss",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Kolya/Kolya_Normal 1.png"));
            config.floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game_Assets/Cardboard/Floor/Materials/Floor_1.mat");
            config.paper=Material("HousePaper",new Color(.64f,.42f,.23f));config.ink=Material("PaperInk",new Color(.10f,.065f,.035f));
            config.cream=Material("WarmPaper",new Color(.85f,.71f,.5f));config.heart=Material("PaperHearts",new Color(.75f,.16f,.17f));config.rain=Material("SilverRain",new Color(.55f,.68f,.8f));config.rain.shader=Shader.Find("Universal Render Pipeline/Unlit");EditorUtility.SetDirty(config.rain);
            config.dialogueVoices=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/1f35504e23eda9e.mp3"),AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/78651f0f862d281.mp3"),AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/fde0ce4ab8216a9.mp3")};
            var floorAudio=Object.FindFirstObjectByType<FactoryFloorAudio>();config.plop=floorAudio && floorAudio.clip?floorAudio.clip:AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/бросок предмета.mp3");
            config.whoosh=Sound("Paper fwoosh",.6f,0);config.thunder=Sound("Cardboard thunder",2.7f,1);config.rainLoop=Sound("Rain ambience",3,2);
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();File.WriteAllText("Library/FactoryPaperDramaAssets.txt","SUCCESS paper drama assets prepared, source UV preserved.");
        }
    }
}