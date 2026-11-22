using UnityEngine;

namespace Cardboard
{
    public class FactoryWorkbenchSound : MonoBehaviour
    {
        public AudioClip leverClip;
        public AudioClip[] cuttingClips;
        public AudioSource cuttingFirst,cuttingSecond,leverSpeaker;
        public const float CuttingOffset=.12f;
        float cutUntil=-1,secondAt;
        void Awake()
        {if(cuttingFirst)cuttingFirst.volume=0;if(cuttingSecond)cuttingSecond.volume=0;}
        // Legacy workbenches can call this without restoring synthetic clunks.
        public void Clunk(bool weight){}
        public void Lever(Vector3 position)
        {
            if(!leverSpeaker || !leverClip)return;
            leverSpeaker.transform.position=position;leverSpeaker.PlayOneShot(leverClip);
        }
        public void Cut(float progress)
        {
            if(progress<=0 || !cuttingFirst || !cuttingSecond || cuttingClips==null || cuttingClips.Length<2)return;
            if(Time.time>=cutUntil)
            {
                cuttingFirst.clip=cuttingClips[0];cuttingSecond.clip=cuttingClips[1];
                cuttingFirst.volume=cuttingSecond.volume=0;cuttingSecond.Stop();cuttingFirst.Play();secondAt=Time.time+CuttingOffset;
            }
            cutUntil=Time.time+.18f;
        }
        void Update()
        {
            var game=CardboardFactoryGame.Instance;
            bool active=Time.time<cutUntil && game && !game.MenuActive && !game.Ended && !game.CaptureActive && !game.IntroBusy;
            Layer(cuttingFirst,active,.30f);
            Layer(cuttingSecond,active && Time.time>=secondAt,.25f);
        }
        static void Layer(AudioSource speaker,bool active,float volume)
        {
            if(!speaker)return;
            if(active && !speaker.isPlaying)speaker.Play();
            speaker.volume=Mathf.MoveTowards(speaker.volume,active?volume:0,Time.deltaTime*(active?6:3));
            if(!active && speaker.volume<=0)speaker.Stop();
        }
        public void StopCutting()
        {
            cutUntil=-1;
            if(cuttingFirst){cuttingFirst.Stop();cuttingFirst.volume=0;}
            if(cuttingSecond){cuttingSecond.Stop();cuttingSecond.volume=0;}
        }
        void OnDisable(){StopCutting();if(leverSpeaker)leverSpeaker.Stop();}
    }
}
