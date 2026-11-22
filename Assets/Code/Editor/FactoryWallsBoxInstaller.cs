using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryWallsBoxInstaller
    {
        const string AssetsRoot="Assets/Game_Assets/Cardboard/";
        const string BoxModel=AssetsRoot+"Box/Model/Box.fbx";
        const string BoxTexture=AssetsRoot+"Box/Texture/Box.png";
        const float TileHeight=CardboardFactoryWorld.WallHeight;
        static readonly HashSet<string> WallNames=new HashSet<string>
        {
            "North workstation wall","West workshop wall","East wall above exit","East wall below exit",
            "South wall west of canteen door","South mechanism wall","Canteen south wall",
            "Canteen west wall","Canteen east wall","Canteen north wall","Canteen passage side wall"
        };

        static Texture2D Texture(string path,TextureWrapMode wrap)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Texture is missing: "+path);
            if(importer.wrapMode!=wrap || !importer.mipmapEnabled || importer.anisoLevel!=8 || importer.npotScale!=TextureImporterNPOTScale.None)
            {
                importer.wrapMode=wrap;importer.mipmapEnabled=true;importer.anisoLevel=8;
                importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string name,Texture2D texture)
        {
            var material=FactoryPresentationInstaller.Paper(name,Color.white,texture);
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            EditorUtility.SetDirty(material);return material;
        }
        static Bounds BoundsIn(Transform visual,Transform space)
        {
            bool first=true;Bounds result=default;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var bounds=renderer.localBounds;
                var point=space.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
                if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
            }
            return result;
        }
        static int Boxes(Transform world,Material material,StringBuilder report)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(BoxModel);
            if(!model)throw new InvalidOperationException("Box model is missing.");
            int count=0;
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            foreach(var bath in station.baths)
            {
                var old=bath.Find("Imported Box vessel");if(old)Undo.DestroyObjectImmediate(old.gameObject);
                var wrapper=new GameObject("Imported Box vessel").transform;wrapper.SetParent(bath,false);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,wrapper);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                    CardboardSurface.Configure(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var bounds=BoundsIn(wrapper,bath);
                // The re-export changed the FBX origin. Align the vessel itself, not the old offset.
                wrapper.localPosition+=new Vector3(0,.367f,0)-bounds.center;
                Undo.RegisterCreatedObjectUndo(wrapper.gameObject,"Update textured vessel");
                bounds=BoundsIn(wrapper,bath);
                Require(Vector3.Distance(bounds.center,new Vector3(0,.367f,0))<.001f,"Vessel is centered under its liquid");
                var liquid=BoundsIn(bath.Find("Finishing liquid"),bath);
                Require(liquid.min.x>bounds.min.x && liquid.max.x<bounds.max.x && liquid.min.z>bounds.min.z && liquid.max.z<bounds.max.z && liquid.max.y<bounds.max.y,
                    "Liquid fits inside the new vessel");
                Require(bath.Find("Bath gear icon") && bath.Find("Vessel collision"),"Bath icon and collision are preserved");
                report.AppendLine("PASS "+station.name+" / "+bath.name+": new Box.fbx, original UV, untinted texture, liquid inside; bounds="+bounds);
                count++;
            }
            return count;
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}

        static Mesh Panels(Transform wall,Bounds bounds,Material[] materials,int variant,float tileHeight)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=materials.Select(_=>new List<int>()).ToArray();
            foreach(var normal in new[]{Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down})
            {
                var right=Mathf.Abs(normal.y)<.5f?Vector3.Cross(normal,Vector3.up):Vector3.right;
                var up=Mathf.Abs(normal.y)<.5f?Vector3.up:Vector3.Cross(right,normal);
                float halfU=Vector3.Dot(bounds.extents,Abs(right)),halfV=Vector3.Dot(bounds.extents,Abs(up));
                var start=bounds.center+Vector3.Scale(bounds.extents,normal)-right*halfU-up*halfV;
                float width=wall.TransformVector(right*halfU*2).magnitude,height=wall.TransformVector(up*halfV*2).magnitude;
                int column=0;
                for(float u=0;u<width-.00001f;column++)
                {
                    int firstVariant=(variant+column)%materials.Length;
                    var texture=materials[firstVariant].GetTexture("_BaseMap");
                    float tileWidth=tileHeight*texture.width/texture.height;
                    float uEnd=Mathf.Min(width,u+tileWidth);int row=0;
                    for(float v=0;v<height-.00001f;row++)
                    {
                        float vEnd=Mathf.Min(height,v+tileHeight);int index=vertices.Count;
                        foreach(var point in new[]{new Vector2(u,v),new Vector2(uEnd,v),new Vector2(uEnd,vEnd),new Vector2(u,vEnd)})
                        {
                            vertices.Add(start+right*(point.x/width*halfU*2)+up*(point.y/height*halfV*2));
                            normals.Add(normal);uv.Add(new Vector2((point.x-u)/tileWidth,(point.y-v)/tileHeight));
                        }
                        triangles[(firstVariant+row)%materials.Length].AddRange(new[]{index,index+2,index+1,index,index+3,index+2});
                        v=vEnd;
                    }
                    u=uEnd;
                }
            }
            var mesh=new Mesh {name=wall.name+" • undistorted cardboard panels"};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=materials.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        static Vector3 Abs(Vector3 value)=>new Vector3(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!saved){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            saved.Clear();saved.indexFormat=mesh.indexFormat;saved.name=mesh.name;
            saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.tangents=mesh.tangents;
            saved.subMeshCount=mesh.subMeshCount;
            for(int i=0;i<mesh.subMeshCount;i++)saved.SetIndices(mesh.GetIndices(i),mesh.GetTopology(i),i,false);
            saved.bounds=mesh.bounds;saved.UploadMeshData(false);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
        }
        static void CheckUV(Transform wall,Mesh mesh,Material[] materials,float tileHeight)
        {
            var points=mesh.vertices;var uv=mesh.uv;
            for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
            {
                var texture=materials[submesh].GetTexture("_BaseMap");
                var scale=new Vector2(tileHeight*texture.width/texture.height,tileHeight);
                var indices=mesh.GetTriangles(submesh);
                for(int i=0;i<indices.Length;i+=3)for(int edge=0;edge<3;edge++)
                {
                    int a=indices[i+edge],b=indices[i+(edge+1)%3];
                    float metres=wall.TransformVector(points[a]-points[b]).magnitude;
                    Require(Mathf.Abs(metres-Vector2.Scale(uv[a]-uv[b],scale).magnitude)<.002f,"Wall texture aspect and physical scale must be preserved: "+wall.name);
                }
            }
        }
        public const string BackboardTexture="Assets/Game_Assets/Cardboard/Walls/Без названия45_20261018220257.png";
        public static Material[] WallMaterials()
        {
            var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{AssetsRoot+"Walls"})
                .Select(AssetDatabase.GUIDToAssetPath).Where(path=>path!=BackboardTexture).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            var illustrated=paths.Where(path=>path.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)).ToArray();
            var plain=paths.Where(path=>!path.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)).ToArray();
            var mixed=new List<string>();
            for(int i=0;i<Mathf.Max(illustrated.Length,plain.Length);i++)
            {if(i<illustrated.Length)mixed.Add(illustrated[i]);if(i<plain.Length)mixed.Add(plain[i]);}
            Require(mixed.Count>0,"Wall textures are missing.");
            return mixed.Select((path,i)=>Material("CardboardWall_"+(i+1),Texture(path,TextureWrapMode.Repeat))).ToArray();
        }
        public static void Paint(Transform part,string assetName,Material[] materials,int variant=0,float tileHeight=TileHeight)
        {
            var filter=part.GetComponent<MeshFilter>();var renderer=part.GetComponent<MeshRenderer>();
            var previous=renderer.bounds;
            var mesh=Panels(part,filter.sharedMesh.bounds,materials,variant,tileHeight);
            Undo.RecordObjects(new Object[]{filter,renderer},"Apply cardboard surface");
            filter.sharedMesh=SaveMesh(mesh,FactoryPresentationInstaller.Generated+assetName+".asset");
            renderer.sharedMaterials=materials;CardboardSurface.Configure(renderer);
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
            Require(Vector3.Distance(previous.center,renderer.bounds.center)<.001f && Vector3.Distance(previous.size,renderer.bounds.size)<.001f,"Surface dimensions must stay unchanged");
            CheckUV(part,filter.sharedMesh,materials,tileHeight);
        }
        public static void Walls(Transform world)
        {
            Directory.CreateDirectory(FactoryPresentationInstaller.Generated);
            var report=new StringBuilder();var materials=WallMaterials();int wallIndex=0;var used=new HashSet<int>();
            foreach(var wall in world.GetComponentsInChildren<Transform>(true).Where(item=>WallNames.Contains(item.name) || item.name=="Doorway infill"))
            {
                Require(wall.GetComponent<BoxCollider>(),"Wall collision is missing: "+wall.name);
                Paint(wall,"FactoryWallPanels_"+wallIndex.ToString("D2"),materials,wallIndex%materials.Length);
                var mesh=wall.GetComponent<MeshFilter>().sharedMesh;
                for(int i=0;i<materials.Length;i++)if(mesh.GetIndexCount(i)>0)used.Add(i);
                report.AppendLine("PASS "+wall.name+": original bounds and collision; original texture aspect and common physical scale");
                wallIndex++;
            }
            Require(used.Count==materials.Length,"Every available wall texture must be used.");
            var board=Material("WorkstationCardboardBackboard",Texture(BackboardTexture,TextureWrapMode.Clamp));int boardIndex=0;
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var part=station.transform.Find("Workstation backboard");
                // One continuous sheet, with uniform pixel density on the broad face and narrow edges.
                float height=part.GetComponent<Renderer>().bounds.size.x*board.mainTexture.height/board.mainTexture.width;
                Paint(part,"WorkstationBackboard_"+boardIndex++,new[]{board},0,height);
            }
            Require(boardIndex==4,"All four workstation backboards must use the reserved texture.");
            report.AppendLine("SUCCESS: "+wallIndex+" walls use all "+materials.Length+" mixed textures; reserved plain 45 texture is used only by the four workstation backboards.");
            File.WriteAllText("Library/FactoryWallsUpdate.txt",report.ToString());
        }
        public static void Apply(Transform world)
        {
            Walls(world);var report=new StringBuilder();
            int boxes=Boxes(world,Material("TexturedFinishingBox",Texture(BoxTexture,TextureWrapMode.Clamp)),report);
            Require(boxes==12,"All twelve workstation vessels must be updated");
            report.AppendLine("SUCCESS: 12 textured vessels; fully rough, double-sided, receiving and casting two-sided shadows.");
            File.WriteAllText("Library/FactoryWallsBoxUpdate.txt",report.ToString());
        }
        [MenuItem("Cardboard/Update walls and box")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update walls and vessels in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            EditorSceneManager.SaveScene(game.gameObject.scene);Directory.CreateDirectory("Backups/WallsAndBox");
            const string backup="Backups/WallsAndBox/FactoryShift-before-update.unity";
            if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            Apply(game.transform);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            Preview();
        }
        public static void Preview()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try
            {
                camera.fieldOfView=75;
                var eye=new Vector3(CardboardFactoryWorld.WorkshopEast-2,2.5f,-10);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(CardboardFactoryWorld.WorkshopWest+9,2.6f,7)-eye));
                CardboardFactorySceneBuilder.Capture("TexturedFactoryWalls");
                eye=game.station.transform.TransformPoint(new Vector3(4.3f,1.4f,-2.3f));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.baths[1].position+Vector3.up*.35f-eye));
                CardboardFactorySceneBuilder.Capture("TexturedFinishingBoxes");
            }
            finally{camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);}
        }
    }
}
