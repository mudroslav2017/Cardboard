using UnityEngine;

namespace Cardboard
{
    public class FactoryMechanismAudio : MonoBehaviour
    {
        public AudioSource[] speakers;
        public float[] volumes;
        void Awake()
        {
            for(int i=0;i<speakers.Length;i++)
            {
                var source=speakers[i];if(!source || !source.clip)continue;
                source.volume=0;source.time=Mathf.Repeat(i*1.91f,source.clip.length);
            }
        }
        void Update()
        {
            var game=CardboardFactoryGame.Instance;
            bool active=game && !game.MenuActive && !game.Ended;
            for(int i=0;i<speakers.Length;i++)
            {
                var source=speakers[i];if(!source || !source.clip)continue;
                float target=active?volumes[i]:0;
                if(active && !source.isPlaying)source.Play();
                source.volume=Mathf.MoveTowards(source.volume,target,Time.deltaTime*.45f);
                if(!active && source.volume<=0)source.Stop();
            }
        }
        void OnDisable(){foreach(var source in speakers)if(source)source.Stop();}
    }
}
