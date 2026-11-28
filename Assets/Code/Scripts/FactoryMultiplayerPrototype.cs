#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;

namespace Cardboard
{
    [DefaultExecutionOrder(-70)]
    internal sealed class FactoryMultiplayerPrototype : MonoBehaviour
    {
        const float SendInterval = 1f / 30f;
        const float WorldInterval = .05f;
        const float ConnectionTimeout = 8;
        sealed class GuestLink
        {
            public FactoryPrototypeConnection Wire;
            public FactoryPrototypeFrame LastFrame;
            public int Role, WorldSent;
            public float Deadline, LastRemoteTime, NextSend, NextWorld;
        }

        CardboardFactoryGame game;
        CardboardRunner localPlayer;
        CharacterController localBody;
        CardboardCamera view;
        FactoryWorkstation station;
        FactoryPrototypeMenu menu;
        FactoryCoopShift shift;
        readonly GuestLink[] guests = new GuestLink[FactoryCoopShift.MaximumPlayers];
        readonly FactoryCoopAvatar[] avatars = new FactoryCoopAvatar[FactoryCoopShift.MaximumPlayers];
        readonly Transform[] puppets = new Transform[FactoryCoopShift.MaximumPlayers];
        readonly CardboardRunner[] puppetMotion = new CardboardRunner[FactoryCoopShift.MaximumPlayers];
        readonly float[] puppetPhases = new float[FactoryCoopShift.MaximumPlayers];
        TcpListener listener;
        TcpClient dialing;
        Task dial;
        FactoryPrototypeConnection connection;
        readonly List<FactoryCoopCommand> pending = new List<FactoryCoopCommand>();
        readonly Queue<(int Version, float Degrees, Vector2 Peeler)> visualInputs = new Queue<(int, float, Vector2)>();
        Vector2 pendingPeeler, unconfirmedPeeler;
        float nextSend, deadline;
        float pendingWheel, unconfirmedWheel, lastSnapshotTime, leverStarted, lastEcho;
        int acknowledgedWorld, inputVersion, leverVersion, ping;
        StationPart predictedLever;
        bool leverConfirmed;
        bool assignedRole;
        bool showSetup, paused, receivedFrame, draggingWheel, hasWheelAngle;
        Vector2 wheelPointer;
        float wheelAngle;
        string address = "127.0.0.1", portText = "7777", status = "";

