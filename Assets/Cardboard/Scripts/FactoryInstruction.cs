using UnityEngine;

namespace Cardboard
{
    // Input is routed by the game's E/Q handler, so pickup cannot also flip in the same frame.
    public class FactoryInstruction : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Transform sheet;
        public BoxCollider pickup;
        public Vector3 floorPosition;
        public Quaternion floorRotation=Quaternion.identity;
        public bool Held {get;private set;}
        public bool Reverse {get;private set;}
        public bool Animating {get;private set;}
        Quaternion fromRotation, toRotation;
        Vector3 fromPosition;
        float travel;
        const float Duration=.38f;
        public bool Take()
        {
            if(Held || !game || game.HandsBusy || game.HasPart || game.PlayerBlocked || game.MenuActive ||
                game.CaptureActive || game.Ended || !Camera.main) return false;
            Held=true;Reverse=false;pickup.enabled=false;
            if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false);
            sheet.SetParent(Camera.main.transform,true);
            fromPosition=sheet.localPosition;fromRotation=sheet.localRotation;toRotation=Quaternion.identity;
            travel=0;Animating=true;
            game.Toast("E — перевернуть инструкцию · Q — положить",5);
            return true;
        }
        public bool Flip()
        {
            if(!Held || Animating || game.MenuActive || game.CaptureActive || game.Ended) return false;
            Reverse=!Reverse;fromRotation=sheet.localRotation;
            toRotation=Quaternion.Euler(0,Reverse?180:0,0);fromPosition=sheet.localPosition;
            travel=0;Animating=true;return true;
        }
        void Update()
        {
            if(!Held) return;
            if(game.MenuActive || game.CaptureActive || game.Ended){ResetFloor();return;}
            if(!Animating)return;
            travel+=Time.deltaTime;
            float t=Mathf.SmoothStep(0,1,travel/Duration);
            sheet.localPosition=Vector3.Lerp(fromPosition,new Vector3(0,-.075f,.78f),t);
            sheet.localRotation=Quaternion.Slerp(fromRotation,toRotation,t);
            if(travel>=Duration)Animating=false;
        }
        public void PutDown()
        {
            if(!Held)return;
            var player=CardboardRunner.Player;
            if(player)
            {
                Vector3 direction=Camera.main.transform.forward;direction.y=0;
                if(direction.sqrMagnitude<.01f)direction=player.transform.forward;
                Vector3 place=player.transform.position+direction.normalized*.8f;
                // Floor layer 8 includes furniture: land on the first surface below the feet.
                if(Physics.Raycast(place+Vector3.up*.25f,Vector3.down,out var hit,2,1<<8,QueryTriggerInteraction.Ignore))
                {
                    floorPosition=hit.point+Vector3.up*.03f;
                    floorRotation=Quaternion.Euler(90,Camera.main.transform.eulerAngles.y,0);
                }
            }
            ResetFloor();
        }
        public void ResetFloor()
        {
            Held=Reverse=Animating=false;travel=0;
            sheet.SetParent(transform,false);sheet.localScale=Vector3.one;
            sheet.SetPositionAndRotation(floorPosition,floorRotation);pickup.enabled=true;
        }
        void OnDestroy(){if(Held && sheet)Destroy(sheet.gameObject);}
    }
}
