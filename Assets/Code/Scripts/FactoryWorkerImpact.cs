using UnityEngine;

namespace Cardboard
{
    public enum FactoryWorkerEmotion { None,Machine,Jump,Run,Thrown }

    public class FactoryWorkerImpact : MonoBehaviour
    {
        public Transform message;
        public Transform[] reactionIcons;
        public FactoryWorkerEmotion Emotion {get;private set;}
        public int EmotionVariant {get;private set;}=-1;
        public AudioSource voice;
        public AudioClip[] voices;
        [Range(0,1)] public float voiceVolume=.55f;
        public float voiceFadeIn=.2f,voiceFadeOut=.4f;
        public bool Reacting => Time.time<lookUntil;
        public Vector3 PushVelocity {get;private set;}
        CardboardRunner worker;
        CharacterController controller;
        float struckAt,lookUntil,voiceStartedAt,voiceAttackFrom,voiceVisibility=1;
        Vector3 localHit;
        void Awake(){worker=GetComponent<CardboardRunner>();controller=GetComponent<CharacterController>();ResetReaction();}
        public void Notice(float duration=2.7f,FactoryWorkerEmotion emotion=FactoryWorkerEmotion.Machine)
        {
            if(!worker)worker=GetComponent<CardboardRunner>();
            if(!worker || worker.isPlayer)return;
            bool already=Reacting;lookUntil=Time.time+duration;
            if(!already){struckAt=Time.time;localHit=Vector3.zero;}
            SetEmotion(emotion);if(message)message.gameObject.SetActive(true);
            if(voice && voices!=null && voices.Length>0)
            {
                voiceStartedAt=Time.time;voiceAttackFrom=voice.isPlaying?voice.volume:0;voiceVisibility=1;
                if(!voice.isPlaying)
                {
                    voice.volume=0;voice.clip=voices[Random.Range(0,voices.Length)];
                    voice.loop=true;voice.mute=false;voice.Play();
                }
            }
        }
        public void ReceiveImpact(Vector3 direction,float strength)
        {
            Notice(2.7f,FactoryWorkerEmotion.Thrown);if(!worker || worker.isPlayer)return;
            direction.y=0;PushVelocity=direction.normalized*Mathf.Clamp(strength,.4f,3);
            localHit=transform.InverseTransformDirection(PushVelocity);struckAt=Time.time;worker.PauseFor(.1f);
        }
        public bool TickReaction(float dt)
        {
            if(!Reacting)return false;
            var game=CardboardFactoryGame.Instance;
            if(game && game.Ended){ResetReaction();return false;}
            if(game && game.captureSequence && game.captureSequence.ControlsWorkers)return false;
            if(!controller)controller=GetComponent<CharacterController>();
            var displacement=PushVelocity*dt;
            if(controller && controller.enabled)controller.Move(displacement);else transform.position+=displacement;
            PushVelocity*=Mathf.Exp(-6*dt);
            if(CardboardRunner.Player && Time.time-struckAt>.16f)
            {
                var facing=CardboardRunner.Player.transform.position-transform.position;facing.y=0;
                if(facing.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-7*dt));
            }
            if(worker.puppet)
            {
                float recoil=Mathf.Exp(-(Time.time-struckAt)*7);
                worker.puppet.localPosition=new Vector3(0,-.16f,0);
                worker.puppet.localRotation=Quaternion.Euler(localHit.z*9*recoil,0,-localHit.x*13*recoil);
            }
            return true;
        }
        void LateUpdate()
        {
            var game=CardboardFactoryGame.Instance;
            if(!Reacting || (game && game.Ended)){ResetReaction();return;}
            bool visible=!game || !game.MenuActive;
            if(message)
            {
                message.gameObject.SetActive(visible);
                if(Camera.main)
                {
                    Vector3 facing=Camera.main.transform.position-message.position;
                    if(facing.sqrMagnitude>.001f)message.rotation=Quaternion.LookRotation(facing.normalized,Vector3.up);
                }
            }
            TickVoice(Time.time,Time.deltaTime,visible);
        }
        void SetEmotion(FactoryWorkerEmotion emotion)
        {
            Emotion=emotion;
            EmotionVariant=emotion==FactoryWorkerEmotion.Machine?0:emotion==FactoryWorkerEmotion.Jump?1:
                emotion==FactoryWorkerEmotion.Run?2:emotion==FactoryWorkerEmotion.Thrown?Random.Range(3,5):-1;
            if(reactionIcons!=null)for(int i=0;i<reactionIcons.Length;i++)if(reactionIcons[i])reactionIcons[i].gameObject.SetActive(i==EmotionVariant);
        }
        public void TickVoice(float now,float dt,bool visible)
        {
            if(!voice || !voice.isPlaying)return;
            voice.mute=false;
            float attack=Mathf.SmoothStep(0,1,(now-voiceStartedAt)/Mathf.Max(.01f,voiceFadeIn));
            float release=Mathf.SmoothStep(0,1,(lookUntil-now)/Mathf.Max(.01f,voiceFadeOut));
            voiceVisibility=Mathf.MoveTowards(voiceVisibility,visible?1:0,Mathf.Max(0,dt)/Mathf.Max(.01f,visible?voiceFadeIn:voiceFadeOut));
            voice.volume=Mathf.Lerp(voiceAttackFrom,voiceVolume,attack)*release*voiceVisibility;
        }
        public void ResetReaction()
        {lookUntil=0;SetEmotion(FactoryWorkerEmotion.None);PushVelocity=Vector3.zero;if(message)message.gameObject.SetActive(false);if(voice){voice.volume=0;voice.Stop();}}
        void OnDisable(){ResetReaction();}
    }
}
