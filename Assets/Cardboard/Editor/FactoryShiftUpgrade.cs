using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryShiftUpgrade
    {
        public const string PlateModel="Assets/Game_Assets/Cardboard/Plate/Plate.fbx";
        public const string PlateTexture="Assets/Game_Assets/Cardboard/Plate/Без названия51_20261019132326 (2).png";
        public const string MessageModel="Assets/Game_Assets/Cardboard/Massage/messege.fbx";
        public const string MessageTexture="Assets/Game_Assets/Cardboard/Massage/Massage.png";
        public static void Inspect()
        {
            var report=new StringBuilder();
            foreach(var path in new[]{PlateModel,MessageModel})
            {
                var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                report.AppendLine(path);
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))report.AppendLine(filter.name+" local="+filter.sharedMesh.bounds+" world="+filter.GetComponent<Renderer>().bounds+" pose="+filter.transform.localToWorldMatrix);
                Object.DestroyImmediate(instance);
            }
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            foreach(var renderer in game.lunch.tray.GetComponentsInChildren<Renderer>(true))report.AppendLine("TRAY "+renderer.name+" active="+renderer.gameObject.activeSelf+" bounds="+renderer.bounds+" local="+game.lunch.tray.InverseTransformPoint(renderer.bounds.center));
            foreach(var path in Directory.GetFiles("Assets/Game_Assets/Sounds","*.mp3"))
            {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\','/'));report.AppendLine(path+" duration="+(clip?clip.length:0));}
            File.WriteAllText("Library/FactoryShiftAssets.txt",report.ToString());
        }
        static Rigidbody Body(Transform root,float mass)
        {
            var body=root.GetComponent<Rigidbody>();if(!body)body=root.gameObject.AddComponent<Rigidbody>();
            body.mass=mass;body.isKinematic=true;body.useGravity=false;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations=12;body.solverVelocityIterations=6;body.linearDamping=.06f;body.angularDamping=.15f;
            root.gameObject.isStatic=false;root.gameObject.layer=8;return body;
        }
        static Bounds BoundsIn(Transform root,Transform space)
        {
            bool first=true;Bounds result=default;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var bounds=renderer.localBounds;
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    Vector3 point=space.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
            }
            return result;
        }
        static PhysicsMaterial Contact()
        {
            const string path="Assets/Cardboard/Generated/LunchContact.physicMaterial";
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if(!material){material=new PhysicsMaterial("Dishes on cardboard");AssetDatabase.CreateAsset(material,path);}
            material.staticFriction=.32f;material.dynamicFriction=.23f;material.bounciness=.025f;
            material.frictionCombine=PhysicsMaterialCombine.Minimum;material.bounceCombine=PhysicsMaterialCombine.Minimum;EditorUtility.SetDirty(material);return material;
        }
        static Rigidbody Item(Transform item,float mass,PhysicsMaterial material)
        {
            foreach(var collider in item.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            var body=Body(item,mass);
            foreach(var filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.gameObject.activeSelf)continue;
                filter.gameObject.isStatic=false;filter.gameObject.layer=8;
                var shape=filter.gameObject.AddComponent<MeshCollider>();shape.sharedMesh=filter.sharedMesh;shape.convex=true;shape.sharedMaterial=material;shape.contactOffset=.0015f;
            }
            return body;
        }
        static Transform Plate(Transform tray)
        {
            var existing=tray.Find("Plate • textured model");if(existing)Object.DestroyImmediate(existing.gameObject);
            var mesh=Bake(PlateModel,"LunchPlate",0,Vector3.zero,true,.25f,false,"Bowl");
            var material=Paper("LunchPlate",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(PlateTexture));CardboardSurface.Configure(material);
            var plate=MeshObject("Plate • textured model",tray,mesh,material,new Vector3(-.10f,.026f-mesh.bounds.min.y,0));
            return plate;
        }
        static void Meal(Transform tray,FactoryLunch lunch,bool player)
        {
            bool active=tray.gameObject.activeSelf;tray.gameObject.SetActive(true);
            try
            {
                var plate=Plate(tray);var material=Contact();
                var cup=tray.Find(FactoryLunchProps.CupName);
                if(!cup)cup=FactoryLunchProps.Cup(tray);
                var apple=tray.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Apple");
                var bowl=tray.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Bowl");
                if(bowl){bowl.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(bowl.gameObject);}
                var items=new System.Collections.Generic.List<Rigidbody>{Item(plate,.18f,material)};
                if(cup)
                {
                    cup.SetParent(tray,true);cup.localPosition=new Vector3(.20f,.027f-cup.GetComponent<MeshFilter>().sharedMesh.bounds.min.y,.035f);
                    items.Add(Item(cup,.16f,material));
                }
                if(apple)
                {
                    var old=tray.Find("Lunch apple");if(old)Object.DestroyImmediate(old.gameObject);
                    var item=Group("Lunch apple",tray,Vector3.zero);
                    // Copy a mesh from the imported meal without breaking its prefab hierarchy.
                    var visual=MeshObject("Apple",item,apple.GetComponent<MeshFilter>().sharedMesh,apple.sharedMaterial,Vector3.zero);
                    visual.localRotation=tray.rotation==Quaternion.identity?apple.transform.rotation:Quaternion.Inverse(tray.rotation)*apple.transform.rotation;
                    visual.localScale=apple.transform.lossyScale/tray.lossyScale.x;
                    var bounds=BoundsIn(item,tray);
                    item.localPosition=new Vector3(-.10f,BoundsIn(plate,tray).max.y+.006f,0)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    apple.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(apple.gameObject);
                    items.Add(Item(item,.11f,material));
                }
                if(!player)return;
                var physics=tray.GetComponent<FactoryTrayPhysics>();if(!physics)physics=tray.gameObject.AddComponent<FactoryTrayPhysics>();
                physics.lunch=lunch;physics.body=Body(tray,.45f);physics.contents=items.ToArray();
                physics.homes=items.Select(i=>i.transform.localPosition).ToArray();physics.rotations=items.Select(i=>i.transform.localRotation).ToArray();
                var support=tray.Find("Physical tray support");if(support)Object.DestroyImmediate(support.gameObject);
                support=Group("Physical tray support",tray,Vector3.zero);
                var visualTray=tray.Find(FactoryTrayInstaller.VisualName);var extent=BoundsIn(visualTray,tray);
                physics.halfSize=new Vector2(extent.extents.x,extent.extents.z);
                // A compound base and four low rims keep the dish concavity open for contacts.
                Action<string,Vector3,Vector3> shape=(name,center,size)=>
                {
                    var node=Group(name,support,center);node.gameObject.layer=8;
                    var collider=node.gameObject.AddComponent<BoxCollider>();collider.size=size;collider.sharedMaterial=material;collider.contactOffset=.0015f;
                };
                shape("Tray base",new Vector3(0,.012f,0),new Vector3(extent.size.x-.025f,.018f,extent.size.z-.025f));
                float x=extent.extents.x-.012f,z=extent.extents.z-.012f;
                shape("Left rim",new Vector3(-x,.033f,0),new Vector3(.018f,.035f,extent.size.z));
                shape("Right rim",new Vector3(x,.033f,0),new Vector3(.018f,.035f,extent.size.z));
                shape("Near rim",new Vector3(0,.033f,-z),new Vector3(extent.size.x,.035f,.018f));
                shape("Far rim",new Vector3(0,.033f,z),new Vector3(extent.size.x,.035f,.018f));
                lunch.physicsTray=physics;lunch.food=items.Select(i=>i.transform).ToArray();EditorUtility.SetDirty(lunch);
            }
            finally{tray.gameObject.SetActive(active);}
        }
        static void Mechanism(CardboardFactoryGame game)
        {
            var old=game.gears!=null && game.gears.Length>0?game.gears[0].transform.parent:null;
            Vector3 position=old?old.position:new Vector3(CardboardFactoryWorld.WorkshopEast-9.9f,0,CardboardFactoryWorld.WorkshopSouth+.8f);
            if(old)Object.DestroyImmediate(old.gameObject);
            var machine=Group("THE GREAT MECHANISM • reference gear assembly",game.transform,position);machine.localScale=Vector3.one*.8f;
            var wall=Paper("MechanismWallBacking",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(FactoryWallsBoxInstaller.BackboardTexture));CardboardSurface.Configure(wall);
            var backing=Box("Machine chassis • cardboard wall texture",machine,new Vector3(0,3.35f,0),new Vector3(15.4f,6.75f,.45f),wall);
            backing.gameObject.layer=8;backing.gameObject.AddComponent<BoxCollider>();
            var basePart=Box("Machine foundation",machine,new Vector3(0,-.025f,.05f),new Vector3(15.7f,.32f,.8f),Paper("MechanismFrame",Hex("775035")));basePart.gameObject.layer=8;basePart.gameObject.AddComponent<BoxCollider>();
            // Centers and radii follow the supplied 1557 × 799 layout. Types: round=1, fan=2, star=3.
            var layout=new[]{new Vector4(286,171,94,2),new Vector4(446,192,71,3),new Vector4(337,358,104,1),
                new Vector4(159,430,92,3),new Vector4(304,587,119,2),new Vector4(490,495,90,3),new Vector4(624,284,115,2),
                new Vector4(672,482,105,1),new Vector4(818,350,94,3),new Vector4(934,484,66,1),new Vector4(974,142,97,1),
                new Vector4(1136,181,72,3),new Vector4(1028,335,109,2),new Vector4(1262,331,124,1),
                new Vector4(1098,526,95,3),new Vector4(1301,563,110,2),new Vector4(1435,434,90,3)};
            game.gears=new FactoryGear[layout.Length];
            for(int i=0;i<layout.Length;i++)
            {
                var item=layout[i];int type=Mathf.RoundToInt(item.w);
                var socket=Group("Gear socket "+(i+1),machine,new Vector3((800-item.x)*.01f,(705-item.y)*.01f+.12f,.43f));
                var gear=socket.gameObject.AddComponent<FactoryGear>();gear.index=i;gear.recipe=FactoryRecipe.ForType(type);game.gears[i]=gear;
                float scale=item.z*.02f/2.16f;
                gear.healthy=Group("Rotating gear",socket,Vector3.zero);gear.healthy.localScale=Vector3.one*scale;ImportedFactoryGears.Create(gear.healthy,type);
                gear.damaged=Group("Broken gear • stopped",socket,Vector3.zero);gear.damaged.localScale=Vector3.one*scale;ImportedFactoryGears.Create(gear.damaged,type);
                var damaged=Paper("DamagedGear"+type,new Color(.65f,.35f,.27f),AssetDatabase.LoadAssetAtPath<Material>(FactoryGearImporter.Root+"/Gear"+type+".mat").GetTexture("_BaseMap"));CardboardSurface.Configure(damaged);
                foreach(var renderer in gear.damaged.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=damaged;
                for(int j=0;j<3;j++)
                {
                    var crack=Box("Fracture",gear.damaged,new Vector3(-.3f+j*.27f,.22f-j*.2f,.10f),new Vector3(.42f,.035f,.012f),Paper("MechanismFracture",Hex("353D39")));
                    crack.localRotation=Quaternion.Euler(0,0,j%2==0?-48:29);
                }
                var hit=socket.gameObject.AddComponent<BoxCollider>();hit.size=new Vector3(item.z*.019f,item.z*.019f,.28f);hit.isTrigger=true;
                var interact=socket.gameObject.AddComponent<FactoryInteractable>();interact.action=FactoryAction.Gear;interact.gear=gear;interact.prompt="E — заменить сломанную шестерёнку";
                var lamp=Group("Fault indicator",socket,new Vector3(0,item.z*.01f+.08f,.1f));gear.indicator=lamp.gameObject.AddComponent<Light>();gear.indicator.range=1.1f;gear.indicator.intensity=1.1f;gear.indicator.shadows=LightShadows.None;
                gear.SetBroken(false);
            }
            EditorUtility.SetDirty(game);
        }
        static void Printers(CardboardFactoryGame game)
        {
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var printer=station.transform.Find("Automatic work order printer");if(!printer)continue;
                if(station.orderCard)Object.DestroyImmediate(station.orderCard.gameObject);
                station.orderCard=FactoryPunchcard.Create(printer,1,Paper("OrderCardPaper",Hex("FFF0CF")),Paper("OrderCardInk",Hex("353D39")));station.orderCard.gameObject.SetActive(false);
                station.orderView=station.orderCard.GetComponent<FactoryPunchcardView>();station.orderNumber=null;
                var signal=printer.GetComponent<FactoryPrinterSignal>();if(!signal)signal=printer.gameObject.AddComponent<FactoryPrinterSignal>();signal.station=station;station.printerSignal=signal;
                signal.housing=printer.Find("Printer housing").GetComponent<Renderer>();
                var material=Paper("WarningPrinterHousing",Hex("775035"));CardboardSurface.Configure(material);material.EnableKeyword("_EMISSION");signal.housing.sharedMaterial=material;
                var old=printer.Find("Printer warning lamp");if(old)Object.DestroyImmediate(old.gameObject);
                var lamp=Box("Printer warning lamp",printer,new Vector3(.26f,.21f,.13f),new Vector3(.12f,.12f,.05f),material);
                signal.lamp=lamp.GetComponent<Renderer>();
                var light=lamp.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=2.8f;light.color=Color.red;light.intensity=2;light.shadows=LightShadows.None;light.enabled=false;signal.beacon=light;
                var speaker=printer.GetComponent<AudioSource>();if(!speaker)speaker=printer.gameObject.AddComponent<AudioSource>();speaker.playOnAwake=false;signal.speaker=speaker;signal.ring=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/Ring.mp3");speaker.clip=signal.ring;
                EditorUtility.SetDirty(station);
            }
        }
        static void Messages(CardboardFactoryGame game)
        {
            var mesh=Bake(MessageModel,"WorkerMessage",0,Vector3.zero,true,.6f);
            var material=Paper("WorkerMessage",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(MessageTexture));CardboardSurface.Configure(material);
            var clips=Directory.GetFiles("Assets/Game_Assets/Sounds","*.mp3").Where(p=>!System.IO.Path.GetFileNameWithoutExtension(p).Equals("Ring",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\','/'))).Where(c=>c).ToArray();
            foreach(var worker in game.crowd.runners)
            {
                if(!worker || worker.isPlayer)continue;
                var reaction=worker.GetComponent<FactoryWorkerImpact>();if(!reaction)reaction=worker.gameObject.AddComponent<FactoryWorkerImpact>();
                var old=worker.transform.Find("Messege • reaction");if(old)Object.DestroyImmediate(old.gameObject);
                reaction.message=MeshObject("Messege • reaction",worker.transform,mesh,material,new Vector3(0,2.7f,0));reaction.message.gameObject.layer=9;reaction.message.gameObject.SetActive(false);
                var speaker=worker.GetComponent<AudioSource>();if(!speaker)speaker=worker.gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake=false;speaker.loop=true;speaker.spatialBlend=1;speaker.volume=.55f;speaker.minDistance=1.25f;speaker.maxDistance=16;speaker.rolloffMode=AudioRolloffMode.Logarithmic;
                reaction.voice=speaker;reaction.voices=clips;PlaceMessage(worker,reaction.message);EditorUtility.SetDirty(reaction);
            }
        }
        static void PlaceMessage(CardboardRunner worker,Transform message)
        {
            float top=worker.transform.position.y;
            if(worker.puppet)foreach(var renderer in worker.puppet.GetComponentsInChildren<Renderer>())
                if(renderer.enabled)top=Mathf.Max(top,renderer.bounds.max.y);
            float halfHeight=0;
            foreach(var renderer in message.GetComponentsInChildren<Renderer>(true))
                halfHeight=Mathf.Max(halfHeight,renderer.bounds.extents.y);
            var position=message.position;
            position.y=Mathf.Max(worker.transform.TransformPoint(new Vector3(0,2.7f,0)).y,top+.3f+halfHeight);
            message.position=position;EditorUtility.SetDirty(message);
        }
        public static void RaiseMessagesCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Raise messages in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var report=new StringBuilder();int count=0;
            foreach(var worker in game.crowd.runners)
            {
                if(!worker || worker.isPlayer)continue;
                var reaction=worker.GetComponent<FactoryWorkerImpact>();if(!reaction || !reaction.message)continue;
                PlaceMessage(worker,reaction.message);count++;
                float top=worker.puppet.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).Max(r=>r.bounds.max.y);
                float bottom=reaction.message.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.y);
                report.AppendLine(worker.name+": messageY="+reaction.message.localPosition.y+" head clearance="+(bottom-top));
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryMessageHeights.txt","Raised "+count+" worker messages\n"+report);
        }
        public static void AudioCurrent()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            var ring=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/Ring.mp3");
            foreach(var signal in game.GetComponentsInChildren<FactoryPrinterSignal>(true)){signal.ring=ring;signal.speaker.clip=ring;EditorUtility.SetDirty(signal);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        public static void Apply(Transform root)
        {
            var game=root.GetComponent<CardboardFactoryGame>();
            Meal(game.lunch.tray,game.lunch,true);
            foreach(var tray in game.lunch.diners.meals)Meal(tray,game.lunch,false);
            Mechanism(game);Printers(game);Messages(game);FactoryReactionPresentationInstaller.Apply(root);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply shift upgrade in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();Apply(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryShiftUpgrade.txt","SUCCESS: plates, physical tray, 17-gear assembly, diagram cards, printer alarm and worker reactions installed.");
        }
    }
}
