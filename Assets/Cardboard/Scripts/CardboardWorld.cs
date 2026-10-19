using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Cardboard.CardboardArt;

namespace Cardboard
{
    public class CardboardWorld : MonoBehaviour
    {
        public static CardboardWorld Instance;
        public List<CardboardRunner> runners = new List<CardboardRunner>();
        void Awake() { Instance = this; if (transform.childCount == 0) Build(); }

        public void Build()
        {
            Initialize(); runners.Clear();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("BFC9BB");
            RenderSettings.fog = false;

            var sun = Group("Afternoon softbox", transform, Vector3.zero).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.45f; sun.color = Hex("FFF0D8");
            sun.transform.rotation = Quaternion.Euler(48, -40, 0); sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .6f; sun.shadowBias = .05f;
            var fill = Group("Cool fill", transform, Vector3.zero).gameObject.AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .4f; fill.color = Hex("C7E4DF");
            fill.transform.rotation = Quaternion.Euler(35, 130, 0);

            // Visible lamination and fluted edges make the island a tabletop construction.
            var island = Group("Layered cardboard diorama", transform, new Vector3(0,-.32f,0));
            for (int i = 0; i < 4; i++)
            {
                var layer = Disc("Kraft layer " + i, island, Vector3.down * i * .14f, 1, .13f, i == 0 ? Grass : Kraft, 64, .018f);
                layer.localRotation = Quaternion.Euler(90, 0, 0); layer.localScale = new Vector3(12.5f, 8.8f, 1);
            }
            for (int i = 0; i < 150; i++)
            {
                float a = i * Mathf.PI * 2 / 150;
                var flute = Box("Exposed corrugation", island, new Vector3(Mathf.Cos(a) * 12.47f, -.21f, Mathf.Sin(a) * 8.78f), new Vector3(.05f,.2f,.055f), Edge);
                flute.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            var track = Disc("Oval paper track", transform, new Vector3(0,-.235f,0), 1, .025f, Path, 80);
            track.localRotation = Quaternion.Euler(90,0,0); track.localScale = new Vector3(9.6f,6.25f,1);
            var lawn = Disc("Inner cutout", transform, new Vector3(0,-.20f,0), 1, .025f, Grass, 64,.025f);
            lawn.localRotation = Quaternion.Euler(90,0,0); lawn.localScale = new Vector3(6.7f,3.8f,1);
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                var dash = Box("Painted lane dash", transform, new Vector3(Mathf.Cos(a)*8.25f,-.208f,Mathf.Sin(a)*5.1f), new Vector3(.40f,.016f,.06f), Cream);
                dash.localRotation = Quaternion.Euler(0, -Mathf.Atan2(5.1f*Mathf.Cos(a),-8.25f*Mathf.Sin(a))*Mathf.Rad2Deg, 0);
            }
            for (int i = 0; i < 5; i++) Box("Start line", transform, new Vector3(8.2f,-.185f,(i-2)*.22f), new Vector3(2.1f,.018f,.10f), Cream);

            // Handmade scenery stays outside the playable oval.
            Tree(new Vector3(-9.2f,0,-5.1f), 1.3f, -18);
            Tree(new Vector3(-10,0,-2.2f), .95f, 15);
            Tree(new Vector3(8.5f,0,-5.3f), 1.25f, -15);
            Tree(new Vector3(10,0,-3.8f), .9f, 22);
            Tree(new Vector3(-5.2f,0,-6.9f), 1.05f, 8);
            var rng = new System.Random(56);
            for (int i = 0; i < 55; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                float radius = .9f + (float)rng.NextDouble() * .05f;
                Vector3 p = new Vector3(Mathf.Cos(a)*11.9f*radius, -.13f, Mathf.Sin(a)*8.2f*radius);
                var tuft = Group("Folded grass", transform, p);
                tuft.localRotation = Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                for (int j = 0; j < 3; j++)
                {
                    var blade = Panel("Paper blade", tuft, new Vector3((j-1)*.09f,.10f,0), new[] {new Vector2(-.08f,-.1f), new Vector2(.08f,-.1f), new Vector2(.05f,.16f+j*.07f)}, .035f, i%3 == 0 ? Gold : DarkLeaf);
                    blade.localRotation = Quaternion.Euler(0,j*45,(j-1)*-22);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * 2.4f;
                var flower = Group("Paper daisy", transform, new Vector3(Mathf.Cos(a)*5.3f,0,Mathf.Sin(a)*2.7f));
                Box("Stem", flower, new Vector3(0,.13f,0),new Vector3(.04f,.3f,.04f),DarkLeaf);
                for (int j = 0; j < 5; j++)
                {
                    float b = j*Mathf.PI*2/5;
                    Disc("Petal",flower,new Vector3(Mathf.Cos(b)*.10f,.32f+Mathf.Sin(b)*.10f,0),.09f,.025f,Cream,8);
                }
                Disc("Pollen",flower,new Vector3(0,.32f,.025f),.065f,.03f,Gold,10);
            }
            Sign(new Vector3(-1.4f,0,-4), "HEADING SOMEWHERE", -8);
            var bench = Group("Folded picnic bench", transform,new Vector3(4,0,-3));
            for (int i = -1; i <= 1; i+=2) Box("A-frame leg",bench,new Vector3(i*.65f,.3f,0),new Vector3(.12f,.6f,.65f),Kraft);
            Box("Seat",bench,new Vector3(0,.65f,0),new Vector3(2,.12f,.7f),Coral);
            Box("Backrest",bench,new Vector3(0,1.1f,-.32f),new Vector3(2,.6f,.1f),Kraft);
            for (int i = 0; i < 3; i++)
            {
                var box = Box("Abandoned delivery",transform,new Vector3(-4+i*.65f,.23f,1.5f+i*.35f),new Vector3(.7f,.7f,.7f),Kraft);
                box.localRotation = Quaternion.Euler(0,i*19,0);
                Box("Packing tape",box,new Vector3(0,.51f,0),new Vector3(.23f,.015f,1.02f),Tape);
            }

            for (int i = 0; i < 12; i++)
            {
                float a = (i-1) * Mathf.PI * 2 / 11;
                Vector3 pos = i == 0 ? new Vector3(0,0,5.4f) : i == 1 ? new Vector3(-1.8f,0,1.5f) : i == 2 ? new Vector3(2,0,-.5f) : new Vector3(Mathf.Cos(a)*8,0,Mathf.Sin(a)*5);
                var runner = CreateRunner(i, pos); runners.Add(runner);
                Vector3 face = new Vector3(-Mathf.Sin(a)*8,0,Mathf.Cos(a)*5);
                runner.transform.rotation = i == 0 ? Quaternion.Euler(0,180,0) : Quaternion.LookRotation(face);
                runner.Pose(i*1.79f, i == 0 ? 0 : .8f);
            }
            var cameraObject = Group("Main Camera",transform,new Vector3(0,1.62f,5.4f)).gameObject;
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = false; camera.fieldOfView = CardboardCamera.GameplayFov;
            camera.nearClipPlane = .05f; camera.farClipPlane = 150;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("AFC6B5");
            camera.transform.rotation = Quaternion.Euler(0,180,0); cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<CardboardCamera>(); rig.player = runners[0].transform;
            cameraObject.AddComponent<CardboardMarker>();
            gameObject.AddComponent<CardboardHUD>();
        }

        CardboardRunner CreateRunner(int index, Vector3 position)
        {
            var root=Group(index==0?"YOU • first-person player":"Cardboard cutout "+index,transform,position);
            var runner=root.gameObject.AddComponent<CardboardRunner>();
            runner.isPlayer=index==0;runner.personality=index;
            var pivot=Group("Whole-sheet sway pivot",root,new Vector3(0,-.16f,0));
            runner.puppet=pivot;
            var sheet=CardboardCutout.Create(pivot,index%6);
            if(index!=0)
            {
                sheet.gameObject.AddComponent<MeshCollider>().sharedMesh=sheet.GetComponent<MeshFilter>().sharedMesh;
                sheet.gameObject.AddComponent<CardboardDrawingSurface>();
            }
            return runner;
        }
        void Tree(Vector3 pos, float scale, float turn)
        {
            var tree = Group("Slot-together cardboard tree",transform,pos); tree.localScale = Vector3.one*scale;
            tree.localRotation = Quaternion.Euler(0,turn,0);
            Box("Trunk",tree,new Vector3(0,1,0),new Vector3(.24f,2.2f,.24f),Kraft);
            for (int j=0;j<2;j++)
            {
                var canopy = Group("Crossed foliage",tree,new Vector3(0,2.15f,0)); canopy.localRotation = Quaternion.Euler(0,j*90,0);
                Panel("Cut treetop",canopy,Vector3.zero,new[] {new Vector2(-1.1f,-.5f),new Vector2(1.1f,-.5f),new Vector2(.65f,.22f),new Vector2(.84f,.22f),new Vector2(.35f,.9f),new Vector2(.49f,.9f),new Vector2(0,1.6f),new Vector2(-.49f,.9f),new Vector2(-.35f,.9f),new Vector2(-.84f,.22f),new Vector2(-.65f,.22f)},.09f,j==0?DarkLeaf:Leaf);
                Stroke("Fold line",canopy,new Vector3(0,-.45f,.052f),new Vector3(0,1.5f,.052f),.025f,Leaf);
            }
            Box("Foot slot A",tree,new Vector3(0,-.1f,0),new Vector3(1,.1f,.18f),Kraft);
            Box("Foot slot B",tree,new Vector3(0,-.1f,0),new Vector3(.18f,.1f,1),Kraft);
        }

        void Sign(Vector3 pos, string text, float angle)
        {
            var sign = Group("Trail sign",transform,pos); sign.localRotation = Quaternion.Euler(0,angle,0);
            Box("Post",sign,new Vector3(0,.8f,0),new Vector3(.16f,1.8f,.16f),Kraft);
            Panel("Sign face",sign,new Vector3(0,1.65f,0),Rect(2.6f,.65f),.1f,Cream);
            Label(text,sign,new Vector3(0,1.65f,.07f),.085f);
        }
    }
}

