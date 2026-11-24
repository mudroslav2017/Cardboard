using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class FactoryKitchen : MonoBehaviour
    {
        public const float AmountPerIngredient = .90f;
        const float BoilDuration = 7f;
        public CardboardFactoryGame game;
        public FactoryKitchenTool board, pan;
        public Transform knife, peeler, pot, broth, lid;
        public Transform peelingStand;
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
        public bool Preparing => prep != PrepMode.None;
        Transform PrepAnchor => prep == PrepMode.Peeler ? peelingStand : board.transform;
        public Vector3 PrepCameraPosition => PrepAnchor.TransformPoint(new Vector3(prep == PrepMode.Knife ? .14f : 0, .97f, -.86f));
        public Quaternion PrepCameraRotation => Quaternion.LookRotation(PrepAnchor.TransformPoint(new Vector3(0, .04f, 0)) - PrepCameraPosition);
        public const float BladeHalfLength = .32f;

        enum PrepMode { None, Knife, Peeler }
        readonly List<FactoryKitchenFood> pieces = new List<FactoryKitchenFood>();
        PrepMode prep;
        FactoryKitchenFood peelingPotato;
        Vector2 pointer;
        int prepStartedFrame;
        bool burner, dustySoup, charging, pressing, panHasFood, panNeedsFlip, panHasBurntFood;
        float boil, peelDistance, knifeAngle, lastChop;
        Vector2 flick;
        Vector3 knifeHome, peelerHome;
        Quaternion knifeRotation, peelerRotation;
        float lidHeight;
        string hint;
        float hintUntil;
        GUIStyle promptStyle;
        AudioClip crunch;

        FactoryPhysicsProp Held => game.objectCarry ? game.objectCarry.Held : null;
        FactoryKitchenFood HeldFood => Held ? Held.GetComponent<FactoryKitchenFood>() : null;
        FactoryKitchenTool HeldTool => Held ? Held.GetComponent<FactoryKitchenTool>() : null;
        bool PotFull => RatInPot >= AmountPerIngredient && PotatoInPot >= AmountPerIngredient;
        bool Active => game && game.State == FactoryState.Working && !game.MenuActive && !game.IntroActive &&
            !game.CaptureActive && !game.EndingBusy && !game.FilmActive;

        void Awake()
        {
            knifeHome = knife.localPosition;
            peelerHome = peeler.localPosition;
            knifeRotation = knife.localRotation;
            peelerRotation = peeler.localRotation;
            lidHeight = lid.localPosition.y;
            crunch = MakePaperSound("Knife through cardboard", .16f, false);
            sizzle.clip = MakePaperSound("Cardboard frying", 1.2f, true);
        }

        public void ResetKitchen()
        {
            foreach (var piece in pieces) if (piece) RemoveObject(piece);
            pieces.Clear();
            RatStock = Portions = 0;
            RatInPot = PotatoInPot = 0;
            burner = dustySoup = false;
            boil = hintUntil = 0;
            sizzle.Stop();
            EndPrep();
            CancelFlick();
            board.ResetTool();
            pan.ResetTool();
            ShowTrayMeal(false, false);
            RefreshCookingFeedback(false, false, false);
        }

        public void ReceiveRats(int count)
        {
            RatStock += count;
            ShowHint("BASKET IN. TAKE A RAT AT THE PREP BENCH.");
        }

        public void ShowTrayMeal(bool visible, bool dusty)
        {
            traySoup.SetActive(visible);
            trayDust.SetActive(dusty);
        }

        public bool OwnsHeld(FactoryPhysicsProp prop) => prop &&
            (prop == board.Prop || prop == pan.Prop || prop.GetComponent<FactoryKitchenFood>());

        public bool AcceptsHeld(KitchenAction action)
        {
            switch (action)
            {
                case KitchenAction.Prep: return Held == board.Prop || HeldFood && HeldFood.Peeled;
                case KitchenAction.Board: return HeldFood && HeldFood.Peeled;
                case KitchenAction.Peeler: return HeldFood && !HeldFood.Peeled;
                case KitchenAction.Pan: return Held == board.Prop;
                case KitchenAction.Burner: return Held == pan.Prop || HeldFood;
                case KitchenAction.Pot: return HeldTool || HeldFood;
                case KitchenAction.Bin: return HeldFood;
                default: return false;
            }
        }

        public string Prompt(KitchenAction action)
        {
            switch (action)
            {
                case KitchenAction.Rat: return RatStock > 0 ? "E - LAY OUT RAT  /  " + RatStock + " IN STOCK" : "WAITING FOR THE DIVER'S BASKET";
                case KitchenAction.Potato: return "E - TAKE A POTATO";
                case KitchenAction.Knife: return "E - GRIP KNIFE";
                case KitchenAction.Peeler: return HeldFood && !HeldFood.Peeled ? "E - PEEL POTATO" : "BRING A POTATO TO THE PEELER";
                case KitchenAction.Board: return HeldFood ? "E - SET INGREDIENT ON THE BOARD" : "E - PICK UP CHOPPING BOARD";
                case KitchenAction.Prep: return HeldFood ? "E - SET INGREDIENT ON THE BOARD" : "E - PUT BOARD ON PREP BENCH";
                case KitchenAction.Pan: return Held == board.Prop ? "E - TIP FROM BOARD (GET CLOSE)" : "E - PICK UP FRYING PAN";
                case KitchenAction.Burner:
                    if (Held == pan.Prop) return "E - SET PAN ON BURNER";
                    if (HeldFood) return "E - DROP PIECE INTO PAN";
                    if (!burner) return "E - LIGHT BURNER";
                    return panHasBurntFood ? "E - TURN BURNER OFF  /  FOOD BURNT" : "E - TURN BURNER OFF";
                case KitchenAction.Spatula: return panNeedsFlip ? "E - FLIP THE PIECES  /  SIDE BROWNED" :
                    panHasFood ? "E - FLIP THE PIECES" : "SPATULA";
                case KitchenAction.Pot: return Held ? "E - TIP INTO STOCKPOT" : PotStatus;
                case KitchenAction.Serve: return Portions > 0 ? "E - LADLE SOUP ONTO TRAY  /  " + Portions + " LEFT" : "LADLE READY WHEN SOUP IS DONE";
                case KitchenAction.Bin: return "E - DISCARD HELD PIECE";
                default: return "";
            }
        }

        public bool Use(KitchenAction action)
        {
            if (!Active || game.HasPart || game.HandsBusy && !AcceptsHeld(action)) return false;
            switch (action)
            {
                case KitchenAction.Rat: return LoadRat();
                case KitchenAction.Potato: return TakePotato();
                case KitchenAction.Knife: return BeginPrep(PrepMode.Knife);
                case KitchenAction.Peeler: return BeginPrep(PrepMode.Peeler);
                case KitchenAction.Board:
                    if (HeldFood) return LayHeldOnBoard();
                    return board.Prop.Take(game);
                case KitchenAction.Prep:
                    if (HeldFood) return LayHeldOnBoard();
                    if (Held != board.Prop) return false;
                    SetDown(board); return true;
                case KitchenAction.Pan:
                    if (Held == board.Prop)
                        return Vector3.Distance(board.transform.position, pan.transform.position) < .72f && pan.ReadyAtStation
                            ? TipBetween(board, pan.transform.position + Vector3.up * .065f, .40f, false) : ShowHint("HOLD THE BOARD NEAR THE PAN'S RIM TO TIP IT IN.");
                    return pan.Prop.Take(game);
                case KitchenAction.Burner:
                    if (Held == pan.Prop) SetDown(pan);
                    else if (HeldFood) return DropHeldIntoPan();
                    else burner = !burner;
                    return true;
                case KitchenAction.Spatula: return FlipPan();
                case KitchenAction.Pot: return TipIntoPot();
                case KitchenAction.Serve: return Serve();
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

        bool LoadRat()
        {
            if (RatStock == 0) return ShowHint("THE DIVER NEEDS TO DELIVER A FULL BASKET.");
            if (!board.ReadyAtStation) return ShowHint("SET THE CHOPPING BOARD DOWN NEAR THE PREP BENCH.");
            if (pieces.Exists(piece => AvailableOn(board, piece))) return ShowHint("CLEAR THE CHOPPING BOARD FIRST.");
            RatStock--;
            var cuttingBoard = board.transform;
            var position = cuttingBoard.TransformPoint(Vector3.up * (.034f - ratModel.bounds.min.y));
            SpawnPiece(true, ratModel, ratMaterials, 1, position, cuttingBoard.rotation, null);
            ShowHint("PICK UP THE KNIFE.");
            return true;
        }

        bool TakePotato()
        {
            var camera = Camera.main;
            if (!camera) return false;
            var eyes = camera.transform;
            var potato = SpawnPiece(false, potatoModel, potatoMaterials, 1,
                eyes.position + eyes.forward * .68f - Vector3.up * .18f, Quaternion.identity, null);
            if (potato.Prop.Take(game))
            {
                ShowHint("BRING THE POTATO TO THE PEELER.");
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
            if (!food || !food.Peeled) return ShowHint("PEEL THE POTATO FIRST.");
            if (pieces.Exists(piece => AvailableOn(board, piece))) return ShowHint("CLEAR THE CHOPPING BOARD FIRST.");
            var body = food.Prop.Body;
            var cuttingBoard = board.transform;
            DropHeld(cuttingBoard.TransformPoint(Vector3.up * (.045f - food.Model.bounds.min.y)));
            body.rotation = cuttingBoard.rotation;
            body.transform.rotation = cuttingBoard.rotation;
            return true;
        }

        bool BeginPrep(PrepMode mode)
        {
            if (mode == PrepMode.Peeler)
            {
                var potato = HeldFood;
                if (!potato || potato.Peeled) return ShowHint("BRING A POTATO TO THE PEELER.");
                if (pieces.Exists(piece => piece && piece != potato && !piece.Prop.IsHeld &&
                    Vector3.Distance(piece.Prop.Body.position, peelingStand.position) < .22f))
                    return ShowHint("CLEAR THE PEELING STAND FIRST.");
                game.objectCarry.Release(false);
                peelingPotato = potato;
                potato.RestForCutting(peelingStand.position, PeelRotation(potato));
            }
            if (mode == PrepMode.Knife)
            {
                if (!board.ReadyAtStation) return ShowHint("SET THE BOARD DOWN NEAR THE PREP BENCH FIRST.");
                if (!pieces.Exists(piece => AvailableOn(board, piece) && piece.Peeled)) return ShowHint("LAY A PREPARED INGREDIENT ON THE BOARD.");
                board.HoldSteadyForPrep();
                var cuttingBoard = board.transform;
                foreach (var piece in pieces)
                {
                    if (!AvailableOn(board, piece) || !piece.Peeled) continue;
                    Vector3 position = cuttingBoard.InverseTransformPoint(piece.Prop.Body.position);
                    position.y = .034f - piece.Model.bounds.min.y;
                    Vector3 forward = Vector3.ProjectOnPlane(piece.Prop.Body.rotation * Vector3.forward, cuttingBoard.up);
                    Quaternion rotation = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward, cuttingBoard.up) : cuttingBoard.rotation;
                    piece.RestForCutting(cuttingBoard.TransformPoint(position), rotation);
                }
            }
            prep = mode;
            prepStartedFrame = Time.frameCount;
            pointer = new Vector2(0, mode == PrepMode.Knife ? 0 : .23f);
            pressing = false;
            peelDistance = 0;
            knifeAngle = 0;
            lastChop = float.NegativeInfinity;
            PosePrepTool(false);
            return true;
        }

        public void EndPrep()
        {
            foreach (var piece in pieces) if (piece) piece.ReleaseFromCutting();
            board.ResumeAfterPrep();
            peelingPotato = null;
            prep = PrepMode.None;
            pressing = false;
            foreach (var dash in knifeDashes) dash.gameObject.SetActive(false);
            knife.SetLocalPositionAndRotation(knifeHome, knifeRotation);
            peeler.SetLocalPositionAndRotation(peelerHome, peelerRotation);
        }

        public bool ConsumesMouse(Mouse mouse) => Active && !CardboardMarker.DrawingMode &&
            (Preparing || HeldTool && (charging || mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame));

        public void HandleMouse(Mouse mouse, Vector2 delta)
        {
            if (Preparing)
            {
                if (mouse.rightButton.wasPressedThisFrame || Time.frameCount != prepStartedFrame && Keyboard.current?.eKey.wasPressedThisFrame == true)
                { EndPrep(); return; }
                if (prep == PrepMode.Knife)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > .01f) SetKnifeAngle(knifeAngle + Mathf.Sign(scroll) * 12f);
                    MovePrepTool(delta, mouse.leftButton.wasPressedThisFrame);
                }
                else MovePrepTool(delta, mouse.leftButton.isPressed);
                return;
            }
            if (mouse.rightButton.wasPressedThisFrame) { CancelFlick(); return; }
            if (mouse.leftButton.wasPressedThisFrame) { charging = true; flick = Vector2.zero; }
            if (!charging) return;
            flick = Vector2.ClampMagnitude(flick + delta, 175f);
            HeldTool.Pitch = Mathf.Clamp(flick.y * .10f, -8f, 12f);
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Toss(HeldTool, flick);
                CancelFlick();
            }
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
            pointer += pixels * .0026f;
            pointer.x = Mathf.Clamp(pointer.x, -.39f, .39f);
            pointer.y = Mathf.Clamp(pointer.y, -.31f, .31f);
            if (prep == PrepMode.Knife)
            {
                if (pressed)
                {
                    lastChop = Time.time;
                    CutUnderBlade();
                }
            }
            else if (pressed) Peel(previous, pointer);
            else if (pressing) peelDistance = 0;
            pressing = pressed;
            if (Preparing) PosePrepTool(pressed);
        }

        void PosePrepTool(bool pressed)
        {
            if (prep == PrepMode.Peeler)
            {
                peeler.SetPositionAndRotation(peelingStand.TransformPoint(new Vector3(pointer.x, pressed ? .085f : .20f, pointer.y)), peelingStand.rotation);
                return;
            }
            Quaternion yaw = Quaternion.Euler(0, knifeAngle, 0);
            var cuttingBoard = board.transform;
            Vector3 center = new Vector3(pointer.x, 0, pointer.y);
            float chop = Mathf.Sin(Mathf.Clamp01((Time.time - lastChop) / .23f) * Mathf.PI);
            float top = .023f;
            foreach (var piece in pieces)
                if (piece && piece.OnCuttingBoard)
                    top = Mathf.Max(top, cuttingBoard.InverseTransformPoint(piece.transform.position).y + piece.Model.bounds.max.y);
            knife.SetPositionAndRotation(cuttingBoard.TransformPoint(center + Vector3.up * Mathf.Lerp(top + .135f, .052f, chop)),
                cuttingBoard.rotation * yaw);
            for (int i = 0; i < knifeDashes.Length; i++)
            {
                Transform dash = knifeDashes[i];
                dash.gameObject.SetActive(true);
                float forward = Mathf.Lerp(-BladeHalfLength + .018f, BladeHalfLength - .018f, i / (float)(knifeDashes.Length - 1));
                dash.SetPositionAndRotation(cuttingBoard.TransformPoint(center + yaw * new Vector3(0, top + .011f, forward)), cuttingBoard.rotation * yaw);
            }
        }

        public void SetKnifeAngle(float angle) { knifeAngle = Mathf.Repeat(angle, 360); }

        void CutUnderBlade()
        {
            var cuttingBoard = board.transform;
            Quaternion yaw = cuttingBoard.rotation * Quaternion.Euler(0, knifeAngle, 0);
            Vector3 center = cuttingBoard.TransformPoint(new Vector3(pointer.x, 0, pointer.y));
            bool cutAnything = false;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var parent = pieces[i];
                if (!parent || !parent.OnCuttingBoard) continue;
                var node = parent.transform;
                Vector3 normal = node.InverseTransformDirection(yaw * Vector3.right);
                Vector3 blade = node.InverseTransformDirection(yaw * Vector3.forward);
                if (!FactoryKitchenMeshCut.TrySplit(parent.Model, node.InverseTransformPoint(center), normal, blade, BladeHalfLength,
                    out var first, out var second)) continue;
                float firstVolume = FactoryKitchenMeshCut.Volume(first);
                float secondVolume = FactoryKitchenMeshCut.Volume(second);
                if (firstVolume < 2e-9f || secondVolume < 2e-9f)
                {
                    Destroy(first);
                    Destroy(second);
                    continue;
                }
                float firstAmount = parent.Amount * firstVolume / (firstVolume + secondVolume);
                SpawnCutPiece(parent, first, firstAmount, normal * .0009f);
                SpawnCutPiece(parent, second, parent.Amount - firstAmount, -normal * .0009f);
                pieces.RemoveAt(i);
                RemoveObject(parent);
                cutAnything = true;
            }
            if (cutAnything) PlayCrunch();
        }

        void Peel(Vector2 from, Vector2 to)
        {
            if (!peelingPotato || Mathf.Abs(to.x) > .20f || to.y >= from.y) return;
            peelDistance += Mathf.Clamp(from.y, -.12f, .12f) - Mathf.Clamp(to.y, -.12f, .12f);
            if (peelDistance < .16f) return;
            peelDistance = 0;
            peelingPotato.RemovePeel(potatoPaper);
            PlayCrunch();
            if (!peelingPotato.Peeled)
            {
                peelingPotato.Prop.Body.rotation = PeelRotation(peelingPotato);
                peelingPotato.transform.rotation = peelingPotato.Prop.Body.rotation;
                return;
            }
            EndPrep();
            ShowHint("POTATO PEELED. PICK IT UP AND PUT IT ON THE CUTTING BOARD.");
        }

        Quaternion PeelRotation(FactoryKitchenFood potato) => peelingStand.rotation *
            Quaternion.Euler((potato.PeelRemoved + .5f) * 360 / potato.PeelCount - 90, 0, 0);

        void SpawnCutPiece(FactoryKitchenFood parent, Mesh model, float amount, Vector3 separation)
        {
            Vector3 center = model.bounds.center;
            var vertices = model.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] -= center;
            model.vertices = vertices;
            model.RecalculateBounds();
            Vector3 world = parent.transform.TransformPoint(center + separation);
            var piece = SpawnPiece(parent.Rat, model, parent.Materials, amount, world, parent.transform.rotation, parent);
            piece.RestForCutting(world, parent.transform.rotation);
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
            }
            else
            {
                var box = node.AddComponent<BoxCollider>();
                box.center = model.bounds.center;
                box.size = Vector3.Max(model.bounds.size, Vector3.one * .003f);
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
                    !HeldFood.Sliced ? "SLICE THE INGREDIENT FIRST." : "BROWN BOTH SIDES OF THE RAT FIRST.");
                DropHeld(pot.position + Vector3.up * .44f);
                return true;
            }
            var tool = HeldTool;
            if (!tool || Vector3.Distance(tool.transform.position, pot.position) > 1.7f)
                return ShowHint("MOVE THE BOARD OR PAN NEXT TO THE POT.");
            return TipBetween(tool, pot.position + Vector3.up * .19f, .52f, true);
        }

        bool TipBetween(FactoryKitchenTool tool, Vector3 destination, float flight, bool cookedOnly)
        {
            int launched = 0;
            foreach (var piece in pieces)
            {
                if (!AvailableOn(tool, piece) || (cookedOnly ? !piece.ReadyForPot : piece.Burnt)) continue;
                float spacing = tool.transform.InverseTransformPoint(piece.Prop.Body.position).x * .67f;
                // The lip of the tilted vessel lifts its contents before they leave it.
                Vector3 releasePoint = piece.Prop.Body.position + Vector3.up * (tool.pan ? .17f : .07f);
                piece.transform.position = releasePoint;
                piece.Prop.Body.position = releasePoint;
                Vector3 displacement = destination + tool.transform.right * spacing - releasePoint;
                Launch(piece, displacement / flight - .5f * Physics.gravity * flight);
                launched++;
            }
            if (launched > 0) tool.Kick();
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

        bool FlipPan()
        {
            if (!pan.ReadyAtStation) return ShowHint("SET THE PAN DOWN NEAR THE BURNER FIRST.");
            bool flipped = false;
            foreach (var piece in pieces)
            {
                if (!AvailableOn(pan, piece) || piece.Burnt) continue;
                piece.Flip();
                flipped = true;
            }
            return flipped || ShowHint("GET THE PIECES IN THE PAN FIRST.");
        }

        static bool AvailableOn(FactoryKitchenTool tool, FactoryKitchenFood piece) =>
            piece && !piece.Prop.IsHeld && tool.Contains(piece.Prop.Body.position);

        void FixedUpdate() { if (Active) TickCooking(Time.fixedDeltaTime); }

        public void TickCooking(float dt)
        {
            bool frying = false, turning = false, scorched = false;
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
                if (piece.Prop.IsHeld || piece.OnCuttingBoard) continue;
                if (panHeating && AvailableOn(pan, piece))
                {
                    piece.Fry(dt);
                    frying |= !piece.Burnt;
                    turning |= !piece.Burnt && !piece.Fried && piece.SideReady;
                    scorched |= piece.Burnt;
                }
                if (Portions > 0 || !piece.ReadyForPot) continue;
                Vector3 local = pot.InverseTransformPoint(piece.Prop.Body.position);
                if (new Vector2(local.x, local.z).sqrMagnitude > .26f * .26f || local.y < 0 || local.y > .25f) continue;
                if (piece.Rat)
                {
                    if (RatInPot >= AmountPerIngredient) continue;
                    RatInPot = Mathf.Min(AmountPerIngredient, RatInPot + piece.Amount);
                }
                else
                {
                    if (PotatoInPot >= AmountPerIngredient) continue;
                    PotatoInPot = Mathf.Min(AmountPerIngredient, PotatoInPot + piece.Amount);
                }
                dustySoup |= piece.Dusty;
                pieces.RemoveAt(i);
                RemoveObject(piece);
            }
            if (PotFull && Portions == 0)
            {
                boil += dt;
                if (boil >= BoilDuration)
                {
                    Portions = 2;
                    game.PlayCaptureSound("good");
                    ShowHint("SOUP'S UP. LADLE TWO BOWLS AT THE COUNTER.");
                }
            }
            RefreshCookingFeedback(frying, turning, scorched);
        }

        bool Serve()
        {
            if (Portions == 0) return ShowHint("WAIT FOR THE STOCKPOT TO RING.");
            if (!game.lunch.TryOfferKitchenMeal(dustySoup)) return ShowHint("TAKE THE TRAY TO A TABLE TO CLEAR THE COUNTER.");
            Portions--;
            if (Portions == 0)
            {
                RatInPot = PotatoInPot = 0;
                boil = 0;
                dustySoup = false;
            }
            game.PlayCaptureSound("good");
            ShowHint("PICK UP THE SOUP TRAY AT THE COUNTER.");
            return true;
        }

        public string HeldPrompt(FactoryPhysicsProp prop) => prop == board.Prop || prop == pan.Prop
            ? "HOLD LMB + FLICK FORWARD - TOSS CONTENTS  /  RMB - SET DOWN"
            : "LMB - THROW PIECE  /  RMB - SET DOWN";

        string PotStatus => Portions > 0 ? "SOUP READY  /  " + Portions + " BOWLS" : PotFull
            ? "BOILING  /  " + Mathf.CeilToInt(Mathf.Max(0, BoilDuration - boil)) + " S"
            : $"STOCKPOT  /  FRIED RAT {RatInPot / AmountPerIngredient:P0}   POTATO {PotatoInPot / AmountPerIngredient:P0}";

        void RefreshCookingFeedback(bool frying, bool turning, bool scorched)
        {
            panHasFood = frying;
            panNeedsFlip = turning;
            panHasBurntFood = scorched;
            burnerLamp.enabled = burner;
            broth.gameObject.SetActive(RatInPot > 0 || PotatoInPot > 0);
            Vector3 lidPosition = lid.localPosition;
            lidPosition.y = lidHeight + (PotFull && Portions == 0 ? Mathf.Abs(Mathf.Sin(Time.time * 24)) * .026f : 0);
            lid.localPosition = lidPosition;
            if (scorched && !scorchSmoke.isPlaying) scorchSmoke.Play();
            if (!scorched && scorchSmoke.isPlaying) scorchSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (frying || scorched)
            {
                sizzle.pitch = scorched ? .73f : turning ? 1.18f : .93f;
                sizzle.volume = turning || scorched ? .37f : .22f;
                if (!sizzle.isPlaying) sizzle.Play();
            }
            else if (sizzle.isPlaying) sizzle.Stop();
        }

        void Update()
        {
            if (Active && Application.isFocused) return;
            if (Preparing) EndPrep();
            if (charging) CancelFlick();
            if (sizzle.isPlaying) sizzle.Stop();
        }

        void PlayCrunch() { if (crunch) AudioSource.PlayClipAtPoint(crunch, board.transform.position, .55f); }

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
            if (!Active || CardboardMarker.DrawingMode || !Camera.main ||
                !Preparing && Vector3.Distance(Camera.main.transform.position, transform.position) > 7f && !OwnsHeld(Held)) return;
            string prompt = Preparing ? prep == PrepMode.Knife ? "LMB - SLICE  /  WHEEL - TURN KNIFE  /  RMB - LEAVE" :
                "HOLD LMB AND PULL THE PEELER TOWARD YOU  /  RMB - LEAVE" : game.FocusPrompt;
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
        }

        static void RemoveObject(FactoryKitchenFood piece)
        {
            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
        }
    }
}
