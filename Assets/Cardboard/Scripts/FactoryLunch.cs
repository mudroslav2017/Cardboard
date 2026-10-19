using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public enum LunchState { Waiting, Ready, Carrying, Served, Spilled }

    [DefaultExecutionOrder(-30)]
    public class FactoryLunch : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Transform tray, pickup, destination;
        public Transform[] food;
        public GameObject pickupHit, destinationHit;
        public TextMesh counterSign, tableSign;
        public float lunchAfter=60;
        public FactoryLunchCrowd diners;
        public LunchState State {get;private set;}
        public bool Carrying => State==LunchState.Carrying;
        public float Tilt {get;private set;}
        public float AngularSpeed {get;private set;}
        public float Elapsed {get;private set;}
        public float CarryTime {get;private set;}
        public float ResultUntil {get;private set;}
        public bool CanServe => Carrying && CardboardRunner.Player &&
            Vector3.Distance(CardboardRunner.Player.transform.position,destination.position)<2.5f && Mathf.Abs(Tilt)<=.4f;
        Vector3[] foodHomes, spillStarts, spillSpeeds;
        Quaternion[] foodRotations;
        float spillTime;

        void Awake() { CacheFood(); }
        void CacheFood()
        {
            if(food==null) return;
            if(foodHomes!=null && foodHomes.Length==food.Length && foodRotations!=null && foodRotations.Length==food.Length &&
                spillStarts!=null && spillStarts.Length==food.Length && spillSpeeds!=null && spillSpeeds.Length==food.Length) return;
            foodHomes=new Vector3[food.Length];foodRotations=new Quaternion[food.Length];
            spillStarts=new Vector3[food.Length];spillSpeeds=new Vector3[food.Length];
            for(int i=0;i<food.Length;i++) {foodHomes[i]=food[i].localPosition;foodRotations[i]=food[i].localRotation;}
        }
        public void ResetLunch()
        {
            CacheFood();State=LunchState.Waiting;Tilt=AngularSpeed=Elapsed=CarryTime=spillTime=ResultUntil=0;
            if(diners) diners.ResetLunch();
            tray.SetParent(transform,false);tray.SetPositionAndRotation(pickup.position,pickup.rotation);tray.gameObject.SetActive(true);
            for(int i=0;i<food.Length;i++)
            {
                food[i].SetParent(tray,false);food[i].localPosition=foodHomes[i];food[i].localRotation=foodRotations[i];
                food[i].gameObject.SetActive(true);
            }
            RefreshWorld();
        }
        bool Gameplay => game && !game.MenuActive && !game.Ended && !game.CaptureActive;
        public void TickSchedule(float dt)
        {
            if(!Gameplay || State!=LunchState.Waiting) return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=lunchAfter)
            {
                State=LunchState.Ready;RefreshWorld();
                if(diners) diners.Begin();
                game.Toast("Обед! Возьмите поднос на раздаче в столовой.",8);game.PlayCaptureSound("good");
            }
        }
        public bool TryTake()
        {
            if(!Gameplay || State!=LunchState.Ready || game.HasPart || game.HandsBusy || game.PlayerBlocked || !CardboardRunner.Player ||
                CardboardRunner.Player.JumpHeight>.01f || Vector3.Distance(CardboardRunner.Player.transform.position,pickup.position)>3.5f) return false;
            if(game.station) game.station.EndGrip(false);
            if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false);
            State=LunchState.Carrying;Tilt=.12f;AngularSpeed=.16f;CarryTime=0;
            tray.SetParent(Camera.main.transform,false);RefreshWorld();PoseTray();
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            game.Toast("Донесите обед до отмеченного стола. ЛКМ — наклон влево, ПКМ — вправо.",6);
            return true;
        }
        void Update()
        {
            if(!Gameplay) return;
            if(State==LunchState.Spilled) { TickSpill(Time.deltaTime);return; }
            if(!Carrying || !Application.isFocused) return;
            var mouse=Mouse.current;
            TickBalance(Time.deltaTime,mouse!=null && mouse.leftButton.wasPressedThisFrame,mouse!=null && mouse.rightButton.wasPressedThisFrame);
        }
        public void TickBalance(float dt,bool leftClick,bool rightClick)
        {
            if(!Gameplay || !Carrying || dt<=0) return;
            AngularSpeed+=((rightClick?1:0)-(leftClick?1:0))*.42f;
            // Fixed-size substeps keep a dropped frame from changing the balancing difficulty.
            float remaining=Mathf.Min(dt,.25f);
            while(remaining>0 && Carrying)
            {
                float step=Mathf.Min(.02f,remaining);remaining-=step;CarryTime+=step;
                float wobble=.28f*Mathf.Sin(CarryTime*1.7f)+.14f*Mathf.Sin(CarryTime*3.9f+.6f);
                AngularSpeed+=(Tilt*.5f+wobble-AngularSpeed*.18f)*step;
                Tilt+=AngularSpeed*step;
                if(Mathf.Abs(Tilt)>=1) Spill();
            }
            if(Carrying) PoseTray();
        }
        void LateUpdate() { if(Carrying) PoseTray(); }
        void PoseTray()
        {
            tray.localPosition=new Vector3(Mathf.Sin(CarryTime*2)*.012f,-.39f,.72f);
            tray.localRotation=Quaternion.Euler(9+Mathf.Sin(CarryTime*2.6f)*1.5f,0,-Tilt*38);
            for(int i=0;i<food.Length;i++) food[i].localPosition=foodHomes[i]+Vector3.right*Tilt*Mathf.Abs(Tilt)*.09f;
        }
        public bool TryServe()
        {
            if(!Gameplay || !CanServe) return false;
            State=LunchState.Served;Tilt=AngularSpeed=0;ResultUntil=Time.time+6;
            tray.SetParent(transform,false);tray.SetPositionAndRotation(destination.position,destination.rotation);
            for(int i=0;i<food.Length;i++) food[i].localPosition=foodHomes[i];
            RefreshWorld();game.Toast("Обед доставлен. Картонки ничего не заподозрили. Возвращайтесь к работе.",7);
            game.PlayCaptureSound("good");return true;
        }
        void Spill()
        {
            PoseTray();State=LunchState.Spilled;spillTime=0;ResultUntil=Time.time+6;
            tray.SetParent(transform,true);
            float side=Mathf.Sign(Tilt);
            for(int i=0;i<food.Length;i++)
            {
                spillStarts[i]=food[i].position;
                spillSpeeds[i]=Camera.main.transform.TransformDirection(new Vector3(side*(.8f+i*.17f),.4f,.4f+i*.12f));
                food[i].SetParent(transform,true);
            }
            RefreshWorld();game.RegisterMistake("Уронил еду с подноса во время обеда.");
            game.PlayCaptureSound("impact");
        }
        public void TickSpill(float dt)
        {
            if(State!=LunchState.Spilled) return;
            spillTime+=Mathf.Max(0,dt);
            for(int i=0;i<food.Length;i++)
            {
                if(food[i].position.y<=-.08f) continue;
                float vy=spillSpeeds[i].y;
                float impactTime=(vy+Mathf.Sqrt(vy*vy+19.6f*Mathf.Max(0,spillStarts[i].y+.08f)))/9.8f;
                float flight=Mathf.Min(spillTime,impactTime);
                var position=spillStarts[i]+spillSpeeds[i]*flight+Vector3.down*(4.9f*flight*flight);
                position.y=Mathf.Max(-.08f,position.y);food[i].position=position;
                food[i].Rotate(new Vector3(100,70,140)*dt,Space.World);
            }
            if(spillTime>1.2f) tray.gameObject.SetActive(false);
        }
        public void CancelCarry()
        {
            if(diners) diners.Cancel();
            if(Carrying) State=LunchState.Spilled;
            tray.gameObject.SetActive(false);RefreshWorld();
        }
        void RefreshWorld()
        {
            pickupHit.SetActive(State==LunchState.Waiting || State==LunchState.Ready);
            destinationHit.SetActive(Carrying);
            counterSign.text=State==LunchState.Waiting?"РАЗДАЧА\nОБЕД ПО ЗВОНКУ":State==LunchState.Ready?"ОБЕД\nВОЗЬМИТЕ ПОДНОС":"РАЗДАЧА\nОБЕД ВЫДАН";
            tableSign.text=State==LunchState.Served?"ПРИЯТНОГО\nАППЕТИТА!":"ОБЕДЕННЫЙ СТОЛ\nПОДНОС СЮДА";
        }
    }
}
