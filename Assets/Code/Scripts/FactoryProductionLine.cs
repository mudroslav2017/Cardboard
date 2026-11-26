using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public sealed class FactoryProductionLine : MonoBehaviour
    {
        [SerializeField] CardboardFactoryGame game;
        [SerializeField] FactoryCraneJob crane;
        [SerializeField] Material cardboard, edge, ink;
        [SerializeField] Material blockout;

        enum Work { Empty, Loose, InHand, Rolling, LatheReady, Cutting, LatheDone, DrillReady, Drilling, DrillDone }
        enum Ride { None, Belt, Winding, Airborne, Crumpled }

        const float BeltSpeed = 1.65f;
        const int MaxMisses = 3;
        const float CuttingWindow = .086f;
        const float StrokeDuration = .18f;
        static readonly float[] CutTargets = { .42f, .68f, .35f, .60f };

        FactoryProductionModels models;
        FactoryProductionPiece piece;
        FactoryInteractable stock, intake, exit, latheMount, latheButton, drillMount, drillButton;
        Work work;
        Ride ride;
        CardboardRunner rider;
        Vector3 rideOrigin, rideLanding, feedOrigin, cutterHome, quillHome;
        bool puppetWasVisible;
        Vector3 puppetHome, puppetScale;
        Quaternion puppetRotation, rotorHome;
        float pressTravel, beltTravel, rideTime, drillTime, spindleTime, stroke, windingAngle;
        int cuts, misses, pickedUpFrame, strikeResult;
        GUIStyle promptStyle, counterStyle;

        internal bool Carrying => work == Work.InHand && piece;
        internal bool HoldsPlayer => ride != Ride.None || work == Work.Cutting;
        internal bool LatheRunning => work == Work.Cutting;
        internal int Cuts => cuts;
        internal int Misses => misses;
        internal int CutCount => CutTargets.Length;
        internal int MissLimit => MaxMisses;
        internal float TimingCursor => Mathf.PingPong(spindleTime * .85f, 1f);
        internal float WindowCenter => CutTargets[Mathf.Min(cuts, CutTargets.Length - 1)];
        internal float WindowRadius => CuttingWindow;
        internal int StrikeResult => stroke > 0f ? strikeResult : 0;
        internal string HeldPrompt => "RMB - PUT DOWN  /  LMB - THROW";

        void Awake()
        {
            models = new FactoryProductionModels(transform, blockout);
            stock = AddControl(models.StockControl.gameObject);
            intake = AddControl(models.IntakeControl.gameObject);
            exit = AddControl(models.ExitControl.gameObject);
            latheMount = AddControl(models.LatheMountControl.gameObject);
            latheButton = AddControl(models.LatheButtonControl.gameObject);
            drillMount = AddControl(models.DrillMountControl.gameObject);
            drillButton = AddControl(models.DrillButtonControl.gameObject);
            cutterHome = models.LatheCutter.localPosition;
            quillHome = models.DrillQuill.localPosition;
            rotorHome = models.LatheRotor.localRotation;
            RefreshPrompts();
        }

        static FactoryInteractable AddControl(GameObject target)
        {
            var control = target.AddComponent<FactoryInteractable>();
            control.action = FactoryAction.Production;
            return control;
        }

        internal void ResetLine()
        {
            if (models == null) return;
            CancelPlayer();
            RemovePiece();
            cuts = misses = strikeResult = 0;
            beltTravel = stroke = drillTime = spindleTime = 0f;
            models.AnimatePress(0f);
            models.LatheRotor.localRotation = rotorHome;
            models.LatheCutter.localPosition = cutterHome;
            models.DrillQuill.localPosition = quillHome;
        }

        internal void CancelPlayer()
        {
            if (ride != Ride.None) EndRide();
            if (work == Work.Cutting)
            {
                SetWork(Work.LatheReady);
                models.LatheCutter.localPosition = cutterHome;
                stroke = 0f;
                AdoptView(models.LatheSocket.position - EyePosition(models.LatheOperator.position));
            }
            if (Carrying) Drop(null, false);
        }

        internal bool Use(FactoryInteractable target, CardboardRunner player)
        {
            if (!player || !game || !crane || game.PlayerBlocked || game.CaptureActive || !crane.InHall(player.transform.position))
                return false;

            if (target == stock && work == Work.Empty && !game.HandsBusy && !game.HasPart)
            {
                if (!Camera.main) return false;
                piece = FactoryProductionPiece.Create(transform, models.Stock.position, cardboard, edge, ink);
                AddControl(piece.gameObject);
                return TakePiece();
            }
            if (target == intake)
            {
                if (Carrying && piece.Stage == ProductionPieceStage.Raw)
                {
                    BeginRolling();
                    return true;
                }
                if (work != Work.Rolling && !game.HandsBusy && !game.HasPart)
                {
                    BoardBelt(player);
                    return true;
                }
            }
            if ((target == exit || (piece && target.gameObject == piece.gameObject)) && work == Work.Loose)
                return (target != exit || AtOutfeed(piece.Body.position)) && TakePiece();

            if (target == latheMount)
            {
                if (Carrying && piece.Stage == ProductionPieceStage.Flattened)
                {
                    piece.Hold(models.LatheSocket, Vector3.up * piece.HalfThickness, Quaternion.identity);
                    SetWork(Work.LatheReady);
                    return true;
                }
                if (work == Work.LatheDone) return TakePiece();
            }
            if (target == latheButton && work == Work.LatheReady && !game.HandsBusy && !game.HasPart)
            {
                player.PlaceAtWorkAnchor(models.LatheOperator.position, true);
                cuts = misses = strikeResult = 0;
                spindleTime = stroke = 0f;
                SetWork(Work.Cutting);
                return true;
            }
            if (target == drillMount)
            {
                if (Carrying && piece.Stage == ProductionPieceStage.Toothed)
                {
                    piece.Hold(models.DrillSocket, Vector3.up * piece.HalfThickness, Quaternion.identity);
                    SetWork(Work.DrillReady);
                    return true;
                }
                if (work == Work.DrillDone) return TakePiece();
            }
            if (target == drillButton && work == Work.DrillReady && !game.HandsBusy && !game.HasPart)
            {
                drillTime = 0f;
                SetWork(Work.Drilling);
                return true;
            }
            return false;
        }

        bool TakePiece()
        {
            var camera = Camera.main;
            if (!camera || game.HandsBusy || game.HasPart) return false;
            if (piece.Stage == ProductionPieceStage.Finished)
            {
                // Feed the existing repair and finishing loop with the production line's round-hub gear.
                game.ReceiveWorkbenchPart(FactoryRecipe.ForType(3, false));
                RemovePiece();
                return true;
            }
            if (CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false);
            piece.Hold(camera.transform, new Vector3(-.29f, -.44f, .86f), Quaternion.Euler(-19f, 13f, -10f));
            pickedUpFrame = Time.frameCount;
            SetWork(Work.InHand);
            return true;
        }

        void Drop(Camera camera, bool thrown)
        {
            Vector3 velocity = Vector3.zero;
            if (camera && thrown) velocity = camera.transform.forward * 6f + Vector3.up * .35f;
            piece.Release(transform, velocity);
            SetWork(Work.Loose);
        }

        void Update()
        {
            if (models == null || !game || game.MenuActive || game.FilmActive || game.CaptureActive || game.Ended) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            bool left = Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (work == Work.Cutting)
            {
                if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && cuts < CutTargets.Length)
                {
                    CancelPlayer();
                    return;
                }
                if (left)
                {
                    if (Cursor.lockState == CursorLockMode.Locked) Strike();
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        FactoryVolumeCursor.SetSystemVisible(false);
                    }
                }
            }
            else if (Carrying && !game.PlayerBlocked && Time.frameCount != pickedUpFrame && mouse != null && Application.isFocused)
            {
                if (mouse.rightButton.wasPressedThisFrame) Drop(Camera.main, false);
                else if (left && Cursor.lockState == CursorLockMode.Locked) Drop(Camera.main, true);
            }

            TickMachines(Mathf.Min(Time.deltaTime, .05f));
        }

        void FixedUpdate()
        {
            if (models == null || !game || game.CaptureActive || game.Ended) return;
            if (work == Work.Loose && piece)
            {
                if (piece.Body.position.y < transform.position.y - 3f)
                {
                    RemovePiece();
                    return;
                }
                if (piece.Stage == ProductionPieceStage.Raw && OnInfeed(piece.Body.position, piece.HalfThickness))
                    BeginRolling();
            }

            var player = CardboardRunner.Player;
            if (ride == Ride.None && work != Work.Rolling && player && !game.PlayerBlocked && !game.HandsBusy &&
                !game.HasPart && !crane.HoldsPlayer && OnInfeed(player.transform.position, 0f))
                BoardBelt(player);
        }

        bool OnInfeed(Vector3 worldPosition, float restHeight)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 feed = models.Intake.localPosition;
            float beltTop = models.Nip.localPosition.y - .054f;
            return Mathf.Abs(local.x - feed.x) <= 1.08f &&
                local.z >= feed.z - .65f && local.z <= models.Nip.localPosition.z - .45f &&
                Mathf.Abs(local.y - restHeight - beltTop) < .13f;
        }

        bool AtOutfeed(Vector3 position)
        {
            Vector3 offset = transform.InverseTransformDirection(position - models.OutfeedEnd.position);
            return Mathf.Abs(offset.x) < 1.25f && Mathf.Abs(offset.y) < .42f && offset.z > -1.32f && offset.z < .53f;
        }

        void BeginRolling()
        {
            if (ride == Ride.Belt) return;
            pressTravel = 0f;
            feedOrigin = models.Intake.localPosition;
            if (work == Work.Loose)
            {
                var center = transform.InverseTransformPoint(piece.Body.position);
                feedOrigin.x = center.x;
                feedOrigin.z = center.z;
            }
            piece.Hold(transform, feedOrigin, Quaternion.identity);
            SetWork(Work.Rolling);
        }

        void TickMachines(float dt)
        {
            if (work == Work.Rolling || ride == Ride.Belt)
            {
                beltTravel += BeltSpeed * dt;
                models.AnimatePress(beltTravel);
            }
            if (work == Work.Rolling) TickRolling(dt);
            if (work == Work.Cutting) TickLathe(dt);
            if (work == Work.Drilling) TickDrill(dt);
            if (ride != Ride.None) TickRide(dt);
            if (work == Work.Loose && piece && piece.Stage == ProductionPieceStage.Flattened)
                exit.prompt = AtOutfeed(piece.Body.position) ? "E - TAKE STENCILLED SHEET" : "";
        }

        void TickRolling(float dt)
        {
            Vector3 nip = models.Nip.localPosition;
            Vector3 exitPoint = models.OutfeedEnd.localPosition;
            pressTravel = Mathf.Min(pressTravel + BeltSpeed * dt, exitPoint.z - feedOrigin.z);
            float z = feedOrigin.z + pressTravel;
            float squash = Mathf.InverseLerp(nip.z - .60f, nip.z + .12f, z);
            float height = Mathf.Lerp(feedOrigin.y, nip.y, squash);
            float centering = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(feedOrigin.z, nip.z, z));
            if (z >= nip.z && piece.Stage == ProductionPieceStage.Raw)
                piece.SetStage(ProductionPieceStage.Flattened);
            piece.transform.localPosition = new Vector3(Mathf.Lerp(feedOrigin.x, nip.x, centering), height, z);
            if (z < exitPoint.z - .001f) return;
            piece.Release(transform);
            SetWork(Work.Loose);
        }

        void Strike()
        {
            if (work != Work.Cutting || cuts == CutTargets.Length) return;
            stroke = StrokeDuration;
            if (Mathf.Abs(TimingCursor - WindowCenter) <= CuttingWindow)
            {
                strikeResult = 1;
                cuts++;
                return;
            }
            strikeResult = -1;
            if (++misses < MaxMisses) return;
            SetWork(Work.LatheReady);
            models.LatheCutter.localPosition = cutterHome;
            rideLanding = models.LatheEject.position;
            BeginRide(CardboardRunner.Player, Ride.Winding);
            game.RegisterMistake("The lathe jammed and caught your cardboard suit.", .5f);
        }

        void TickLathe(float dt)
        {
            spindleTime += dt;
            models.LatheRotor.Rotate(Vector3.up, 390f * dt, Space.Self);
            stroke = Mathf.Max(0f, stroke - dt);
            models.LatheCutter.localPosition = cutterHome +
                Vector3.forward * (.75f * Mathf.Sin(Mathf.PI * stroke / StrokeDuration));
            if (cuts < CutTargets.Length || stroke > 0f) return;
            piece.SetStage(ProductionPieceStage.Toothed);
            SetWork(Work.LatheDone);
            AdoptView(models.LatheSocket.position - EyePosition(models.LatheOperator.position));
        }

        void TickDrill(float dt)
        {
            drillTime += dt;
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .48f, drillTime)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.54f, 1.04f, drillTime)));
            models.DrillQuill.localPosition = quillHome + Vector3.down * (.55f * down);
            models.DrillQuill.Rotate(Vector3.up, 950f * dt, Space.Self);
            if (drillTime >= .50f && piece.Stage != ProductionPieceStage.Finished)
                piece.SetStage(ProductionPieceStage.Finished);
            if (drillTime < 1.04f) return;
            SetWork(Work.DrillDone);
            game.Toast("Gear drilled. Take it with E.", 3f);
        }

        void BoardBelt(CardboardRunner player)
        {
            rideLanding = transform.TransformPoint(models.OutfeedEnd.localPosition + new Vector3(-1.75f, -.934f, 1f));
            BeginRide(player, Ride.Belt);
        }

        void BeginRide(CardboardRunner player, Ride kind)
        {
            if (!player) return;
            rider = player;
            ride = kind;
            rideTime = windingAngle = 0f;
            rideOrigin = player.transform.position;
            rider.PlaceAtWorkAnchor(rideOrigin, false);
            if (!rider.puppet) return;
            puppetWasVisible = rider.puppet.gameObject.activeSelf;
            puppetHome = rider.puppet.localPosition;
            puppetScale = rider.puppet.localScale;
            puppetRotation = rider.puppet.localRotation;
            if (kind == Ride.Winding) rider.puppet.gameObject.SetActive(true);
        }

        void TickRide(float dt)
        {
            if (!rider) { ride = Ride.None; return; }
            rideTime += dt;
            if (ride == Ride.Belt)
            {
                Vector3 start = models.Intake.position + Vector3.down * .105f;
                Vector3 end = models.OutfeedEnd.position + Vector3.down * .054f;
                float trip = Vector3.Distance(start, end) / BeltSpeed;
                float beltPhase = Mathf.InverseLerp(.35f, .35f + trip, rideTime);
                if (rideTime < .35f)
                    rider.transform.position = Vector3.Lerp(rideOrigin, start, Mathf.SmoothStep(0f, 1f, rideTime / .35f));
                else if (beltPhase < 1f)
                    rider.transform.position = Vector3.Lerp(start, end, beltPhase);
                else
                    rider.transform.position = Vector3.Lerp(end, rideLanding, Mathf.SmoothStep(0f, 1f, (rideTime - .35f - trip) / .5f));
                if (rideTime >= .85f + trip) EndRide();
                return;
            }

            if (ride == Ride.Winding)
            {
                float snag = Mathf.Clamp01(rideTime / StrokeDuration);
                models.LatheCutter.localPosition = cutterHome + Vector3.forward * (.75f * Mathf.Sin(Mathf.PI * snag));
                float acceleration = Mathf.SmoothStep(0f, 1f, rideTime / 1.35f);
                windingAngle += Mathf.Lerp(240f, 1470f, acceleration) * dt;
                models.LatheRotor.Rotate(Vector3.up, Mathf.Lerp(390f, 1250f, acceleration) * dt, Space.Self);
                Vector3 spindle = models.LatheSocket.position;
                Vector3 outward = -transform.right;
                Vector3 radial = Quaternion.AngleAxis(windingAngle, outward) * (Vector3.up * .65f);
                rider.transform.position = Vector3.Lerp(rideOrigin, spindle + outward * .45f + radial,
                    Mathf.SmoothStep(0f, 1f, rideTime / .3f));
                PoseCrumple(Quaternion.AngleAxis(windingAngle, Vector3.forward), acceleration * .65f);
                if (rideTime < 1.35f) return;
                rideOrigin = rider.transform.position;
                rideTime = 0f;
                ride = Ride.Airborne;
                return;
            }

            if (ride == Ride.Airborne)
            {
                float t = Mathf.Clamp01(rideTime / .95f);
                rider.transform.position = Vector3.Lerp(rideOrigin, rideLanding, t) + Vector3.up * (2.6f * Mathf.Sin(t * Mathf.PI));
                windingAngle += 800f * dt;
                PoseCrumple(Quaternion.Euler(35f, windingAngle, windingAngle * .74f), Mathf.Lerp(.65f, 1f, t));
                if (t < 1f) return;
                rideTime = 0f;
                ride = Ride.Crumpled;
            }

            PoseCrumple(Quaternion.Euler(0f, -13f, 19f), 1f - Mathf.SmoothStep(0f, 1f, (rideTime - .38f) / .50f));
            if (rideTime >= .88f) EndRide();
        }

        void PoseCrumple(Quaternion rotation, float amount)
        {
            if (!rider.puppet) return;
            rider.puppet.localPosition = Vector3.Lerp(puppetHome, new Vector3(0f, -.1f, .08f), amount);
            rider.puppet.localScale = Vector3.Scale(puppetScale, Vector3.Lerp(Vector3.one, new Vector3(.47f, .33f, .64f), amount));
            rider.puppet.localRotation = rotation;
        }

        void EndRide()
        {
            var previous = ride;
            ride = Ride.None;
            if (!rider) return;
            if (rider.puppet)
            {
                rider.puppet.SetLocalPositionAndRotation(puppetHome, puppetRotation);
                rider.puppet.localScale = puppetScale;
                rider.puppet.gameObject.SetActive(puppetWasVisible);
            }
            rider.PlaceAtWorkAnchor(rideLanding, true);
            AdoptView(previous == Ride.Belt ? transform.forward : transform.right);
            rider = null;
        }

        static Vector3 EyePosition(Vector3 feet) => feet + Vector3.up * 1.62f;

        void AdoptView(Vector3 direction)
        {
            var camera = Camera.main;
            if (!camera || !camera.TryGetComponent(out CardboardCamera view)) return;
            camera.transform.rotation = Quaternion.LookRotation(direction.normalized);
            view.AdoptCurrentView();
        }

        internal bool TryGetView(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (!HoldsPlayer) return false;
            if (ride == Ride.None)
            {
                position = EyePosition(models.LatheOperator.position);
                rotation = Quaternion.LookRotation(models.LatheSocket.position - position);
                return true;
            }
            if (!rider) return false;
            if (ride == Ride.Belt)
            {
                float fromNip = Mathf.Abs(Vector3.Dot(rider.transform.position - models.Nip.position, transform.forward));
                float eyeHeight = Mathf.Lerp(.12f, .62f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1.36f, fromNip)));
                position = rider.transform.position + Vector3.up * eyeHeight;
                rotation = Quaternion.LookRotation(transform.forward + Vector3.down * .08f);
                return true;
            }
            position = models.LatheEject.position - transform.right * 1.85f - transform.forward * 2.6f + Vector3.up * 2.35f;
            Vector3 subject = rider.transform.position + Vector3.up * (ride == Ride.Crumpled ? .35f : .60f);
            rotation = Quaternion.LookRotation(subject - position);
            return true;
        }

        void OnGUI()
        {
            if (!game || game.MenuActive || game.FilmActive || game.Ended || game.CaptureActive ||
                game.CameraOverride || CardboardMarker.DrawingMode) return;

            bool timing = LatheRunning;
            if (!timing && (game.PlayerBlocked || (!Carrying && !NearStations()))) return;
            float scale = Mathf.Min(Screen.width / 1080f, Screen.height / 660f);
            if (scale <= 0f) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.color = Color.white;
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            var paper = new Color(.99f, .91f, .76f);
            var panel = new Color(.09f, .12f, .11f, .94f);
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    font = CardboardTypography.DefaultFont, fontSize = 16, alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = paper;
                counterStyle = new GUIStyle(promptStyle) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            }
            if (timing)
            {
                const float track = 420f;
                float x = (width - track) * .5f;
                float y = height * .65f;
                Fill(new Rect(x - 16f, y - 28f, track + 32f, 88f), panel);
                GUI.Label(new Rect(x, y - 24f, track, 20f), $"CUTS  {Cuts}/{CutCount}     MISSES  {Misses}/{MissLimit}", counterStyle);
                Fill(new Rect(x, y, track, 19f), new Color(.22f, .24f, .19f));
                Color target = StrikeResult < 0 ? new Color(.81f, .36f, .28f) :
                    StrikeResult > 0 ? new Color(.57f, .73f, .46f) : new Color(.92f, .71f, .32f);
                Fill(new Rect(x + track * (WindowCenter - WindowRadius), y, track * 2f * WindowRadius, 19f), target);
                Fill(new Rect(x + (track - 4f) * TimingCursor, y - 5f, 4f, 29f), paper);
                GUI.Label(new Rect(x, y + 26f, track, 29f), "LMB - CUT IN THE MARKED ZONE     TAB - STOP", promptStyle);
            }
            else
            {
                Fill(new Rect(width * .5f - 1f, height * .5f - 1f, 2f, 2f), paper);
                var focus = game.Focus;
                string label = focus && focus.action == FactoryAction.Production ? game.FocusPrompt : Carrying ? HeldPrompt : "";
                if (!string.IsNullOrEmpty(label))
                {
                    var rect = new Rect((width - 420f) * .5f, height * .5f + 21f, 420f, 32f);
                    Fill(rect, panel);
                    GUI.Label(rect, label, promptStyle);
                }
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        bool NearStations()
        {
            var player = CardboardRunner.Player;
            if (!player || !crane || !crane.InHall(player.transform.position) || crane.HoldsPlayer) return false;
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            return Mathf.Abs(local.x) < 4.2f && local.z > -1.4f && local.z < 21f;
        }

        static void Fill(Rect area, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void RemovePiece()
        {
            if (piece)
            {
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
            piece = null;
            SetWork(Work.Empty);
        }

        void SetWork(Work next)
        {
            work = next;
            RefreshPrompts();
        }

        void RefreshPrompts()
        {
            stock.prompt = work == Work.Empty ? "E - TAKE THICK CARDBOARD" : "FINISH THE BLANK ALREADY IN THE LINE";
            intake.prompt = Carrying && piece.Stage == ProductionPieceStage.Raw ? "E - FEED CARDBOARD INTO ROLLERS" :
                work == Work.Rolling ? "ROLLING CARDBOARD" : "E - STEP ONTO THE CONVEYOR";
            exit.prompt = "";
            latheMount.prompt = work == Work.LatheDone ? "E - TAKE CUT GEAR" :
                work == Work.LatheReady ? "PRESS THE LATHE START BUTTON" :
                work == Work.Cutting ? "CUTTING TEETH" : "E - LOAD A STENCILLED SHEET";
            latheButton.prompt = work == Work.LatheReady ? "E - START LATHE" : "";
            drillMount.prompt = work == Work.DrillDone ? "E - TAKE FINISHED GEAR" :
                work == Work.DrillReady ? "PRESS THE DRILL START BUTTON" :
                work == Work.Drilling ? "DRILLING CENTER HOLE" : "E - LOAD A CUT GEAR";
            drillButton.prompt = work == Work.DrillReady ? "E - DRILL CENTER HOLE" : "";
            if (!piece) return;
            piece.GetComponent<FactoryInteractable>().prompt = work == Work.Loose
                ? (piece.Stage == ProductionPieceStage.Raw ? "E - PICK UP CARDBOARD" :
                    piece.Stage == ProductionPieceStage.Flattened ? "E - TAKE STENCILLED SHEET" : "E - PICK UP GEAR")
                : "";
        }
    }
}
