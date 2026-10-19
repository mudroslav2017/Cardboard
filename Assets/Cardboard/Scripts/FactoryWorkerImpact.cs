using UnityEngine;

namespace Cardboard
{
    public class FactoryWorkerImpact : MonoBehaviour
    {
        public Transform message;
        public AudioSource voice;
        public AudioClip[] voices;
        public bool Reacting => Time.time<lookUntil;
        public Vector3 PushVelocity {get;private set;}
        CardboardRunner worker;
        CharacterController controller;
        float struckAt,lookUntil;
        Vector3 localHit;
        void Awake(){worker=GetComponent<CardboardRunner>();controller=GetComponent<CharacterController>();ResetReaction();}
        public void Notice(float duration=2.7f)
        {
            if(!worker)worker=GetComponent<CardboardRunner>();
            if(!worker || worker.isPlayer)return;
            bool already=Reacting;lookUntil=Time.time+duration;
            if(!already){struckAt=Time.time;localHit=Vector3.zero;}
            if(message)message.gameObject.SetActive(true);
            if(voice && voices!=null && voices.Length>0 && !voice.isPlaying)
            {voice.clip=voices[Random.Range(0,voices.Length)];voice.loop=true;voice.Play();}
        }
        public void ReceiveImpact(Vector3 direction,float strength)
        {
            Notice();if(!worker || worker.isPlayer)return;
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
            if(voice)voice.mute=!visible;
        }
        public void ResetReaction()
        {lookUntil=0;PushVelocity=Vector3.zero;if(message)message.gameObject.SetActive(false);if(voice)voice.Stop();}
        void OnDisable(){ResetReaction();}
    }
}