        bool IsHost => listener != null;
        bool Playing => game.MultiplayerPrototype;
        bool OnMainMenu => game.mainMenu && game.mainMenu.Phase == FactoryMenuPhase.Main &&
            !game.FilmActive && (!game.paperDrama || !game.paperDrama.Active);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= AddToFactory;
            SceneManager.sceneLoaded += AddToFactory;
            AddToFactory(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void AddToFactory(Scene scene, LoadSceneMode mode)
        {
            var factory = CardboardFactoryGame.Instance;
            if (scene.name != "FactoryShift" || !factory || !factory.station || !factory.mainMenu ||
                factory.GetComponent<FactoryMultiplayerPrototype>()) return;
            factory.gameObject.AddComponent<FactoryMultiplayerPrototype>().game = factory;
        }

        void Start()
        {
            menu = new FactoryPrototypeMenu(transform, ShowSetup, BackToMenu, HostFromMenu, JoinFromMenu,
                () => SetPaused(false), () => SceneManager.LoadScene(game.gameObject.scene.path));
        }

        void Update()
        {
            var now = Time.unscaledTime;
            try
            {
                if (dial != null && (dial.IsCompleted || now >= deadline)) FinishDial(now);
                if (!Playing) return;
                if (IsHost)
                {
                    AcceptGuests(now);
                    ReceiveGuests(now);
                }
                else
                {
                    ReceiveHost(now);
                    if (connection != null && now >= deadline) LoseHost("The host stopped responding.");
                }
            }
            catch (Exception error) when (error is SocketException || error is IOException)
            {
                if (IsHost) status = error.Message;
                else LoseHost(error.Message);
            }

            if (!Playing) return;
            var keyboard = Keyboard.current;
            bool active = !paused && Application.isFocused && (IsHost || receivedFrame);
            game.SetCoopPresentation(active && !game.PlayerBlocked ? shift.Target(LocalFrame().Aim) : null,
                active && keyboard != null && keyboard.cKey.isPressed && !CardboardMarker.DrawingMode && !game.PlayerBlocked);
            if (IsHost)
            {
                var mouse = Mouse.current;
                shift.SetControls(FactoryCoopShift.Host, ReadControls(Keyboard.current, mouse, !paused && Application.isFocused));
                shift.Balance(FactoryCoopShift.Host, !paused && mouse != null && mouse.leftButton.isPressed,
                    !paused && mouse != null && mouse.rightButton.isPressed);
                shift.Observe(FactoryCoopShift.Host, localPlayer.transform.position, view.transform.rotation, localPlayer.JumpHeight,
                    localPlayer.speed, Time.deltaTime);
                shift.Tick(Time.deltaTime);
            }
            if (!Application.isFocused || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (CardboardMarker.DrawingMode) CardboardMarker.Instance.SetDrawingMode(false);
                else SetPaused(!paused);
                return;
            }
            if (paused || !IsHost && !receivedFrame) return;
            if (IsHost && game.Ended && keyboard.rKey.wasPressedThisFrame)
            {
                shift.Reset();
                MoveToStartingPosition();
                return;
            }
            HandleInput(keyboard, Mouse.current);
        }

        int LocalRole => shift.LocalRole;

        void HandleInput(Keyboard keyboard, Mouse mouse)
        {
            bool unavailable = game.Ended || shift.Local.Hand.Dipping || CardboardMarker.DrawingMode || LocalPullingLever;
            if (draggingWheel && (unavailable || mouse == null || !mouse.leftButton.isPressed))
                StopWheel();
            if (unavailable || Cursor.lockState != CursorLockMode.Locked) return;
            var frame = LocalFrame();
            if (shift.LocalUsesKitchen)
            {
                if (keyboard.eKey.wasPressedThisFrame) Command(FactoryCoopAction.Interact, frame);
                if (mouse != null)
                {
                    var command = new FactoryCoopCommand
                    {
                        Action = FactoryCoopAction.KitchenInput, Yaw = frame.Yaw, Pitch = frame.Pitch,
                        PointerDelta = mouse.delta.ReadValue(), Amount = mouse.scroll.ReadValue().y, Buttons = frame.Controls,
                        Pressed = mouse.leftButton.wasPressedThisFrame, Released = mouse.leftButton.wasReleasedThisFrame,
                        SecondaryPressed = mouse.rightButton.wasPressedThisFrame
                    };
                    Dispatch(command, frame);
                }
                return;
            }
            if (shift.LocalUsesCrane)
            {
                if (keyboard.tabKey.wasPressedThisFrame) Command(FactoryCoopAction.LeaveTool, frame);
                if (keyboard.spaceKey.wasPressedThisFrame) Command(FactoryCoopAction.ToolSpace, frame);
                if (keyboard.rKey.wasPressedThisFrame) Command(FactoryCoopAction.ToolReset, frame);
                return;
            }
            if (shift.LocalUsesProduction)
            {
                if (keyboard.tabKey.wasPressedThisFrame) Command(FactoryCoopAction.LeaveTool, frame);
                if (game.production.LatheRunning)
                {
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame) Command(FactoryCoopAction.ToolPrimary, frame);
                    return;
                }
            }
            if (shift.LocalCarriesTray)
            {
                if (keyboard.eKey.wasPressedThisFrame) Command(FactoryCoopAction.Interact, frame);
                return;
            }
            if (draggingWheel)
            {
                Vector2 delta = mouse.delta.ReadValue();
                wheelPointer += delta;
                var center = (Vector2)game.mainMenu.lens.WorldToScreenPoint(station.WheelCenter);
                var offset = wheelPointer - center;
                float turn = (delta.x - delta.y) * .45f;
                if (offset.sqrMagnitude < 225) hasWheelAngle = false;
                else
                {
                    float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                    if (hasWheelAngle) turn = -Mathf.DeltaAngle(wheelAngle, angle);
                    wheelAngle = angle;
                    hasWheelAngle = true;
                }
                if (Mathf.Abs(turn) >= .01f) Command(FactoryCoopAction.WheelTurn, frame, Mathf.Clamp(turn, -65, 65));
                return;
            }
            var target = shift.Target(frame.Aim);
            if (keyboard.eKey.wasPressedThisFrame)
            {
                Command(FactoryCoopAction.Interact, frame);
                return;
            }
            if (keyboard.qKey.wasPressedThisFrame) Command(FactoryCoopAction.Drop, frame);
            if (mouse == null || game.R82C4916Item && game.R82C4916Item.Drawn) return;
            if (mouse.rightButton.wasPressedThisFrame) Command(FactoryCoopAction.Drop, frame);
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (shift.LocalHandsBusy)
            {
                Command(FactoryCoopAction.Throw, frame);
                return;
            }
            if (!target || !target.TryGetComponent(out FactoryStationControl control) || control.station != station) return;
            if (control.part != StationPart.Wheel)
            {
                Command(FactoryCoopAction.Interact, frame);
                return;
            }
            if (!station.CanInteract(control.part, 0, shift.Local)) return;
            draggingWheel = true;
            wheelPointer = mouse.position.ReadValue();
            wheelAngle = 0;
            hasWheelAngle = false;
            Command(FactoryCoopAction.WheelStart, frame);
        }

