using UnityEngine;

namespace Cardboard
{
    public class FactoryPrinterSignal : MonoBehaviour
    {
        public FactoryWorkstation station;
        public Renderer housing,lamp;
        public Light beacon;
        public AudioSource speaker;
        public AudioClip ring;
        public bool Signalling {get;private set;}
        AudioClip tone;
        MaterialPropertyBlock properties;
        void Awake()
        {
            if(!speaker)speaker=gameObject.GetComponent<AudioSource>();
            if(!speaker)speaker=gameObject.AddComponent<AudioSource>();
            const int rate=22050;var samples=new float[rate*3/5];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                if(t<.16f)samples[i]=Mathf.Sin(2*Mathf.PI*880*t)*.22f*Mathf.Min(1,t/.01f,(.16f-t)/.015f);
            }
            tone=AudioClip.Create("Work order printer • warning beep",samples.Length,1,rate,false);tone.SetData(samples,0);
            speaker.clip=ring?ring:tone;speaker.loop=true;speaker.playOnAwake=false;speaker.volume=.35f;
            speaker.spatialBlend=1;speaker.minDistance=2;speaker.maxDistance=17;speaker.rolloffMode=AudioRolloffMode.Linear;
            SetSignal(false);
        }
        void Update(){SetSignal(station && station.IsPrinting && CardboardFactoryGame.Instance && !CardboardFactoryGame.Instance.Ended && !CardboardFactoryGame.Instance.MenuActive);}
        public void SetSignal(bool active)
        {
            Signalling=active;
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetColor("_EmissionColor",active?new Color(3.2f,.025f,.012f):Color.black);
            if(housing)housing.SetPropertyBlock(properties);
            properties.SetColor("_BaseColor",active?new Color(1,.035f,.012f):new Color(.18f,.025f,.018f));
            if(lamp)lamp.SetPropertyBlock(properties);
            if(beacon){beacon.enabled=active;beacon.color=Color.red;beacon.intensity=2.2f+Mathf.Sin(Time.time*15)*.25f;}
            if(speaker){if(active && !speaker.isPlaying)speaker.Play();else if(!active && speaker.isPlaying)speaker.Stop();}
        }
        void OnDisable(){SetSignal(false);}
        void OnDestroy(){if(tone)Destroy(tone);}
    }
}
