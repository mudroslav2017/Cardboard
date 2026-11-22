using UnityEngine;

namespace Cardboard
{
    public class FactoryFloorAudio : MonoBehaviour
    {
        public static FactoryFloorAudio Instance {get;private set;}
        public AudioClip clip;
        public AudioSource[] Speakers {get;private set;}
        float readyAt;
        int next;
        void Awake()
        {
            Instance=this;readyAt=Time.time+.5f;if(clip)clip.LoadAudioData();
            Speakers=new AudioSource[6];
            for(int i=0;i<Speakers.Length;i++)
            {
                var node=new GameObject("Floor impact sound "+(i+1));node.transform.SetParent(transform,false);
                var source=node.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.spatialBlend=1;
                source.minDistance=1;source.maxDistance=12;source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0;Speakers[i]=source;
            }
            foreach(var body in GetComponentsInChildren<Rigidbody>(true))
                if(!body.GetComponent<FactoryFloorImpact>())body.gameObject.AddComponent<FactoryFloorImpact>();
        }
        public static bool IsFloor(Collider shape)
        {
            if(!shape || shape.attachedRigidbody)return false;
            return shape.name=="Main workshop floor" || shape.name=="Main workshop floor surface" || shape.name=="Canteen floor" ||
                shape.name=="Canteen floor surface" || shape.name=="Canteen doorway floor" || shape.name=="Corridor floor";
        }
        public bool Play(Vector3 point,float speed)
        {
            var game=CardboardFactoryGame.Instance;
            if(!clip || Time.time<readyAt || (game && (game.MenuActive || game.Ended)))return false;
            var source=Speakers[next];next=(next+1)%Speakers.Length;source.transform.position=point;
            source.volume=Mathf.Lerp(.14f,.38f,Mathf.InverseLerp(.9f,8,speed));source.pitch=Random.Range(.96f,1.04f);source.Play();return true;
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
