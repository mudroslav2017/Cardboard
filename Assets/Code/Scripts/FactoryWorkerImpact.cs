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
        public bool Reacting => pinUntil > 0f || Time.time<lookUntil;
        public Vector3 PushVelocity {get;private set;}
        const float PinSeconds = 5f;
        const float WallReach = .5f;
        const float PinLift = .24f;
        const int WallMask = 1 << 8;
        CardboardRunner worker;
        CharacterController controller;
        float struckAt,lookUntil,voiceStartedAt,voiceAttackFrom,voiceVisibility=1;
        float pinUntil, pinGroundY;
        Vector3 pinPosition, pinNormal;
        Vector3 localHit;
        internal Transform StapleMount => worker && worker.puppet ? worker.puppet : transform;
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
        internal float ReceiveStapleImpact(Vector3 direction)
        {
            if (!worker || worker.isPlayer) return 0f;
            if (pinUntil > 0f) return Mathf.Max(0f, pinUntil - Time.time);
            ReceiveImpact(direction, 3f);
            if (!controller || !controller.enabled || !FindPinWall(direction, out float wallDistance)) return 0f;

            pinGroundY = transform.position.y;
            float clearance = controller.radius + controller.skinWidth;
            pinPosition = transform.position - pinNormal * Mathf.Max(0f, wallDistance - clearance) + Vector3.up * PinLift;
            pinUntil = Time.time + PinSeconds;
            lookUntil = pinUntil;
            return PinSeconds;
        }

        bool FindPinWall(Vector3 direction, out float distance)
        {
            Vector3 bearing = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (bearing.sqrMagnitude < .001f) bearing = Vector3.forward;
            bearing.Normalize();
            Vector3 origin = transform.TransformPoint(controller.center);
            float reach = controller.radius + WallReach;
            distance = reach;
            pinNormal = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                Vector3 ray = Quaternion.AngleAxis(i * 45f, Vector3.up) * bearing;
                // Allow for the angle between samples, then check perpendicular distance to the wall.
                if (!Physics.Raycast(origin, ray, out var hit, reach * 1.09f, WallMask, QueryTriggerInteraction.Ignore) ||
                    hit.rigidbody || hit.collider.bounds.size.y < 1.45f || Mathf.Abs(hit.normal.y) > .25f)
                    continue;

                Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                float depth = Vector3.Dot(origin - hit.point, normal);
                if (depth > distance) continue;
                distance = depth;
                pinNormal = normal;
            }
            return pinNormal != Vector3.zero;
        }

        bool TickWallPin(float dt)
        {
            if (!controller || !controller.enabled)
            {
                ClearWallPin();
                return false;
            }

            PushVelocity = Vector3.zero;
            if (Time.time >= pinUntil)
            {
                float height = transform.position.y - pinGroundY;
                if (height > .005f &&
                    (controller.Move(Vector3.down * Mathf.Min(height, 2.4f * dt)) & CollisionFlags.Below) == 0)
                    return true;
                ClearWallPin();
                return false;
            }

            float follow = 1f - Mathf.Exp(-18f * dt);
            controller.Move((pinPosition - transform.position) * follow);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(pinNormal), follow);
            float settled = 1f - Mathf.Clamp01(Vector3.Distance(transform.position, pinPosition) * 8f);
            PoseRecoil(-transform.InverseTransformDirection(pinNormal) * (.24f * settled));
            return true;
        }

        public bool TickReaction(float dt)
        {
            if(!Reacting)return false;
            var game=CardboardFactoryGame.Instance;
            if(game && game.Ended){ResetReaction();return false;}
            if(game && game.captureSequence && game.captureSequence.ControlsWorkers)return false;
            if(pinUntil > 0f) return TickWallPin(dt);
            if(!controller)controller=GetComponent<CharacterController>();
            var displacement=PushVelocity*dt;
            if(controller && controller.enabled)controller.Move(displacement);else transform.position+=displacement;
            PushVelocity*=Mathf.Exp(-6*dt);
            if(CardboardRunner.Player && Time.time-struckAt>.16f)
            {
                var facing=CardboardRunner.Player.transform.position-transform.position;facing.y=0;
                if(facing.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-7*dt));
            }
            PoseRecoil(Vector3.zero);
            return true;
        }
        void PoseRecoil(Vector3 offset)
        {
            if (!worker.puppet) return;
            float recoil = Mathf.Exp(-(Time.time - struckAt) * 7f);
            worker.puppet.localPosition = new Vector3(0f, -.16f, 0f) + offset;
            worker.puppet.localRotation = Quaternion.Euler(localHit.z * 9f * recoil, 0f, -localHit.x * 13f * recoil);
        }

        void ClearWallPin()
        {
            if (pinUntil <= 0f) return;
            pinUntil = 0f;
            float height = transform.position.y - pinGroundY;
            if (height > .005f && controller && controller.enabled && gameObject.activeInHierarchy)
                controller.Move(Vector3.down * height);
            if (worker && worker.puppet) worker.puppet.localPosition = new Vector3(0f, -.16f, 0f);
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
        {
            ClearWallPin();
            lookUntil=0;SetEmotion(FactoryWorkerEmotion.None);PushVelocity=Vector3.zero;if(message)message.gameObject.SetActive(false);if(voice){voice.volume=0;voice.Stop();}
        }
        void OnDisable(){ResetReaction();}
    }
}
