using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class FactoryKitchen : MonoBehaviour
    {
        public const float AmountPerIngredient = 7f;
        public const int BowlsPerPot = 10;
        const float BoilDuration = 30f;
        public CardboardFactoryGame game;
        public FactoryKitchenTool board, pan;
        public Transform knife, peeler, pot, broth, lid;
        public Transform[] knifeDashes;
        public Renderer burnerLamp;
        public ParticleSystem scorchSmoke;
        public GameObject traySoup, trayDust;
        public Mesh ratModel, potatoModel;
        public Material[] ratMaterials, potatoMaterials;
        public Material potatoPaper;
        public AudioSource sizzle;
        public int RatStock { get; private set; }
        public int Portions { get; private set; }
        public float RatInPot { get; private set; }
        public float PotatoInPot { get; private set; }
        public bool Preparing { get; private set; }
        public Vector3 PrepCameraPosition => board.transform.TransformPoint(new Vector3(0, .79f, -.70f));
        public Quaternion PrepCameraRotation => Quaternion.LookRotation(PeelOrigin - PrepCameraPosition, board.transform.up);
        public FactoryKitchenService Service { get; private set; }
        public const float BladeHalfLength = .32f;

        readonly List<FactoryKitchenFood> pieces = new List<FactoryKitchenFood>();
        FactoryKitchenFood peelingPotato;
        FactoryKitchenUtensil kitchenKnife, spatula, ladle;
        FactoryKitchenTool stockpot;
        Transform soupBurnerSeat;
        Renderer soupCoals;
        Vector2 pointer;
        bool peelerPressed;
        int prepStartedFrame;
        bool burner, soupBurnerLit, charging, panHasCookedFood, panHasBurntFood;
        float boil, peelAngle, peelSoundAt;
        Vector3 PeelOrigin => board.transform.TransformPoint(Vector3.up * (.016f - peelingPotato.PeelBottom));
        Quaternion PeelRotation => board.transform.rotation * Quaternion.AngleAxis(peelAngle, Vector3.right);
        Vector2 flick;
        Vector3 peelerHome;
        Quaternion peelerRotation;
        float lidHeight;
        string hint;
        float hintUntil;
        GUIStyle promptStyle;
        AudioClip crunch, knifeTap;
        Mesh cuttableRat, cuttablePotato;

        FactoryPhysicsProp Held => game.objectCarry ? game.objectCarry.Held : null;
        FactoryKitchenFood HeldFood => Held ? Held.GetComponent<FactoryKitchenFood>() : null;
        FactoryKitchenTool HeldTool => Held ? Held.GetComponent<FactoryKitchenTool>() : null;
        FactoryKitchenTool HeldTransferTool => HeldTool == stockpot ? null : HeldTool;
        FactoryKitchenUtensil HeldUtensil => Held ? Held.GetComponent<FactoryKitchenUtensil>() : null;
        internal bool KnifeHeld => kitchenKnife && HeldUtensil == kitchenKnife;
        bool HasIngredient(bool rat) => (rat ? RatInPot : PotatoInPot) >= AmountPerIngredient - .0001f &&
            Service.IngredientPieces(rat) >= BowlsPerPot;
        bool PotFull => HasIngredient(true) && HasIngredient(false);
        bool PotOnBurner
        {
            get
            {
                var body = stockpot.Prop.Body;
                return !stockpot.Prop.IsHeld && (body.position - soupBurnerSeat.position).sqrMagnitude < .16f * .16f &&
                    Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > .95f && body.linearVelocity.sqrMagnitude < .08f;
            }
        }
        bool HeatingSoup => soupBurnerLit && PotOnBurner;
        bool Active => game && game.State == FactoryState.Working &&
            (!game.MenuActive || game.MultiplayerPrototype && game.CoopKitchenAuthority) && !game.IntroActive &&
            !game.CaptureActive && !game.EndingBusy && !game.FilmActive;
        internal bool ToolsActive => Active && (game.MultiplayerPrototype || !game.PlayerBlocked && !CardboardMarker.DrawingMode);
        internal bool CoopHolding => Held;
        internal string CoopHint => Time.time <= hintUntil ? hint : "";
        internal float CoopPeel => Preparing && peelingPotato ? peelingPotato.PeelProgress : 0;
        internal Vector2 CoopPeelPointer => pointer;
        internal bool CoopPeelerPressed => peelerPressed;
        internal void ApplyCoopCounters(FactoryCoopKitchenState state)
        {
            RatStock = state.Stock;
            Portions = state.Portions;
            RatInPot = state.RatMass;
            PotatoInPot = state.PotatoMass;
        }

        void Awake()
        {
            peelerHome = peeler.localPosition;
            peelerRotation = peeler.localRotation;
            lidHeight = lid.localPosition.y;
            crunch = MakePaperSound("Knife through cardboard", .16f, false);
            knifeTap = MakePaperSound("Knife hits the board", .075f, false);
            sizzle.clip = MakePaperSound("Cardboard frying", 1.2f, true);
            cuttableRat = SizeForKitchen(ratModel);
            cuttablePotato = SizeForKitchen(potatoModel);
            SetUpStockpot();
            SetUpUtensils();
            Service = gameObject.AddComponent<FactoryKitchenService>();
            Service.SetUp(this);
            Service.SetUpLadle(ladle);
        }

        void SetUpStockpot()
        {
            var rest = CardboardArt.Group("Stockpot rest", transform, pot.localPosition);
            rest.rotation = pot.rotation;
            var body = pot.gameObject.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            pot.gameObject.AddComponent<FactoryPhysicsProp>().itemName = "stockpot";
            stockpot = pot.gameObject.AddComponent<FactoryKitchenTool>();
            stockpot.Stockpot = true;
            stockpot.home = rest;
            stockpot.Dock();

            Material stove = sizzle.GetComponent<Renderer>().sharedMaterial;
            var stovePosition = rest.localPosition + new Vector3(-.92f, .038f, 0);
            var hob = StockpotFixture("Soup burner", stovePosition, new Vector3(.79f, .145f, .83f), stove, KitchenAction.SoupBurner);
            soupBurnerSeat = CardboardArt.Group("Stockpot seat", transform, stovePosition + Vector3.up * .10f);
            soupBurnerSeat.rotation = rest.rotation;
            var coal = Instantiate(burnerLamp.gameObject, transform);
            coal.name = "Soup burner coals";
            coal.transform.localPosition = stovePosition + Vector3.up * .077f;
            soupCoals = coal.GetComponent<Renderer>();
            soupCoals.enabled = false;
            var knob = CardboardArt.Box("Soup burner knob", hob, new Vector3(0, -.10f, -.54f),
                new Vector3(.18f, .37f, .13f), burnerLamp.sharedMaterial);
            knob.gameObject.AddComponent<BoxCollider>();
            StockpotFixture("Stockpot trivet", rest.localPosition - Vector3.up * .034f, new Vector3(.73f, .025f, .73f),
                stove, KitchenAction.PotRest);
        }

        Transform StockpotFixture(string name, Vector3 position, Vector3 scale, Material material, KitchenAction action)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = name;
            model.transform.SetParent(transform, false);
            model.transform.localPosition = position;
            model.transform.localScale = scale;
            model.GetComponent<Renderer>().sharedMaterial = material;
            var control = model.AddComponent<FactoryKitchenControl>();
            control.kitchen = this;
            control.action = action;
            model.AddComponent<FactoryInteractable>().action = FactoryAction.Kitchen;
            return model.transform;
        }

        static Mesh SizeForKitchen(Mesh source)
        {
            var mesh = Instantiate(source);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] *= .64f;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        public void ResetKitchen()
        {
            foreach (var piece in pieces) if (piece) RemoveObject(piece);
            pieces.Clear();
            RatStock = Portions = 0;
            RatInPot = PotatoInPot = 0;
            burner = soupBurnerLit = false;
            boil = hintUntil = 0;
            sizzle.Stop();
            EndPrep();
            CancelFlick();
            board.ResetTool();
            pan.ResetTool();
            stockpot.ResetTool();
            kitchenKnife.Rest();
            spatula.Rest();
            ladle.Rest();
            Service.ResetService();
            if (traySoup) traySoup.SetActive(false);
            RefreshCookingFeedback(false, false, false);
        }

        public void ReceiveRats(int count)
        {
            RatStock += count;
            ShowHint("BASKET IN. TAKE A RAT AT THE PREP BENCH.");
        }

        void SetUpUtensils()
        {
            var handles = GetComponentsInChildren<FactoryKitchenControl>(true);
            Transform ladleRest = System.Array.Find(handles, control => control.action == KitchenAction.Serve).transform;
            var spatulaHead = transform.Find("Spatula head");
            var spatulaHandle = transform.Find("Spatula handle");
            var ladleCup = ladleRest.Find("Ladle bowl");
            var ladleHandle = ladleRest.Find("Wood ladle grip");
            var spatulaModel = AssembleUtensil("Frying spatula", spatulaHead, spatulaHandle);
            var ladleModel = AssembleUtensil("Soup ladle", ladleCup, ladleHandle);
            var sleeve = System.Array.Find(handles, control => control.action == KitchenAction.Prep)
                .GetComponentInChildren<Renderer>().sharedMaterial;
            kitchenKnife = FactoryKitchenUtensil.Create(this, FactoryKitchenUtensil.Kind.Knife, knife,
                new Vector3(0, .036f, -.003f), new Vector3(.017f, .061f, .635f), Vector3.zero, potatoPaper, sleeve);
            spatula = FactoryKitchenUtensil.Create(this, FactoryKitchenUtensil.Kind.Spatula, spatulaModel,
                new Vector3(0, 0, .14f), new Vector3(.119f, .012f, .126f), Vector3.forward * .14f, potatoPaper, sleeve);
            ladle = FactoryKitchenUtensil.Create(this, FactoryKitchenUtensil.Kind.Ladle, ladleModel,
                new Vector3(0, -.022f, .14f), new Vector3(.16f, .04f, .16f), Vector3.forward * .14f, potatoPaper, sleeve);
            foreach (var shape in pan.GetComponentsInChildren<Collider>()) shape.contactOffset = .0015f;
            foreach (var shape in board.GetComponentsInChildren<Collider>()) shape.contactOffset = .0015f;
            if (knifeDashes != null)
                foreach (var dash in knifeDashes) if (dash) dash.gameObject.SetActive(false);
        }

        Transform AssembleUtensil(string name, Transform head, Transform handle)
        {
            var root = CardboardArt.Group(name, transform, Vector3.zero);
            Vector3 axis = Vector3.ProjectOnPlane(head.position - handle.position, Vector3.up);
            root.SetPositionAndRotation(head.position - axis.normalized * .14f, Quaternion.LookRotation(axis));
            head.SetParent(root, false);
            head.SetLocalPositionAndRotation(Vector3.forward * .14f, Quaternion.identity);
            handle.SetParent(root, false);
            handle.SetLocalPositionAndRotation(Vector3.back * .041f, Quaternion.identity);
            return root;
        }

        internal bool ReadUtensilInput(Mouse mouse, Vector2 delta, float sensitivity)
        {
            if (!ToolsActive || !HeldUtensil) return false;
            if (HeldUtensil != ladle) return HeldUtensil.ReadInput(mouse, delta, sensitivity);
            if (mouse.leftButton.wasPressedThisFrame) UseLadle(game.ActionLens);
            return false;
        }

        internal bool UseLadle(Transform view)
        {
            if (HeldUtensil != ladle || Service.LadleBusy) return false;
            var target = game.TargetForRay(new Ray(view.position, view.forward));
            if (target && target.TryGetComponent(out FactoryKitchenControl control))
            {
                if (Service.HasScoop && control.action == KitchenAction.Bowl && Service.StartPour(control.slot, ladle, view.eulerAngles.y))
                    return true;
                if (!Service.HasScoop && control.action == KitchenAction.Pot && Portions > 0 &&
                    Vector3.Dot(pot.up, Vector3.up) > .90f && Service.StartScoop(ladle, view.eulerAngles.y))
                    return true;
            }
            return ShowHint(Service.HasScoop ? "AIM AT AN EMPTY BOWL AND PRESS LMB." : "AIM AT THE COOKED SOUP AND PRESS LMB.");
        }

        internal bool ReturnUtensil()
        {
            if (!HeldUtensil) return false;
            if (HeldUtensil == ladle && Service.LadleBusy) return true;
            game.objectCarry.Release(false);
            return true;
        }

        internal float SpatulaLevel => pan.Prop.Body.position.y + .0285f;

        public bool OwnsHeld(FactoryPhysicsProp prop) => prop &&
            (prop == board.Prop || prop == pan.Prop || prop == stockpot.Prop || prop.GetComponent<FactoryKitchenFood>() ||
             prop.GetComponent<FactoryKitchenUtensil>() || Service && Service.OwnsBowl(prop));

        internal bool IgnoreHeldCollider(Collider shape)
        {
            if (!OwnsHeld(Held)) return false;
            if (shape.transform.IsChildOf(Held.transform)) return true;
            var tool = HeldTransferTool;
            var piece = shape.GetComponentInParent<FactoryKitchenFood>();
            return tool && piece && (AvailableOn(tool, piece) || tool == board && AvailableOn(pan, piece));
        }

        public bool AcceptsHeld(KitchenAction action)
        {
            switch (action)
            {
                case KitchenAction.Prep: return Held == board.Prop || HeldFood;
                case KitchenAction.Board: return HeldFood;
                case KitchenAction.Pan: return Held == board.Prop;
                case KitchenAction.Burner: return Held == pan.Prop || Held == board.Prop || HeldFood;
                case KitchenAction.Pot: return HeldTransferTool || HeldFood || HeldUtensil == ladle;
                case KitchenAction.SoupBurner:
                case KitchenAction.PotRest: return Held == stockpot.Prop;
                case KitchenAction.Bowl: return HeldUtensil == ladle;
                case KitchenAction.Bin: return HeldFood;
                case KitchenAction.Tray: return Service.OwnsBowl(Held);
                default: return false;
            }
        }

        public string Prompt(KitchenAction action, int slot = 0)
        {
            switch (action)
            {
                case KitchenAction.Rat: return RatStock > 0 ? "E - LAY OUT RAT  /  " + RatStock + " IN STOCK" : "WAITING FOR THE DIVER'S BASKET";
                case KitchenAction.Potato: return "E - TAKE A POTATO";
                case KitchenAction.Knife: return "E - GRIP KNIFE";
                case KitchenAction.Board: return HeldFood ? BoardFoodPrompt : "E - PICK UP CHOPPING BOARD";
                case KitchenAction.Prep: return HeldFood ? BoardFoodPrompt : "E - PUT BOARD ON PREP BENCH";
                case KitchenAction.Pan: return Held == board.Prop ? "E - TIP BOARD INTO PAN" :
                    panHasCookedFood ? "E - PICK UP FRYING PAN  /  RAT READY" : "E - PICK UP FRYING PAN";
                case KitchenAction.Burner:
                    if (Held == pan.Prop) return "E - SET PAN ON BURNER";
                    if (Held == board.Prop) return "E - TIP BOARD INTO PAN";
                    if (HeldFood) return "E - DROP PIECE INTO PAN";
                    if (!burner) return "E - LIGHT BURNER";
                    return panHasBurntFood ? "E - TURN BURNER OFF  /  FOOD BURNT" :
                        panHasCookedFood ? "E - TURN BURNER OFF  /  RAT READY" : "E - TURN BURNER OFF";
                case KitchenAction.Spatula: return "E - TAKE SPATULA";
                case KitchenAction.Pot: return HeldUtensil == ladle ? HeldPrompt(Held) :
                    Held ? "E - TIP INTO STOCKPOT" : "E - PICK UP STOCKPOT  /  " + PotStatus;
                case KitchenAction.SoupBurner:
                    if (Held == stockpot.Prop) return "E - SET STOCKPOT ON BURNER";
                    return soupBurnerLit ? "E - TURN SOUP BURNER OFF  /  " + PotStatus : "E - LIGHT SOUP BURNER";
                case KitchenAction.PotRest: return Held == stockpot.Prop ? "E - PUT STOCKPOT ON TRIVET" : "";
                case KitchenAction.Serve: return Portions > 0 ? "E - TAKE LADLE  /  " + Portions + " SCOOPS LEFT" : "LADLE READY WHEN SOUP IS DONE";
                case KitchenAction.Bin: return "E - DISCARD HELD PIECE";
                case KitchenAction.Bowl: return HeldUtensil == ladle ? HeldPrompt(Held) : Service.BowlPrompt(slot);
                case KitchenAction.Tray: return Service.TrayPrompt(slot, Held);
                default: return "";
            }
        }

        public bool Use(KitchenAction action, int slot = 0)
        {
            if (!Active || game.HasPart || game.HandsBusy && !AcceptsHeld(action)) return false;
            switch (action)
            {
                case KitchenAction.Rat: return LoadRat();
                case KitchenAction.Potato: return TakePotato();
                case KitchenAction.Knife: return kitchenKnife.Take(game);
                case KitchenAction.Board:
                    if (HeldFood) return LayHeldOnBoard();
                    return board.Prop.Take(game);
                case KitchenAction.Prep:
                    if (HeldFood) return LayHeldOnBoard();
                    if (Held != board.Prop) return false;
                    SetDown(board); return true;
                case KitchenAction.Pan:
                    if (Held == board.Prop) return TipIntoPan();
                    return pan.Prop.Take(game);
                case KitchenAction.Burner:
                    if (Held == pan.Prop) SetDown(pan);
                    else if (Held == board.Prop)
                        return pan.ReadyAtStation ? TipIntoPan() : ShowHint("SET THE PAN ON THE BURNER FIRST.");
                    else if (HeldFood) return DropHeldIntoPan();
                    else burner = !burner;
                    return true;
                case KitchenAction.Spatula: return spatula.Take(game);
                case KitchenAction.Pot: return Held ? TipIntoPot() : stockpot.Prop.Take(game);
                case KitchenAction.SoupBurner:
                    if (Held == stockpot.Prop) return PutStockpot(soupBurnerSeat);
                    soupBurnerLit = !soupBurnerLit;
                    return true;
                case KitchenAction.PotRest: return Held == stockpot.Prop && PutStockpot(stockpot.home);
                case KitchenAction.Serve: return (Portions > 0 || Service.HasScoop) && ladle.Take(game);
                case KitchenAction.Bowl: return Service.TakeBowl(slot);
                case KitchenAction.Tray: return Service.SetBowlOnTray(slot, Held);
                case KitchenAction.Bin:
                    if (!HeldFood) return false;
                    var discarded = HeldFood;
                    game.objectCarry.Release(false);
                    pieces.Remove(discarded);
                    RemoveObject(discarded);
                    return true;
                default: return false;
            }
        }

        string BoardFoodPrompt => HeldFood.Peeled ? "E - SET INGREDIENT ON THE BOARD" : "E - PEEL POTATO ON BOARD";

        bool PutStockpot(Transform stand)
        {
            game.objectCarry.Release(false);
            stockpot.Dock(stand);
            return true;
        }

        bool LoadRat()
        {
            if (RatStock == 0) return ShowHint("THE DIVER NEEDS TO DELIVER A FULL BASKET.");
            if (!board.ReadyAtStation) return ShowHint("SET THE CHOPPING BOARD DOWN NEAR THE PREP BENCH.");
            if (pieces.Exists(piece => AvailableOn(board, piece))) return ShowHint("CLEAR THE CHOPPING BOARD FIRST.");
            RatStock--;
            var cuttingBoard = board.transform;
            var position = cuttingBoard.TransformPoint(Vector3.up * (.034f - cuttableRat.bounds.min.y));
            SpawnPiece(true, cuttableRat, ratMaterials, 1, position, cuttingBoard.rotation, null);
            ShowHint("PICK UP THE KNIFE.");
            return true;
        }

        bool TakePotato()
        {
            var eyes = game.ActionLens;
            if (!eyes) return false;
            var potato = SpawnPiece(false, cuttablePotato, potatoMaterials, 1,
                eyes.position + eyes.forward * .68f - Vector3.up * .18f, Quaternion.identity, null);
            if (potato.Prop.Take(game))
            {
                ShowHint("PUT THE POTATO ON THE CHOPPING BOARD TO PEEL IT.");
                return true;
            }
            pieces.Remove(potato);
            RemoveObject(potato);
            return false;
        }

        bool LayHeldOnBoard()
        {
            if (!board.ReadyAtStation) return ShowHint("SET THE BOARD DOWN NEAR THE PREP BENCH FIRST.");
            var food = HeldFood;
            if (!food) return false;
            if (pieces.Exists(piece => AvailableOn(board, piece))) return ShowHint("CLEAR THE CHOPPING BOARD FIRST.");
            if (!food.Peeled)
            {
                game.objectCarry.Release(false);
                BeginPeeling(food);
                return true;
            }
            var body = food.Prop.Body;
            var cuttingBoard = board.transform;
            DropHeld(cuttingBoard.TransformPoint(Vector3.up * (.045f - food.Model.bounds.min.y)));
            body.rotation = cuttingBoard.rotation;
            body.transform.rotation = cuttingBoard.rotation;
            return true;
        }

        internal bool CanResumePeeling(FactoryKitchenFood potato) => !potato.Peeled && board.ReadyAtStation && AvailableOn(board, potato);

        internal bool ResumePeeling(FactoryKitchenFood potato)
        {
            if (!ToolsActive || game.HasPart || game.HandsBusy || !CanResumePeeling(potato)) return false;
            if (pieces.Exists(piece => piece != potato && AvailableOn(board, piece)))
            {
                ShowHint("CLEAR THE CHOPPING BOARD FIRST.");
                return true;
            }
            BeginPeeling(potato);
            return true;
        }

        void BeginPeeling(FactoryKitchenFood potato)
        {
            peelingPotato = potato;
            peelAngle = 0;
            potato.TurnForPeeling(peelAngle, potatoPaper);
            potato.RestForPeeling(PeelOrigin, PeelRotation);
            Preparing = true;
            prepStartedFrame = Time.frameCount;
            pointer = new Vector2(0, .13f);
            peelSoundAt = float.NegativeInfinity;
            PosePeeler(false);
        }

        public void EndPrep()
        {
            if (peelingPotato) peelingPotato.ReleaseFromPeeling();
            peelingPotato = null;
            Preparing = false;
            peeler.SetLocalPositionAndRotation(peelerHome, peelerRotation);
        }

        public bool ConsumesMouse(Mouse mouse) => Active && !CardboardMarker.DrawingMode &&
            (Preparing || HeldTransferTool && (charging || mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame));

        public void HandleMouse(Mouse mouse, Vector2 delta)
            => HandleToolInput(delta, mouse.scroll.ReadValue().y, mouse.leftButton.isPressed,
                mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame,
                mouse.rightButton.wasPressedThisFrame, Keyboard.current?.eKey.wasPressedThisFrame == true);

        void HandleToolInput(Vector2 delta, float scroll, bool primary, bool pressed, bool released, bool secondary, bool interact)
        {
            if (Preparing)
            {
                if (secondary || Time.frameCount != prepStartedFrame && interact)
                { EndPrep(); return; }
                if (Mathf.Abs(scroll) > .01f) TurnPotato(Mathf.Sign(scroll) * 30f);
                MovePrepTool(delta, primary);
                return;
            }
            if (secondary) { CancelFlick(); return; }
            if (pressed) { charging = true; flick = Vector2.zero; }
            if (!charging) return;
            flick = Vector2.ClampMagnitude(flick + delta, 175f);
            HeldTool.Pitch = Mathf.Clamp(flick.y * .10f, -8f, 12f);
            if (released)
            {
                Toss(HeldTool, flick);
                CancelFlick();
            }
        }

        internal void CoopInput(FactoryCoopCommand command)
        {
            if (!Active) return;
            bool primary = (command.Buttons & FactoryCoopControls.Primary) != 0;
            bool secondary = (command.Buttons & FactoryCoopControls.Secondary) != 0;
            if (Preparing || HeldTransferTool)
            {
                HandleToolInput(command.PointerDelta, command.Amount, primary, command.Pressed, command.Released, command.SecondaryPressed, false);
                if (!Preparing && command.SecondaryPressed) game.objectCarry.Release(false);
            }
            else if (HeldUtensil)
            {
                if (HeldUtensil == ladle && command.Pressed) UseLadle(game.ActionLens);
                else HeldUtensil.ReadInput(primary, secondary, command.Amount, command.PointerDelta, .10f);
                if (command.SecondaryPressed) ReturnUtensil();
            }
            else if (Held && (command.Pressed || command.SecondaryPressed))
                game.objectCarry.TickInput(command.Pressed, command.SecondaryPressed);
        }

        public void CancelFlick()
        {
            charging = false;
            flick = Vector2.zero;
            if (HeldTool) HeldTool.Pitch = 0;
        }

        public void MovePrepTool(Vector2 pixels, bool pressed)
        {
            if (!Preparing) return;
            Vector2 previous = pointer;
            pointer = MovePeelPointer(pointer, pixels);
            if (pressed) Peel(previous, pointer);
            if (Preparing) PosePeeler(pressed);
        }

        internal static Vector2 MovePeelPointer(Vector2 current, Vector2 pixels)
        {
            var next = current + pixels * .0026f;
            return new Vector2(Mathf.Clamp(next.x, -.23f, .23f), Mathf.Clamp(next.y, -.14f, .14f));
        }

        void PosePeeler(bool pressed)
        {
            peelerPressed = pressed;
            float height = peelingPotato.PeelHeight(pointer) + (pressed ? .001f : .064f);
            var rotation = board.transform.rotation;
            peeler.SetPositionAndRotation(PeelOrigin + rotation * new Vector3(pointer.x, height - .018f, pointer.y - .035f), rotation);
        }

        internal bool TryCut(FactoryKitchenFood parent, Vector3 center, Vector3 edge)
        {
            if (!parent.Peeled || parent.Burnt || !pieces.Contains(parent) || !AvailableOn(board, parent)) return false;
            var body = parent.Prop.Body;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            Vector3 blade = Vector3.ProjectOnPlane(edge, Vector3.up).normalized;
            Vector3 normal = inverse * Vector3.Cross(Vector3.up, blade);
            if (!FactoryKitchenMeshCut.TrySplit(parent.Model, inverse * (center - body.position), normal,
                inverse * blade, BladeHalfLength, out var first, out var second)) return false;
            float firstVolume = FactoryKitchenMeshCut.Volume(first);
            float secondVolume = FactoryKitchenMeshCut.Volume(second);
            if (firstVolume < 2e-9f || secondVolume < 2e-9f)
            {
                Destroy(first);
                Destroy(second);
                return false;
            }
            float firstAmount = parent.Amount * firstVolume / (firstVolume + secondVolume);
            SpawnCutPiece(parent, first, firstAmount, normal * .002f);
            SpawnCutPiece(parent, second, parent.Amount - firstAmount, -normal * .002f);
            pieces.Remove(parent);
            RemoveObject(parent);
            PlayCrunch();
            return true;
        }

        void Peel(Vector2 from, Vector2 to)
        {
            if (!peelingPotato.ScrapePeel(from, to, potatoPaper)) return;
            if (crunch && Time.time >= peelSoundAt)
            {
                AudioSource.PlayClipAtPoint(crunch, board.transform.position, .32f);
                peelSoundAt = Time.time + .11f;
            }
            if (!peelingPotato.Peeled) return;
            EndPrep();
            ShowHint("POTATO PEELED. READY TO SLICE.");
        }

        void TurnPotato(float step)
        {
            peelAngle = Mathf.Repeat(peelAngle + step, 360f);
            peelingPotato.TurnForPeeling(peelAngle, potatoPaper);
            peelingPotato.MoveWhilePeeling(PeelOrigin, PeelRotation);
        }

        void SpawnCutPiece(FactoryKitchenFood parent, Mesh model, float amount, Vector3 separation)
        {
            Vector3 center = model.bounds.center;
            var vertices = model.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] -= center;
            model.vertices = vertices;
            model.RecalculateBounds();
            var body = parent.Prop.Body;
            Vector3 world = body.position + body.rotation * (center + separation);
            var piece = SpawnPiece(parent.Rat, model, parent.Materials, amount, world, body.rotation, parent);
            piece.Prop.Body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, .6f) + body.rotation * separation * 45f;
        }

        FactoryKitchenFood SpawnPiece(bool rat, Mesh model, Material[] materials, float amount,
            Vector3 position, Quaternion rotation, FactoryKitchenFood source)
        {
            var node = new GameObject(rat ? "Rat piece" : "Potato piece");
            node.transform.SetParent(transform);
            node.transform.SetPositionAndRotation(position, rotation);
            node.AddComponent<MeshFilter>().sharedMesh = model;
            node.AddComponent<MeshRenderer>().sharedMaterials = materials;
            var collisionMesh = FactoryKitchenMeshCut.CreateCollisionMesh(model);
            if (collisionMesh)
            {
                var shape = node.AddComponent<MeshCollider>();
                shape.convex = true;
                shape.sharedMesh = collisionMesh;
                shape.contactOffset = .0015f;
            }
            else
            {
                var box = node.AddComponent<BoxCollider>();
                box.center = model.bounds.center;
                box.size = Vector3.Max(model.bounds.size, Vector3.one * .003f);
                box.contactOffset = .0015f;
            }
            var body = node.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(.008f, .24f * amount);
            node.AddComponent<FactoryPhysicsProp>().itemName = node.name;
            var food = node.AddComponent<FactoryKitchenFood>();
            food.Setup(rat, amount, model, materials, source);
            var interactable = node.AddComponent<FactoryInteractable>();
            interactable.action = FactoryAction.PhysicsProp;
            pieces.Add(food);
            return food;
        }

        public int Toss(FactoryKitchenTool tool, Vector2 gesture)
        {
            float strength = Mathf.Clamp01(gesture.y / 130f);
            if (strength < .13f) return 0;
            Vector3 forward = tool.transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 across = Vector3.Cross(Vector3.up, forward);
            Vector3 velocity = forward * Mathf.Lerp(.52f, 3.95f, strength) + Vector3.up * Mathf.Lerp(1.30f, 3.12f, strength) +
                across * Mathf.Clamp(gesture.x * .017f, -1.5f, 1.5f);
            int launched = 0;
            foreach (var piece in pieces)
            {
                if (!AvailableOn(tool, piece)) continue;
                Launch(piece, velocity + across * (tool.transform.InverseTransformPoint(piece.Prop.Body.position).x * .55f));
                launched++;
            }
            if (launched > 0) tool.Kick();
            return launched;
        }

        bool TipIntoPot()
        {
            if (!Held) return ShowHint(PotStatus);
            if (HeldFood)
            {
                if (!HeldFood.ReadyForPot) return ShowHint(HeldFood.Burnt ? "THAT PIECE IS BURNT. USE THE SCRAP BIN." :
                    !HeldFood.Sliced ? "SLICE THE INGREDIENT FIRST." : "FRY THE RAT UNTIL IT SHOWS READY.");
                if (!HeldFood.FitsLadle) return ShowHint("CUT THIS PIECE SMALLER SO IT FITS IN THE LADLE.");
                DropHeld(pot.position + Vector3.up * .44f);
                return true;
            }
            var tool = HeldTransferTool;
            return tool && TipBetween(tool, pot.position + pot.up * .37f, true);
        }

        bool TipIntoPan()
        {
            if (Vector3.Dot(pan.Prop.Body.rotation * Vector3.up, Vector3.up) < .72f)
                return ShowHint("SET THE PAN UPRIGHT FIRST.");
            return TipBetween(board, pan.Prop.Body.position + Vector3.up * .17f, false);
        }

        bool TipBetween(FactoryKitchenTool tool, Vector3 destination, bool intoPot)
        {
            int launched = 0;
            bool tooLarge = false, stillRaw = false;
            foreach (var piece in pieces)
            {
                if (!AvailableOn(tool, piece) || piece.Burnt) continue;
                if (intoPot && !piece.ReadyForPot) { stillRaw = true; continue; }
                if (intoPot && !piece.FitsLadle) { tooLarge = true; continue; }
                float spacing = Mathf.Clamp(tool.transform.InverseTransformPoint(piece.Prop.Body.position).x * .58f, -.13f, .13f);
                // The lip of the tilted vessel lifts its contents before they leave it.
                Vector3 releasePoint = piece.Prop.Body.position + Vector3.up * (tool.pan ? .20f : .12f);
                piece.transform.position = releasePoint;
                piece.Prop.Body.position = releasePoint;
                Vector3 displacement = destination + tool.transform.right * spacing - releasePoint;
                float distance = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude;
                float flight = Mathf.Clamp(.40f + distance * .17f, .45f, .85f);
                Launch(piece, displacement / flight - .5f * Physics.gravity * flight);
                launched++;
            }
            if (launched > 0) tool.Kick();
            if (tooLarge) ShowHint("CUT LARGE PIECES SMALLER SO THEY FIT IN THE LADLE.");
            if (stillRaw && !tooLarge) ShowHint("COOK THE RAW PIECES BEFORE ADDING THEM TO THE POT.");
            if (tooLarge || stillRaw) return launched > 0;
            return launched > 0 || ShowHint("GET THE CHOPPED INGREDIENTS ON THE BOARD OR PAN FIRST.");
        }

        bool DropHeldIntoPan()
        {
            if (!pan.ReadyAtStation) return ShowHint("SET THE PAN DOWN NEAR THE BURNER FIRST.");
            Vector3 landing = pan.transform.position;
            float bestClearance = -1;
            for (int slot = 0; slot <= 6; slot++)
            {
                Vector3 candidate = slot == 6 ? pan.transform.position : pan.transform.TransformPoint(
                    Quaternion.Euler(0, slot * 60f, 0) * Vector3.forward * .157f);
                float clearance = 1;
                foreach (var piece in pieces)
                {
                    if (!AvailableOn(pan, piece)) continue;
                    Vector3 separation = piece.Prop.Body.position - candidate;
                    clearance = Mathf.Min(clearance, separation.x * separation.x + separation.z * separation.z);
                }
                if (clearance <= bestClearance) continue;
                landing = candidate;
                bestClearance = clearance;
            }
            DropHeld(landing + Vector3.up * .19f);
            return true;
        }

        void DropHeld(Vector3 position)
        {
            var body = Held.Body;
            game.objectCarry.Release(false);
            body.transform.position = position;
            body.position = position;
            body.angularVelocity = body.linearVelocity = Vector3.zero;
        }

        static void Launch(FactoryKitchenFood piece, Vector3 velocity)
        {
            piece.LeaveSurface();
            var body = piece.Prop.Body;
            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(4f, 1f, -2f);
            body.WakeUp();
        }

        void SetDown(FactoryKitchenTool tool)
        {
            // Move the pieces with a tool as it is set into its stand, leaving them loose afterwards.
            foreach (var piece in pieces)
            {
                if (!AvailableOn(tool, piece)) continue;
                var body = piece.Prop.Body;
                if (Vector3.Dot(body.linearVelocity - tool.Prop.Body.linearVelocity, tool.transform.up) > .60f) continue;
                body.position = tool.home.TransformPoint(tool.transform.InverseTransformPoint(body.position));
                body.linearVelocity = body.angularVelocity = Vector3.zero;
            }
            game.objectCarry.Release(false);
            tool.Dock();
        }

        static bool AvailableOn(FactoryKitchenTool tool, FactoryKitchenFood piece) =>
            piece && !piece.Prop.IsHeld && tool.Contains(piece.Prop.Body.position);

        void FixedUpdate()
        {
            if (!Active) return;
            if (Preparing) peelingPotato.MoveWhilePeeling(PeelOrigin, PeelRotation);
            TickCooking(Time.fixedDeltaTime);
            Service.Tick(HeldUtensil == ladle ? ladle : null, Time.fixedDeltaTime);
        }

        public void TickCooking(float dt)
        {
            bool frying = false, cooked = false, scorched = false, justCooked = false;
            bool panHeating = burner && pan.ReadyAtStation;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (!piece || piece.Prop.Body.position.y < -5f)
                {
                    if (piece) RemoveObject(piece);
                    pieces.RemoveAt(i);
                    continue;
                }
                if (piece.Prop.IsHeld || piece.BeingPeeled) continue;
                piece.SettleOnBoard(board);
                if (AvailableOn(pan, piece) && piece.OnSurface(pan))
                {
                    bool wasFried = piece.Fried;
                    if (panHeating) piece.Fry(dt);
                    frying |= panHeating && !piece.Burnt;
                    cooked |= piece.Rat && piece.Fried;
                    justCooked |= piece.Rat && !wasFried && piece.Fried;
                    scorched |= piece.Burnt;
                }
                if (PotFull || !piece.ReadyForPot || !piece.FitsLadle) continue;
                Vector3 local = pot.InverseTransformPoint(piece.Prop.Body.position);
                if (new Vector2(local.x, local.z).sqrMagnitude > .26f * .26f || local.y < 0 || local.y > .25f) continue;
                if (piece.Rat)
                {
                    if (HasIngredient(true)) continue;
                    RatInPot += piece.Amount;
                }
                else
                {
                    if (HasIngredient(false)) continue;
                    PotatoInPot += piece.Amount;
                }
                pieces.RemoveAt(i);
                Service.AddIngredient(piece);
            }
            if (PotFull && HeatingSoup && !Service.BatchCooked)
            {
                boil += dt;
                if (boil >= BoilDuration)
                {
                    Portions = BowlsPerPot;
                    Service.FinishBoiling();
                    game.PlayCaptureSound("good");
                    ShowHint("SOUP'S UP. FILL TEN BOWLS WITH THE LADLE.");
                }
            }
            if (justCooked) game.PlayCaptureSound("good");
            RefreshCookingFeedback(frying, cooked, scorched);
        }

        internal void Scooped()
        {
            Portions--;
        }

        public string HeldPrompt(FactoryPhysicsProp prop)
        {
            if (KnifeHeld) return "";
            if (HeldUtensil == spatula) return "HOLD LMB - LOWER  /  HOLD RMB + MOVE MOUSE - TILT  /  E - RETURN";
            if (HeldUtensil == ladle) return Service.LadleBusy ? "SERVING SOUP" : Service.HasScoop
                ? "LMB AT AN EMPTY BOWL - POUR  /  E - RETURN"
                : "LMB AT THE STOCKPOT - SCOOP  /  E - RETURN";
            if (prop == stockpot.Prop) return "E AT SOUP BURNER - SET STOCKPOT DOWN  /  RMB - PUT DOWN";
            if (Service.OwnsBowl(prop)) return "E AT A TRAY - SET BOWL DOWN  /  RMB - PUT DOWN";
            return prop == board.Prop || prop == pan.Prop ? "HOLD LMB + FLICK FORWARD - TOSS CONTENTS  /  RMB - SET DOWN"
                : "LMB - THROW PIECE  /  RMB - SET DOWN";
        }

        string PotStatus
        {
            get
            {
                if (Portions > 0) return "SOUP READY  /  " + Portions + " BOWLS";
                if (Service.BatchCooked) return "ALL SOUP LADLED";
                if (PotFull)
                {
                    int remaining = Mathf.CeilToInt(Mathf.Max(0, BoilDuration - boil));
                    return !PotOnBurner ? $"SET ON SOUP BURNER  /  {remaining} S LEFT" :
                        !soupBurnerLit ? $"LIGHT SOUP BURNER  /  {remaining} S LEFT" : $"COOKING SOUP  /  {remaining} S";
                }
                if (RatInPot >= AmountPerIngredient - .0001f && !HasIngredient(true))
                    return $"ADD {BowlsPerPot - Service.IngredientPieces(true)} MORE RAT PIECES FOR TEN BOWLS";
                if (PotatoInPot >= AmountPerIngredient - .0001f && !HasIngredient(false))
                    return $"ADD {BowlsPerPot - Service.IngredientPieces(false)} MORE POTATO PIECES FOR TEN BOWLS";
                return $"STOCKPOT  /  FRIED RAT {Mathf.Min(RatInPot, AmountPerIngredient):0.##}/7   POTATO {Mathf.Min(PotatoInPot, AmountPerIngredient):0.##}/7";
            }
        }

        void RefreshCookingFeedback(bool frying, bool cooked, bool scorched)
        {
            panHasCookedFood = cooked;
            panHasBurntFood = scorched;
            burnerLamp.enabled = burner;
            soupCoals.enabled = soupBurnerLit;
            broth.gameObject.SetActive(Service.BatchCooked ? Portions > 0 : RatInPot > 0 || PotatoInPot > 0);
            var brothPosition = broth.localPosition;
            brothPosition.y = Service.BatchCooked ? Mathf.Lerp(.06f, .22f, Portions / (float)BowlsPerPot) : .20f;
            broth.localPosition = brothPosition;
            Vector3 lidPosition = lid.localPosition;
            lidPosition.y = lidHeight + (PotFull && HeatingSoup && !Service.BatchCooked ? Mathf.Abs(Mathf.Sin(Time.time * 24)) * .026f : 0);
            lid.localPosition = lidPosition;
            if (scorched && !scorchSmoke.isPlaying) scorchSmoke.Play();
            if (!scorched && scorchSmoke.isPlaying) scorchSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (frying || burner && scorched && pan.ReadyAtStation)
            {
                sizzle.pitch = scorched ? .73f : .93f;
                sizzle.volume = scorched ? .37f : .22f;
                if (!sizzle.isPlaying) sizzle.Play();
            }
            else if (sizzle.isPlaying) sizzle.Stop();
        }

        void Update()
        {
            if (Active && (game.MultiplayerPrototype || Application.isFocused)) return;
            if (Preparing) EndPrep();
            if (charging) CancelFlick();
            if (sizzle.isPlaying) sizzle.Stop();
        }

        void PlayCrunch() { if (crunch) AudioSource.PlayClipAtPoint(crunch, board.transform.position, .55f); }
        internal void PlayKnifeTap(Vector3 point) { if (knifeTap) AudioSource.PlayClipAtPoint(knifeTap, point, .45f); }

        static AudioClip MakePaperSound(string name, float seconds, bool hiss)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            var random = new System.Random(48);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float noise = (float)random.NextDouble() * 2 - 1;
                float time = i / (float)rate;
                filtered = Mathf.Lerp(filtered, noise, hiss ? .66f : .14f);
                float amplitude = hiss ? .32f + Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 118)), 26) * .44f :
                    Mathf.Min(1, time * 480) * (Mathf.Exp(-time * 30) + Mathf.Exp(-Mathf.Abs(time - .065f) * 54));
                samples[i] = filtered * amplitude * .68f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (crunch) Destroy(crunch);
            if (knifeTap) Destroy(knifeTap);
            if (cuttableRat) Destroy(cuttableRat);
            if (cuttablePotato) Destroy(cuttablePotato);
            if (sizzle && sizzle.clip) Destroy(sizzle.clip);
        }

        bool ShowHint(string message)
        {
            hint = message;
            hintUntil = Time.time + 3.5f;
            return false;
        }

        void OnGUI()
        {
            if (!game || game.MultiplayerPrototype || !Active || KnifeHeld || CardboardMarker.DrawingMode || !Camera.main ||
                !Preparing && Vector3.Distance(Camera.main.transform.position, transform.position) > 7f && !OwnsHeld(Held)) return;
            string prompt = Preparing ? $"LMB + MOVE - PEEL  /  WHEEL - TURN  /  {Mathf.FloorToInt(peelingPotato.PeelProgress * 100f)}%  /  RMB - LEAVE" : game.FocusPrompt;
            bool kitchenFocus = game.Focus && (game.Focus.action == FactoryAction.Kitchen || game.Focus.GetComponent<FactoryKitchenFood>() ||
                game.Focus.action == FactoryAction.Lunch);
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = CardboardTypography.DefaultFont };
                promptStyle.normal.textColor = new Color(1, .94f, .81f);
            }
            promptStyle.fontSize = Mathf.Clamp(Screen.height / 38, 15, 28);
            if (Preparing || kitchenFocus || OwnsHeld(Held))
                GUI.Label(new Rect(0, Preparing ? Screen.height * .79f : Screen.height * .54f, Screen.width, 40), prompt, promptStyle);
            if (!Preparing && !game.lunch.Carrying) GUI.Label(new Rect(Screen.width * .5f - 12, Screen.height * .5f - 14, 24, 28), "+", promptStyle);
            if (Time.time < hintUntil) GUI.Label(new Rect(0, Screen.height * .87f, Screen.width, 48), hint, promptStyle);
            if (!Preparing && Event.current.type == EventType.Repaint) DrawPanStatus(Camera.main);
        }

        void DrawPanStatus(Camera camera)
        {
            if ((camera.transform.position - pan.Prop.Body.position).sqrMagnitude > 9f) return;
            var previous = GUI.color;
            foreach (var piece in pieces)
            {
                if (!piece || !piece.Rat || !AvailableOn(pan, piece) || !piece.OnSurface(pan) ||
                    !burner && piece.FryProgress <= 0f) continue;
                var view = camera.WorldToViewportPoint(piece.transform.position + Vector3.up * (piece.Model.bounds.extents.y + .074f));
                if (view.z <= 0 || view.x <= 0 || view.x >= 1 || view.y <= 0 || view.y >= 1) continue;
                float width = Mathf.Clamp(Screen.height * .053f, 32f, 55f);
                var bar = new Rect(view.x * Screen.width - width * .5f, (1f - view.y) * Screen.height, width, 7f);
                GUI.color = new Color(.12f, .08f, .055f, .95f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = piece.Burnt ? new Color(.94f, .31f, .18f) : piece.Fried ? new Color(.53f, .87f, .39f) : new Color(1f, .71f, .31f);
                GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * piece.FryProgress, bar.height - 4), Texture2D.whiteTexture);
                if (piece.Fried || piece.Burnt)
                    GUI.Label(new Rect(bar.center.x - 46, bar.y - 24, 92, 24), piece.Fried ? "READY" : "BURNT", promptStyle);
            }
            GUI.color = previous;
        }

        static void RemoveObject(FactoryKitchenFood piece)
        {
            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
        }
    }
}
