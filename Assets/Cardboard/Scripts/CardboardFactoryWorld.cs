using UnityEngine;
using UnityEngine.Rendering;
using static Cardboard.CardboardArt;
using static Cardboard.FactoryGeometry;

namespace Cardboard
{
    public class CardboardFactoryWorld : MonoBehaviour
    {
        public const float WorkshopLength=44.8f/1.45f, WorkshopWidth=28f/1.5f, WallHeight=5.8f;
        public const float WorkshopCenterX=-5.6f;
        public const float WorkshopWest=WorkshopCenterX-WorkshopLength*.5f, WorkshopEast=WorkshopCenterX+WorkshopLength*.5f;
        public const float WorkshopNorth=WorkshopWidth*.5f, WorkshopSouth=-WorkshopWidth*.5f;
        public const float StationZ=WorkshopNorth-2.8f, PlayerSpawnZ=StationZ-2.7f;
        public const float CanteenWidth=26f, CanteenDepth=18f, CanteenSouth=-34f, CanteenNorth=-16f;
        public const float CanteenWest=-26f, CanteenEast=0;
        public void Build()
        {
            Initialize();
            var crowd=GetComponent<CardboardWorld>();crowd.runners.Clear();
            var game=GetComponent<CardboardFactoryGame>();game.crowd=crowd;
            game.gearPaper=Gold;game.gearEdge=Edge;game.gearInk=Ink;
            game.gearMeshes=new[] {GearMesh(8),GearMesh(10),GearMesh(12)};
            game.hubMeshes=new[] {HubMesh(FactoryHub.Square),HubMesh(FactoryHub.Hexagon),HubMesh(FactoryHub.Round)};
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogColor=Hex("3E4E45");
            RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=40;RenderSettings.fogEndDistance=100;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Hex("8C9989");
            var sun=Group("Warm factory skylight",transform,Vector3.zero).gameObject.AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=Hex("FFE9BD");sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(57,-28,0);sun.shadowStrength=.65f;
            Floor("Main workshop",new Vector3(WorkshopCenterX,0,0),new Vector2(WorkshopLength,WorkshopWidth));
            Solid("North workstation wall",transform,new Vector3(WorkshopCenterX,2.7f,WorkshopNorth),new Vector3(WorkshopLength,WallHeight,.5f),DarkLeaf);
            Solid("West workshop wall",transform,new Vector3(WorkshopWest,2.7f,0),new Vector3(.5f,WallHeight,WorkshopWidth),Leaf);
            Solid("East wall above exit",transform,new Vector3(WorkshopEast,2.7f,(WorkshopNorth-.4f)*.5f),new Vector3(.5f,WallHeight,WorkshopNorth+.4f),Leaf);
            Solid("East wall below exit",transform,new Vector3(WorkshopEast,2.7f,(WorkshopSouth-3.6f)*.5f),new Vector3(.5f,WallHeight,-3.6f-WorkshopSouth),Leaf);
            Solid("South wall west of canteen door",transform,new Vector3((WorkshopWest-14.5f)*.5f,2.7f,WorkshopSouth),new Vector3(-14.5f-WorkshopWest,WallHeight,.5f),DarkLeaf);
            Solid("South mechanism wall",transform,new Vector3((WorkshopEast-11.5f)*.5f,2.7f,WorkshopSouth),new Vector3(WorkshopEast+11.5f,WallHeight,.5f),DarkLeaf);
            // Shared dining room, with a clear central aisle from the workshop.
            var canteen=Group("Empty canteen",transform,Vector3.zero);
            Solid("Canteen floor",canteen,new Vector3(-13,-.32f,-25),new Vector3(CanteenWidth,.3f,CanteenDepth),Kraft);
            Box("Canteen floor surface",canteen,new Vector3(-13,-.16f,-25),new Vector3(CanteenWidth,.025f,CanteenDepth),Path);
            Solid("Canteen south wall",canteen,new Vector3(-13,2.7f,CanteenSouth),new Vector3(CanteenWidth,5.8f,.4f),Leaf);
            Solid("Canteen west wall",canteen,new Vector3(-26,2.7f,-25),new Vector3(.4f,5.8f,CanteenDepth),Leaf);
            Solid("Canteen east wall",canteen,new Vector3(0,2.7f,-25),new Vector3(.4f,5.8f,CanteenDepth),Leaf);
            for(int side=-1;side<=1;side+=2)
                Solid("Canteen north wall",canteen,new Vector3(-13+side*7.25f,2.7f,-16),new Vector3(11.5f,5.8f,.4f),Leaf);
            float passageLength=WorkshopSouth-CanteenNorth;
            float passageZ=(WorkshopSouth+CanteenNorth)*.5f;
            Solid("Canteen doorway floor",canteen,new Vector3(-13,-.32f,passageZ),new Vector3(3,.3f,passageLength+.4f),Path);
            foreach(int side in new[]{-1,1})
                Solid("Canteen passage side wall",canteen,new Vector3(-13+side*1.65f,2.7f,passageZ),new Vector3(.30f,WallHeight,passageLength+.5f),Leaf);
            Box("Canteen passage ceiling",canteen,new Vector3(-13,5.64f,passageZ),new Vector3(3.6f,.08f,passageLength+.5f),Kraft);
            for(int side=-1;side<=1;side+=2)
                Solid("Canteen door jamb",canteen,new Vector3(-13+side*1.5f,1.6f,-15),new Vector3(.18f,3.5f,2.4f),Edge);
            Box("Canteen doorway lintel",canteen,new Vector3(-13,3.5f,-15),new Vector3(3.2f,.2f,2.4f),Edge);
            for(float x=WorkshopWest+2;x<=WorkshopEast-2;x+=4)
            {
                Box("Wall corrugation",transform,new Vector3(x,2.8f,WorkshopNorth-.32f),new Vector3(.10f,5.7f,.12f),Edge);
                Box("Roof truss",transform,new Vector3(x,5.6f,0),new Vector3(.15f,.28f,WorkshopWidth),Edge);
            }
            for(float z=WorkshopSouth+3;z<=WorkshopNorth-2;z+=5)
            {
                Box("Overhead beam",transform,new Vector3(WorkshopCenterX,5.5f,z),new Vector3(WorkshopLength,.22f,.18f),Kraft);
                for(float x=WorkshopWest+5;x<=WorkshopEast-3;x+=8)
                {
                    Box("Folded light fitting",transform,new Vector3(x,4.8f,z),new Vector3(2,.15f,.65f),Cream);
                    var light=Group("Practical workshop lamp",transform,new Vector3(x,4.5f,z)).gameObject.AddComponent<Light>();
                    light.type=LightType.Point;light.range=10;light.intensity=2.1f;light.color=Hex("FFDEA2");
                }
            }
            Box("Factory ceiling sheets",transform,new Vector3(WorkshopCenterX,5.64f,0),new Vector3(WorkshopLength,.08f,WorkshopWidth),Kraft);
            Machine(game);
            game.stations=new FactoryWorkstation[4];game.workerPosts=new Vector3[3];
            for(int i=0;i<4;i++)
            {
                game.stations[i]=FactoryWorkstationBuilder.Build(transform,game,new Vector3(0,0,StationZ),i==1);
                if(i==1) game.station=game.stations[i];
            }
            LayoutStations(game);
            Exit(game);ExecutionTools(game);
            game.patrol=new[] {new Vector3(WorkshopWest+4,0,0),new Vector3(WorkshopCenterX,0,1),new Vector3(WorkshopEast-4,0,0),new Vector3(WorkshopEast-4,0,-4),new Vector3(WorkshopCenterX,0,-4.5f),new Vector3(WorkshopWest+5,0,-4)};
            for(int i=0;i<12;i++)
            {
                Vector3 pos=i==0?new Vector3(game.station.transform.localPosition.x,0,PlayerSpawnZ):
                    i>=2 && i<=4?game.workerPosts[i-2]:new Vector3(Mathf.Lerp(WorkshopWest+2,WorkshopEast-3.2f,(i%6)/5f),0,-2+(i/6)*3);
                var root=Group(i==0?"YOU • disguised worker 017":i==1?"Inspector • cardboard shift supervisor":"Cardboard factory worker "+i,transform,pos);
                var runner=root.gameObject.AddComponent<CardboardRunner>();runner.isPlayer=i==0;runner.personality=i;
                var pivot=Group("Whole-sheet sway pivot",root,new Vector3(0,-.16f,0));runner.puppet=pivot;
                var sheet=CardboardCutout.Create(pivot,i%6);
                root.localRotation=Quaternion.Euler(0,i<=4?0:i*53,0);
                root.gameObject.layer=9;
                var controller=root.gameObject.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.28f;
                controller.center=new Vector3(0,.76f,0);controller.stepOffset=.12f;controller.skinWidth=.035f;
                if(i!=0)
                {
                    sheet.gameObject.AddComponent<MeshCollider>().sharedMesh=sheet.GetComponent<MeshFilter>().sharedMesh;
                    sheet.gameObject.AddComponent<CardboardDrawingSurface>();
                }
                crowd.runners.Add(runner);runner.Pose(i*1.79f,i==0?0:.6f);
            }
            var cameraObject=Group("Main Camera",transform,new Vector3(game.station.transform.localPosition.x,1.62f,PlayerSpawnZ)).gameObject;cameraObject.tag="MainCamera";
            var cam=cameraObject.AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=120;cam.fieldOfView=CardboardCamera.GameplayFov;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("3E4E45");cam.transform.rotation=Quaternion.identity;
            cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<CardboardCamera>().player=crowd.runners[0].transform;
            cameraObject.AddComponent<CardboardMarker>();
            var hud=FactoryMistakeHUD.Create(transform,cam);
            var grade=FactoryDeathGrade.Create(hud.transform);
            game.captureSequence=FactoryCaptureSequence.Create(transform,game,grade);
        }
        public void LayoutStations(CardboardFactoryGame game)
        {
            Vector3 oldPlayerStation=game.station.transform.position;
            Bounds footprint=FactoryWorkstationBuilder.Footprint(game.stations[0].transform);
            float left=WorkshopWest+.25f, right=WorkshopEast-.25f;
            float gap=(right-left-footprint.size.x*game.stations.Length)/(game.stations.Length+1);
            if(gap<0) throw new System.InvalidOperationException("Workstations do not fit inside workshop.");
            float cursor=left+gap;int post=0;
            for(int i=0;i<game.stations.Length;i++)
            {
                var station=game.stations[i].transform;
                station.localPosition=new Vector3(cursor-footprint.min.x,station.localPosition.y,StationZ);
                if(i!=1) game.workerPosts[post++]=new Vector3(station.localPosition.x,0,StationZ-1.65f);
                cursor+=footprint.size.x+gap;
            }
            if(game.crowd.runners.Count>0)
            {
                foreach(var runner in game.crowd.runners)
                {
                    if(runner.isPlayer)runner.transform.localPosition=new Vector3(game.station.transform.localPosition.x,0,PlayerSpawnZ);
                    else if(runner.personality>=2 && runner.personality<=4)runner.transform.localPosition=game.workerPosts[runner.personality-2];
                }
                var camera=GetComponentInChildren<Camera>();
                if(camera)camera.transform.localPosition=new Vector3(game.station.transform.localPosition.x,1.62f,PlayerSpawnZ);
                if(game.instruction){game.instruction.floorPosition+=game.station.transform.position-oldPlayerStation;game.instruction.ResetFloor();}
            }
        }
        void Floor(string name,Vector3 center,Vector2 size)
        {
            Solid(name+" floor",transform,center+Vector3.down*.32f,new Vector3(size.x,.3f,size.y),Kraft);
            Box(name+" floor surface",transform,center+Vector3.down*.16f,new Vector3(size.x,.025f,size.y),Path);
            for(int x=-(int)size.x/2+2;x<size.x/2;x+=2)
                Box("Floor fold",transform,center+new Vector3(x,-.139f,0),new Vector3(.025f,.008f,size.y),Edge);
            for(int z=-(int)size.y/2+2;z<size.y/2;z+=2)
                Box("Floor fold",transform,center+new Vector3(0,-.136f,z),new Vector3(size.x,.008f,.025f),Edge);
        }
        void Machine(CardboardFactoryGame game)
        {
            var machine=Group("THE GREAT MECHANISM • giant cardboard press drive",transform,new Vector3(WorkshopEast-9.9f,0,WorkshopSouth+.8f));
            machine.localScale=Vector3.one*.8f;
            Solid("Machine chassis",machine,new Vector3(0,2.35f,0),new Vector3(21,4.8f,.7f),Teal);
            Solid("Machine foundation",machine,new Vector3(0,.22f,.1f),new Vector3(22,.7f,1.5f),Ink);
            for(int i=0;i<35;i++)
            {
                var stripe=Box("Warning stripe",machine,new Vector3(-10.5f+i*.62f,.6f,.88f),new Vector3(.35f,.28f,.025f),i%2==0?Gold:Ink);
                stripe.localRotation=Quaternion.Euler(0,0,-25);
            }
            for(int i=-1;i<=1;i+=2)
            {
                Rod("Pressure pipe",machine,new Vector3(i*10.1f,.4f,.5f),new Vector3(i*4.65f,3.8f,.5f),.15f,Edge);
                for(int j=0;j<4;j++) Disc("Machine bolt",machine,new Vector3(i*10.1f,1+j*.9f,.39f),.075f,.04f,Cream,8);
            }
            game.gears=new FactoryGear[3];
            for(int i=0;i<3;i++)
            {
                var root=Group("Gear socket "+(char)('A'+i),machine,new Vector3((i-1)*6,2.7f,.55f));
                var gear=root.gameObject.AddComponent<FactoryGear>();gear.index=i;gear.recipe=FactoryRecipe.ForType(i+1);
                gear.healthy=Group("Operational gear",root,Vector3.zero);
                GearVisual(gear.healthy,gear.recipe,game.gearMeshes,game.hubMeshes,Gold,Edge,Ink);
                gear.damaged=Group("Broken teeth",root,Vector3.zero);
                GearVisual(gear.damaged,gear.recipe,game.gearMeshes,game.hubMeshes,Gold,Edge,Ink);
                foreach(var renderer in gear.damaged.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int m=0;m<materials.Length;m++) { materials[m]=new Material(materials[m]);materials[m].color=new Color(1,.52f,.36f);Generated.Add(materials[m]); }
                    renderer.sharedMaterials=materials;
                }
                Stroke("Crack",gear.damaged,new Vector3(-.65f,.2f,.045f),new Vector3(.2f,-.4f,.045f),.025f,Ink);
                var hit=Group("Gear interaction",root,Vector3.zero);
                var hitbox=hit.gameObject.AddComponent<BoxCollider>();hitbox.size=new Vector3(2.2f,2.2f,.5f);
                Interaction(hit,FactoryAction.Gear,"E — обслужить узел",gear);
                gear.indicator=Group("Fault indicator",root,new Vector3(0,1.2f,.1f)).gameObject.AddComponent<Light>();
                gear.indicator.type=LightType.Point;gear.indicator.range=2;gear.indicator.intensity=1.4f;
                gear.SetBroken(true);game.gears[i]=gear;
            }
        }
        void Exit(CardboardFactoryGame game)
        {
            var gate=Group("End of shift exit",transform,new Vector3(WorkshopEast-.2f,0,-2));
            gate.localRotation=Quaternion.Euler(0,90,0);
            game.exitGate=Solid("Sliding exit gate",gate,new Vector3(0,1.45f,0),new Vector3(2.6f,3.2f,.35f),Teal);
            var inside=Box("Exit control",gate,new Vector3(0,1.4f,-.25f),new Vector3(1.4f,.8f,.08f),Cream);
            Interaction(inside,FactoryAction.Exit,"E — закончить смену");
        }
        void ExecutionTools(CardboardFactoryGame game)
        {
            game.captureStage=Group("Capture animation set outside workshop",transform,new Vector3(1000,0,1000));
            var stage=game.captureStage;
            var board=Solid("Employee correction board",stage,new Vector3(9,1.35f,3),new Vector3(2.8f,3.1f,.28f),Kraft);
            game.victim=Group("Caught worker cardboard victim",stage,new Vector3(9,.15f,3.3f));
            CardboardCutout.Create(game.victim,0);game.victim.gameObject.SetActive(false);
            game.pins=Group("Pin capture animation",stage,new Vector3(9,.15f,3.3f));
            game.buttons=Group("Drawing pin button capture animation",stage,new Vector3(9,.15f,3.3f));
            Vector2[] points={new Vector2(-.2f,1.95f),new Vector2(.2f,1.75f),new Vector2(-.22f,1.35f),new Vector2(.20f,1.13f),new Vector2(-.2f,.8f),new Vector2(.2f,.5f),new Vector2(0,1.55f),new Vector2(0,.2f)};
            for(int i=0;i<points.Length;i++)
            {
                var pin=Group("Long pin "+i,game.pins,new Vector3(points[i].x,points[i].y,1.9f));
                Rod("Steel pin shaft",pin,Vector3.zero,new Vector3(0,0,.75f),.018f,Cream);
                var head=Disc("Pin head",pin,new Vector3(0,0,.78f),.10f,.10f,i%2==0?Coral:Teal,16);
                var button=Group("Button tack "+i,game.buttons,new Vector3(points[i].x,points[i].y,1.9f));
                Rod("Tack needle",button,Vector3.zero,new Vector3(0,0,.24f),.026f,Edge);
                Disc("Big button",button,new Vector3(0,0,.29f),.13f,.06f,i%2==0?Gold:Cream,24);
                for(int h=0;h<4;h++) Disc("Button hole",button,new Vector3(h%2==0?-.035f:.035f,h<2?-.035f:.035f,.327f),.014f,.005f,Ink,8);
            }
            game.pins.gameObject.SetActive(false);game.buttons.gameObject.SetActive(false);
            var press=Group("Hydraulic cardboard press",stage,new Vector3(9,0,-3.5f));
            Solid("Press bed",press,new Vector3(0,.12f,0),new Vector3(3.2f,.5f,3.5f),Ink);
            for(int side=-1;side<=1;side+=2) Solid("Press upright",press,new Vector3(side*1.45f,2.1f,0),new Vector3(.28f,4.2f,2.8f),Edge);
            Box("Press lintel",press,new Vector3(0,4.2f,0),new Vector3(3.4f,.5f,3),Teal);
            Rod("Piston",press,new Vector3(0,2.3f,0),new Vector3(0,4.2f,0),.2f,Cream);
            game.pressHead=Box("Descending press plate",press,new Vector3(0,3.3f,0),new Vector3(2.9f,.3f,2.9f),Coral);
        }
    }
}

