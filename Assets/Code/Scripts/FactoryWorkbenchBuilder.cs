using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.FactoryGeometry;

namespace Cardboard
{
    public static class FactoryWorkbenchBuilder
    {
        static FactoryWorkbench machine;
        static Transform LabelPlate(string words,Transform parent,Vector3 position,Vector2 size,float type=.035f)
        {
            var plate=Group(words,parent,position);
            Box("Crooked screwed-on instruction plate",plate,Vector3.zero,new Vector3(size.x,size.y,.025f),Cream);
            WorldLabel(words,plate,new Vector3(0,0,.025f),type,size.x-.035f);
            for(int i=-1;i<=1;i+=2) Disc("Screw",plate,new Vector3(i*(size.x/2-.035f),size.y/2-.035f,.025f),.014f,.012f,Edge,8);
            return plate;
        }
        static Transform Control(WorkbenchPart part,Vector3 position,Vector3 size)
        {
            var t=Group("Physical control • "+part,machine.transform,position);
            var hit=t.gameObject.AddComponent<BoxCollider>();hit.size=size;
            var control=t.gameObject.AddComponent<WorkbenchControl>();control.machine=machine;control.part=part;
            Interaction(t,FactoryAction.Bench,"");return t;
        }
        static Transform Lever(WorkbenchPart part,Vector3 position,float length,Material color)
        {
            var t=Control(part,position,new Vector3(.3f,length+.16f,.24f));
            t.GetComponent<BoxCollider>().center=new Vector3(0,length/2,.1f);
            Disc("Lever pivot",t,Vector3.zero,.10f,.12f,Ink,12);
            Rod("Chunky wooden lever",t,new Vector3(0,0,.08f),new Vector3(0,length,.08f),.035f,Edge);
            Box("Square grip wrapped in tape",t,new Vector3(0,length,.1f),new Vector3(.23f,.15f,.20f),color);
            return t;
        }
        public static FactoryWorkbench Build(Transform parent,CardboardFactoryGame game)
        {
            machine=Group("KR-3 • manual gear cutting and laminating machine",parent,new Vector3(-8,0,.5f)).gameObject.AddComponent<FactoryWorkbench>();
            machine.gameObject.AddComponent<FactoryWorkbenchSound>();
            var root=machine.transform;
            Solid("Heavy assembly bench",root,new Vector3(0,.85f,0),new Vector3(5.2f,.25f,1.8f),Teal);
            for(int side=-1;side<=1;side+=2) Solid("Workbench trestle",root,new Vector3(side*2.1f,.3f,0),new Vector3(.2f,.9f,1.5f),Edge);
            Solid("Machine panel",root,new Vector3(0,1.78f,.1f),new Vector3(4.9f,1.65f,.35f),DarkLeaf);
            LabelPlate("KR-3 / MANUAL GEAR CUTTING MACHINE",root,new Vector3(0,2.72f,.32f),new Vector2(4.6f,.28f),.065f);

            machine.toothDial=Control(WorkbenchPart.Teeth,new Vector3(-1.78f,2.08f,.46f),new Vector3(.6f,.6f,.23f));
            Disc("Tooth selector face",machine.toothDial,Vector3.zero,.29f,.12f,Kraft,12);
            Box("Tooth selector handle",machine.toothDial,new Vector3(0,0,.11f),new Vector3(.10f,.45f,.15f),Edge);
            Disc("Asymmetric tooth selector index",machine.toothDial,new Vector3(0,.22f,.20f),.045f,.025f,Gold,8);
            for(int i=0;i<3;i++)
            {
                float angle=(90+i*120)*Mathf.Deg2Rad;
                WorldLabel(new[] {"8","12","10"}[i],root,new Vector3(-1.78f+Mathf.Cos(angle)*.39f,2.08f+Mathf.Sin(angle)*.39f,.36f),.044f,.4f,true);
            }
            LabelPlate("TEETH / REVERSE STROKE",root,new Vector3(-1.78f,1.53f,.35f),new Vector2(1.1f,.19f),.029f);

            machine.hubDial=Control(WorkbenchPart.Hub,new Vector3(-.68f,2.08f,.46f),new Vector3(.57f,.57f,.23f));
            Disc("Axle die selector",machine.hubDial,Vector3.zero,.28f,.10f,Teal,12);
            for(int i=0;i<3;i++)
            {
                float angle=(90-i*120)*Mathf.Deg2Rad;
                var shape=MeshPart("Visible axle stamp "+i,machine.hubDial,new Vector3(Mathf.Cos(angle)*.17f,Mathf.Sin(angle)*.17f,.08f),game.hubMeshes[i],Cream,Edge);
                shape.localScale=Vector3.one*.28f;
            }
            Box("Fixed index mark",root,new Vector3(-.68f,2.46f,.40f),new Vector3(.04f,.13f,.03f),Gold);
            LabelPlate("HUB / UPPER DIE",root,new Vector3(-.68f,1.53f,.35f),new Vector2(1.03f,.19f),.027f);

            machine.feedLever=Lever(WorkbenchPart.Feed,new Vector3(-2.02f,1.08f,.96f),.36f,Gold);
            LabelPlate("FEED\nUP / DOWN",root,new Vector3(-2.02f,.64f,.99f),new Vector2(.91f,.3f),.033f);
            for(int i=0;i<6;i++) Box("Raw stock",root,new Vector3(-1.1f,1+i*.025f,-.39f),new Vector3(1,.02f,.54f),Kraft);
            machine.feedRollerA=Group("Driven upper feed roller",root,new Vector3(-1.1f,1.41f,.59f));
            machine.feedRollerB=Group("Counter-rotating lower feed roller",root,new Vector3(-1.1f,1.20f,.59f));
            foreach(var roller in new[] {machine.feedRollerA,machine.feedRollerB})
            {
                Rod("Feed roller",roller,new Vector3(-.35f,0,0),new Vector3(.35f,0,0),.075f,Teal);
                Box("Painted roller stripe",roller,new Vector3(0,.075f,0),new Vector3(.61f,.015f,.045f),Gold);
                for(int side=-1;side<=1;side+=2) Box("Roller bearing",root,roller.localPosition+new Vector3(side*.40f,0,-.07f),new Vector3(.11f,.20f,.22f),Edge);
            }

            // An open frame shows the packet and the finished part through its bars.
            Box("Cutting chamber back",root,new Vector3(.25f,1.35f,.40f),new Vector3(1.2f,.7f,.05f),Ink);
            machine.output=Control(WorkbenchPart.Output,new Vector3(.25f,1.32f,.64f),new Vector3(1.1f,.62f,.12f));
            machine.sheets=new Transform[3];
            for(int i=0;i<3;i++) machine.sheets[i]=Box("Individually fed cardboard layer "+(i+1),root,new Vector3(.25f,1.12f+i*.075f,.58f),new Vector3(.90f,.045f,.17f),Kraft);
            machine.clampPlaten=Group("Moving packet clamp",root,new Vector3(.25f,1.72f,.60f));
            Box("Clamping platen",machine.clampPlaten,Vector3.zero,new Vector3(.99f,.085f,.22f),Gold);
            for(int side=-1;side<=1;side+=2) Rod("Clamp guide rod",root,new Vector3(.25f+side*.52f,1.08f,.53f),new Vector3(.25f+side*.52f,1.79f,.53f),.025f,Cream);
            machine.cuttingHead=Group("Lever-driven cutting punch",root,new Vector3(.25f,1.84f,.68f));
            Box("Moving die head",machine.cuttingHead,Vector3.zero,new Vector3(.63f,.10f,.20f),Coral);
            Box("Cutting edge",machine.cuttingHead,new Vector3(0,-.08f,0),new Vector3(.59f,.065f,.055f),Cream);
            machine.guard=Group("Sliding safety grille",root,new Vector3(.25f,1.33f,.83f));machine.guardHome=machine.guard.localPosition;
            for(int i=-1;i<=1;i+=2) Box("Grille rail",machine.guard,new Vector3(0,i*.35f,0),new Vector3(1.27f,.07f,.06f),Coral);
            for(int i=-2;i<=2;i++) Box("Grille upright",machine.guard,new Vector3(i*.28f,0,0),new Vector3(.035f,.7f,.045f),Edge);
            var guardLatch=Control(WorkbenchPart.Guard,new Vector3(1.03f,1.44f,.90f),new Vector3(.28f,.34f,.20f));
            Box("Guard slide handle",guardLatch,Vector3.zero,new Vector3(.23f,.26f,.16f),Coral);
            LabelPlate("GUARD",root,new Vector3(1.03f,1.09f,.95f),new Vector2(.65f,.17f),.028f);

            machine.clampLever=Lever(WorkbenchPart.Clamp,new Vector3(-.6f,1.04f,1.0f),.35f,Teal);
            LabelPlate("CLAMP",root,new Vector3(-.57f,.64f,1.0f),new Vector2(.8f,.20f),.034f);
            var reject=Control(WorkbenchPart.Reject,new Vector3(-1.23f,.73f,1.0f),new Vector3(.23f,.18f,.25f));machine.rejectHandle=reject;
            Box("Scrap ejector grip",reject,Vector3.zero,new Vector3(.20f,.13f,.23f),Coral);
            LabelPlate("DISCARD",root,new Vector3(-1.3f,.47f,1.01f),new Vector2(.8f,.18f),.028f);
            LabelPlate("OUTPUT / TAKE SHEET",root,new Vector3(.30f,.79f,1.02f),new Vector2(1.4f,.18f),.030f);

            machine.flywheel=Control(WorkbenchPart.Crank,new Vector3(1.55f,2.05f,.60f),new Vector3(.92f,.92f,.24f));
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;
                Rod("Uneven flywheel rim",machine.flywheel,new Vector3(Mathf.Cos(a)*.43f,Mathf.Sin(a)*.43f,0),new Vector3(Mathf.Cos(b)*.43f,Mathf.Sin(b)*.43f,0),.048f,Edge);
            }
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;Rod("Flywheel spoke",machine.flywheel,Vector3.zero,new Vector3(Mathf.Cos(a)*.40f,Mathf.Sin(a)*.40f,0),.027f,Kraft);
            }
            Disc("Wheel hub",machine.flywheel,Vector3.zero,.10f,.16f,Teal,12);
            Rod("Crank grip spindle",machine.flywheel,new Vector3(.38f,0,.02f),new Vector3(.38f,0,.27f),.045f,Ink);
            machine.driveGearA=Group("Flywheel transmission cog",root,new Vector3(.98f,2.58f,.43f));
            machine.driveGearB=Group("Meshed counter-rotating transmission cog",root,new Vector3(1.24f,2.58f,.43f));
            MeshPart("Drive cog",machine.driveGearA,Vector3.zero,game.gearMeshes[0],Gold,Edge).localScale=Vector3.one*.14f;
            MeshPart("Follower cog",machine.driveGearB,Vector3.zero,game.gearMeshes[0],Edge,Ink).localScale=Vector3.one*.14f;
            LabelPlate("FLYWHEEL\n3 CLOCKWISE TURNS",root,new Vector3(1.53f,1.40f,.38f),new Vector2(1.48f,.29f),.030f);