        void Command(FactoryCoopAction action, FactoryPrototypeFrame frame, float amount = 0)
        {
            var command = new FactoryCoopCommand { Action = action, Yaw = frame.Yaw, Pitch = frame.Pitch, Amount = amount };
            Dispatch(command, frame);
        }

        void Dispatch(FactoryCoopCommand command, FactoryPrototypeFrame frame)
        {
            if (IsHost)
            {
                int previousRelocation = shift.Local.Relocation;
                shift.Execute(LocalRole, command, frame.Aim);
                shift.ApplyLocalRelocation(localPlayer, previousRelocation);
            }
            else if (receivedFrame)
            {
                if (!QueueCommand(command)) return;
                if (command.Action == FactoryCoopAction.WheelTurn) pendingWheel += command.Amount;
                if (command.Action == FactoryCoopAction.KitchenInput && shift.LocalPeels) pendingPeeler += command.PointerDelta;
                if (command.Action == FactoryCoopAction.Interact && !shift.LocalHandsBusy && !shift.LocalUsesKitchen &&
                    !shift.LocalUsesCrane && !shift.LocalUsesProduction && !shift.LocalCarriesTray &&
                    shift.Target(frame.Aim) is FactoryInteractable target && target &&
                    target.TryGetComponent(out FactoryStationControl control) && control.station == station &&
                    (control.part == StationPart.Feed || control.part == StationPart.Press) &&
                    station.CanInteract(control.part, control.number, shift.Local))
                {
                    predictedLever = control.part;
                    leverStarted = Time.unscaledTime;
                    leverVersion = inputVersion + 1;
                    leverConfirmed = false;
                    station.PlayLeverSound(control.part);
                }
                nextSend = 0;
            }
        }

        bool QueueCommand(FactoryCoopCommand command)
        {
            if (IsKitchenMotion(command))
            {
                if (command.PointerDelta.sqrMagnitude == 0) return false;
                int lastIndex = pending.Count - 1;
                if (lastIndex >= 0 && IsKitchenMotion(pending[lastIndex]) && pending[lastIndex].Buttons == command.Buttons)
                {
                    command.PointerDelta = Vector2.ClampMagnitude(pending[lastIndex].PointerDelta + command.PointerDelta, 220);
                    pending[lastIndex] = command;
                    return true;
                }
            }
            if (pending.Count >= 128) return false;
            pending.Add(command);
            return true;
        }

        static bool IsKitchenMotion(FactoryCoopCommand command) => command.Action == FactoryCoopAction.KitchenInput &&
            command.Amount == 0 && !command.Pressed && !command.Released && !command.SecondaryPressed;

        void StopWheel()
        {
            if (!draggingWheel) return;
            draggingWheel = false;
            Command(FactoryCoopAction.WheelEnd, LocalFrame());
        }

        void LateUpdate()
        {
            UpdateMenu();
            if (!Playing) return;
            if (IsHost) avatars[0] = AvatarFrom(LocalFrame());
            PoseWorkers();
            shift.ShowParts(game.mainMenu.lens, puppets);
            float now = Time.unscaledTime;
            station.PresentCoopControls(!paused && draggingWheel, unconfirmedWheel + pendingWheel,
                !paused && LocalPullingLever, predictedLever, PredictedLeverTravel(now),
                IsHost ? 0 : Mathf.Clamp(now - lastSnapshotTime, 0, .15f));
            if (!IsHost && shift.LocalPeels)
                shift.PresentPeeler(unconfirmedPeeler + pendingPeeler, !paused && Mouse.current?.leftButton.isPressed == true);
            if (IsHost)
            {
                SendToGuests(now);
                return;
            }
            if (connection == null) return;
            if (now < nextSend) return;
            try
            {
                var frame = LocalFrame();
                if (pending.Count > 0)
                {
                    frame.Commands = pending.ToArray();
                    frame.InputVersion = inputVersion + 1;
                }
                if (connection.Send(frame))
                {
                    inputVersion = frame.InputVersion;
                    if (pendingWheel != 0 || pendingPeeler != Vector2.zero)
                    {
                        visualInputs.Enqueue((inputVersion, pendingWheel, pendingPeeler));
                        unconfirmedWheel += pendingWheel;
                        unconfirmedPeeler += pendingPeeler;
                        pendingWheel = 0;
                        pendingPeeler = Vector2.zero;
                    }
                    pending.Clear();
                    nextSend = now + SendInterval;
                }
            }
            catch (Exception error) when (error is SocketException || error is IOException) { LoseHost(error.Message); }
        }

