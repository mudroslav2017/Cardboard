using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public enum StationStage { Empty, Dropping, Conveying, Loaded, Cutting, Cut, Dipping }

    [DefaultExecutionOrder(-40)]
    public class FactoryWorkstation : MonoBehaviour
    {
        public bool isPlayerStation = true;
        public Transform feedLever, pressLever, selector, wheel, wheelBase, press, guardModel;
        public Transform feedGrip, pressGrip, wheelGrip;
        public Transform feedHandle,pressHandle;
        public Transform FeedMotion => feedHandle?feedHandle:feedLever;
        public Transform PressMotion => pressHandle?pressHandle:pressLever;
        public FactoryOperatorHand operatorHand;
        public FactoryMachiningVFX machiningEffects;
        public AnimationClip guardClip;
        public BoxCollider guardHit;
        public Transform sheet, cutPart, pumpPlate, pressureNeedle, progressFill, orderCard;
        public TextMesh orderNumber;
        public FactoryPunchcardView orderView;
        public FactoryPrinterSignal printerSignal;
        public const float PrintDuration=2.6f;
        public bool IsPrinting=>QueuedOrder>0 && (OrderNumber!=QueuedOrder || OrderTravel<1);
        public Transform[] baths;
        public Renderer[] pressureLamps;
        public Material lampOff, lampOn;
        public TextMesh selectorNumber;
        public Transform[] selectorIcons;
        public Vector3 dropPosition = new Vector3(-2.67f, 1.8f, 0);
        public Vector3 conveyorPosition = new Vector3(-2.67f, 1.17f, 0);
        public Vector3 cuttingPosition = new Vector3(-.08f, 1.03f, .12f);
        public Vector3 progressOrigin = new Vector3(2.049f,1.68f,-.485f);
        public StationStage Stage { get; private set; }
        public int SelectedType { get; private set; } = 1;
        public int CutType { get; private set; }
        public float Pressure { get; private set; }
        public float CutProgress { get; private set; }
        public bool PressClosed { get; private set; }
        public bool GuardClosed { get; private set; }
        public float GuardBlend { get; private set; }
        public bool Manipulating { get; private set; }
        public bool LeverAnimating { get; private set; }
        public float LeverTravel { get; private set; }
        public int OrderNumber { get; private set; }
        public int QueuedOrder { get; private set; }
        public float OrderTravel { get; private set; }
        public bool ConsumesMouse => Manipulating || LeverAnimating || Time.frameCount == releasedFrame;
        public bool Nearby => Camera.main && Vector3.Distance(Camera.main.transform.position, transform.TransformPoint(new Vector3(0, 1.2f, -.8f))) < 6;
        public bool PressureReady => Pressure >= .999f;
        public bool CanCut => !LeverAnimating && (Stage == StationStage.Loaded || Stage == StationStage.Cutting) && PressClosed && pressBlend >= .99f && GuardClosed && GuardBlend >= .99f && PressureReady;
        public bool CanPump => !LeverAnimating && (Stage == StationStage.Loaded || Stage == StationStage.Cutting) && PressClosed && pressBlend >= .99f && GuardClosed && GuardBlend >= .99f;
        CardboardFactoryGame Game => CardboardFactoryGame.Instance;
        Vector3 pressHome, pumpHome, wheelAxis;
        Quaternion feedHome, pressLeverHome, selectorHome, wheelHome;
        float elapsed, pressBlend, landingPulse, wheelAngle, lastAngle, leverTime, autoTurnRemaining;
        Vector2 previousDragPointer;
        int dippingBath, releasedFrame = -1;
        bool initialized, hasAngle, returnToLook, leverFired, wheelAttemptFault, wrongTypeFault;
        public bool ExecutionControlled {get;private set;}
        StationPart activeLever;
        Vector2 pointer;
        GameObject dippingVisual;
        FactoryWorkbenchSound sound;

        void Awake() { Initialize(); }
        void Initialize()
        {
            if (initialized || !press) return;
            initialized = true; pressHome = press.localPosition; pumpHome = pumpPlate.localPosition;
            feedHome = FeedMotion.localRotation; pressLeverHome = PressMotion.localRotation; selectorHome = selector.localRotation;
            wheelHome = wheel.localRotation;
            wheelAxis = wheel.parent.InverseTransformDirection(transform.forward).normalized;
            sound = GetComponent<FactoryWorkbenchSound>();
        }
        public void RebindModels() {initialized=false;Initialize();ResetMachine();}
        public void ResetMachine()
        {
            Initialize(); EndGrip(false);ExecutionControlled=false;wheelAttemptFault=wrongTypeFault=false;
            Stage = StationStage.Empty; SelectedType = 1; CutType = 0; Pressure = CutProgress = GuardBlend = pressBlend = elapsed = 0;
            PressClosed = GuardClosed = false; landingPulse = wheelAngle = 0;
            OrderNumber = QueuedOrder = 0; OrderTravel = 0;
            if(printerSignal)printerSignal.SetSignal(false);
            if(sound)sound.StopCutting();
            if (dippingVisual) Destroy(dippingVisual);
            if (machiningEffects) machiningEffects.Clear();
            if (initialized) Refresh();
        }
        public void PrintOrder(int number) { QueuedOrder = Mathf.Max(number,0); }
        bool Available => isPlayerStation && Game && !Game.MenuActive && !Game.Ended && !Game.HandsBusy && !Game.IntroBusy && Game.State != FactoryState.Chased;
        void Update()
        {
            if (ExecutionControlled) return;
            Tick(Time.deltaTime);
            if (!Available) { EndGrip(false); return; }
            if (!Application.isFocused) { if (Manipulating && autoTurnRemaining <= 0) EndGrip(false); return; }
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (Manipulating && autoTurnRemaining <= 0)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) EndGrip(false);
                else if (mouse == null || !mouse.leftButton.isPressed) EndGrip();
                else { pointer += mouse.delta.ReadValue(); DragWheel(pointer); }
            }
            else if (!LeverAnimating && !Manipulating && !Game.HasPart && mouse != null && mouse.leftButton.wasPressedThisFrame && !CardboardMarker.DrawingMode)
            {
                var camera = Camera.main;
                Ray ray = Cursor.lockState == CursorLockMode.Locked ? camera.ViewportPointToRay(new Vector3(.5f, .5f)) : camera.ScreenPointToRay(mouse.position.ReadValue());
                var target = Game.TargetForRay(ray);
                var control = target ? target.GetComponent<FactoryStationControl>() : null;
                if (control && control.station == this)
                {
                    if (control.part == StationPart.Wheel) BeginWheel(mouse.position.ReadValue());
                    else control.Use();
                }
            }
        }
        // Also used by the Play Mode checks, so timed machinery has a single path.
        public void Tick(float dt)
        {
            if(Game && !Game.MenuActive && !Game.Ended && !Game.CaptureActive && (!Game.intro || Game.intro.AllowPrinter))TickOrder(dt);
            if (!Available) return;
            if (LeverAnimating)
            {
                leverTime += dt;
                LeverTravel = leverTime < .35f ? Mathf.SmoothStep(0, 1, leverTime / .35f) :
                    leverTime < .45f ? 1 : Mathf.SmoothStep(1, 0, (leverTime - .45f) / .55f);
                if (!leverFired && leverTime >= .35f)
                {
                    leverFired = true;
                    if (activeLever == StationPart.Feed)
                    { Stage = StationStage.Dropping; elapsed = 0; sheet.localPosition = dropPosition;wrongTypeFault=false; }
                    else
                    {
                        PressClosed = !PressClosed;
                        if (!PressClosed) { Pressure = 0; if (Stage != StationStage.Cut) { CutProgress = 0; Stage = StationStage.Loaded; } }
                    }
                    Clunk(true);
                }
                if (leverTime >= 1)
                { LeverAnimating = false; LeverTravel = 0; releasedFrame = Time.frameCount; if (operatorHand) operatorHand.Release(); }
            }
            GuardBlend = Mathf.MoveTowards(GuardBlend, GuardClosed ? 1 : 0, dt / .65f);
            pressBlend = Mathf.MoveTowards(pressBlend, PressClosed ? 1 : 0, dt / .55f);
            landingPulse = Mathf.MoveTowards(landingPulse, 0, dt * 3);
            if (autoTurnRemaining > 0)
            {
                float turn = Mathf.Min(autoTurnRemaining, 150 * dt); autoTurnRemaining -= turn;
                TurnWheel(turn); if (autoTurnRemaining <= 0) EndGrip();
            }
            elapsed += dt;
            if (Stage == StationStage.Dropping)
            {
                float t = Mathf.Clamp01(elapsed / .6f);
                sheet.localPosition = Vector3.Lerp(dropPosition, conveyorPosition, t * t);
                if (elapsed >= .6f) { Stage = StationStage.Conveying; elapsed = 0; }
            }
            else if (Stage == StationStage.Conveying)
            {
                sheet.localPosition = Vector3.Lerp(conveyorPosition, cuttingPosition, Mathf.SmoothStep(0, 1, elapsed / 2.2f));
                if (elapsed >= 2.2f) { Stage = StationStage.Loaded; sheet.localPosition = cuttingPosition; Clunk(false); }
            }
            else if (Stage == StationStage.Dipping)
            {
                if (dippingVisual)
                {
                    float t = Mathf.Clamp01(elapsed / 1.1f);
                    dippingVisual.transform.position = baths[dippingBath - 1].position + Vector3.up * Mathf.Lerp(1.05f, .35f, Mathf.Sin(t * Mathf.PI));
                }
                if (elapsed >= 1.1f)
                {
                    Game.FinishDip(dippingBath); if (dippingVisual) Destroy(dippingVisual);
                    Stage = StationStage.Empty; elapsed = 0; Clunk(false);
                }
            }
            Refresh();
        }
        void TickOrder(float dt)
        {
            if (!orderCard) return;
            if (OrderNumber != QueuedOrder && OrderTravel > 0) OrderTravel = Mathf.MoveTowards(OrderTravel, 0, dt / 1.6f);
            else
            {
                OrderNumber = QueuedOrder;
                OrderTravel = Mathf.MoveTowards(OrderTravel, OrderNumber == 0 ? 0 : 1, dt / PrintDuration);
            }
            orderCard.gameObject.SetActive(OrderTravel > 0);
            if(orderNumber)orderNumber.text="";
            if(!orderView)orderView=orderCard.GetComponent<FactoryPunchcardView>();
            if(orderView && OrderNumber>0 && Game && OrderNumber<=Game.gears.Length)orderView.ShowType(Game.gears[OrderNumber-1].recipe.Type);
            if(printerSignal)printerSignal.SetSignal(IsPrinting);
            orderCard.localPosition = new Vector3(0, -.65f * Mathf.SmoothStep(0, 1, OrderTravel), .055f);
        }
        void PullLever(StationPart part)
        {
            activeLever = part; LeverAnimating = true; leverTime = LeverTravel = 0; leverFired = false;
            if(sound)sound.Lever((part==StationPart.Feed?feedGrip:pressGrip).position);
            if (operatorHand) operatorHand.Attach(part == StationPart.Feed ? feedGrip : pressGrip);
        }
        public bool Operate(StationPart part, int number = 0)
        {
            if (!Available || Manipulating || LeverAnimating || Stage == StationStage.Dipping) return false;
            bool accepted = false;
            switch (part)
            {
                case StationPart.Feed:
                    if (Stage == StationStage.Empty && !Game.HasPart && !PressClosed && !GuardClosed)
                    { PullLever(part); accepted = true; }
                    break;
                case StationPart.Selector:
                    if ((Stage == StationStage.Empty || Stage == StationStage.Loaded) && !Game.HasPart && !PressClosed && !GuardClosed)
                    { SelectedType = SelectedType % 3 + 1;wrongTypeFault=false; accepted = true; }
                    break;
                case StationPart.Press:
                    if (!GuardClosed && GuardBlend <= .01f && ((!PressClosed && Stage == StationStage.Loaded) || (PressClosed && (Stage == StationStage.Loaded || Stage == StationStage.Cutting || Stage == StationStage.Cut))))
                    { PullLever(part); accepted = true; }
                    break;
                case StationPart.Guard:
                    if (GuardClosed || (PressClosed && pressBlend >= .99f && (Stage == StationStage.Loaded || Stage == StationStage.Cutting)))
                    { GuardClosed = !GuardClosed; if (!GuardClosed) Pressure = 0; accepted = true; }
                    break;
                case StationPart.Wheel:
                    if (BeginWheel(Vector2.zero)) { autoTurnRemaining = 60; accepted = true; }
                    break;
                case StationPart.Output:
                    if (Stage == StationStage.Cut && !PressClosed && pressBlend <= .01f && !GuardClosed && GuardBlend <= .01f && !Game.HasPart)
                    {
                        Game.ReceiveWorkbenchPart(FactoryRecipe.ForType(CutType, false));
                        Stage = StationStage.Empty; CutProgress = Pressure = 0; accepted = true;
                    }
                    break;
                case StationPart.Bath:
                    if (number >= 1 && number <= 3 && Stage == StationStage.Empty && Game.BeginDip())
                    {
                        dippingBath = number; elapsed = 0; Stage = StationStage.Dipping;
                        dippingVisual = new GameObject("Gear immersed in bath " + number);
                        dippingVisual.transform.localScale = Vector3.one * .28f;
                        dippingVisual.transform.rotation = Quaternion.Euler(0, 0, 15);
                        FactoryGeometry.GearVisual(dippingVisual.transform, Game.HeldRecipe, Game.gearMeshes, Game.hubMeshes, Game.gearPaper, Game.gearEdge, Game.gearInk);
                        accepted = true;
                    }
                    break;
            }
            Clunk(!accepted); Refresh(); return accepted;
        }
        public bool IsOnPump(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) - pumpHome;
            return Mathf.Abs(local.x) <= .8f && Mathf.Abs(local.z) <= .6f;
        }
        public bool IsPumpingPlayer => Available && CanPump && CardboardRunner.Player && IsOnPump(CardboardRunner.Player.transform.position);
        public bool TryPump(Vector3 landingPosition)
        {
            if (!Available || !CanPump || !IsOnPump(landingPosition) || PressureReady) return false;
            Pressure = Mathf.Min(1, Pressure + .25f); landingPulse = 1; Clunk(true); Refresh(); return true;
        }
        public bool TurnWheel(float degrees)
        {
            if (!Available) return false;
            wheelAngle += degrees;
            if (!CanCut)
            {
                if(Mathf.Abs(degrees)>.01f && !wheelAttemptFault && Stage!=StationStage.Cut)
                {
                    wheelAttemptFault=true;
                    string reason=Stage!=StationStage.Loaded && Stage!=StationStage.Cutting?"Turned the wheel without a sheet under the press.":
                        !PressClosed || pressBlend<.99f?"Turned the wheel before closing the press.":
                        !GuardClosed || GuardBlend<.99f?"Turned the wheel before raising the guard.":
                        "Turned the wheel without full pressure.";
                    Game.RegisterMistake(reason,.5f);
                }
                Refresh(); return false;
            }
            if(SelectedType!=Game.OrderedType && !wrongTypeFault)
            {
                wrongTypeFault=true;
                Game.RegisterMistake("Cut the wrong gear type: the diagram does not match the punchcard.");
                if(Game.CaptureActive) return false;
            }
            float previousProgress=CutProgress;
            CutProgress = Mathf.Clamp01(CutProgress + degrees / 1080);
            if(machiningEffects)machiningEffects.Cut(Mathf.Max(0,CutProgress-previousProgress));
            if(sound)sound.Cut(Mathf.Max(0,CutProgress-previousProgress));
            Stage = CutProgress >= .9999f ? StationStage.Cut : StationStage.Cutting;
            if (Stage == StationStage.Cut)
            {
                CutProgress = 1; CutType = SelectedType; BuildCutPart(); Clunk(true);
                Pressure = 0; EndGrip();
            }
            Refresh(); return true;
        }
        public bool BeginWheel(Vector2 screenPointer)
        {
            if (!Available || Manipulating || LeverAnimating || Stage == StationStage.Dipping) return false;
            Manipulating = true;wheelAttemptFault=false; hasAngle = false; pointer = previousDragPointer = screenPointer; autoTurnRemaining = 0;
            returnToLook = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.Locked; FactoryVolumeCursor.SetSystemVisible(false);
            if (operatorHand) operatorHand.Attach(wheelGrip);
            DragWheel(pointer); return true;
        }
        public void DragWheel(Vector2 screenPointer)
        {
            if (!Manipulating || !Camera.main) return;
            Vector2 center=Camera.main.WorldToScreenPoint(WheelCenter);
            Vector2 offset=screenPointer-center,delta=screenPointer-previousDragPointer;previousDragPointer=screenPointer;
            if(offset.sqrMagnitude<225)
            {hasAngle=false;TurnWheel((delta.x-delta.y)*.45f);return;}
            float angle=Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg;
            if(hasAngle)TurnWheel(Mathf.Clamp(-Mathf.DeltaAngle(lastAngle,angle),-65,65));
            else if(delta.sqrMagnitude>0)TurnWheel((delta.x-delta.y)*.45f);
            lastAngle=angle;hasAngle=true;
        }
        public Vector3 WheelCenter => wheel?wheel.position:transform.TransformPoint(new Vector3(2.049f,.798f,-.334f));
        public void EndGrip(bool capture = true)
        {
            bool wasManipulating = Manipulating;
            Manipulating = LeverAnimating = false; LeverTravel = 0; autoTurnRemaining = 0; hasAngle = false;
            if (operatorHand) operatorHand.Release();
            if (!wasManipulating) return;
            releasedFrame = Time.frameCount;
            bool lockLook = capture && returnToLook;
            Cursor.lockState = lockLook ? CursorLockMode.Locked : CursorLockMode.None; FactoryVolumeCursor.SetSystemVisible(!lockLook);
        }
        public void BeginExecution()
        {
            Initialize();EndGrip(false);ExecutionControlled=true;
            GuardClosed=false;GuardBlend=0;
            sheet.gameObject.SetActive(false);cutPart.gameObject.SetActive(false);
            if(guardClip) guardClip.SampleAnimation(guardModel.gameObject,0);
            if(guardHit) {guardHit.transform.localPosition=new Vector3(-.05f,.45f,-.05f);guardHit.size=new Vector3(.9f,.15f,.95f);}
            SetExecutionPose(0,0);
        }
        public void SetExecutionPose(float leverTravel,float crush)
        {
            PressMotion.localRotation=pressLeverHome*Quaternion.Euler(-58*Mathf.Clamp01(leverTravel),0,0);
            press.localPosition=pressHome+Vector3.down*(.65f*Mathf.Clamp01(crush));
        }
        void BuildCutPart()
        {
            foreach (Transform child in cutPart) Destroy(child.gameObject);
            FactoryGeometry.GearVisual(cutPart, FactoryRecipe.ForType(CutType, false), Game.gearMeshes, Game.hubMeshes, Game.gearPaper, Game.gearEdge, Game.gearInk);
        }
        void Clunk(bool heavy) { if (sound) sound.Clunk(heavy); }
        public void Refresh()
        {
            if (!initialized) return;
            sheet.gameObject.SetActive(Stage == StationStage.Dropping || Stage == StationStage.Conveying || Stage == StationStage.Loaded || Stage == StationStage.Cutting);
            cutPart.gameObject.SetActive(Stage == StationStage.Cut);
            if (Stage == StationStage.Loaded || Stage == StationStage.Cutting) sheet.localPosition = cuttingPosition;
            sheet.localRotation = Quaternion.Euler(0, -wheelAngle, 0);
            cutPart.localRotation = Quaternion.Euler(90, 0, wheelAngle);
            press.localPosition = pressHome + Vector3.down * (pressBlend * .65f);
            if (guardClip && guardModel) guardClip.SampleAnimation(guardModel.gameObject, GuardBlend * guardClip.length);
            if (guardHit)
            {
                guardHit.transform.localPosition = Vector3.Lerp(new Vector3(-.05f,.45f,-.05f),new Vector3(-.05f,1.18f,-.48f),GuardBlend);
                guardHit.size = Vector3.Lerp(new Vector3(.9f,.15f,.95f),new Vector3(.83f,1.1f,.25f),GuardBlend);
            }
            FeedMotion.localRotation = feedHome * Quaternion.Euler(LeverAnimating && activeLever == StationPart.Feed ? -58 * LeverTravel : 0, 0, 0);
            PressMotion.localRotation = pressLeverHome * Quaternion.Euler(LeverAnimating && activeLever == StationPart.Press ? -58 * LeverTravel : 0, 0, 0);
            selector.localRotation = selectorHome * Quaternion.Euler(0, 0, (SelectedType - 1) * 120);
            wheel.localRotation = Quaternion.AngleAxis(-wheelAngle, wheelAxis) * wheelHome;
            if(selectorNumber) selectorNumber.text = SelectedType.ToString();
            pumpPlate.localPosition = pumpHome + Vector3.down * landingPulse * .045f;
            pressureNeedle.localRotation = Quaternion.Euler(0, 0, 65 - Pressure * 130);
            for (int i = 0; i < pressureLamps.Length; i++) pressureLamps[i].sharedMaterial = Pressure >= (i + 1) * .25f ? lampOn : lampOff;
            progressFill.localScale = new Vector3(Mathf.Max(.001f, 1.1f * CutProgress), .05f, .025f);
            progressFill.localPosition = progressOrigin + progressFill.localRotation*Vector3.right * (1.1f * (CutProgress - 1) * .5f);
            if (operatorHand) operatorHand.SyncPose();
        }
        void OnApplicationFocus(bool focused) { if (!focused && Manipulating) EndGrip(false); }
        void OnDisable() { EndGrip(false); }
    }
}
