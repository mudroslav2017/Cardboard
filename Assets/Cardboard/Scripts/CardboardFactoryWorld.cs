using UnityEngine;
using UnityEngine.Rendering;
using static Cardboard.CardboardArt;
using static Cardboard.FactoryGeometry;

namespace Cardboard
{
    public class CardboardFactoryWorld : MonoBehaviour
    {
        public void Build()
        {
            Initialize();
            var crowd=GetComponent<CardboardWorld>();crowd.runners.Clear();
            var game=GetComponent<CardboardFactoryGame>();game.crowd=crowd;
            game.gearPaper=Gold;game.gearEdge=Edge;game.gearInk=Ink;
            game.gearMeshes=new[] {GearMesh(8),GearMesh(10),GearMesh(12)};
            game.hubMeshes=new[] {HubMesh(FactoryHub.Square),HubMesh(FactoryHub.Hexagon),HubMesh(FactoryHub.Round)};
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogColor=Hex("3E4E45");
            RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=17;RenderSettings.fogEndDistance=42;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Hex("8C9989");
            var sun=Group("Warm factory skylight",transform,Vector3.zero).gameObject.AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=Hex("FFE9BD");sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(57,-28,0);sun.shadowStrength=.65f;
            Solid("Factory cardboard floor",transform,new Vector3(0,-.32f,0),new Vector3(26,.3f,21),Kraft);
            Box("Printed floor surface",transform,new Vector3(0,-.16f,0),new Vector3(25.8f,.025f,20.8f),Path);
            for(int x=-12;x<=12;x+=2) Box("Floor fold",transform,new Vector3(x,-.139f,0),new Vector3(.025f,.008f,20),Edge);
            for(int z=-10;z<=10;z+=2) Box("Floor fold",transform,new Vector3(0,-.136f,z),new Vector3(26,.008f,.025f),Edge);
            Solid("North factory wall",transform,new Vector3(0,2.7f,-10),new Vector3(26,5.8f,.5f),DarkLeaf);
            Solid("West factory wall",transform,new Vector3(-13,2.7f,0),new Vector3(.5f,5.8f,21),Leaf);
            Solid("East factory wall",transform,new Vector3(13,2.7f,0),new Vector3(.5f,5.8f,21),Leaf);
            Solid("Front wall left",transform,new Vector3(-7.25f,2.7f,10),new Vector3(11.5f,5.8f,.5f),DarkLeaf);
            Solid("Front wall right",transform,new Vector3(7.25f,2.7f,10),new Vector3(11.5f,5.8f,.5f),DarkLeaf);
            for(int x=-12;x<=12;x+=2)
            {
                Box("Wall corrugation",transform,new Vector3(x,2.8f,-9.68f),new Vector3(.10f,5.7f,.12f),Edge);
                Box("Roof truss",transform,new Vector3(x,5.6f,0),new Vector3(.15f,.28f,20),Edge);
            }
            for(int z=-8;z<=8;z+=4)
            {
                Box("Overhead beam",transform,new Vector3(0,5.5f,z),new Vector3(26,.22f,.18f),Kraft);
                var lamp=Box("Folded light fitting",transform,new Vector3(0,4.8f,z),new Vector3(2,.15f,.65f),Ink);
                Box("Lamp diffuser",lamp,new Vector3(0,-.55f,0),new Vector3(.9f,.1f,.8f),Cream);
                var light=Group("Practical workshop lamp",transform,new Vector3(0,4.5f,z)).gameObject.AddComponent<Light>();
                light.type=LightType.Point;light.range=10;light.intensity=3.1f;light.color=Hex("FFDEA2");
            }
            for(int side=-1;side<=1;side+=2)
            for(int z=-8;z<=8;z++)
                Box("Painted walkway dash",transform,new Vector3(side*5.3f,-.12f,z),new Vector3(.10f,.015f,.55f),Gold);
            Machine(game);
            game.workbench=FactoryWorkbenchBuilder.Build(transform,game);
            DisguiseLocker();
            Exit(game);
            ExecutionTools(game);
            Props();
            game.patrol=new[] {new Vector3(-4,0,-3),new Vector3(0,0,-3.7f),new Vector3(4,0,-3),new Vector3(4,0,1),new Vector3(0,0,4.5f),new Vector3(-4,0,1)};
            for(int i=0;i<12;i++)
            {
                Vector3 pos=i==0?new Vector3(0,0,7.3f):new Vector3(-4+(i-1)%5*2,0,-2+(i-1)/5*2.7f);
                var root=Group(i==0?"YOU • disguised worker 017":i==1?"Inspector • cardboard shift supervisor":"Cardboard factory worker "+i,transform,pos);
                var runner=root.gameObject.AddComponent<CardboardRunner>();runner.isPlayer=i==0;runner.personality=i;
                var pivot=Group("Whole-sheet sway pivot",root,new Vector3(0,-.16f,0));runner.puppet=pivot;
                var sheet=CardboardCutout.Create(pivot,i%6);
                root.localRotation=Quaternion.Euler(0,i==0?180:i*53,0);
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
            var cameraObject=Group("Main Camera",transform,new Vector3(0,1.62f,7.3f)).gameObject;cameraObject.tag="MainCamera";
            var cam=cameraObject.AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=80;cam.fieldOfView=72;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("3E4E45");cam.transform.rotation=Quaternion.Euler(0,180,0);
            cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<CardboardCamera>().player=crowd.runners[0].transform;
            cameraObject.AddComponent<CardboardMarker>();gameObject.AddComponent<FactoryHUD>();
        }
        void Machine(CardboardFactoryGame game)
        {
            var machine=Group("THE GREAT MECHANISM • giant cardboard press drive",transform,new Vector3(0,0,-7.7f));
            Solid("Machine chassis",machine,new Vector3(0,2.35f,0),new Vector3(10.5f,4.8f,.7f),Teal);
            Solid("Machine foundation",machine,new Vector3(0,.22f,.1f),new Vector3(11.5f,.7f,1.5f),Ink);
            WorldLabel("ГЛАВНЫЙ МЕХАНИЗМ  /  ЛИНИЯ 017",machine,new Vector3(0,4.35f,.4f),.13f,9.8f,true);
            for(int i=0;i<18;i++)
            {
                var stripe=Box("Warning stripe",machine,new Vector3(-5.2f+i*.62f,.6f,.88f),new Vector3(.35f,.28f,.025f),i%2==0?Gold:Ink);
                stripe.localRotation=Quaternion.Euler(0,0,-25);
            }
            for(int i=-1;i<=1;i+=2)
            {
                Rod("Pressure pipe",machine,new Vector3(i*4.65f,.4f,.5f),new Vector3(i*4.65f,3.8f,.5f),.15f,Edge);
                for(int j=0;j<4;j++) Disc("Machine bolt",machine,new Vector3(i*4.9f,1+j*.9f,.39f),.075f,.04f,Cream,8);
            }
            game.gears=new FactoryGear[3];
            for(int i=0;i<3;i++)
            {
                var root=Group("Gear socket "+(char)('A'+i),machine,new Vector3((i-1)*2.45f,2.7f,.55f));
                var gear=root.gameObject.AddComponent<FactoryGear>();gear.index=i;gear.recipe=new FactoryRecipe(8+i*2,(FactoryHub)i,new[] {2,3,1}[i]);
                gear.healthy=Group("Operational gear",root,Vector3.zero);
                GearVisual(gear.healthy,gear.recipe,game.gearMeshes,game.hubMeshes,Gold,Edge,Ink);
                gear.damaged=Group("Broken teeth",root,Vector3.zero);
                MeshPart("Missing tooth gear",gear.damaged,Vector3.zero,GearMesh(gear.recipe.teeth,true),Coral,Edge);
                MeshPart("Damaged axle",gear.damaged,new Vector3(0,0,.07f),game.hubMeshes[i],Ink,Edge);
                Stroke("Crack",gear.damaged,new Vector3(-.65f,.2f,.045f),new Vector3(.2f,-.4f,.045f),.025f,Ink);
                var hit=Group("Gear interaction",root,Vector3.zero);
                var hitbox=hit.gameObject.AddComponent<BoxCollider>();hitbox.size=new Vector3(2.2f,2.2f,.5f);
                Interaction(hit,FactoryAction.Gear,"E — обслужить узел",gear);
                var paper=Box("Instruction beneath gear "+(char)('A'+i),machine,new Vector3((i-1)*2.45f,1.03f,.48f),new Vector3(2.18f,.96f,.07f),Cream);
                WorldLabel("НАРЯД "+(char)('A'+i)+"\n"+gear.recipe.teeth+" ЗУБЬЕВ  /  "+FactoryRecipe.HubName(gear.recipe.hub).ToUpper()+"\nСЛОЁВ КАРТОНА: "+gear.recipe.layers,machine,new Vector3((i-1)*2.45f,1.03f,.54f),.058f,2.05f);
                Interaction(paper,FactoryAction.Blueprint,"E — запомнить инструкцию",gear);
                gear.indicator=Group("Fault indicator",root,new Vector3(0,1.2f,.1f)).gameObject.AddComponent<Light>();
                gear.indicator.type=LightType.Point;gear.indicator.range=2;gear.indicator.intensity=1.4f;
                gear.SetBroken(true);game.gears[i]=gear;
            }
        }
        void DisguiseLocker()
        {
            var locker=Solid("Spare cardboard disguise locker",transform,new Vector3(-10.8f,1.25f,6.8f),new Vector3(1.9f,2.8f,.6f),Coral);
            var interact=Box("Disguise locker door",locker,new Vector3(0,0,.55f),new Vector3(.92f,.92f,.08f),Kraft);
            // Labels live in world units rather than inheriting the cabinet's nonuniform scale.
            WorldLabel("МАСКИРОВКА\nЗАПАСНЫЕ МАСКИ",transform,new Vector3(-10.8f,2.2f,7.18f),.075f,1.75f);
            Interaction(interact,FactoryAction.Disguise,"E — поправить картонную маскировку");
        }
        void Exit(CardboardFactoryGame game)
        {
            var gate=Group("End of shift exit",transform,new Vector3(0,0,9.75f));
            game.exitGate=Solid("Sliding exit gate",gate,new Vector3(0,1.45f,0),new Vector3(2.6f,3.2f,.35f),Teal);
            var inside=Box("Exit control",gate,new Vector3(0,1.4f,-.25f),new Vector3(1.4f,.8f,.08f),Cream);
            Interaction(inside,FactoryAction.Exit,"E — закончить смену");
            var sign=Group("Exit sign facing into factory",gate,new Vector3(0,3.3f,-.24f));sign.localRotation=Quaternion.Euler(0,180,0);
            WorldLabel("ВЫХОД / ПОСЛЕ СМЕНЫ",sign,Vector3.zero,.09f,3f,true);
        }
        void ExecutionTools(CardboardFactoryGame game)
        {
            var board=Solid("Employee correction board",transform,new Vector3(9,1.35f,3),new Vector3(2.8f,3.1f,.28f),Kraft);
            WorldLabel("НЕПРАВИЛЬНЫХ — ПРИКОЛОТЬ",transform,new Vector3(9,3.15f,3.2f),.073f,2.7f,true);
            game.victim=Group("Caught worker cardboard victim",transform,new Vector3(9,.15f,3.3f));
            CardboardCutout.Create(game.victim,0);game.victim.gameObject.SetActive(false);
            game.pins=Group("Pin capture animation",transform,new Vector3(9,.15f,3.3f));
            game.buttons=Group("Drawing pin button capture animation",transform,new Vector3(9,.15f,3.3f));
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
            var press=Group("Hydraulic cardboard press",transform,new Vector3(9,0,-3.5f));
            Solid("Press bed",press,new Vector3(0,.12f,0),new Vector3(3.2f,.5f,3.5f),Ink);
            for(int side=-1;side<=1;side+=2) Solid("Press upright",press,new Vector3(side*1.45f,2.1f,0),new Vector3(.28f,4.2f,2.8f),Edge);
            Box("Press lintel",press,new Vector3(0,4.2f,0),new Vector3(3.4f,.5f,3),Teal);
            Rod("Piston",press,new Vector3(0,2.3f,0),new Vector3(0,4.2f,0),.2f,Cream);
            game.pressHead=Box("Descending press plate",press,new Vector3(0,3.3f,0),new Vector3(2.9f,.3f,2.9f),Coral);
            WorldLabel("ПРЕСС / ПОСТОРОННИМ НЕ ЛЕЗТЬ",press,new Vector3(0,4.65f,1.6f),.075f,3.1f,true);
        }
        void Props()
        {
            for(int i=0;i<5;i++)
            {
                var box=Solid("Shipping carton",transform,new Vector3(-10+(i%2)*1.3f,.4f,-5.7f+(i/2)*1.3f),new Vector3(1.1f,1.1f,1),Kraft);
                Box("Carton tape",box,new Vector3(0,.51f,0),new Vector3(.18f,.02f,1),Tape);
            }
            for(int i=0;i<4;i++)
            {
                var window=Box("High factory window",transform,new Vector3(-9+i*6,4.3f,-9.66f),new Vector3(3,1.4f,.04f),Cream);
                Box("Window mullion",window,Vector3.zero,new Vector3(.025f,1.05f,2),Edge);
            }
            WorldLabel("БУДЬ КАК ВСЕ.\nНЕ ДЕЛАЙ ОШИБОК.",transform,new Vector3(-8,3.2f,-9.3f),.13f,4.5f,true);
            WorldLabel("ЗАВОД КАРТОННЫХ ЛЮДЕЙ\nСМЕНА 017",transform,new Vector3(8,3.2f,-9.3f),.11f,4.5f,true);
        }
    }
}

