using UnityEngine;
namespace Cardboard
{
    public partial class FactoryPaperDrama
    {
        AudioSource voice;float voiceAt,voiceUntil;
        public int CueCount {get;private set;}
        void BuildSound()
        {
            effects=Speaker("Paper foley",.65f);voice=Speaker("Dialogue • game worker remarks",.55f);rainAudio=Speaker("Rain ambience",.2f);rainAudio.clip=config.rainLoop;rainAudio.loop=true;
        }
        AudioSource Speaker(string name,float volume)
        {var speaker=Node(name,stage,Vector3.zero).gameObject.AddComponent<AudioSource>();speaker.playOnAwake=false;speaker.ignoreListenerPause=true;speaker.spatialBlend=0;speaker.volume=volume;return speaker;}
        void PaperVoice(float time,float until,int variant)
        {
            voice.Stop();if(config.dialogueVoices==null || config.dialogueVoices.Length==0)return;
            voice.clip=config.dialogueVoices[variant%config.dialogueVoices.Length];voice.time=0;voice.volume=0;voice.pitch=1;voice.loop=true;
            voiceAt=time;voiceUntil=until;voice.Play();CueCount++;
        }
        void Cue(AudioClip clip,float volume=1,float pitch=1)
        {if(clip){effects.pitch=pitch;effects.PlayOneShot(clip,volume);CueCount++;}}
        void PlayCrossedCues(float before,float after)
        {
            var starts=new[]{.85f,SplitEnd+.1f,BossTalkAt+.1f,RoofAt+.2f,RoofAt+2.2f,FarewellAt+.1f,LandingAt+2.2f};
            var ends=new[]{GreetingEnd,RecognitionAt,RoofAt,RoofAt+2.1f,FarewellAt,FallAt-.15f,FadeAt};
            for(int i=0;i<starts.Length;i++)if(before<starts[i] && after>=starts[i])PaperVoice(starts[i],ends[i],i);
            if(before<ShockAt && after>=ShockAt)Cue(config.plop,.2f);
            foreach(float time in new[]{RoofAt+1.1f,RoofAt+2.5f})if(before<time && after>=time)Cue(config.thunder,.7f);
            foreach(float time in new[]{GlideAt+.8f,GlideAt+2.4f,GlideAt+4,GlideAt+5.6f,GlideAt+7.2f})if(before<time && after>=time)Cue(config.whoosh,.95f);
            if(before<DiveAt && after>=DiveAt)Cue(config.whoosh,1.2f,1.8f);
            if(before<LandingAt+.2f && after>=LandingAt+.2f)Cue(config.plop,.7f);
        }
        void RefreshAmbience()
        {
            bool audible=Active && Playing && !game.MenuActive && Elapsed>=RoofAt && Elapsed<GlideAt;
            if(audible && !rainAudio.isPlaying)rainAudio.Play();else if(!audible && rainAudio.isPlaying)rainAudio.Stop();
            if(voice && voice.isPlaying){voice.volume=.55f*Mathf.SmoothStep(0,1,(Elapsed-voiceAt)/.2f)*Mathf.SmoothStep(0,1,(voiceUntil-Elapsed)/.4f);if(Elapsed>=voiceUntil)voice.Stop();}
        }
        void StopAudio(){if(effects)effects.Stop();if(voice)voice.Stop();if(rainAudio)rainAudio.Stop();}
    }
}