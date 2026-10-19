using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;

namespace Cardboard.Editor
{
    public static class FactoryPresentationInstaller
    {
        public const string Generated="Assets/Cardboard/Generated/";
        const string AssetsRoot="Assets/Game_Assets/Cardboard/";
        static readonly StringBuilder report=new StringBuilder();

        public static Material Matte(Material material)
        {
            if(!material)return material;
            CardboardSurface.Configure(material);
            EditorUtility.SetDirty(material);return material;
        }
        public static Material SourceMaterial(string folder)
        {
            var ids=AssetDatabase.FindAssets("t:Material",new[]{AssetsRoot+folder});
            if(ids.Length==0)throw new InvalidOperationException("Material is missing: "+folder);
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(ids[0]));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Cull",0);material.doubleSidedGI=true;
            return Matte(material);
        }
        public static Material Paper(string name,Color color,Texture texture=null,bool lettering=false)
        {
            string path=Generated+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find(lettering?"Cardboard/WorldInk":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            if(!lettering)
            {
                material.SetColor("_BaseColor",color);material.SetTexture("_BaseMap",texture);
                material.SetFloat("_Cull",0);material.doubleSidedGI=true;Matte(material);
            }
            return material;
        }
        static Vector2 SharpEnd(Vector3[] vertices)
        {
            var points=new List<Vector2>();
            foreach(var vertex in vertices){Vector2 p=vertex;if(!points.Contains(p))points.Add(p);}
            points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            var hull=new List<Vector2>();
            for(int pass=0;pass<2;pass++)
            {
                int start=hull.Count;
                foreach(var p in points)
                {
                    while(hull.Count>=start+2)
                    {
                        Vector2 a=hull[hull.Count-1]-hull[hull.Count-2],b=p-hull[hull.Count-1];
                        if(a.x*b.y-a.y*b.x>0.000001f)break;
                        hull.RemoveAt(hull.Count-1);
                    }
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count-1);points.Reverse();
            }
            Vector2 tip=Vector2.up;float sharpest=361;
            for(int i=0;i<hull.Count;i++)
            {
                var p=hull[i];float angle=Vector2.Angle(hull[(i+hull.Count-1)%hull.Count]-p,hull[(i+1)%hull.Count]-p);
                if(angle<sharpest){sharpest=angle;tip=p;}
            }
            return tip;
        }
        public static Mesh Bake(string modelPath,string name,float yaw,Vector3 pivot,bool center=false,float width=0,bool pointerUp=false)
        {
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(importer && !importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                var filter=source.GetComponentInChildren<MeshFilter>();
                if(!filter)throw new InvalidOperationException("Mesh is missing: "+modelPath);
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=name;
                Matrix4x4 pose=Matrix4x4.Rotate(Quaternion.Euler(0,yaw,0))*filter.transform.localToWorldMatrix;
                Matrix4x4 normalPose=pose.inverse.transpose;
                var vertices=mesh.vertices;var normals=mesh.normals;
                Bounds bounds=default;
                for(int i=0;i<vertices.Length;i++)
                {
                    vertices[i]=pose.MultiplyPoint3x4(vertices[i]);
                    if(i==0)bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);
                    if(normals.Length==vertices.Length)normals[i]=normalPose.MultiplyVector(normals[i]).normalized;
                }
                if(center)pivot=bounds.center;
                float scale=width>0?width/bounds.size.x:1;
                for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-pivot)*scale;
                if(pointerUp)
                {
                    Vector2 tip=SharpEnd(vertices);
                    Quaternion aim=Quaternion.Euler(0,0,Vector2.SignedAngle(tip,Vector2.up));
                    for(int i=0;i<vertices.Length;i++){vertices[i]=aim*vertices[i];if(normals.Length==vertices.Length)normals[i]=aim*normals[i];}
                    report.AppendLine("Selector sharp end calibrated from "+tip+" to up.");
                }
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                string path=Generated+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved)
                {
                    saved.Clear();saved.indexFormat=mesh.indexFormat;saved.name=mesh.name;
                    saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.tangents=mesh.tangents;saved.colors32=mesh.colors32;
                    saved.subMeshCount=mesh.subMeshCount;
                    for(int submesh=0;submesh<mesh.subMeshCount;submesh++)saved.SetIndices(mesh.GetIndices(submesh),mesh.GetTopology(submesh),submesh,false);
                    saved.bounds=mesh.bounds;saved.UploadMeshData(false);
                    UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
                }
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }
        public static Transform MeshObject(string name,Transform parent,Mesh mesh,Material material,Vector3 position)
        {
            var root=Group(name,parent,position);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=material;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;
            return root;
        }
        static Transform Imported(string name,string path,Transform station,float yaw,Material material)
        {
            var old=station.Find(name);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=Group(name,station,Vector3.zero);root.localRotation=Quaternion.Euler(0,yaw,0);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot+path),root);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;
                renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            return root;
        }
        static Bounds In(Transform root,Transform space)
        {
            bool first=true;Bounds bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var b=renderer.localBounds;
                Vector3 p=space.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
            return bounds;
        }
        static Vector3 Outlet(Transform pipe,Transform station)
        {
            float lowest=float.MaxValue;var points=new List<Vector3>();
            foreach(var filter in pipe.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                {Vector3 p=station.InverseTransformPoint(filter.transform.TransformPoint(vertex));points.Add(p);lowest=Mathf.Min(lowest,p.y);}
            Bounds bounds=default;bool first=true;
            foreach(var point in points)if(point.y<lowest+.015f)
            {if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
            return bounds.center;
        }
        static Transform Icon(Transform parent,int type,Vector3 position,float diameter,string name)
        {
            var root=Group(name,parent,position);root.localRotation=Quaternion.Euler(0,180,0);
            var prefab=Resources.Load<GameObject>("FactoryGears/Gear"+type);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);
            float size=Mathf.Max(prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.y);
            instance.transform.localScale=Vector3.one*(diameter/size);
            return root;
        }
        static void Station(FactoryWorkstation station,Material conveyorMaterial,Material selectorMaterial,Material pipeMaterial,Material wheelMaterial,Mesh selectorMesh,Mesh wheelMesh)
        {
            var root=station.transform;
            var conveyor=Imported("Static conveyor","Conveer/Model/Conveer.fbx",root,90,conveyorMaterial);
            FactoryWorkstationUpgrade.PaintConveyor(conveyor);
            var table=In(root.Find("Table"),root);var b=In(conveyor,root);
            var roller=Array.Find(conveyor.GetComponentsInChildren<Renderer>(),item=>item.name=="Cylinder");
            float top=In(roller.transform,root).max.y;
            conveyor.localPosition+=new Vector3(table.min.x+.012f-b.max.x,station.cuttingPosition.y-station.sheet.localScale.y*.5f-top,station.cuttingPosition.z-b.center.z);
            b=In(conveyor,root);
            float feedX=Mathf.Lerp(b.min.x,b.max.x,.25f);
            station.conveyorPosition=new Vector3(feedX,station.cuttingPosition.y,station.cuttingPosition.z);
            var pipe=Imported("Material pipe","Pipe/Model/Pipe.fbx",root,-90,pipeMaterial);
            Vector3 outlet=Outlet(pipe,root);
            pipe.localPosition+=new Vector3(feedX-outlet.x,0,station.cuttingPosition.z-outlet.z);
            station.dropPosition=new Vector3(feedX,outlet.y+.13f,station.cuttingPosition.z);

            if(station.selector)UnityEngine.Object.DestroyImmediate(station.selector.gameObject);
            var numbers=new List<GameObject>();
            foreach(Transform child in root)
                if(child.name=="Selected type" || child.name.StartsWith("Number ") || child.name.StartsWith("Selector gear "))numbers.Add(child.gameObject);
            foreach(var item in numbers)UnityEngine.Object.DestroyImmediate(item);
            station.selectorNumber=null;
            station.selector=MeshObject("Gear type selector pivot",root,selectorMesh,selectorMaterial,new Vector3(-1.13f,2.016f,.68f));
            if(station.isPlayerStation)
            {
                station.selector.gameObject.AddComponent<BoxCollider>().size=new Vector3(.60f,.65f,.26f);
                FactoryGeometry.Interaction(station.selector,FactoryAction.Station,"");
                var control=station.selector.gameObject.AddComponent<FactoryStationControl>();control.station=station;control.part=StationPart.Selector;
            }
            station.selectorIcons=new Transform[3];
            for(int i=0;i<3;i++)
            {
                Vector3 direction=Quaternion.Euler(0,0,120*i)*Vector3.up;
                station.selectorIcons[i]=Icon(root,i+1,station.selector.localPosition+direction*.51f+Vector3.back*.02f,.25f,"Selector gear "+(i+1));
                var bath=station.baths[i];var number=bath.Find("Number "+(i+1));if(number)UnityEngine.Object.DestroyImmediate(number.gameObject);
                var old=bath.Find("Bath gear icon");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
                Icon(bath,i+1,new Vector3(0,.42f,-.708f),.35f,"Bath gear icon");
            }

            var assembly=station.wheelBase;
            foreach(var prefabRoot in assembly.GetComponentsInChildren<Transform>())
                if(prefabRoot!=assembly && PrefabUtility.IsAnyPrefabInstanceRoot(prefabRoot.gameObject))
                {PrefabUtility.UnpackPrefabInstance(prefabRoot.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);break;}
            if(station.wheel)UnityEngine.Object.DestroyImmediate(station.wheel.gameObject);
            station.wheel=MeshObject("Shturval_Rotate",assembly,wheelMesh,wheelMaterial,Vector3.zero);
            station.wheel.SetPositionAndRotation(root.TransformPoint(new Vector3(2.049f,.817f,-.334f)),root.rotation);
            station.wheelGrip=Group("Hand grip on textured round rim",station.wheel,new Vector3(0,.48f,-.147f));
            station.RebindModels();
            report.AppendLine(root.name+": conveyor right edge="+b.max.x+" roller/blank height="+station.conveyorPosition.y+" pipe/blank alignment="+station.dropPosition+"; selector, three circular gear icons, three bath icons and textured rotating wheel installed.");
        }
        public static void Apply(Transform root)
        {
            report.Clear();
            var pipeImporter=AssetImporter.GetAtPath(AssetsRoot+"Pipe/Model/Pipe.fbx") as ModelImporter;
            if(!pipeImporter.isReadable){pipeImporter.isReadable=true;pipeImporter.SaveAndReimport();}
            Material conveyor=SourceMaterial("Conveer/Texture/Materials"),selector=SourceMaterial("Krutilka/Texture/Materials"),pipe=SourceMaterial("Pipe/Texture/Materials"),wheel=SourceMaterial("Shturval/Texture/Materials");
            Mesh selectorMesh=Bake(AssetsRoot+"Krutilka/Model/Krutilka.fbx","WorkstationSelector",90,Vector3.zero,true,0,true);
            Mesh wheelMesh=Bake(AssetsRoot+"Shturval/Shturval_Rotate.fbx","WorkstationWheelRotate",90,new Vector3(2.049f,.817f,-.334f));
            foreach(var station in root.GetComponentsInChildren<FactoryWorkstation>(true))Station(station,conveyor,selector,pipe,wheel,selectorMesh,wheelMesh);
            FactoryMenuBuilder.Create(root);
            var materials=new HashSet<Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Game_Assets","Assets/Cardboard/Resources"}))
            {var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(material)materials.Add(material);}
            foreach(var material in materials)Matte(material);
            File.WriteAllText("Library/FactoryPresentationUpdate.txt",report+"SUCCESS: all workstations updated, imported menu buttons installed, all scene and game-asset materials fully rough.\n");
        }
        [MenuItem("Cardboard/Update menu and workstation assets")]
        public static void ApplyCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply updates in Edit Mode.");
            var world=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryWorld>();
            if(!world)throw new InvalidOperationException("Factory scene is missing.");
            EditorSceneManager.SaveScene(world.gameObject.scene);
            Directory.CreateDirectory("Backups/MenuUpdate");
            if(!File.Exists("Backups/MenuUpdate/FactoryShift-before-menu.unity"))File.Copy(world.gameObject.scene.path,"Backups/MenuUpdate/FactoryShift-before-menu.unity");
            Apply(world.transform);FactoryFloorInstaller.Apply(world.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
        }
    }
}
