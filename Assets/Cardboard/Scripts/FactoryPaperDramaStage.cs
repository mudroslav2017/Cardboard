using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Cardboard
{
    public partial class FactoryPaperDrama
    {
        sealed class Actor
        {
            public Transform root,model;public Vector3[] vertices;public FactoryWorkerAppearance face;public Vector3 leftContact,rightContact;
            public Vector3 Contact(bool right)=>root.TransformPoint(right?rightContact:leftContact);
            public Vector3 Head=>model.TransformPoint(new Vector3(0,1.76f,0));
            public Vector3 Mouth=>model.TransformPoint(new Vector3(0,1.54f,model.GetComponent<MeshFilter>().sharedMesh.bounds.min.z));
        }
        Actor hero,wife,boss;Transform room,door,bouquet,badge,rainRoot;
        TextMesh heroLine,questionLine,bossLine,finalLine,roofLine;
        readonly List<LineRenderer> speedLines=new List<LineRenderer>();Transform[] hearts;
        Bounds homeBounds;Vector3 roof,landing,roomOrigin,roofFallOrigin;float ground;
        readonly List<LineRenderer> rainLines=new List<LineRenderer>();LineRenderer lightning;
        Vector3[] rainSeeds;AudioSource effects,rainAudio;
        Transform Node(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        Transform Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.name=name;t.SetParent(parent,false);t.localPosition=position;t.localScale=size;
            var collider=t.GetComponent<Collider>();if(collider)Destroy(collider);t.GetComponent<Renderer>().sharedMaterial=material;return t;
        }
        Transform Imported(GameObject prefab,Transform parent,Vector3 position)
        {var t=Instantiate(prefab,parent,false).transform;t.localPosition=position;return t;}
        Actor Person(string resource,string name)
        {
            var person=new Actor();person.root=Node(name,stage,Vector3.zero);
            person.model=Instantiate(Resources.Load<GameObject>("CardboardCharacters/"+resource),person.root,false).transform;
            person.vertices=person.model.GetComponent<MeshFilter>().sharedMesh.vertices;person.face=person.model.GetComponent<FactoryWorkerAppearance>();person.face.SetState(FactoryFaceState.Normal);
            float left=float.NegativeInfinity,right=float.NegativeInfinity;
            foreach(var vertex in person.vertices){var p=person.model.localRotation*vertex;if(p.y<1.45f || p.y>1.95f)continue;float l=-p.x-.5f*Mathf.Abs(p.y-1.7f)+p.z*.01f,r=p.x-.5f*Mathf.Abs(p.y-1.7f)+p.z*.01f;if(l>left){left=l;person.leftContact=p;}if(r>right){right=r;person.rightContact=p;}}
            return person;
        }
        void PersonPose(Actor actor,Vector3 position,Quaternion rotation,bool planted=false,float surface=0)
        {
            actor.root.SetPositionAndRotation(position,rotation);
            if(planted){float lowest=float.PositiveInfinity;foreach(var v in actor.vertices)lowest=Mathf.Min(lowest,actor.model.TransformPoint(v).y);actor.root.position+=Vector3.up*(surface+.002f-lowest);}
        }
        Transform Shape(string name,Transform parent,Vector2[] outline,float depth,Material material)
        {
            var t=Node(name,parent,Vector3.zero);var mesh=new Mesh();var vertices=new Vector3[outline.Length+1];vertices[0]=Vector3.zero;
            for(int i=0;i<outline.Length;i++)vertices[i+1]=new Vector3(outline[i].x,outline[i].y,depth);
            var triangles=new int[outline.Length*3];for(int i=0;i<outline.Length;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%outline.Length+1;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;
        }
        TextMesh Text(string name,string text,Transform parent,Vector3 position,float size,Color color)
        {
            var t=Node(name,parent,position);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.font=CardboardTypography.DefaultFont;label.fontSize=80;label.characterSize=size*.35f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;
            label.GetComponent<Renderer>().sharedMaterial=label.font.material;return label;
        }
        void GroundPlane()
        {
            var t=Node("Street • same tiled floor as factory",stage,new Vector3(homeBounds.center.x,ground,homeBounds.center.z));var mesh=new Mesh();
            var vertices=new[]{new Vector3(-75,0,-75),new Vector3(-75,0,75),new Vector3(75,0,75),new Vector3(75,0,-75)};
            var uv=new Vector2[4];for(int i=0;i<4;i++){var p=t.TransformPoint(vertices[i]);uv[i]=new Vector2(p.x/4,p.z/4);}
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=config.floor;
        }
        void FindHome()
        {
            MeshFilter house=null;
            foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(filter.name.StartsWith("house_01",StringComparison.OrdinalIgnoreCase) && !filter.transform.IsChildOf(stage)){house=filter;break;}
            if(!house)throw new InvalidOperationException("The placed house_01 is missing from the scene.");
            ExistingHouse=house.GetComponent<Renderer>();homeBounds=ExistingHouse.bounds;ground=homeBounds.min.y;
            float x=homeBounds.max.x-.85f,z=homeBounds.center.z;
            float height=RoofSurfaceAt(house,x,z);if(float.IsNegativeInfinity(height))height=homeBounds.max.y-.6f;
            roof=new Vector3(x,height,z);landing=new Vector3(homeBounds.max.x+4.8f,ground,homeBounds.center.z+.5f);
            GlideStart=roof+new Vector3(3.3f,-2,0);GlideEnd=roof+new Vector3(3.3f,-5.6f,0);
        }
        static float RoofSurfaceAt(MeshFilter filter,float x,float z)
        {
            var mesh=filter.sharedMesh;var v=mesh.vertices;var triangles=mesh.triangles;float result=float.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=filter.transform.TransformPoint(v[triangles[i]]);var b=filter.transform.TransformPoint(v[triangles[i+1]]);var c=filter.transform.TransformPoint(v[triangles[i+2]]);
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.00001f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det,w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;
                if(u>=-.001f && w>=-.001f && u+w<=1.001f)result=Mathf.Max(result,u*a.y+w*b.y+(1-u-w)*c.y);
            }
            return result;
        }
        void BuildStage()
        {
            FindHome();
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!renderer.name.StartsWith("house_01",StringComparison.OrdinalIgnoreCase) && !renderer.name.StartsWith("house_02",StringComparison.OrdinalIgnoreCase))session.Hide(renderer);
            GroundPlane();roomOrigin=new Vector3(homeBounds.center.x+60,ground,homeBounds.center.z-20);room=Node("Home interior • cutaway",stage,roomOrigin);
            Box("Back cardboard wall",room,new Vector3(0,2.1f,-2.2f),new Vector3(7.1f,4.2f,.15f),config.paper);
            Box("Entry wall left",room,new Vector3(-1.725f,2.1f,5.5f),new Vector3(3.75f,4.2f,.15f),config.paper);
            Box("Entry wall right",room,new Vector3(2.575f,2.1f,5.5f),new Vector3(2.05f,4.2f,.15f),config.paper);
            Box("Door lintel",room,new Vector3(.85f,3.42f,5.5f),new Vector3(1.4f,1.56f,.17f),config.cream);
            Box("Left cardboard wall",room,new Vector3(-3.5f,2.1f,1.65f),new Vector3(.16f,4.2f,7.8f),config.paper);
            Box("Right cardboard wall",room,new Vector3(3.5f,2.1f,1.65f),new Vector3(.16f,4.2f,7.8f),config.paper);
            Box("Interior ceiling",room,new Vector3(0,4.22f,1.65f),new Vector3(7.2f,.1f,7.8f),config.cream);
            Box("Home skirting board",room,new Vector3(0,.08f,-2.08f),new Vector3(7,.12f,.07f),config.cream);
            door=Node("Door hinge",room,new Vector3(.15f,0,5.45f));Box("Opening cardboard door",door,new Vector3(.675f,1.32f,0),new Vector3(1.35f,2.64f,.1f),config.cream);
            Box("Door handle",door,new Vector3(1.15f,1.12f,-.09f),new Vector3(.13f,.05f,.06f),config.ink);
            Imported(config.sofa,room,new Vector3(.85f,0,-.9f));
            hero=Person("HumanNastya","Nastya • husband");wife=Person("HumanLera","Lera • wife");boss=Person("HumanKolya","Kolya • boss");
            boss.face.normal=config.boss;boss.face.SetState(FactoryFaceState.Angry);boss.face.SetState(FactoryFaceState.Normal);
            bouquet=Imported(config.flowers,stage,Vector3.zero);
            badge=Imported(config.badge,boss.root,new Vector3(.23f,1.06f,.095f));
            heroLine=Text("Husband dialogue","Любимая,\nя дома!",stage,Vector3.zero,.055f,new Color(1,.95f,.82f));
            questionLine=Text("Husband recognition dialogue","Хуанг?",stage,Vector3.zero,.055f,new Color(1,.95f,.82f));
            bossLine=Text("Boss dialogue","Ты завтра\nне опаздывай.",stage,Vector3.zero,.05f,new Color(1,.95f,.82f));
            finalLine=Text("Final dialogue","Почему я такой\nнеудачник",stage,Vector3.zero,.055f,new Color(1,.95f,.82f));
            roofLine=Text("Roof dialogue","Хуанг,\nкак ты мог",stage,Vector3.zero,.048f,new Color(1,.95f,.82f));
            hearts=new Transform[2];for(int i=0;i<hearts.Length;i++)
                hearts[i]=Shape("Paper kiss heart "+i,stage,new[]{new Vector2(0,-.12f),new Vector2(-.14f,.02f),new Vector2(-.12f,.12f),new Vector2(-.04f,.14f),new Vector2(0,.08f),new Vector2(.06f,.14f),new Vector2(.14f,.1f),new Vector2(.15f,.02f)},0,config.heart);
            var city=Node("City blocks",stage,Vector3.zero);
            for(int row=-4;row<=4;row++)for(int col=-4;col<=4;col++)
            {
                if((row==0 || row==1) && col>=0 && col<=3)continue;
                float x=homeBounds.center.x+col*8.5f+(row%2==0?0:1.2f),z=homeBounds.center.z+row*14;
                var building=Imported((row+col)%2==0?config.house1:config.house2,city,new Vector3(x,ground,z));
                building.localScale=Vector3.one*(.88f+((row+4)*3+(col+4)*2)%5*.12f);
                building.localRotation=Quaternion.Euler(0,(row+col)%2==0?0:180,0);
            }
            foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(filter.name.StartsWith("house_02",StringComparison.OrdinalIgnoreCase) && !filter.transform.IsChildOf(stage))
                {var b=filter.GetComponent<Renderer>().bounds;if(b.min.y>ground+.1f)Box("Foundation below placed house 2",stage,new Vector3(b.center.x,(ground+b.min.y)*.5f,b.center.z),new Vector3(b.size.x,b.min.y-ground,b.size.z),config.paper);}
            secondCamera=Node("Second panel camera",stage,Vector3.zero).gameObject.AddComponent<Camera>();secondCamera.depth=lens.depth+1;secondCamera.nearClipPlane=.04f;secondCamera.farClipPlane=250;secondCamera.clearFlags=CameraClearFlags.SolidColor;secondCamera.backgroundColor=new Color(.13f,.1f,.07f);secondCamera.cullingMask=lens.cullingMask;secondCamera.enabled=false;secondCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            moon=Light("City overcast light",LightType.Directional,Vector3.zero,new Color(.75f,.83f,1),.9f,100);moon.transform.rotation=Quaternion.Euler(48,-35,0);moon.shadows=LightShadows.Soft;
            interiorKey=Light("Warm home light",LightType.Point,roomOrigin+new Vector3(.4f,2.7f,.5f),new Color(1,.81f,.58f),9,12);
            streetKey=Light("Hero film softbox",LightType.Point,roof+new Vector3(5,3,5),new Color(.73f,.84f,1),12,24);
            underlight=Light("White light from under face",LightType.Point,Vector3.zero,Color.white,0,4);
            flash=Light("Lightning flash",LightType.Directional,Vector3.zero,new Color(.8f,.9f,1),0,100);flash.transform.rotation=Quaternion.Euler(75,20,0);
            var volume=Node("Brief cold blue shock grade",stage,Vector3.zero).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=200;volume.weight=0;
            coldProfile=ScriptableObject.CreateInstance<VolumeProfile>();var grade=coldProfile.Add<ColorAdjustments>(true);grade.colorFilter.Override(new Color(.38f,.62f,1));grade.postExposure.Override(-.35f);grade.contrast.Override(22);grade.saturation.Override(-25);volume.profile=coldProfile;cold=volume;
            foreach(var t in hero.root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=14;
            foreach(var t in wife.root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=16;
            foreach(var t in boss.root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=15;
            BuildAccents();BuildWeather();
        }
        Light Light(string name,LightType type,Vector3 position,Color color,float intensity,float range)
        {var light=Node(name,stage,Vector3.zero).gameObject.AddComponent<Light>();light.transform.position=position;light.type=type;light.color=color;light.intensity=intensity;light.range=range;return light;}
        void BuildAccents()
        {
            redlight=Light("Red boss recognition light",LightType.Point,Vector3.zero,Color.red,0,4);
            badgeLight=Light("Green badge sweep light",LightType.Spot,Vector3.zero,new Color(.08f,1,.16f),0,.65f);badgeLight.spotAngle=30;
            glintMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));glintMaterial.color=new Color(.03f,1,.08f);glintMaterial.EnableKeyword("_EMISSION");glintMaterial.SetColor("_EmissionColor",new Color(.1f,4,.25f));CardboardSurface.Configure(glintMaterial);
            badgeGlint=Box("Green reflection across badge",badge,Vector3.zero,new Vector3(.018f,.18f,.003f),glintMaterial);
            var bloom=Node("Badge emission bloom",stage,Vector3.zero).gameObject.AddComponent<Volume>();bloom.isGlobal=true;bloom.priority=201;bloomProfile=ScriptableObject.CreateInstance<VolumeProfile>();var settings=bloomProfile.Add<Bloom>(true);settings.threshold.Override(.8f);settings.intensity.Override(.65f);settings.scatter.Override(.55f);bloom.profile=bloomProfile;
            fadeRoot=Node("Film fade to black",stage,Vector3.zero);var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.bounds=new Bounds(Vector3.zero,Vector3.one*20);ownedMeshes.Add(mesh);
            fadeRoot.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;fadeRenderer=fadeRoot.gameObject.AddComponent<MeshRenderer>();fadeMaterial=new Material(Resources.Load<Shader>("PaperDrama/FilmFade"));fadeRenderer.sharedMaterial=fadeMaterial;fadeRenderer.shadowCastingMode=ShadowCastingMode.Off;fadeRenderer.receiveShadows=false;
            for(int i=0;i<26;i++){var line=Node("Fast fall speed streak",stage,Vector3.zero).gameObject.AddComponent<LineRenderer>();line.positionCount=2;line.widthMultiplier=.022f;line.sharedMaterial=config.rain;line.shadowCastingMode=ShadowCastingMode.Off;speedLines.Add(line);}
        }
        void BuildWeather()
        {
            rainRoot=Node("Rain and gusts • seekable",stage,Vector3.zero);rainSeeds=new Vector3[380];var random=new System.Random(1964);
            for(int i=0;i<rainSeeds.Length;i++)
            {
                rainSeeds[i]=new Vector3((float)random.NextDouble()*28-14,(float)random.NextDouble()*22,(float)random.NextDouble()*28-14);
                var line=Node("Rain streak",rainRoot,Vector3.zero).gameObject.AddComponent<LineRenderer>();line.sharedMaterial=config.rain;line.widthMultiplier=.018f;line.positionCount=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;rainLines.Add(line);
            }
            lightning=Node("Paper lightning bolt",rainRoot,Vector3.zero).gameObject.AddComponent<LineRenderer>();lightning.sharedMaterial=config.cream;lightning.widthMultiplier=.09f;lightning.positionCount=6;
            var center=new Vector3(homeBounds.center.x,ground,homeBounds.center.z);
            lightning.SetPositions(new[]{center+new Vector3(-10,19,-2),center+new Vector3(-11,16,-2),center+new Vector3(-9,16,-2),center+new Vector3(-12,13,-2),center+new Vector3(-11,13,-2),center+new Vector3(-13,10,-2)});
        }
    }
}