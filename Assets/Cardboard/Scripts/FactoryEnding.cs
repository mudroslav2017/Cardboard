using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public enum FactoryEndingPhase { None, BellDelay, LookAround, Running, WhiteOut, MenuReveal, WaitingForLunch }

    [DefaultExecutionOrder(35)]
    public class FactoryEnding : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Transform glow;
        public Image white;
        public FactoryEndingPhase Phase {get;private set;}
        public float PhaseTime {get;private set;}
        public bool Active=>Phase!=FactoryEndingPhase.None && Phase!=FactoryEndingPhase.MenuReveal;
        public bool CameraControlled=>Active && Phase!=FactoryEndingPhase.BellDelay;
        public bool Running=>Phase==FactoryEndingPhase.Running;
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public IReadOnlyList<Vector3> Route=>route;
        readonly List<Vector3> route=new List<Vector3>();
        int point;
        float runTime,runSpeed,homeFov;
        Vector3 lookPosition;
        Quaternion lookRotation,runFacing;
        Camera lens;
        CharacterController body;
        bool bodyWasEnabled;

        void Update(){Tick(Phase==FactoryEndingPhase.MenuReveal?Time.unscaledDeltaTime:Time.deltaTime);}
        void Change(FactoryEndingPhase phase){Phase=phase;PhaseTime=0;}
        void Fade(float alpha){if(white)white.color=new Color(1,1,1,Mathf.Clamp01(alpha));}

        public bool Begin()
        {
            if(Phase!=FactoryEndingPhase.None || game.MenuActive || game.Ended || game.CaptureActive || game.IntroActive || !game.ReadyToLeave || !CardboardRunner.Player)return false;
            lens=Camera.main;homeFov=lens.fieldOfView;
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false,false);
            if(game.station)game.station.EndGrip(false);
            game.Toast("Six repairs complete. Wait for the lunch bell...",3);
            Change(FactoryEndingPhase.BellDelay);return true;
        }

        void BeginLunchWait()
        {
            if(game.objectCarry)game.objectCarry.Clear();
            if(game.lunch)game.lunch.BeginFinalLunch();
            foreach(var worker in game.GetComponentsInChildren<FactoryWorkerImpact>())worker.ResetReaction();
            var player=CardboardRunner.Player.transform;
            body=player.GetComponent<CharacterController>();bodyWasEnabled=body && body.enabled;
            if(body)body.enabled=false;
            lookPosition=lens.transform.position;lookRotation=lens.transform.rotation;
            CameraPosition=lookPosition;CameraRotation=lookRotation;
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            game.Toast("Wait until everyone has left for lunch.",10);
            Change(FactoryEndingPhase.WaitingForLunch);
        }

        void BeginEscape()
        {
            if(game.exitDoor)game.exitDoor.SetOpen(true);
            if(glow)glow.gameObject.SetActive(true);
            var player=CardboardRunner.Player.transform;
            lookPosition=CameraPosition;lookRotation=CameraRotation;
            route.Clear();
            var inside=new Vector3(CardboardFactoryWorld.WorkshopEast-1.3f,player.position.y,-2);
            route.AddRange(new FactoryCaptureNavigation(.42f).Path(player.position,inside));
            // Navigation covers the workshop; the straight corridor continues beyond its grid.
            route.Add(inside);
            route.Add(new Vector3(CardboardFactoryWorld.WorkshopEast+1,player.position.y,-2));
            route.Add(new Vector3(CardboardFactoryWorld.WorkshopEast+14.4f,player.position.y,-2));
            point=0;runSpeed=runTime=0;
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            game.Toast("Everyone has left for lunch. Now is your chance.",4);
            Change(FactoryEndingPhase.LookAround);
        }

        public void Tick(float dt)
        {
            if(Phase==FactoryEndingPhase.None || dt<=0)return;
            dt=Mathf.Min(dt,.1f);PhaseTime+=dt;
            switch(Phase)
            {
                case FactoryEndingPhase.BellDelay:
                    if(PhaseTime>=2)BeginLunchWait();
                    break;
                case FactoryEndingPhase.WaitingForLunch:
                    var doorway=new Vector3(-13,lookPosition.y,CardboardFactoryWorld.WorkshopSouth-.3f);
                    var watch=Quaternion.LookRotation(doorway-lookPosition,Vector3.up);
                    CameraPosition=lookPosition+Vector3.up*Mathf.Sin(PhaseTime*2)*.009f;
                    CameraRotation=Quaternion.Slerp(lookRotation,watch,Mathf.SmoothStep(0,1,PhaseTime/1.2f));
                    if(!game.lunch || !game.lunch.diners || game.lunch.diners.AllInCanteen)BeginEscape();
                    break;
                case FactoryEndingPhase.LookAround:
                    float yaw=PhaseTime<1?Mathf.Lerp(0,-56,Mathf.SmoothStep(0,1,PhaseTime)):
                        PhaseTime<2.2f?Mathf.Lerp(-56,55,Mathf.SmoothStep(0,1,(PhaseTime-1)/1.2f)):
                        Mathf.Lerp(55,0,Mathf.SmoothStep(0,1,(PhaseTime-2.2f)/.7f));
                    CameraPosition=lookPosition+Vector3.up*Mathf.Sin(PhaseTime*2)*.009f;
                    CameraRotation=lookRotation*Quaternion.Euler(Mathf.Sin(PhaseTime*1.8f)*2,yaw,0);
                    if(PhaseTime>=2.9f){runFacing=CameraRotation;Change(FactoryEndingPhase.Running);}
                    break;
                case FactoryEndingPhase.Running:
                    Run(dt);break;
                case FactoryEndingPhase.WhiteOut:
                    Fade(Mathf.Lerp(.65f,1,Mathf.SmoothStep(0,1,PhaseTime/.65f)));
                    if(PhaseTime>=.7f)
                    {
                        RestoreBody();lens.fieldOfView=homeFov;
                        game.mainMenu.Show();Change(FactoryEndingPhase.MenuReveal);
                    }
                    break;
                case FactoryEndingPhase.MenuReveal:
                    Fade(1-Mathf.SmoothStep(0,1,PhaseTime/.8f));
                    if(PhaseTime>=.8f){if(glow)glow.gameObject.SetActive(false);Change(FactoryEndingPhase.None);}
                    break;
            }
        }

        void Run(float dt)
        {
            var player=CardboardRunner.Player;
            while(point<route.Count && Vector3.Distance(player.transform.position,route[point])<.16f)point++;
            if(point>=route.Count){EnterGlow();return;}
            Vector3 direction=route[point]-player.transform.position;direction.y=0;
            var facing=Quaternion.LookRotation(direction.normalized,Vector3.up);
            float alignment=Vector3.Dot(player.transform.forward,direction.normalized);
            runSpeed=Mathf.MoveTowards(runSpeed,Mathf.Lerp(2.8f,6.4f,Mathf.InverseLerp(-.5f,1,alignment)),dt*5);
            player.transform.rotation=Quaternion.Slerp(player.transform.rotation,facing,1-Mathf.Exp(-8*dt));
            player.transform.position=Vector3.MoveTowards(player.transform.position,route[point],runSpeed*dt);
            player.speed=runSpeed;runTime+=dt;
            float strength=Mathf.InverseLerp(1,6.4f,runSpeed);
            Vector3 shake=new Vector3(Mathf.Sin(runTime*10.8f)*.018f,Mathf.Sin(runTime*21.6f)*.035f,0)*strength;
            CameraPosition=player.transform.position+Vector3.up*1.62f+player.transform.rotation*shake;
            runFacing=Quaternion.Slerp(runFacing,facing,1-Mathf.Exp(-7*dt));
            CameraRotation=runFacing*
                Quaternion.Euler(Mathf.Sin(runTime*21.6f)*.45f*strength,0,Mathf.Sin(runTime*10.8f)*.65f*strength);
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,CardboardCamera.GameplayFov+5,1-Mathf.Exp(-2*dt));
            float endDistance=Vector3.Distance(player.transform.position,route[route.Count-1]);
            Fade(Mathf.InverseLerp(3,.25f,endDistance)*.65f);
            if(endDistance<.25f)EnterGlow();
        }
        public void EnterGlow()
        {
            if(Phase!=FactoryEndingPhase.Running)return;
            if(CardboardRunner.Player)CardboardRunner.Player.speed=0;
            Change(FactoryEndingPhase.WhiteOut);
        }
        void RestoreBody(){if(body)body.enabled=bodyWasEnabled;body=null;}
        public void Cancel()
        {
            RestoreBody();if(lens)lens.fieldOfView=homeFov;
            Phase=FactoryEndingPhase.None;PhaseTime=0;route.Clear();Fade(0);
            if(glow)glow.gameObject.SetActive(false);
        }
        void OnDisable(){Cancel();}
    }
}