        void SendToGuests(float now)
        {
            var local = LocalFrame();
            bool capturedWorld = false;
            foreach (var peer in guests)
            {
                if (peer == null || now < peer.NextSend || !peer.Wire.ReadyToSend) continue;
                try
                {
                    var frame = local;
                    var previous = peer.LastFrame;
                    frame.AssignedRole = peer.Role;
                    frame.Avatars = avatars;
                    frame.Echo = previous.SentAt;
                    frame.AcknowledgedInput = previous.InputVersion;
                    if (now >= peer.NextWorld && previous.AcknowledgedWorld == peer.WorldSent)
                    {
                        if (!capturedWorld)
                        {
                            shift.CaptureWorld();
                            capturedWorld = true;
                        }
                        bool sameRound = previous.AcknowledgedRound == shift.Round;
                        frame.WorldVersion = peer.WorldSent + 1;
                        frame.World = shift.SnapshotForPeer(sameRound ? previous.AcknowledgedStrokes : 0,
                            sameRound ? previous.AcknowledgedKitchenMesh : 0);
                    }
                    if (!peer.Wire.Send(frame)) continue;
                    peer.NextSend = now + SendInterval;
                    if (frame.WorldVersion == 0) continue;
                    peer.WorldSent = frame.WorldVersion;
                    peer.NextWorld = now + WorldInterval;
                }
                catch (Exception error) when (error is SocketException || error is IOException) { DropGuest(peer, error.Message); }
            }
        }

        void StartHost(int port)
        {
            var host = new TcpListener(IPAddress.Any, port);
            try
            {
                host.Start(FactoryCoopShift.MaximumPlayers);
                listener = host;
                EnterFactory();
            }
            catch (SocketException error)
            {
                host.Stop();
                status = error.Message;
            }
        }

