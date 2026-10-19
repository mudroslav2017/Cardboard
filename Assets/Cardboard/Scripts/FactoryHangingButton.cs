using UnityEngine;

namespace Cardboard
{
    public enum FactoryMenuAction { Settings, Play, Exit, Back }
    public class FactoryHangingButton : MonoBehaviour
    {
        public FactoryMenuAction action;
        public BoxCollider hit;
        public Vector3 home;
        public float scale=1;
        public bool Hovered {get;private set;}
        public float PressTravel {get;private set;}
        Vector3 offset,velocity,tilt,tiltVelocity;
        float pressAt=-10;
        Vector2 attraction;
        public void Hover(bool value,Vector3 pointer)
        {
            Hovered=value;
            Vector3 center=transform.parent.InverseTransformPoint(hit.transform.position);
            attraction=value?Vector2.ClampMagnitude((Vector2)(pointer-center),1):Vector2.zero;
        }
        public void Press() {pressAt=Time.unscaledTime;}
        public void Pose(float dt,float lift,float visibility)
        {
            Vector3 target=new Vector3(attraction.x*.13f,attraction.y*.10f,Hovered?-.06f:0);
            offset=Vector3.SmoothDamp(offset,target,ref velocity,.18f,10,dt);
            Vector3 targetTilt=new Vector3(-attraction.y*5,attraction.x*7,-attraction.x*3);
            tilt=Vector3.SmoothDamp(tilt,targetTilt,ref tiltVelocity,.20f,Mathf.Infinity,dt);
            float age=Time.unscaledTime-pressAt;
            PressTravel=age<.55f?Mathf.Sin(Mathf.Clamp01(age/.55f)*Mathf.PI):0;
            float idle=Mathf.Sin(Time.unscaledTime*1.2f+(int)action*1.8f)*.75f;
            transform.localScale=Vector3.one*scale;
            transform.localPosition=home+offset+Vector3.up*lift+Vector3.forward*(PressTravel*.28f);
            transform.localRotation=Quaternion.Euler(tilt+new Vector3(-PressTravel*28,0,idle));
            if(hit) hit.enabled=visibility>.99f;
        }
        public void ResetMotion() {offset=velocity=tilt=tiltVelocity=Vector3.zero;pressAt=-10;attraction=Vector2.zero;Hovered=false;PressTravel=0;}
    }
}
