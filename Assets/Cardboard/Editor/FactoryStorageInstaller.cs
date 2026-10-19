using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryStorageInstaller
    {
        const string AssetsRoot="Assets/Game_Assets/Cardboard/";
        public const string RootName="Factory storage • barrels and empty boxes";
        static int barrels,boxes;
        static Material barrelMaterial,boxMaterial;
        static GameObject barrelModel,boxModel;
        static Transform Group(string name,Transform parent,Vector3 point)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=point;return root;
        }
        static Bounds BoundsIn(Transform root,Transform space)
        {
            bool first=true;Bounds bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var local=renderer.localBounds;
                var point=space.InverseTransformPoint(renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
            if(first)throw new InvalidOperationException("Missing prop geometry: "+root.name);
            return bounds;
        }
        static void Prepare()
        {
            barrelMaterial=FactoryPresentationInstaller.Paper("TexturedBarrel",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot+"Barrel/Barrel.png"));
            boxMaterial=FactoryPresentationInstaller.Paper("TexturedFinishingBox",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot+"Box/Texture/Box.png"));
            var path=AssetsRoot+"Barrel/Barrel.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool changed=false;
            foreach(var material in asset.GetComponentsInChildren<Renderer>(true).SelectMany(renderer=>renderer.sharedMaterials).Where(material=>material).Distinct())
            {
                if(material==barrelMaterial)continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),barrelMaterial);changed=true;
            }
            if(changed)importer.SaveAndReimport();
            barrelModel=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            boxModel=AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot+"Box/Model/Box.fbx");
        }
        static Transform Prop(Transform parent,bool barrel,Vector3 position,float yaw=0)
        {
            string name=barrel?"Barrel "+(++barrels):"Empty Box "+(++boxes);
            var root=Group(name,parent,position);root.localRotation=Quaternion.Euler(0,yaw,0);root.gameObject.layer=8;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(barrel?barrelModel:boxModel,root);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials=Enumerable.Repeat(barrel?barrelMaterial:boxMaterial,renderer.sharedMaterials.Length).ToArray();
                CardboardSurface.Configure(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            var bounds=BoundsIn(root,root);
            instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float height=barrel?1.05f:.70f,footprint=barrel?.80f:1.20f;
            float size=Mathf.Min(height/bounds.size.y,footprint/Mathf.Max(bounds.size.x,bounds.size.z));
            root.localScale=Vector3.one*size;
            if(barrel)
            {
                var collider=root.gameObject.AddComponent<CapsuleCollider>();collider.direction=1;
                collider.center=Vector3.up*bounds.size.y*.5f;collider.height=bounds.size.y;
                collider.radius=Mathf.Max(bounds.size.x,bounds.size.z)*.5f;
            }
            else
            {
                var collider=root.gameObject.AddComponent<BoxCollider>();
                collider.center=Vector3.up*bounds.size.y*.5f;collider.size=bounds.size;
            }
            FactoryPhysicsInstaller.Prop(root,barrel);
            return root;
        }
        static void Array(Transform holder,string name,Vector3 origin,float yaw)
        {
            var group=Group(name,holder,origin);group.localRotation=Quaternion.Euler(0,yaw,0);
            for(int row=0;row<2;row++)for(int column=0;column<2;column++)
                Prop(group,true,new Vector3(column*.95f,0,row*.95f),(row*2+column)*13-12);
            var bottom=Prop(group,false,new Vector3(2.05f,0,.25f),90);
            float top=BoundsIn(bottom,group).max.y;
            Prop(group,false,new Vector3(2.05f,top,.25f),87);
        }
        public static void Create(Transform world)
        {
            Prepare();barrels=boxes=0;
            var old=world.Find(RootName);if(old)Object.DestroyImmediate(old.gameObject);
            var holder=Group(RootName,world,new Vector3(0,FactoryEnvironmentInstaller.FloorY(world),0));
            float west=CardboardFactoryWorld.WorkshopWest,east=CardboardFactoryWorld.WorkshopEast;
            Array(holder,"Workshop • southwest storage array",new Vector3(west+1.1f,0,CardboardFactoryWorld.WorkshopSouth+1.6f),0);
            Array(holder,"Workshop • west wall array",new Vector3(west+.9f,0,-1.8f),0);
            Array(holder,"Workshop • northeast storage array",new Vector3(east-1.1f,0,2.5f),180);
            Array(holder,"Workshop • southeast storage array",new Vector3(east-1.1f,0,-6),180);
            Array(holder,"Kitchen • northwest corner array",new Vector3(-24.7f,0,-17.8f),0);
            Array(holder,"Kitchen • southwest corner array",new Vector3(-24.7f,0,-32.7f),0);
            Array(holder,"Kitchen • northeast corner array",new Vector3(-1.3f,0,-17),180);
            Array(holder,"Kitchen • southeast corner array",new Vector3(-1.3f,0,-31.5f),180);
            var scattered=Group("Workshop • loose storage",holder,Vector3.zero);
            Prop(scattered,true,new Vector3(west+1.1f,0,1.2f),18);
            Prop(scattered,true,new Vector3(west+5.2f,0,-5),-17);
            Prop(scattered,false,new Vector3(east-6,0,-2),84);
            Prop(scattered,false,new Vector3(CardboardFactoryWorld.WorkshopCenterX-3,0,-1.8f),101);
            File.WriteAllText("Library/FactoryStorageUpdate.txt","SUCCESS: "+barrels+" textured barrels and "+boxes+" empty Box models; eight storage arrays and loose workshop props; uniform scaling, floor contact, solid collision and two-sided matte materials.");
        }
        [MenuItem("Cardboard/Install barrels and empty boxes")]
        public static void InstallCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before placing storage.");
            var world=Object.FindFirstObjectByType<CardboardFactoryWorld>();
            Create(world.transform);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            AuditCurrent();PreviewCurrent();
        }
        public static void AuditCurrent()
        {
            var report=new StringBuilder();
            void Check(bool valid,string text){report.AppendLine((valid?"PASS ":"FAIL ")+text);}
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
                Physics.SyncTransforms();var floor=game.transform.Find("Main workshop floor").GetComponent<Renderer>().bounds;
                Check(Mathf.Abs(floor.size.x-44.8f/1.45f)<.01f && Mathf.Abs(floor.size.z-28f/1.5f)<.01f,"Workshop floor is "+floor.size.x.ToString("F2")+" x "+floor.size.z.ToString("F2")+" metres");
                var holder=game.transform.Find(RootName);
                var props=holder.GetComponentsInChildren<Collider>();
                Check(props.Count(prop=>prop.name.StartsWith("Barrel "))==34,"34 barrels placed");
                Check(props.Count(prop=>prop.name.StartsWith("Empty Box "))==18,"18 empty boxes placed");
                Check(!holder.GetComponentsInChildren<Transform>(true).Any(item=>item.name.Contains("liquid") || item.GetComponent<FactoryStationControl>()),"Decorative boxes contain no liquid or bath controls");
                bool inside=true,uniform=true;
                foreach(var prop in props)
                {
                    var bounds=BoundsIn(prop.transform,game.transform);
                    bool kitchen=bounds.center.z<CardboardFactoryWorld.CanteenNorth;
                    float west=kitchen?CardboardFactoryWorld.CanteenWest:CardboardFactoryWorld.WorkshopWest;
                    float east=kitchen?CardboardFactoryWorld.CanteenEast:CardboardFactoryWorld.WorkshopEast;
                    float south=kitchen?CardboardFactoryWorld.CanteenSouth:CardboardFactoryWorld.WorkshopSouth;
                    float north=kitchen?CardboardFactoryWorld.CanteenNorth:CardboardFactoryWorld.WorkshopNorth;
                    bool fits=bounds.min.x>west+.2f && bounds.max.x<east-.2f && bounds.min.z>south+.2f && bounds.max.z<north-.2f;
                    inside &= fits;if(!fits)report.AppendLine("DETAIL outside "+prop.name+" bounds="+bounds);
                    var scale=prop.transform.localScale;uniform &= Mathf.Abs(scale.x-scale.y)<.0001f && Mathf.Abs(scale.x-scale.z)<.0001f;
                }
                Check(inside,"Every storage prop stays inside the room walls");
                Check(uniform,"Authored prop proportions are preserved");
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Cardboard/Generated/TexturedBarrel.mat");
                Check(material && material.GetFloat("_Cull")==0 && material.GetFloat("_Smoothness")==0 && material.GetTexture("_BaseMap"),"Barrel uses its original texture and a double-sided matte material");
                foreach(var station in game.stations)
                {
                    var points=station.baths.Select(bath=>bath.localPosition).ToArray();
                    Check(points.All(point=>point.x<-2) && points[0].z>points[1].z && points[1].z>points[2].z,station.name+": baths run front to back on the left");
                    report.AppendLine("SIZE "+station.name+" footprint="+FactoryWorkstationBuilder.Footprint(station.transform).size);
                }
                var navigation=new FactoryCaptureNavigation(.32f);
                var start=game.station.transform.TransformPoint(new Vector3(0,0,-2.7f));start.y=0;
                Check(navigation.Path(start,game.lunch.pickup.position).Count>3,"Walkable route from player workstation to kitchen serving counter");
                foreach(var bath in game.station.baths)
                {
                    var target=bath.position+Vector3.right*.95f;target.y=0;
                    Check(navigation.Path(start,target).Count>1,"Player can approach "+bath.name+" from the main aisle");
                }
                var foundation=game.transform.GetComponentsInChildren<Renderer>().First(renderer=>renderer.name=="Machine foundation").bounds;
                Check(foundation.min.x>CardboardFactoryWorld.WorkshopWest+.25f && foundation.max.x<CardboardFactoryWorld.WorkshopEast-.25f,"Main mechanism fits the shortened wall");
                FactoryLayoutAudit.Write();
            }
            catch(Exception error){report.AppendLine("FAIL "+error);}
            File.WriteAllText("Library/FactoryCompactLayoutAudit.txt",report.ToString());
        }
        public static void PreviewCurrent()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var camera=game.mainMenu.lens;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            bool ortho=camera.orthographic;float size=camera.orthographicSize;float near=camera.nearClipPlane;
            float hudAlpha=game.mainMenu.shiftHud.alpha;
            try
            {
                game.mainMenu.shiftHud.alpha=0;
                camera.orthographic=false;camera.fieldOfView=70;
                var station=game.station.transform;
                var eye=station.TransformPoint(new Vector3(-.6f,4,-7.5f));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(station.TransformPoint(new Vector3(-1.1f,.7f,-1.2f))-eye));CardboardFactorySceneBuilder.Capture("CompactWorkstation");
                eye=new Vector3(CardboardFactoryWorld.WorkshopEast-1.5f,3.7f,CardboardFactoryWorld.WorkshopSouth+2.8f);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX-3,1.5f,4)-eye));CardboardFactorySceneBuilder.Capture("CompactWorkshop");
                eye=new Vector3(-12,4.3f,-18.4f);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(-13,.7f,-27)-eye));CardboardFactorySceneBuilder.Capture("KitchenStorage");
                // Cut away the roof in the overhead preview without changing the scene renderers.
                camera.orthographic=true;camera.orthographicSize=23;camera.nearClipPlane=40.2f;
                camera.transform.SetPositionAndRotation(new Vector3(-8,45,-12),Quaternion.Euler(90,0,0));CardboardFactorySceneBuilder.Capture("CompactFloorPlan");
            }
            finally
            {
                game.mainMenu.shiftHud.alpha=hudAlpha;camera.nearClipPlane=near;
                camera.orthographic=ortho;camera.orthographicSize=size;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);
            }
        }
    }
}