        void StartDial(int port)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                status = "Enter the host's address.";
                return;
            }
            dialing = new TcpClient();
            dial = dialing.ConnectAsync(address.Trim(), port);
            deadline = Time.unscaledTime + ConnectionTimeout;
            status = "Connecting...";
        }

        void FinishDial(float now)
        {
            if (!dial.IsCompleted)
            {
                CancelDial();
                status = "Connection timed out.";
                return;
            }
            dial.GetAwaiter().GetResult();
            connection = new FactoryPrototypeConnection(dialing);
            dialing = null;
            dial = null;
            deadline = now + ConnectionTimeout;
            EnterFactory();
        }

        void CancelDial()
        {
            dialing?.Dispose();
            dialing = null;
            dial = null;
        }

        void EnterFactory()
        {
            localPlayer = CardboardRunner.Player;
            localBody = localPlayer.GetComponent<CharacterController>();
            station = game.station;
            view = game.mainMenu.lens.GetComponent<CardboardCamera>();
            game.MultiplayerPrototype = true;
            game.mainMenu.EnterMultiplayerPrototype();
            game.paperDrama.enabled = false;
            game.nastyaLeraTrend.enabled = false;
            game.foreheadTrend.enabled = false;
            game.luciaTrend.enabled = false;
            game.beesTrend.enabled = false;
            if (game.captureSequence) game.captureSequence.AutomaticRestart = false;
            shift = new FactoryCoopShift(game, localBody, IsHost);
            shift.Reset();
            game.CoopLocalPlayer = shift.Local;
            if (CardboardMarker.Instance)
            {
                CardboardMarker.Instance.SubmitCoopStroke = SubmitStroke;
                CardboardMarker.Instance.CoopHandsBusy = () => shift.LocalHandsBusy;
            }
            if (game.R82C4916Item)
            {
                game.R82C4916Item.SubmitCoopShot = () => Command(FactoryCoopAction.SecretToolShot, LocalFrame());
                game.R82C4916Item.CoopHandsBusy = () => shift.LocalHandsBusy || shift.LocalUsesCrane;
            }
            MoveToStartingPosition();
            SetPaused(false);
        }

        void MoveToStartingPosition()
        {
            var offset = LocalRole switch
            {
                FactoryCoopShift.Host => new Vector3(-1.6f, 0, -2.55f),
                FactoryCoopShift.FirstGuest => new Vector3(.2f, 0, -2.55f),
                3 => new Vector3(-2.8f, 0, -3.6f),
                _ => new Vector3(.9f, 0, -3.6f)
            };
            var spawn = station.transform.TransformPoint(offset);
            spawn.y = 0;
            localPlayer.PlaceAtWorkAnchor(spawn, true);
            var focus = station.feedGrip.position;
            foreach (var control in station.GetComponentsInChildren<FactoryStationControl>())
            {
                if (control.part != StationPart.Feed || !control.TryGetComponent(out BoxCollider hit)) continue;
                focus = hit.transform.TransformPoint(hit.center);
                break;
            }
            view.transform.rotation = Quaternion.LookRotation(focus - (spawn + Vector3.up * 1.62f));
            view.AdoptCurrentView();
            view.RefreshPlayerPose(0);
        }

        void AcceptGuests(float now)
        {
            for (int i = 0; i < FactoryCoopShift.MaximumPlayers && listener.Pending(); i++)
            {
                var wire = new FactoryPrototypeConnection(listener.AcceptTcpClient());
                int slot = Array.FindIndex(guests, FactoryCoopShift.FirstGuest - 1, peer => peer == null);
                if (slot < 0)
                {
                    wire.SendAndClose(new FactoryPrototypeFrame { Refusal = "The session is full (4/4)." });
                    continue;
                }
                guests[slot] = new GuestLink { Wire = wire, Role = slot + 1, Deadline = now + ConnectionTimeout };
            }
        }

        void ReceiveGuests(float now)
        {
            foreach (var peer in guests)
            {
                if (peer == null) continue;
                try
                {
                    for (int i = 0; i < 16 && peer.Wire.TryReceive(out var frame); i++)
                    {
                        if (!Valid(frame) || frame.AcknowledgedWorld < 0 || frame.AcknowledgedWorld > peer.WorldSent ||
                            frame.InputVersion < peer.LastFrame.InputVersion || frame.InputVersion > peer.LastFrame.InputVersion + 1)
                            throw new IOException("A guest sent an invalid game state.");
                        ProcessGuestFrame(peer, frame, now);
                    }
                    if (now >= peer.Deadline) DropGuest(peer, "A guest stopped responding.");
                }
                catch (Exception error) when (error is SocketException || error is IOException) { DropGuest(peer, error.Message); }
            }
        }

        void ProcessGuestFrame(GuestLink peer, FactoryPrototypeFrame frame, float now)
        {
            if (!avatars[peer.Role - 1].Active) shift.ConnectPlayer(peer.Role);
            var previousPosition = peer.LastFrame.Position;
            float dt = peer.LastRemoteTime <= 0 ? 0 : Mathf.Max(.025f, now - peer.LastRemoteTime);
            peer.LastFrame = frame;
            peer.LastRemoteTime = now;
            peer.Deadline = now + ConnectionTimeout;
            avatars[peer.Role - 1] = AvatarFrom(frame);
            var distance = frame.Position - previousPosition;
            distance.y = 0;
            shift.Observe(peer.Role, frame.Position, Quaternion.Euler(frame.Pitch, frame.Yaw, 0),
                frame.Jump, dt > 0 ? distance.magnitude / dt : 0, dt);
            shift.Balance(peer.Role, frame.Left, frame.Right);
            shift.SetControls(peer.Role, frame.Controls);
            if (frame.Commands == null || frame.AcknowledgedRound != shift.Round) return;
            foreach (var command in frame.Commands)
            {
                var aim = new Ray(frame.Aim.origin, Quaternion.Euler(command.Pitch, command.Yaw, 0) * Vector3.forward);
                shift.Execute(peer.Role, command, aim);
            }
        }

        void ReceiveHost(float now)
        {
            for (int i = 0; i < 16 && connection != null && connection.TryReceive(out var frame); i++)
            {
                if (!string.IsNullOrEmpty(frame.Refusal)) throw new IOException(frame.Refusal);
                if (frame.AssignedRole < FactoryCoopShift.FirstGuest || frame.AssignedRole > avatars.Length ||
                    frame.Avatars == null || frame.Avatars.Length != avatars.Length ||
                    assignedRole && frame.AssignedRole != LocalRole)
                    throw new IOException("The host sent an invalid player state.");
                if (!assignedRole)
                {
                    assignedRole = true;
                    shift.AssignLocalRole(frame.AssignedRole);
                    game.CoopLocalPlayer = shift.Local;
                    MoveToStartingPosition();
                    nextSend = 0;
                }
                deadline = now + ConnectionTimeout;
                Array.Copy(frame.Avatars, avatars, avatars.Length);
                if (frame.Echo > lastEcho && frame.Echo <= now)
                {
                    lastEcho = frame.Echo;
                    ping = Mathf.RoundToInt((now - frame.Echo) * 1000);
                }
                if (frame.WorldVersion == 0) continue;
                var round = shift.Round;
                var previousRelocation = shift.Local.Relocation;
                if (frame.WorldVersion <= acknowledgedWorld || !shift.ApplySnapshot(frame.World, leverVersion > 0))
                    throw new IOException("The host sent an invalid factory state.");
                lastSnapshotTime = now;
                while (visualInputs.Count > 0 && visualInputs.Peek().Version <= frame.AcknowledgedInput)
                {
                    var input = visualInputs.Dequeue();
                    unconfirmedWheel -= input.Degrees;
                    unconfirmedPeeler -= input.Peeler;
                }
                if (visualInputs.Count == 0)
                {
                    unconfirmedWheel = 0;
                    unconfirmedPeeler = Vector2.zero;
                }
                if (leverVersion > 0 && frame.AcknowledgedInput >= leverVersion)
                {
                    leverConfirmed = station.CoopLeverOwner == LocalRole;
                    if (!leverConfirmed) leverVersion = 0;
                }
                if (shift.Round != round)
                {
                    pending.Clear();
                    ClearPredictions();
                    draggingWheel = false;
                    localPlayer.LeaveSewer();
                    MoveToStartingPosition();
                }
                else shift.ApplyLocalRelocation(localPlayer, previousRelocation);
                acknowledgedWorld = frame.WorldVersion;
                receivedFrame = true;
                nextSend = 0;
            }
        }

        FactoryPrototypeFrame LocalFrame()
        {
            var angles = view.transform.eulerAngles;
            var mouse = Mouse.current;
            return new FactoryPrototypeFrame
            {
                Position = localPlayer.transform.position,
                Yaw = angles.y,
                Pitch = Mathf.DeltaAngle(0, angles.x),
                Jump = localPlayer.JumpHeight,
                SentAt = Time.unscaledTime,
                InputVersion = inputVersion,
                AcknowledgedWorld = acknowledgedWorld,
                AcknowledgedRound = shift.Round,
                AcknowledgedStrokes = shift.AppliedStrokes,
                AcknowledgedKitchenMesh = shift.AppliedKitchenMesh,
                Left = !paused && mouse != null && mouse.leftButton.isPressed,
                Right = !paused && mouse != null && mouse.rightButton.isPressed,
                Controls = ReadControls(Keyboard.current, mouse, !paused && Application.isFocused)
            };
        }

        static FactoryCoopControls ReadControls(Keyboard key, Mouse mouse, bool active)
        {
            if (!active || key == null) return FactoryCoopControls.None;
            FactoryCoopControls result = FactoryCoopControls.None;
            if (key.wKey.isPressed || key.upArrowKey.isPressed) result |= FactoryCoopControls.Forward;
            if (key.sKey.isPressed || key.downArrowKey.isPressed) result |= FactoryCoopControls.Back;
            if (key.aKey.isPressed) result |= FactoryCoopControls.Left;
            if (key.dKey.isPressed) result |= FactoryCoopControls.Right;
            if (key.qKey.isPressed) result |= FactoryCoopControls.Q;
            if (key.eKey.isPressed) result |= FactoryCoopControls.E;
            if (key.leftShiftKey.isPressed) result |= FactoryCoopControls.Precision;
            if (mouse != null && mouse.leftButton.isPressed) result |= FactoryCoopControls.Primary;
            if (mouse != null && mouse.rightButton.isPressed) result |= FactoryCoopControls.Secondary;
            return result;
        }

        void SubmitStroke(CardboardDrawingSurface surface, FactoryCoopStroke stroke)
        {
            if (!shift.IdentifyStroke(surface, ref stroke)) return;
            var frame = LocalFrame();
            var command = new FactoryCoopCommand
            {
                Action = FactoryCoopAction.Draw, Yaw = frame.Yaw, Pitch = frame.Pitch, Stroke = stroke
            };
            Dispatch(command, frame);
        }

        static bool Valid(FactoryPrototypeFrame frame)
        {
            return frame.Position.sqrMagnitude < 20000 && Mathf.Abs(frame.Yaw) <= 360 &&
                Mathf.Abs(frame.Pitch) <= 90 && frame.Jump >= 0 && frame.Jump <= 2 &&
                (frame.Commands == null || frame.Commands.Length <= 128 && Array.TrueForAll(frame.Commands, command =>
                    Math.Abs(command.Pitch) <= 90 && Math.Abs(command.Yaw) <= 360));
        }

        static FactoryCoopAvatar AvatarFrom(FactoryPrototypeFrame frame) => new FactoryCoopAvatar
            { Active = true, Position = frame.Position, Yaw = frame.Yaw, Jump = frame.Jump };

        void PoseWorkers()
        {
            float blend = 1 - Mathf.Exp(-26 * Time.unscaledDeltaTime);
            for (int i = 0; i < puppets.Length; i++)
            {
                var avatar = avatars[i];
                if (!avatar.Active || i + 1 == LocalRole)
                {
                    if (puppets[i]) Destroy(puppets[i].gameObject);
                    puppets[i] = null;
                    puppetMotion[i] = null;
                    puppetPhases[i] = 0;
                    continue;
                }
                var position = avatar.Position + avatar.Jump * Vector3.up;
                var rotation = Quaternion.Euler(0, avatar.Yaw, 0);
                if (!puppets[i])
                {
                    var worker = new GameObject("Co-op worker " + (i + 1)).transform;
                    var model = ImportedCardboardCharacters.Create(worker, i);
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    worker.SetPositionAndRotation(position, rotation);
                    puppets[i] = worker;
                    var movement = worker.gameObject.AddComponent<CardboardRunner>();
                    movement.enabled = false;
                    movement.puppet = model;
                    puppetMotion[i] = movement;
                    continue;
                }
                var puppet = puppets[i];
                var before = puppet.position;
                bool warped = (before - position).sqrMagnitude > 16;
                var after = warped ? position : Vector3.Lerp(before, position, blend);
                puppet.SetPositionAndRotation(after,
                    Quaternion.Slerp(puppet.rotation, rotation, blend));
                var travel = after - before;
                travel.y = 0;
                float distance = warped ? 0 : travel.magnitude;
                puppetPhases[i] += distance * 3;
                puppetMotion[i].Pose(puppetPhases[i], Mathf.Clamp01(distance / Mathf.Max(.001f, Time.unscaledDeltaTime * 1.5f)));
            }
        }

        void SetPaused(bool value)
        {
            if (value) StopWheel();
            paused = value;
            if (game)
            {
                game.CoopInputBlocked = value;
                if (game.mainMenu && Playing) game.mainMenu.SetCoopHud(!value);
            }
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            FactoryVolumeCursor.SetSystemVisible(value);
        }

        void OnApplicationFocus(bool focused)
        {
            if (game && Playing && !focused) SetPaused(true);
        }

        void DropGuest(GuestLink peer, string message)
        {
            peer.Wire.Dispose();
            guests[peer.Role - 1] = null;
            if (avatars[peer.Role - 1].Active) shift.DisconnectGuest(peer.Role);
            avatars[peer.Role - 1] = default;
            status = message;
        }

        void LoseHost(string message)
        {
            CancelDial();
            connection?.Dispose();
            connection = null;
            receivedFrame = false;
            pending.Clear();
            ClearPredictions();
            Array.Clear(avatars, 0, avatars.Length);
            status = message;
            if (Playing && !IsHost) SetPaused(true);
        }

        void ClearPredictions()
        {
            visualInputs.Clear();
            unconfirmedWheel = pendingWheel = 0;
            unconfirmedPeeler = pendingPeeler = Vector2.zero;
            leverVersion = 0;
        }

        void ShowSetup()
        {
            showSetup = true;
            game.mainMenu.enabled = false;
            if (game.mainMenu.pointer) game.mainMenu.pointer.enabled = false;
            FactoryVolumeCursor.SetSystemVisible(true);
        }

        void BackToMenu()
        {
            CancelDial();
            showSetup = false;
            status = "";
            game.mainMenu.enabled = true;
            if (game.mainMenu.pointer) game.mainMenu.pointer.enabled = true;
        }

        bool ReadPort(out int port)
        {
            portText = menu.Port.value;
            if (int.TryParse(portText, out port) && port > 0 && port <= 65535) return true;
            status = "Enter a port between 1 and 65535.";
            return false;
        }

        void HostFromMenu()
        {
            if (ReadPort(out int port)) StartHost(port);
        }

        void JoinFromMenu()
        {
            address = menu.Address.value.Trim();
            if (ReadPort(out int port)) StartDial(port);
        }

        void UpdateMenu()
        {
            if (menu == null) return;
            if (Playing)
            {
                game.mainMenu.SetCoopHud(!paused && !game.CoopCaptureView && !game.EndingBusy);
                game.CoopInputBlocked = paused || !IsHost && !receivedFrame || draggingWheel || LocalPullingLever ||
                    shift.Local.Hand.Dipping || game.CameraOverride || shift.LocalPeels;
                game.CoopLocalTray = shift.LocalCarriesTray;
                game.CoopLocalHands = shift.LocalHandsBusy;
                game.CoopLocalCrane = shift.LocalUsesCrane;
                game.CoopLocalProduction = shift.LocalUsesProduction;
                game.CoopLocalKitchen = shift.LocalUsesKitchen;
            }
            menu.Entry.style.display = !Playing && OnMainMenu && !showSetup ? DisplayStyle.Flex : DisplayStyle.None;
            menu.Setup.style.display = !Playing && showSetup ? DisplayStyle.Flex : DisplayStyle.None;
            menu.Pause.style.display = Playing && paused ? DisplayStyle.Flex : DisplayStyle.None;
            if (!Playing)
            {
                menu.Host.SetEnabled(dial == null);
                menu.Join.SetEnabled(dial == null);
                menu.SetupStatus.text = string.IsNullOrEmpty(status) ? "Up to 4 players. Use the same port on all computers." : status;
            }
            else if (paused)
            {
                menu.Resume.SetEnabled(IsHost || connection != null);
                menu.SessionHeader.text = IsHost ? "Hosting on TCP port " + portText : "Host " + address + ":" + portText;
                menu.SessionStatus.text = status;
            }
        }

        bool LocalPullingLever => station.CoopLeverOwner == LocalRole || PredictedLeverTravel(Time.unscaledTime) >= 0;

        float PredictedLeverTravel(float now) => leverVersion > 0 && !paused &&
            (!station.LeverAnimating || leverConfirmed) && now - leverStarted < 1 ? now - leverStarted : -1;

        void OnGUI()
        {
            if (!Playing || paused) return;
            string role = IsHost ? "HOST" : "GUEST";
            int players = 0;
            foreach (var avatar in avatars) if (avatar.Active) players++;
            string peer = IsHost || receivedFrame ? players + " / " + avatars.Length + " players" : "Waiting for host";
            if (!IsHost && receivedFrame) peer += "  |  " + ping + " ms";
            var order = game.Memorized ? "Order " + game.CurrentOrder + " • type " + game.Memorized.recipe.Type +
                " • bath " + game.Memorized.recipe.finish : game.ReadyToLeave ? "Exit unlocked" : "Waiting for an order";
            GUI.Box(new Rect(16, 84, 310, 66), role + "  |  " + peer + "  |  " +
                Mathf.CeilToInt(game.ShiftRemaining) + "s\n" + order + "\nEsc - session menu");
            if (!string.IsNullOrEmpty(game.Message) && (!IsHost || Time.time <= game.MessageUntil))
                GUI.Box(new Rect(Screen.width / 2f - 225, 20, 450, 30), game.Message);
            if (game.Ended)
            {
                GUI.Box(new Rect(Screen.width / 2f - 190, Screen.height / 2f - 35, 380, 70),
                    (game.State == FactoryState.Escaped ? "Shift complete" : "The factory caught you") +
                    (IsHost ? "\nR - new shift" : "\nWaiting for host to restart"));
                return;
            }
            if (CardboardMarker.DrawingMode) return;
            if (!game.PlayerBlocked) GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 11, 20, 22), "+");
            var target = game.Focus;
            string prompt = game.FocusPrompt;
            if (shift.LocalCarriesTray)
                prompt = game.lunch.CanReachCoopTable(localPlayer.transform.position) ? "E - set down the level tray" :
                    "LMB - balance left  |  RMB - balance right";
            if (shift.LocalUsesCrane) prompt = "WASD / QE - movement  •  Mouse buttons - lift  •  Tab - leave";
            if (shift.LocalPeels) prompt = "LMB + move - peel   •   mouse wheel - turn   •   E - leave";
            else if (shift.NearKitchen(localPlayer.transform.position) && !string.IsNullOrEmpty(shift.KitchenHint)) prompt = shift.KitchenHint;
            else if (target && target.TryGetComponent(out FactoryKitchenControl kitchenControl)) prompt = kitchenControl.Prompt;
            if (game.ratHunt && localPlayer.InSewer)
                GUI.Box(new Rect(16, 156, 245, 45), "Rats " + game.ratHunt.Food + "/" + FactoryRatHunt.Quota +
                    "  |  Strikes " + game.ratHunt.Strikes + "/" + FactoryRatHunt.StrikeLimit);
            if (target && target.TryGetComponent(out FactoryStationControl control) && control.station == station)
                prompt = draggingWheel ? "Move the mouse clockwise to cut" :
                    station.CanInteract(control.part, control.number, shift.Local) ? prompt : "Control unavailable";
            if (!string.IsNullOrEmpty(prompt))
                GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height * .76f, 360, 32), prompt);
        }

        void OnDestroy()
        {
            CancelDial();
            connection?.Dispose();
            listener?.Stop();
            foreach (var peer in guests) peer?.Wire.Dispose();
            foreach (var puppet in puppets) if (puppet) Destroy(puppet.gameObject);
            if (!CardboardCamera.IsQuitting)
            {
                shift?.DisposeMeshes();
                menu?.Dispose();
            }
        }
    }
}
#endif
