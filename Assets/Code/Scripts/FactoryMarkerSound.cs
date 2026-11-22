using UnityEngine;

namespace Cardboard
{
    public class FactoryMarkerSound : MonoBehaviour
    {
        public AudioSource speaker;
        public AudioClip clip;
        CardboardDrawingSurface lastSurface;
        Vector3 lastPoint;
        float audibleUntil=-1;
        public void Stroke(CardboardDrawingSurface surface,Vector3 point)
        {
            if(!speaker || !clip)return;
            bool moving=surface!=lastSurface || (point-lastPoint).sqrMagnitude>.0000001f;
            lastPoint=point;lastSurface=surface;if(!moving)return;
            speaker.transform.position=point;audibleUntil=Time.time+.10f;
            if(!speaker.isPlaying){speaker.clip=clip;speaker.Play();}
        }
        public void EndStroke(){lastSurface=null;audibleUntil=-1;}
        void Update()
        {
            if(!speaker)return;
            var game=CardboardFactoryGame.Instance;
            bool active=Time.time<audibleUntil && CardboardMarker.DrawingMode && (!game || (!game.MenuActive && !game.Ended && !game.PlayerBlocked));
            speaker.volume=Mathf.MoveTowards(speaker.volume,active?.28f:0,Time.deltaTime*(active?4:2));
            if(!active && speaker.volume<=0)speaker.Stop();
        }
        void OnDisable(){EndStroke();if(speaker){speaker.Stop();speaker.volume=0;}}
    }
}
