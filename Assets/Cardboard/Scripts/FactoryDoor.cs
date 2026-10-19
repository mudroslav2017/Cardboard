using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    public class FactoryDoor : MonoBehaviour
    {
        public Transform hinge,passage;
        public bool workersMayOpen;
        public float angle=102,duration=.65f;
        public bool IsOpen {get;private set;}
        public float Openness {get;private set;}
        static readonly List<FactoryDoor> active=new List<FactoryDoor>();
        public string Prompt=>IsOpen?"E - CLOSE DOOR":"E - OPEN DOOR";
        void OnEnable(){if(!active.Contains(this))active.Add(this);}
        void OnDisable(){active.Remove(this);}
        void Update()
        {
            var game=CardboardFactoryGame.Instance;
            if(game && game.MenuActive)return;
            Tick(Time.deltaTime);
        }
        public static bool IsLeaf(Collider collider)
        {var target=collider.GetComponent<FactoryInteractable>();return target && target.action==FactoryAction.Door;}
        public void Toggle(){var game=CardboardFactoryGame.Instance;if(game && game.exitDoor==this && !game.ReadyToLeave){game.Toast("Exit locked. Complete six repairs first.");return;}SetOpen(!IsOpen);}
        public void SetOpen(bool open,bool immediate=false)
        {
            IsOpen=open;
            if(immediate){Openness=open?1:0;Pose();}
        }
        public void Tick(float dt)
        {
            Openness=Mathf.MoveTowards(Openness,IsOpen?1:0,Mathf.Max(0,dt)/Mathf.Max(.05f,duration));
            Pose();
        }
        void Pose()
        {if(hinge)hinge.localRotation=Quaternion.Euler(0,angle*Mathf.SmoothStep(0,1,Openness),0);}
        public static bool ParkedLeafBlocks(Vector3 bottom,Vector3 top,float radius)
        {
            foreach(var door in active)
            {
                if(!door || !door.hinge || !door.passage)continue;
                var leaf=door.hinge.GetComponentInChildren<BoxCollider>();if(!leaf)continue;
                var local=leaf.transform;
                var pose=door.passage.localToWorldMatrix*
                    Matrix4x4.TRS(door.hinge.localPosition,Quaternion.Euler(0,door.angle,0),door.hinge.localScale)*
                    Matrix4x4.TRS(local.localPosition,local.localRotation,local.localScale);
                var center=pose.MultiplyPoint3x4(leaf.center);
                var x=pose.MultiplyVector(Vector3.right);var y=pose.MultiplyVector(Vector3.up);var z=pose.MultiplyVector(Vector3.forward);
                var half=Vector3.Scale(leaf.size,new Vector3(x.magnitude,y.magnitude,z.magnitude))*.5f;
                if(bottom.y-radius>center.y+half.y || top.y+radius<center.y-half.y)continue;
                var offset=bottom-center;
                float dx=Mathf.Max(0,Mathf.Abs(Vector3.Dot(offset,x.normalized))-half.x);
                float dz=Mathf.Max(0,Mathf.Abs(Vector3.Dot(offset,z.normalized))-half.z);
                if(dx*dx+dz*dz<radius*radius)return true;
            }
            return false;
        }
        public static void OpenForWorker(Vector3 position,Vector3 wish)
        {
            if(wish.sqrMagnitude<.01f)return;
            foreach(var door in active)
            {
                if(!door || !door.workersMayOpen || door.IsOpen || !door.passage)continue;
                Vector3 toward=door.passage.position-position;toward.y=0;
                if(toward.sqrMagnitude<2.3f*2.3f && Vector3.Dot(toward,wish)>.05f)door.SetOpen(true);
            }
        }
    }
}
