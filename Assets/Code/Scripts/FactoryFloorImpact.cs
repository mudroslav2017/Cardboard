using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(Rigidbody))]
    public class FactoryFloorImpact : MonoBehaviour
    {
        FactoryPhysicsProp prop;
        float nextImpact;
        void Awake(){prop=GetComponent<FactoryPhysicsProp>();}
        void OnCollisionEnter(Collision collision)
        {
            var audio=FactoryFloorAudio.Instance;
            if(!audio || Time.time<nextImpact || (prop && prop.IsHeld) || !FactoryFloorAudio.IsFloor(collision.collider))return;
            float speed=0;Vector3 point=Vector3.zero;
            for(int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);if(contact.normal.y<.65f)continue;
                float closing=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal));
                if(closing>speed){speed=closing;point=contact.point;}
            }
            if(speed>=.9f && audio.Play(point,speed))nextImpact=Time.time+.35f;
        }
    }
}
