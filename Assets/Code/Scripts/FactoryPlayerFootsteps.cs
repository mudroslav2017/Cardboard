using UnityEngine;

namespace Cardboard
{
    public class FactoryPlayerFootsteps : MonoBehaviour
    {
        public AudioSource speaker;
        public AudioClip[] clips;
        public int StepsPlayed {get;private set;}
        CardboardRunner runner;
        Vector3 previous;
        float distance;
        int lastClip=-1;
        void Awake(){runner=GetComponent<CardboardRunner>();previous=transform.position;}
        void OnEnable(){previous=transform.position;distance=0;}
        void LateUpdate(){TickSteps();}
        public void TickSteps()
        {
            Vector3 current=transform.position,delta=current-previous;previous=current;delta.y=0;
            var game=CardboardFactoryGame.Instance;
            if(!runner || !runner.isPlayer || !speaker || clips==null || clips.Length==0 ||
                runner.JumpHeight>.02f || (game && ((game.PlayerBlocked && !(game.ending && game.ending.Running)) || game.CaptureActive)) || delta.sqrMagnitude>1)
            {distance=0;return;}
            float travel=delta.magnitude;
            if(travel<.001f)return;
            distance+=travel;
            float stride=runner.speed>4?1.8f:1.45f;
            if(distance<stride)return;
            distance%=stride;
            int next=Random.Range(0,clips.Length>1 && lastClip>=0?clips.Length-1:clips.Length);
            if(clips.Length>1 && lastClip>=0 && next>=lastClip)next++;
            lastClip=next;
            speaker.pitch=Random.Range(.96f,1.04f);speaker.PlayOneShot(clips[next]);StepsPlayed++;
        }
        void OnDisable(){if(speaker)speaker.Stop();distance=0;}
    }
}