            var meter=Group("Mechanical spring tension dial",root,new Vector3(.37f,2.19f,.41f));
            Disc("Tension meter",meter,Vector3.zero,.25f,.07f,Cream,24);
            WorldLabel("0        III",meter,new Vector3(0,.11f,.051f),.032f,.46f);
            machine.needle=Group("Physical tension needle",meter,Vector3.zero);
            Stroke("Needle",machine.needle,Vector3.zero,new Vector3(0,.20f,.065f),.024f,Coral);
            machine.readyLamp=Disc("Mechanical ready telltale",root,new Vector3(.37f,1.78f,.42f),.055f,.05f,Ink,12).GetComponent<Renderer>();
            machine.lampOff=Ink;machine.lampOn=Gold;
            machine.cutter=Lever(WorkbenchPart.Cutter,new Vector3(2.20f,1.03f,.95f),.64f,Coral);
            LabelPlate("CUTTER\nFULL STROKE",root,new Vector3(2.08f,.56f,.99f),new Vector2(.8f,.30f),.030f);

            var statusPlate=LabelPlate(" ",root,new Vector3(0,2.99f,.34f),new Vector2(4.7f,.24f),.040f);
            machine.status=Label("NO SHEET LOADED",statusPlate,new Vector3(0,0,.03f),.04f).GetComponent<TextMesh>();
            var manual=LabelPlate("KR-3 OPERATOR MANUAL\n\nAdjust cams with the sheet released.\nThe guard engages the clutch.\nSpring: THREE turns.\nOpening resets tension.\nDo not force the cutter at its stop.\nRelease the clamp to take the part.\nA fourth sheet jams the machine.",root,new Vector3(-2.15f,2.03f,-.65f),new Vector2(1.26f,1.65f),.034f);
            manual.localRotation=Quaternion.Euler(0,25,-4);
            // Put the manual beside the panel, so it must be read by moving to the side.
            manual.localPosition=new Vector3(-3.08f,1.90f,.50f);
            LabelPlate("LMB: CLICK OR DRAG THE LEVER\nSELECTORS / FLYWHEEL: TURN IN A CIRCLE\nFEED: UP AND BACK IN ONE GRIP\nE - PULL / ESC - RELEASE",root,new Vector3(0,.17f,1.0f),new Vector2(4.0f,.53f),.035f);
            machine.scraps=new Transform[3];
            for(int i=0;i<3;i++) machine.scraps[i]=Box("Ejected cardboard scrap "+i,root,Vector3.zero,new Vector3(.55f,.035f,.28f),Kraft);
            machine.gripHand=Group("Right hand physically gripping the machine",root,Vector3.zero);
            Box("Kraft palm",machine.gripHand,Vector3.zero,new Vector3(.15f,.09f,.15f),Kraft);
            for(int i=0;i<4;i++) Box("Folded gripping finger",machine.gripHand,new Vector3(-.06f+i*.04f,-.055f,.045f),new Vector3(.031f,.08f,.09f),Cream);
            Box("Thumb",machine.gripHand,new Vector3(.09f,0,.04f),new Vector3(.055f,.065f,.10f),Kraft);
            Box("Worker sleeve",machine.gripHand,new Vector3(0,-.02f,-.19f),new Vector3(.13f,.12f,.28f),Teal);
            machine.ResetMachine();return machine;
        }
    }
}
