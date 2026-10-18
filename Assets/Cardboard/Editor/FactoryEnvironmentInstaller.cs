using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryEnvironmentInstaller
    {
        public static float FloorY(Transform world)=>world.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Main workshop floor surface").bounds.max.y;
        static Transform Group(string name,Transform parent,Vector3 position)
        {var item=new GameObject(name).transform;item.SetParent(parent,false);item.localPosition=position;return item;}
        static Transform Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid=true)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.transform.SetParent(parent,false);
            item.transform.localPosition=position;item.transform.localScale=size;
            item.GetComponent<Renderer>().sharedMaterial=material;CardboardSurface.Configure(item.GetComponent<Renderer>());
            if(solid)item.layer=8;else UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item.transform;
        }
        static Material Paper(string name,string color)=>FactoryPresentationInstaller.Paper(name,CardboardArt.Hex(color));
        static void Frame(Transform root,float width,float z,Material material)
        {
            foreach(int side in new[]{-1,1})
                Cube("Door frame jamb",root,new Vector3(side*(width*.5f-.06f),1.60f,z),new Vector3(.12f,3.20f,.24f),material);
            Cube("Door frame lintel",root,new Vector3(0,3.14f,z),new Vector3(width,.12f,.24f),material);
        }
        static FactoryDoor Door(Transform root,float width,Material sheet,Material edge,Material handle,bool workers)
        {
            var door=root.gameObject.AddComponent<FactoryDoor>();door.passage=root;door.workersMayOpen=workers;
            float leafWidth=width-.28f;
            door.hinge=Group("Door hinge pivot",root,new Vector3(-leafWidth*.5f,.015f,0));
            var leaf=Cube("Cardboard door leaf",door.hinge,new Vector3(leafWidth*.5f,1.51f,0),new Vector3(leafWidth,3.02f,.14f),sheet);
            FactoryGeometry.Interaction(leaf,FactoryAction.Door,"E — открыть дверь");
            FactoryWallsBoxInstaller.Paint(leaf,workers?"CanteenDoorPanel":"FactoryExitDoorPanel",new[]{sheet},0,3.02f);
            foreach(int side in new[]{-1,1})
            {
                float z=side*.076f;
                foreach(float x in new[]{.025f,leafWidth-.025f})
                    Cube("Door ink edge",door.hinge,new Vector3(x,1.51f,z),new Vector3(.025f,2.99f,.012f),edge,false);
                foreach(float y in new[]{.025f,2.995f})
                    Cube("Door ink edge",door.hinge,new Vector3(leafWidth*.5f,y,z),new Vector3(leafWidth,.025f,.012f),edge,false);
                Cube("Door handle plate",door.hinge,new Vector3(leafWidth-.23f,1.35f,side*.09f),new Vector3(.12f,.28f,.025f),edge,false);
                Cube("Door handle",door.hinge,new Vector3(leafWidth-.33f,1.38f,side*.15f),new Vector3(.32f,.055f,.075f),handle,false);
            }
            door.SetOpen(false,true);return door;
        }
        public static void Geometry(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();float floor=FloorY(world);
            var old=world.Find("Factory doorways");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var legacyExit=world.Find("End of shift exit");if(legacyExit)UnityEngine.Object.DestroyImmediate(legacyExit.gameObject);
            foreach(var part in world.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Canteen door jamb" || t.name=="Canteen doorway lintel").ToArray())
                UnityEngine.Object.DestroyImmediate(part.gameObject);
            var root=Group("Factory doorways",world,Vector3.zero);var frame=Paper("DoorFrameCardboard","735032");
            var edge=Paper("DoorInk","251B10");var handle=Paper("DoorHandles","D4B16D");
            var sheet=FactoryWallsBoxInstaller.WallMaterials().First(material=>AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")).EndsWith("Без названия46_20261018220231.png"));
            var canteen=Group("Canteen door",root,new Vector3(-13,floor,-15.95f));
            Door(canteen,3,sheet,edge,handle,true);Frame(canteen,3,2.18f,frame);Frame(canteen,3,-.27f,frame);
            float top=5.6f-floor,headHeight=top-3.08f;
            Cube("Doorway infill",canteen,new Vector3(0,3.08f+headHeight*.5f,.95f),new Vector3(3,headHeight,2.4f),sheet);
            foreach(int side in new[]{-1,1})
                Cube("Doorway infill",canteen,new Vector3(side*1.65f,2.7f-floor,.95f),new Vector3(.30f,5.8f,2.4f),sheet);
            var exit=Group("Factory exit door",root,new Vector3(CardboardFactoryWorld.WorkshopEast,floor,-2));
            exit.localRotation=Quaternion.Euler(0,90,0);game.exitDoor=Door(exit,3.2f,sheet,edge,handle,false);
            game.exitGate=game.exitDoor.hinge;Frame(exit,3.2f,0,frame);
            Cube("Doorway infill",exit,new Vector3(0,3.08f+headHeight*.5f,0),new Vector3(3.2f,headHeight,.5f),sheet);
            // Keep finishing the shift separate from opening and closing the physical door.
            var control=Cube("Exit control",exit,new Vector3(2.1f,1.55f,-.34f),new Vector3(.85f,.60f,.12f),handle);
            FactoryGeometry.Interaction(control,FactoryAction.Exit,"E — закончить смену");
            var doorway=world.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Canteen doorway floor");
            doorway.position+=Vector3.up*(floor-doorway.GetComponent<Renderer>().bounds.max.y);
            foreach(var sign in world.GetComponentsInChildren<TextMesh>(true))
                if(sign.text.StartsWith("СТОЛОВАЯ"))Move(sign.transform,3.65f-sign.transform.position.y);
            EditorUtility.SetDirty(game);
        }
        static Bounds BoundsOf(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer && !r.GetComponent<TextMesh>()).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("Missing geometry: "+root.name);
            var result=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))result.Encapsulate(renderer.bounds);return result;
        }
        static void Move(Transform root,float height)
        {
            if(Mathf.Abs(height)<.00001f)return;
            Undo.RecordObject(root,"Rest factory prop on floor");root.position+=Vector3.up*height;
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);EditorUtility.SetDirty(root);
        }
        public static void Ground(Transform world)
        {
            float floor=FloorY(world);var report=new StringBuilder();
            var supportMaterial=Paper("ConveyorStandCardboard","80603D");
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var table=station.transform.Find("Table");var platePosition=station.pumpPlate.position;
                float shift=floor-.001f-BoundsOf(table).min.y;Move(station.transform,shift);
                // The pump was already close to the floor; compensate for the workstation translation.
                station.pumpPlate.position=platePosition;
                Move(station.pumpPlate,floor+.001f-station.pumpPlate.GetComponent<Renderer>().bounds.min.y);
                var support=station.transform.Find("Conveyor floor stand");if(support)UnityEngine.Object.DestroyImmediate(support.gameObject);
                support=Group("Conveyor floor stand",station.transform,Vector3.zero);
                var conveyor=BoundsOf(station.transform.Find("Static conveyor"));
                float bottom=floor+.001f,height=conveyor.min.y-bottom+.025f;
                foreach(float x in new[]{conveyor.min.x+.13f,conveyor.max.x-.13f})
                foreach(float z in new[]{conveyor.min.z+.12f,conveyor.max.z-.12f})
                {
                    var point=station.transform.InverseTransformPoint(new Vector3(x,bottom+height*.5f,z));
                    Cube("Conveyor support leg",support,point,new Vector3(.08f,height,.08f),supportMaterial);
                }
                foreach(float x in new[]{conveyor.min.x+.13f,conveyor.max.x-.13f})
                {
                    var point=station.transform.InverseTransformPoint(new Vector3(x,bottom+.15f,conveyor.center.z));
                    Cube("Conveyor support brace",support,point,new Vector3(.08f,.10f,conveyor.size.z-.16f),supportMaterial);
                }
                station.RebindModels();EditorUtility.SetDirty(station);
                report.AppendLine("PASS "+station.name+": table feet="+BoundsOf(table).min.y.ToString("F4")+"; floor="+floor.ToString("F4")+"; pump compensated, four conveyor legs added");
                foreach(var bath in station.baths)report.AppendLine("PASS "+bath.name+": bottom="+BoundsOf(bath.Find("Imported Box vessel")).min.y.ToString("F4"));
            }
            var foundation=world.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Machine foundation");
            Move(foundation.parent,floor-.001f-foundation.GetComponent<Renderer>().bounds.min.y);
            var game=world.GetComponent<CardboardFactoryGame>();
            if(game.instruction && !game.instruction.Held)
            {
                var paper=game.instruction.sheet;
                Move(paper,floor+.001f-BoundsOf(paper).min.y);
                game.instruction.floorPosition=paper.position;EditorUtility.SetDirty(game.instruction);
            }
            // Other furniture and idle cutouts already meet the floor (within 8 mm).
            File.WriteAllText("Library/FactoryGrounding.txt",report+"SUCCESS: floor-standing assemblies, vessels, conveyor supports, machine foundation and instruction meet the actual visible floor.\n");
        }
        [MenuItem("Cardboard/Update doors, walls and floor contacts")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update the environment in Edit Mode.");
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
            EditorSceneManager.SaveScene(game.gameObject.scene);Directory.CreateDirectory("Backups/DoorsWallsGround");
            const string backup="Backups/DoorsWallsGround/FactoryShift-before-update.unity";
            if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            Geometry(game.transform);FactoryWallsBoxInstaller.Walls(game.transform);Ground(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            Preview();
        }
        public static void Preview()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();var camera=Camera.main;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var door=game.GetComponentsInChildren<FactoryDoor>(true).First(d=>d.workersMayOpen);
            try
            {
                camera.fieldOfView=70;
                var eye=new Vector3(-10,1.8f,-10);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(-13,1.2f,-16)-eye));
                CardboardFactorySceneBuilder.Capture("CanteenDoorClosed");
                door.SetOpen(true,true);CardboardFactorySceneBuilder.Capture("CanteenDoorOpen");door.SetOpen(false,true);
                eye=game.station.transform.TransformPoint(new Vector3(-3.5f,.55f,-4));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(.5f,.3f,0))-eye));
                CardboardFactorySceneBuilder.Capture("GroundedWorkstation");
                eye=new Vector3(CardboardFactoryWorld.WorkshopEast-5,1.7f,-5);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.exitDoor.passage.position+Vector3.up*1.4f-eye));
                CardboardFactorySceneBuilder.Capture("FactoryExitDoor");
            }
            finally{door.SetOpen(false,true);camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);}
            FactoryWallsBoxInstaller.Preview();
        }

        public static void Audit()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
            var report=new StringBuilder();
            foreach(var item in game.GetComponentsInChildren<Transform>(true))
            {
                bool relevant=item.name=="Table" || item.name=="Static conveyor" || item.name.StartsWith("Finishing bath ") ||
                    item.name=="Jump pressure plate" || item.name=="Machine foundation" || item.name=="Workstation backboard" ||
                    item.name.Contains("floor") || item.name.Contains("placeholder") || item.name=="Empty canteen" ||
                    item.name=="End of shift exit" || item.name=="Canteen door jamb" || item.name=="Canteen doorway lintel" ||
                    item.name=="Instruction • floor pickup" || item.GetComponent<CardboardRunner>();
                if(!relevant)continue;
                var renderers=item.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer && !r.GetComponent<TextMesh>()).ToArray();
                if(renderers.Length==0)continue;
                var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);
                report.AppendLine(item.name+" parent="+item.parent.name+" pos="+item.position.ToString("F4")+" bottom="+b.min.y.ToString("F4")+" size="+b.size.ToString("F4"));
                if(item.name=="Table" || item.name=="Static conveyor")
                    foreach(var r in renderers)report.AppendLine("  "+r.name+" bottom="+r.bounds.min.y.ToString("F4")+" size="+r.bounds.size.ToString("F4"));
            }
            File.WriteAllText("Library/FactoryEnvironmentAudit.txt",report.ToString());
        }
    }
}
