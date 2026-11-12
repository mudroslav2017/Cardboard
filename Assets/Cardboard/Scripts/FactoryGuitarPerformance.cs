using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(-70),DisallowMultipleComponent]
    public class FactoryGuitarPerformance : MonoBehaviour
    {
        [Min(1)] public float firstShotSeconds=6,secondShotSeconds=6;
        [Min(1)] public float soloSeconds=51;
        [Range(60,200)] public float beatsPerMinute=108;
        [Min(.1f)] public float emotionSeconds=.8f;
        public bool Active {get;private set;}
        public float Elapsed {get;private set;}
        public float SoloAt=>firstShotSeconds+secondShotSeconds;
        public bool Solo=>Active && Elapsed>=SoloAt;
        public Transform Performer=>actor;
        public Transform Guitar=>guitar;
        public FactoryFaceState Face=>face?face.State:FactoryFaceState.Normal;
        public Vector3 Center {get;private set;}
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public float FloorHeight {get;private set;}
        public int Shot=>Elapsed<firstShotSeconds?0:Elapsed<SoloAt?1:2+SoloShot(out _,out _);
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        CardboardFactoryGame game;
        FactoryCircleDance session;
        Transform stage,actor,model,guitar,guitarMesh,mouth;
        FactoryWorkerAppearance face;
        Camera lens;

        static readonly int[] SoloShotOrder={3,1,2,0,4,5};
        static readonly float[] SoloShotLengths={3.5f,3.5f,3.5f,3,3.5f,4};
        Vector3[] footPoints;
        void Awake(){game=GetComponent<CardboardFactoryGame>();}
        void Update()
        {
            var keys=Keyboard.current;
            if(keys!=null && keys.f6Key.wasPressedThisFrame){if(Active)Stop();else Begin();}
            else if(keys!=null && keys.f7Key.wasPressedThisFrame && Active)Restart();
            if(!Active)return;
            if(!session.Active){Stop(false);return;}
            Tick(Time.unscaledDeltaTime);
        }
        void Hide(Renderer renderer)
        {if(renderer && !hidden.ContainsKey(renderer)){hidden.Add(renderer,renderer.enabled);renderer.enabled=false;}}
        public bool Begin()
        {
            if(Active || !game || game.DanceActive)return false;
            var person=Resources.Load<GameObject>("CardboardCharacters/HumanNastya");
            var instrument=Resources.Load<GameObject>("GuitarPerformance/Guitar");
            if(!person || !instrument){Debug.LogError("Prepare guitar recording assets before filming.");return false;}
            session=game.circleDance;if(!session.BeginSession())return false;
            lens=Camera.main;
            Active=true;Elapsed=0;Center=game.transform.TransformPoint(new Vector3(CardboardFactoryWorld.WorkshopCenterX,0,0));
            FloorHeight=FindFloorHeight();Center=new Vector3(Center.x,FloorHeight,Center.z);
            foreach(var worker in game.crowd.runners)
                foreach(var renderer in worker.GetComponentsInChildren<Renderer>(true))Hide(renderer);
            foreach(var prop in game.GetComponentsInChildren<FactoryPhysicsProp>(true))
            {
                var p=prop.transform.position;p.y=Center.y;
                if(Vector3.Distance(p,Center)<2.6f)foreach(var renderer in prop.GetComponentsInChildren<Renderer>(true))Hide(renderer);
            }
            foreach(var sceneFilter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(sceneFilter.sharedMesh && (sceneFilter.sharedMesh.name.ToLowerInvariant().Contains("guitar") || sceneFilter.name.ToLowerInvariant().Contains("guitar")))Hide(sceneFilter.GetComponent<Renderer>());
            stage=new GameObject("Nastya guitar recording stage").transform;stage.SetParent(game.transform,false);stage.position=Center;
            actor=new GameObject("Nastya guitarist").transform;actor.SetParent(stage,false);
            model=Instantiate(person,actor,false).transform;face=model.GetComponent<FactoryWorkerAppearance>();
            face.surprised=face.suspicious;
            var originalMesh=model.GetComponent<MeshFilter>().sharedMesh;
            var supports=new List<Vector3>();
            foreach(var vertex in originalMesh.vertices)if(vertex.y<=originalMesh.bounds.min.y+.05f)supports.Add(vertex);
            footPoints=supports.ToArray();
            guitar=new GameObject("Moving guitar grip").transform;guitar.SetParent(actor,false);
            guitarMesh=Instantiate(instrument,guitar,false).transform;guitarMesh.localPosition=new Vector3(0,-.35f,0);
            mouth=CreateMouth(model,originalMesh.bounds.min.z-.012f);
            var strap=new GameObject("Guitar shoulder strap").AddComponent<LineRenderer>();strap.transform.SetParent(actor,false);
            strap.useWorldSpace=false;strap.positionCount=3;strap.SetPositions(new[]{new Vector3(.28f,1.43f,-.015f),new Vector3(-.23f,1.02f,.13f),new Vector3(-.33f,.87f,.22f)});
            strap.widthMultiplier=.045f;strap.sharedMaterial=Resources.Load<Material>("GuitarPerformance/Ink");strap.numCapVertices=2;
            StageLight("Warm guitar key",new Vector3(2.5f,3.8f,3),new Color(1,.85f,.61f),9);
            StageLight("Guitar rim",new Vector3(-2,3.6f,-2),new Color(.66f,.8f,1),7);
            ApplyPose();Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);return true;
        }
        static Transform CreateMouth(Transform parent,float depth)
        {
            var root=new GameObject("Open surprised mouth");root.transform.SetParent(parent,false);root.transform.localPosition=new Vector3(0,1.43f,depth);
            var mesh=new Mesh();var points=new Vector3[32];var triangles=new int[96];
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;points[i*2]=new Vector3(Mathf.Cos(a)*.058f,Mathf.Sin(a)*.085f,0);
                points[i*2+1]=new Vector3(Mathf.Cos(a)*.035f,Mathf.Sin(a)*.06f,0);
                int j=(i+1)%16,k=i*6;triangles[k]=i*2;triangles[k+1]=j*2;triangles[k+2]=i*2+1;triangles[k+3]=j*2;triangles[k+4]=j*2+1;triangles[k+5]=i*2+1;
            }
            mesh.vertices=points;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("GuitarPerformance/Ink");return root.transform;
        }
        void StageLight(string name,Vector3 position,Color color,float intensity)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(stage,false);light.transform.localPosition=position;
            light.transform.LookAt(Center+Vector3.up*1.15f);light.type=LightType.Spot;light.spotAngle=65;light.range=11;light.intensity=intensity;light.color=color;
        }
        public void Tick(float dt)
        {
            if(!Active || game.MenuActive || dt<=0)return;
            Elapsed=Mathf.Min(Elapsed+dt,SoloAt+soloSeconds);ApplyPose();
        }
        float FindFloorHeight()
        {
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
                if(renderer.name=="Main workshop floor surface")return renderer.bounds.max.y;
            foreach(var hit in Physics.RaycastAll(Center+Vector3.up*2,Vector3.down,6,1<<8,QueryTriggerInteraction.Ignore))
                if(hit.normal.y>.95f && hit.collider.bounds.size.x>6 && hit.collider.bounds.size.z>6)return hit.point.y+.0225f;
            return game.transform.TransformPoint(new Vector3(0,-.1475f,0)).y;
        }
        void ApplyPose()
        {
            float beat=Elapsed*beatsPerMinute/60*Mathf.PI*2;
            float solo=Elapsed-SoloAt;float energy=Mathf.SmoothStep(0,1,Mathf.Max(0,solo)/.45f);
            // Animate the intact cardboard sheet as one rigid object. Its imported mesh is never copied or edited.
            actor.localPosition=Vector3.zero;actor.localScale=Vector3.one;
            actor.localRotation=Quaternion.Euler(energy*(Mathf.Sin(beat*.5f)*6-2),Mathf.Sin(beat*.25f)*Mathf.Lerp(3,8,energy),Mathf.Sin(beat*.5f)*Mathf.Lerp(3,6,energy));
            float lowest=float.PositiveInfinity;
            foreach(var point in footPoints)lowest=Mathf.Min(lowest,model.TransformPoint(point).y);
            actor.position+=Vector3.up*(FloorHeight+.001f-lowest);
            guitar.localPosition=new Vector3(-.17f,.99f,.23f)+new Vector3(0,energy*Mathf.Sin(beat*4)*.008f,0);
            guitar.localRotation=Quaternion.Euler(energy*(Mathf.Sin(beat)*3+Mathf.Sin(beat*4)*1.5f),Mathf.Sin(beat*.5f)*energy*5,-38+Mathf.Sin(beat*.5f)*Mathf.Lerp(2,8,energy));
            var emotion=Solo && ((int)(Mathf.Max(0,solo)/emotionSeconds)%2==1)?FactoryFaceState.Surprised:FactoryFaceState.Angry;
            face.SetState(emotion);mouth.gameObject.SetActive(emotion==FactoryFaceState.Surprised);
            ApplyCamera();
        }
        int SoloShot(out float local,out float duration)
        {
            float t=Mathf.Repeat(Mathf.Max(0,Elapsed-SoloAt),21);var lengths=SoloShotLengths;
            for(int i=0;i<lengths.Length;i++)if(t<lengths[i]){local=t;duration=lengths[i];return SoloShotOrder[i];}else t-=lengths[i];
            local=0;duration=lengths[0];return SoloShotOrder[0];
        }
        void ApplyCamera()
        {
            Vector3 position,target;float fov=54,roll=0;bool shaking=false;
            if(Shot<2)
            {
                float angle,radius,height,targetHeight;
                if(Shot==0)
                {float t=Mathf.SmoothStep(0,1,Elapsed/firstShotSeconds);angle=Mathf.Lerp(-58,18,t);radius=Mathf.Lerp(4.8f,4.3f,t);height=1.65f;targetHeight=1.18f;}
                else
                {float t=Mathf.SmoothStep(0,1,(Elapsed-firstShotSeconds)/secondShotSeconds);angle=Mathf.Lerp(55,5,t);radius=Mathf.Lerp(4.5f,3.7f,t);height=Mathf.Lerp(1.8f,1.5f,t);targetHeight=1.22f;}
                float a=angle*Mathf.Deg2Rad;position=Center+new Vector3(Mathf.Sin(a)*radius,height,Mathf.Cos(a)*radius);target=Center+Vector3.up*targetHeight;
            }
            else
            {
                int shot=SoloShot(out float local,out float duration);float t=Mathf.SmoothStep(0,1,local/duration);
                switch(shot)
                {
                    case 0:
                        float enter=Mathf.SmoothStep(0,1,local/1.2f);float a=Mathf.Lerp(5,Mathf.Sin(local*.3f)*15,enter)*Mathf.Deg2Rad;
                        float radius=Mathf.Lerp(3.7f,3.65f,enter);position=Center+new Vector3(Mathf.Sin(a)*radius,Mathf.Lerp(1.5f,1.48f,enter),Mathf.Cos(a)*radius);target=Center+Vector3.up*1.22f;break;
                    case 1:
                        // A descending overhead arc stays below the workshop ceiling.
                        float topAngle=Mathf.Lerp(-35,35,t)*Mathf.Deg2Rad;
                        position=Center+new Vector3(Mathf.Sin(topAngle)*1.1f,Mathf.Lerp(4.4f,3.9f,t),Mathf.Cos(topAngle)*1.1f);target=Center+Vector3.up*1.1f;fov=65;roll=Mathf.Lerp(-6,6,t);break;
                    case 2:
                        position=Center+new Vector3(Mathf.Lerp(-.7f,.7f,t),.24f,2.3f);target=Center+Vector3.up*1.48f;fov=78;roll=Mathf.Lerp(5,-5,t);break;
                    case 3:
                        var faceTarget=model.TransformPoint(new Vector3(0,1.76f,0));
                        position=faceTarget+actor.forward*Mathf.Lerp(.82f,.68f,t)+actor.right*(Mathf.Sin(local*1.3f)*.07f)+Vector3.up*.035f;
                        target=faceTarget;fov=108;shaking=true;break;
                    case 4:
                        position=Center+new Vector3(Mathf.Lerp(.9f,-.45f,t),.88f,1.8f);target=actor.TransformPoint(new Vector3(-.15f,1.23f,.1f));fov=83;roll=Mathf.Sin(local*1.2f)*4;break;
                    default:
                        float wideAngle=Mathf.Lerp(-40,35,t)*Mathf.Deg2Rad;
                        position=Center+new Vector3(Mathf.Sin(wideAngle)*4,1.65f,Mathf.Cos(wideAngle)*4);target=Center+Vector3.up*1.15f;break;
                }
            }
            Quaternion rotation=Quaternion.LookRotation(target-position)*Quaternion.Euler(0,0,roll);
            if(shaking)
            {
                // Coherent handheld shake is continuous between frames and independent of frame rate.
                float t=Elapsed;Vector3 noise=new Vector3(Mathf.PerlinNoise(t*8.5f,3)-.5f,Mathf.PerlinNoise(7,t*10)-.5f,Mathf.PerlinNoise(t*7,13)-.5f)*2;
                position+=rotation*(noise*.022f);rotation*=Quaternion.Euler(noise.x*1.8f,noise.y*1.8f,noise.z*3.2f);
            }
            CameraPosition=position;CameraRotation=rotation;
            lens.transform.SetPositionAndRotation(CameraPosition,CameraRotation);lens.fieldOfView=fov;
        }
        public bool Restart(){if(!Active)return false;Elapsed=0;ApplyPose();return true;}
        public void Stop(bool restoreMenu=true)
        {
            if(!Active)return;Active=false;
            foreach(var entry in hidden)if(entry.Key)entry.Key.enabled=entry.Value;hidden.Clear();
            if(stage){stage.gameObject.SetActive(false);Destroy(stage.gameObject);}
            if(mouth){var mesh=mouth.GetComponent<MeshFilter>().sharedMesh;if(mesh)Destroy(mesh);}
            if(session && session.Active)session.Stop(restoreMenu);
            stage=actor=model=guitar=guitarMesh=mouth=null;footPoints=null;
        }
        void OnDisable(){Stop(false);}
    }
}