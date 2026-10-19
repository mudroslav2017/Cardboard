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
    [InitializeOnLoad]
    public static class FactoryWorkstationUpgrade
    {
        const string RepairPending="Cardboard.FactoryPeopleRepairPending";
        static Action pendingCaptureRestore;
        static FactoryWorkstationUpgrade()
        {
            AssemblyReloadEvents.beforeAssemblyReload+=RestoreCaptureState;
            EditorApplication.quitting+=RestoreCaptureState;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)RestoreCaptureState();else if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RepairPending,false))EditorApplication.delayCall+=RestorePeopleCurrent;};
            EditorSceneManager.sceneSaving+=(scene,path)=>RestoreCaptureState();
            EditorSceneManager.sceneClosing+=(scene,removing)=>RestoreCaptureState();
        }
        static void RestoreCaptureState()
        {
            var restore=pendingCaptureRestore;pendingCaptureRestore=null;
            restore?.Invoke();
        }
        [MenuItem("Cardboard/Restore factory characters")]
        public static void RestorePeopleCurrent()
        {
            RestoreCaptureState();
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            SessionState.SetBool(RepairPending,EditorApplication.isPlayingOrWillChangePlaymode);
            var runners=game.GetComponentsInChildren<CardboardRunner>(true);
            foreach(var runner in runners)
            {
                runner.gameObject.SetActive(true);
                if(runner.puppet)
                {
                    runner.puppet.gameObject.SetActive(!runner.isPlayer || !EditorApplication.isPlaying);
                    foreach(var renderer in runner.puppet.GetComponentsInChildren<Renderer>(true))
                    {renderer.enabled=true;renderer.forceRenderingOff=false;EditorUtility.SetDirty(renderer);}
                    EditorUtility.SetDirty(runner.puppet.gameObject);
                }
                EditorUtility.SetDirty(runner.gameObject);
            }
            if(game.mainMenu)
            {
                if(game.mainMenu.pointer)game.mainMenu.pointer.gameObject.SetActive(true);
                foreach(var canvas in game.GetComponentsInChildren<Canvas>(true))canvas.enabled=true;
            }
            if(!EditorApplication.isPlaying)
            {EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();}
            int shown=runners.Count(runner=>!runner.isPlayer && runner.puppet && runner.puppet.gameObject.activeInHierarchy &&
                runner.puppet.GetComponentsInChildren<MeshRenderer>().Any(renderer=>renderer.enabled));
            File.WriteAllText("Library/FactoryPeopleRestored.txt","Visible NPCs: "+shown+" / "+runners.Count(runner=>!runner.isPlayer)+
                "; cursor root active: "+game.mainMenu.pointer.gameObject.activeInHierarchy+
                "; interface canvases enabled: "+game.GetComponentsInChildren<Canvas>(true).All(canvas=>canvas.enabled));
            SceneView.RepaintAll();
        }
        public const string ConveyorPath="Assets/Game_Assets/Cardboard/Conveer/Model/Conveer.fbx";
        public const string RailTexture="Assets/Game_Assets/Cardboard/Conveer/Texture/konveerbortic.png";
        public const float BackboardGap=.203f;
        static Bounds BoundsIn(Transform root,Transform space)
        {
            bool first=true;Bounds result=default;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(item=>!item.GetComponent<TextMesh>()))
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var bounds=renderer.localBounds;
                var point=space.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
                if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
            }
            return result;
        }
        public static void PaintConveyor(Transform conveyor)
        {
            var rollers=FactoryPresentationInstaller.Paper("ConveyorRollers",Color.white,
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Cardboard/Conveer/Texture/konveer.png"));
            var rails=FactoryPresentationInstaller.Paper("ConveyorSideRails",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(RailTexture));
            foreach(var renderer in conveyor.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials=Enumerable.Repeat(renderer.name=="Cylinder"?rollers:rails,renderer.sharedMaterials.Length).ToArray();
                CardboardSurface.Configure(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        static void Conveyor(FactoryWorkstation station)
        {
            var parent=station.transform;var old=parent.Find("Static conveyor");
            if(old)Object.DestroyImmediate(old.gameObject);
            var wrapper=new GameObject("Static conveyor").transform;wrapper.SetParent(parent,false);wrapper.localRotation=Quaternion.Euler(0,90,0);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorPath),wrapper);PaintConveyor(wrapper);
            var table=BoundsIn(parent.Find("Table"),parent);var bounds=BoundsIn(wrapper,parent);
            var roller=wrapper.GetComponentsInChildren<MeshRenderer>().First(item=>item.name=="Cylinder");
            var rollerBounds=BoundsIn(roller.transform,parent);
            wrapper.localPosition+=new Vector3(table.min.x+.012f-bounds.max.x,
                station.cuttingPosition.y-station.sheet.localScale.y*.5f-rollerBounds.max.y,
                station.cuttingPosition.z-rollerBounds.center.z);
            bounds=BoundsIn(wrapper,parent);
            float feedX=Mathf.Lerp(bounds.min.x,bounds.max.x,.25f);
            float shift=feedX-station.conveyorPosition.x;
            station.conveyorPosition=new Vector3(feedX,station.cuttingPosition.y,station.cuttingPosition.z);
            var pipe=parent.Find("Material pipe");if(pipe)pipe.localPosition+=Vector3.right*shift;
            station.dropPosition+=Vector3.right*shift;
            // Refit the four original supports to the updated conveyor footprint.
            var support=parent.Find("Conveyor floor stand");
            if(support)
            {
                float floor=FactoryEnvironmentInstaller.FloorY(parent.root);
                var world=BoundsIn(wrapper,parent.root);float bottom=floor+.001f,height=world.min.y-bottom+.025f;int i=0;
                foreach(var leg in support.GetComponentsInChildren<Transform>().Where(item=>item.name=="Conveyor support leg"))
                {
                    int row=i/2,column=i%2;i++;
                    leg.position=parent.root.TransformPoint(new Vector3(column==0?world.min.x+.13f:world.max.x-.13f,bottom+height*.5f,row==0?world.min.z+.12f:world.max.z-.12f));
                    leg.localScale=new Vector3(.08f,height,.08f);
                }
                i=0;foreach(var brace in support.GetComponentsInChildren<Transform>().Where(item=>item.name=="Conveyor support brace"))
                {
                    brace.position=parent.root.TransformPoint(new Vector3(i++==0?world.min.x+.13f:world.max.x-.13f,bottom+.15f,world.center.z));
                    brace.localScale=new Vector3(.08f,.10f,world.size.z-.16f);
                }
            }
            station.RebindModels();EditorUtility.SetDirty(station);
        }
        public static bool NeedsCollision(MeshRenderer renderer)
        {
            if(renderer.GetComponent<TextMesh>() || !renderer.GetComponent<MeshFilter>() ||
                renderer.GetComponentInParent<FactoryOperatorHand>())return false;
            return renderer.name!="Finishing liquid";
        }
        static void Collisions(FactoryWorkstation station)
        {
            foreach(var control in station.GetComponentsInChildren<FactoryStationControl>(true))
                foreach(var collider in control.GetComponents<Collider>().Where(shape=>!(shape is MeshCollider)))collider.isTrigger=true;
            var coarse=station.transform.Find("Workbench collision");
            if(coarse)foreach(var collider in coarse.GetComponents<Collider>())collider.enabled=false;
            foreach(var bath in station.baths)
            {
                coarse=bath.Find("Vessel collision");
                if(coarse)foreach(var collider in coarse.GetComponents<Collider>())collider.enabled=false;
            }
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Cardboard/Generated/PickupPhysics.physicMaterial");
            foreach(var renderer in station.GetComponentsInChildren<MeshRenderer>(true).Where(NeedsCollision))
            {
                bool solid=renderer.GetComponents<Collider>().Any(shape=>shape.enabled && !shape.isTrigger);
                if(!solid)
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;collider.convex=false;
                    collider.sharedMaterial=material;
                }
                renderer.gameObject.layer=8;EditorUtility.SetDirty(renderer.gameObject);
            }
        }
        public static void Apply(Transform world)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(ConveyorPath);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))Conveyor(station);
            FactoryInventoryInstaller.Backboards(world);
            // Keep the east aisle spawn positions clear of the nearby barrel storage array.
            foreach(var runner in world.GetComponentsInChildren<CardboardRunner>(true))
                if(!runner.isPlayer && runner.personality>=5 && runner.transform.position.x>CardboardFactoryWorld.WorkshopEast-3.2f)
                {
                    var body=runner.GetComponent<CharacterController>();bool enabled=body && body.enabled;
                    if(enabled)body.enabled=false;
                    var position=runner.transform.position;position.x=CardboardFactoryWorld.WorkshopEast-3.2f;runner.transform.position=position;
                    if(enabled)body.enabled=true;EditorUtility.SetDirty(runner.transform);
                }
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))Collisions(station);
            var game=world.GetComponent<CardboardFactoryGame>();if(game.instruction)FactoryPhysicsInstaller.Instruction(game.instruction);
        }
        [MenuItem("Cardboard/Update conveyor, workstation collisions and handling")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update workstation assets in Edit Mode.");
            var world=Object.FindFirstObjectByType<CardboardFactoryWorld>();Apply(world.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            AuditCurrent();Preview();
        }
        public static void AuditCurrent()
        {
            var report=new StringBuilder();int pass=0,fail=0;
            void Check(bool value,string text){report.AppendLine((value?"PASS ":"FAIL ")+text);if(value)pass++;else fail++;}
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            foreach(var station in game.stations)
            {
                var conveyor=station.transform.Find("Static conveyor");var renderers=conveyor.GetComponentsInChildren<MeshRenderer>();
                Check(renderers.Length==3,"Updated three-part conveyor: "+station.name);
                Check(renderers.Count(renderer=>AssetDatabase.GetAssetPath(renderer.sharedMaterial.GetTexture("_BaseMap"))==RailTexture)==2,"Both rails use the new authored texture");
                Check(renderers.All(renderer=>renderer.sharedMaterial.GetFloat("_Cull")==0 && renderer.sharedMaterial.GetFloat("_Smoothness")==0),"Conveyor and rails are double-sided and matte");
                var roller=renderers.First(renderer=>renderer.name=="Cylinder");
                Check(Mathf.Abs(BoundsIn(roller.transform,station.transform).max.y-(station.cuttingPosition.y-station.sheet.localScale.y*.5f))<.002f,"Roller tops meet the moving cardboard blank");
                var board=BoundsIn(station.transform.Find("Workstation backboard"),station.transform);
                var table=BoundsIn(station.transform.Find("Table"),station.transform);
                Check(Mathf.Abs(board.min.z-table.max.z-BackboardGap)<.003f,"Backboard is moved 20 cm behind the table");
                var meshes=station.GetComponentsInChildren<MeshRenderer>(true).Where(NeedsCollision).ToArray();
                var missing=meshes.Where(renderer=>!renderer.GetComponents<Collider>().Any(shape=>shape.enabled && !shape.isTrigger)).ToArray();
                Check(meshes.Length>40 && missing.Length==0,"Solid collisions on "+meshes.Length+" workstation mesh objects");
                foreach(var item in missing)report.AppendLine("DETAIL missing collision "+item.name);
                Check(station.GetComponentsInChildren<FactoryStationControl>(true).All(control=>control.GetComponents<Collider>().Where(shape=>!(shape is MeshCollider)).All(shape=>shape.isTrigger)),"Interaction targets do not physically block held objects");
            }
            var instruction=game.instruction;
            Check(instruction.controlHint.GetComponent<TextMesh>().text.Contains("HOLD LMB") && instruction.flipHint.GetComponent<TextMesh>().text.Contains("RMB: PUT DOWN"),"Instruction explains charged LMB throws and RMB placement in English");
            File.WriteAllText("Library/FactoryWorkstationUpgradeAudit.txt","RESULT: "+pass+" PASS, "+fail+" FAIL\n"+report);
        }
        public static void Preview()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var camera=game.mainMenu.lens;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            camera.transform.position=game.station.transform.TransformPoint(new Vector3(-.6f,3.1f,-5.6f));
            camera.transform.rotation=Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(-.65f,1.3f,0))-camera.transform.position);
            camera.fieldOfView=60;CardboardFactorySceneBuilder.Capture("UpdatedConveyorWorkstation");
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
        }
        [MenuItem("Cardboard/Capture menu background without people")]
        public static void CaptureCleanMenu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture the saved factory in Edit Mode.");
            RestoreCaptureState();
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var menu=game.mainMenu;var camera=menu.lens;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var actors=game.GetComponentsInChildren<CardboardRunner>(true).Select(runner=>runner.puppet?runner.puppet:runner.transform);
            var roots=new[]{menu.visuals,menu.settingsPanel,menu.pointer.transform}.Concat(actors).Distinct().ToArray();
            var active=roots.Select(root=>root.gameObject.activeSelf).ToArray();
            var canvases=game.GetComponentsInChildren<Canvas>(true);var canvasState=canvases.Select(canvas=>canvas.enabled).ToArray();
            bool overlay=menu.uiCamera.enabled;
            Action restore=()=>
            {
                if(camera)camera.transform.SetPositionAndRotation(position,rotation);
                if(camera)camera.fieldOfView=fov;if(menu.uiCamera)menu.uiCamera.enabled=overlay;
                for(int i=0;i<roots.Length;i++)if(roots[i])roots[i].gameObject.SetActive(active[i]);
                for(int i=0;i<canvases.Length;i++)if(canvases[i])canvases[i].enabled=canvasState[i];
                SceneView.RepaintAll();
            };
            pendingCaptureRestore=restore;
            foreach(var root in roots)root.gameObject.SetActive(false);
            foreach(var canvas in canvases)canvas.enabled=false;
            menu.uiCamera.enabled=false;
            camera.transform.SetPositionAndRotation(menu.viewPosition,menu.viewRotation);camera.fieldOfView=60;
            EditorApplication.delayCall+=()=>
            {
                if(pendingCaptureRestore!=restore)return;
                SceneView.RepaintAll();
                EditorApplication.delayCall+=()=>
                {
                    if(pendingCaptureRestore!=restore)return;
                    try {CardboardFactorySceneBuilder.Capture("FactoryMenuBackgroundClean");}
                    finally {RestoreCaptureState();}
                };
            };
        }
        public static void Inspect()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorPath);var report=new StringBuilder();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                report.AppendLine(filter.name+" | "+filter.sharedMesh.name+" | "+filter.sharedMesh.bounds);
            File.WriteAllText("Library/FactoryWorkstationUpgradeInspect.txt",report.ToString());
        }
    }
}
