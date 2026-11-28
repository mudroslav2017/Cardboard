#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cardboard
{
    internal sealed partial class FactoryCoopShift
    {
        public const int Host = 1, FirstGuest = 2, MaximumPlayers = 4;
        const int MaximumParts = 16;
        readonly CardboardFactoryGame game;
        readonly FactoryWorkstation station;
        readonly CharacterController localBody;
        readonly FactoryDoor[] doors;
        readonly CardboardRunner[] workers;
        readonly FactorySewerAccess[] sewerAccesses;
        readonly FactoryCoopPlayer[] players = new FactoryCoopPlayer[MaximumPlayers];
        readonly Dictionary<int, LoosePart> parts = new Dictionary<int, LoosePart>();
        readonly Dictionary<FactoryInteractable, int> partTargets = new Dictionary<FactoryInteractable, int>();
        readonly FactoryCoopWorld world = new FactoryCoopWorld();
        readonly Collider exitTrigger;
        int localRole;
        readonly GameObject[] heldVisuals = new GameObject[MaximumPlayers];
        readonly FactoryRecipe[] shownRecipes = new FactoryRecipe[MaximumPlayers];
        int nextPart;
        int trayCarrier;

        sealed class LoosePart
        {
            public FactoryRecipe Recipe;
            public FactoryInteractable Target;
            public Rigidbody Body;
        }

        public FactoryCoopPlayer Local => Player(localRole);
        public int LocalRole => localRole;
        public int WheelOwner { get; private set; }
        public int Round { get; private set; }
        public bool LocalCarriesTray => trayCarrier == localRole;
        public bool LocalUsesCrane => craneOwner == localRole;
        public bool LocalUsesProduction => productionOwner == localRole;

        public FactoryCoopShift(CardboardFactoryGame game, CharacterController localBody, bool host)
        {
            this.game = game;
            this.localBody = localBody;
            station = game.station;
            localRole = host ? Host : FirstGuest;
            for (int i = 0; i < players.Length; i++) players[i] = new FactoryCoopPlayer { Role = i + 1, Active = i == 0 };
            world.Players = new FactoryCoopPlayerState[MaximumPlayers];
            var roots = game.gameObject.scene.GetRootGameObjects();
            doors = roots.SelectMany(root => root.GetComponentsInChildren<FactoryDoor>(true)).ToArray();
            workers = game.crowd.runners.Where(worker => worker && !worker.isPlayer).ToArray();
            sewerAccesses = roots.SelectMany(root => root.GetComponentsInChildren<FactorySewerAccess>(true)).ToArray();
            var exit = roots.SelectMany(root => root.GetComponentsInChildren<FactoryCorridorExit>(true)).FirstOrDefault();
            if (exit) exitTrigger = exit.GetComponent<Collider>();
            world.Doors = new float[doors.Length];
            world.Workers = new FactoryCoopPose[workers.Length];
            if (game.lunch) world.Lunch = new FactoryCoopLunchState();
            if (game.ratHunt) world.Rats = new FactoryCoopRat[game.ratHunt.targets.Length];
            SetUpSharedObjects(roots, host);
            SetUpCrane(host);
            SetUpProduction(host);
            SetUpKitchen(host);
            if (!host)
            {
                foreach (var worker in workers) worker.enabled = false;
                if (game.lunch && game.lunch.physicsTray) game.lunch.physicsTray.enabled = false;
            }
        }

        FactoryCoopPlayer Player(int role) => players[role - 1];
        public void AssignLocalRole(int role) => localRole = role;
        public void ConnectPlayer(int role) => Player(role).Active = true;

        public void Reset()
        {
            game.ResetShift();
            ResetCrane();
            productionOwner = 0;
            ResetKitchenReplication();
            Round++;
            WheelOwner = 0;
            trayCarrier = 0;
            foreach (var player in players)
            {
                player.Hand = default;
                player.Prop = 0;
                player.SewerAccess = player.Relocation = 0;
                player.Jump = 0;
                player.RunTime = player.LastRunTime = 0;
                player.LastToolShot = float.NegativeInfinity;
                player.RunReported = false;
            }
            foreach (int id in parts.Keys.ToArray()) RemovePart(id);
            ResetSharedObjects();
        }

        public void Observe(int role, Vector3 position, Quaternion look, float jump, float speed, float dt)
        {
            var actor = Player(role);
            bool working = game.State == FactoryState.Working;
            var witness = working ? game.FindWitness(position) : null;
            bool pumping = working && station.CanPump && station.IsOnPump(position);
            if (actor.Jump > .035f && jump <= .015f && pumping)
                station.TryPump(position);
            if (witness && !pumping && actor.Jump <= .3f && jump > .3f)
                game.RegisterMistake("Workers noticed a jump away from the pump.", .5f, witness, FactoryWorkerEmotion.Jump);
            if (witness && speed > 4.1f)
            {
                actor.RunTime += dt;
                actor.LastRunTime = Time.unscaledTime;
                if (!actor.RunReported && actor.RunTime >= .75f)
                {
                    actor.RunReported = true;
                    game.RegisterMistake("Workers noticed someone running.", .5f, witness, FactoryWorkerEmotion.Run);
                }
            }
            else if (Time.unscaledTime - actor.LastRunTime >= .5f)
            {
                actor.RunTime = 0;
                actor.RunReported = false;
            }
            actor.Position = position;
            actor.Look = look;
            actor.Jump = jump;
        }

        public void Balance(int role, bool left, bool right)
        {
            Player(role).Left = left;
            Player(role).Right = right;
        }

        public void SetControls(int role, FactoryCoopControls controls) => Player(role).Controls = controls;

        public void ApplyLocalRelocation(CardboardRunner player, int previous)
        {
            if (Local.Relocation != previous && Local.SewerAccess != 0)
                sewerAccesses[Local.SewerAccess - 1].Use(player);
        }

        bool Underground(FactoryCoopPlayer actor) =>
            actor.SewerAccess != 0 && sewerAccesses[actor.SewerAccess - 1].LeadsUnderground;

        public void Tick(float dt)
        {
            station.Tick(dt);
            game.TickShiftClock(dt);
            foreach (var door in doors) door.Tick(dt);
            StepProps(dt);
            StepCrane(dt);
            StepProduction(dt);
            StepKitchen();
            if (game.ratHunt)
            {
                var underground = players.FirstOrDefault(actor => actor.Active && Underground(actor));
                if (underground != null) game.ratHunt.TickCoop(dt, underground.Position);
            }
            if (game.lunch)
            {
                game.lunch.TickCoopSchedule(dt, players);
                if (trayCarrier != 0)
                {
                    if (game.Ended) game.lunch.CancelCarry();
                    if (game.lunch.Carrying) game.lunch.TickCoopBalance(dt, Player(trayCarrier));
                    else trayCarrier = 0;
                }
            }
            if (game.ReadyToLeave && !game.Ended && game.exitDoor && game.exitDoor.IsOpen &&
                Array.TrueForAll(players, actor => !actor.Active || AtExit(actor)))
            {
                if (!game.ending) game.CompleteCoopExit();
                else if (!game.ending.Active) game.ending.Begin();
            }
            if (WheelOwner != 0 && (!station.Manipulating || game.Ended ||
                Vector3.SqrMagnitude(Player(WheelOwner).Position - station.WheelCenter) > 25))
                ReleaseWheel(WheelOwner);

            foreach (var pair in parts.ToArray())
                if (!pair.Value.Body || pair.Value.Body.position.y < -8) RemovePart(pair.Key);
        }

        bool AtExit(FactoryCoopPlayer actor)
        {
            float lane = exitTrigger ? exitTrigger.bounds.center.z : -2;
            return Mathf.Abs(actor.Position.z - lane) <= 2.1f &&
                actor.Position.x >= CardboardFactoryWorld.WorkshopEast - 3.2f;
        }

        public FactoryInteractable Target(Ray aim) => game.TargetForRay(aim, localBody);

        public void Execute(int role, FactoryCoopCommand command, Ray aim)
        {
            if (command.Action == FactoryCoopAction.WheelEnd)
            {
                ReleaseWheel(role);
                return;
            }
            var actor = Player(role);
            if (game.State != FactoryState.Working || game.EndingBusy || actor.Hand.Dipping) return;
            if (role == kitchenOwner)
            {
                if (command.Action == FactoryCoopAction.KitchenInput &&
                    float.IsFinite(command.PointerDelta.x) && float.IsFinite(command.PointerDelta.y) && float.IsFinite(command.Amount))
                {
                    PositionKitchenGrip(actor);
                    command.PointerDelta = Vector2.ClampMagnitude(command.PointerDelta, 220);
                    kitchen.CoopInput(command);
                }
                else if (command.Action == FactoryCoopAction.Interact) UseKitchen(role, actor, Target(aim));
                return;
            }
            if (role == craneOwner)
            {
                game.craneJob.CoopControl(command.Action);
                return;
            }
            if (role == productionOwner && command.Action != FactoryCoopAction.Interact)
            {
                game.production.ControlCoop(command.Action);
                return;
            }
            if (role == trayCarrier)
            {
                if (command.Action == FactoryCoopAction.Interact && game.lunch.CanReachCoopTable(actor.Position))
                {
                    if (game.lunch.TryServeCoop(actor.Position)) trayCarrier = 0;
                    else game.Toast("Hold the tray level, then press E.", 1.5f);
                }
                return;
            }
            switch (command.Action)
            {
                case FactoryCoopAction.WheelTurn:
                    if (WheelOwner == role && station.Manipulating && float.IsFinite(command.Amount) &&
                        (actor.Position - station.WheelCenter).sqrMagnitude <= 25)
                        station.TurnWheel(Mathf.Clamp(command.Amount, -65, 65));
                    break;
                case FactoryCoopAction.Drop:
                case FactoryCoopAction.Throw:
                    bool thrown = command.Action == FactoryCoopAction.Throw;
                    if (actor.Prop > 0) ReleaseProp(actor, aim.direction, thrown);
                    else DropPart(actor, aim, thrown);
                    break;
                case FactoryCoopAction.Draw:
                    if (!HandsBusy(actor) && WheelOwner != role) AddStroke(actor, command.Stroke);
                    break;
                case FactoryCoopAction.SecretToolShot:
                    if (!HandsBusy(actor) && game.R82C4916Item && Time.time - actor.LastToolShot >= .25f)
                    {
                        actor.LastToolShot = Time.time;
                        game.R82C4916Item.ApplyCoopShot(aim);
                    }
                    break;
                case FactoryCoopAction.Interact:
                case FactoryCoopAction.WheelStart:
                    Interact(role, actor, Target(aim), command.Action == FactoryCoopAction.WheelStart);
                    break;
            }
        }

        void Interact(int role, FactoryCoopPlayer actor, FactoryInteractable target, bool grabWheel)
        {
            if (!grabWheel && actor.Prop > 0 && props[actor.Prop - 1].instructionSheet)
            {
                int hand = role - 1;
                propGrip[hand] *= Quaternion.Euler(0, 180, 0);
                return;
            }
            if (!target || WheelOwner == role) return;
            if (target.TryGetComponent(out FactoryStationControl control) && control.station == station)
            {
                if (actor.Prop > 0) return;
                if (grabWheel)
                {
                    if (control.part == StationPart.Wheel && station.CanInteract(control.part, 0, actor) &&
                        station.BeginCoopWheel()) WheelOwner = role;
                }
                else station.Operate(control.part, control.number, actor);
                return;
            }
            if (grabWheel) return;
            switch (target.action)
            {
                case FactoryAction.Gear:
                    if (actor.Hand.HasPart) game.TryCoopInstall(actor, target.gear);
                    else game.ReadBlueprint(target.gear);
                    break;
                case FactoryAction.Blueprint:
                    game.ReadBlueprint(target.gear);
                    break;
                case FactoryAction.Lunch:
                    if (!HandsBusy(actor) && game.lunch && game.lunch.TryTakeCoop(actor)) trayCarrier = role;
                    break;
                case FactoryAction.Crane:
                    if (!HandsBusy(actor) && !Underground(actor) && target.TryGetComponent(out FactoryCraneControl crane))
                        UseCrane(role, actor, crane.access);
                    break;
                case FactoryAction.Production:
                    if ((!HandsBusy(actor) || role == productionOwner) && !Underground(actor))
                        UseProduction(role, actor, target);
                    break;
                case FactoryAction.Kitchen:
                    UseKitchen(role, actor, target);
                    break;
                case FactoryAction.SewerAccess:
                    if (!HandsBusy(actor) && target.TryGetComponent(out FactorySewerAccess access) &&
                        access.LeadsUnderground != Underground(actor))
                    {
                        int index = Array.IndexOf(sewerAccesses, access);
                        if (index < 0) return;
                        actor.SewerAccess = index + 1;
                        actor.Relocation++;
                        actor.Position = access.Arrival;
                    }
                    break;
                case FactoryAction.RatFood:
                    if (!HandsBusy(actor) && game.ratHunt && target.TryGetComponent(out FactoryRatTarget rat))
                        game.ratHunt.TryCollectCoop(rat, Underground(actor));
                    break;
                case FactoryAction.RatDelivery:
                    if (!Underground(actor) && game.ratHunt) game.ratHunt.DeliverCoop();
                    break;
                case FactoryAction.Door:
                    var door = target.GetComponentInParent<FactoryDoor>();
                    if (door) door.Toggle();
                    break;
                case FactoryAction.Exit:
                    if (game.ReadyToLeave && game.exitDoor) game.exitDoor.SetOpen(true);
                    else game.Toast("Repair the main mechanism first.");
                    break;
                case FactoryAction.CoopPart:
                    if (!HandsBusy(actor) && partTargets.TryGetValue(target, out int id))
                    {
                        actor.Hand.Take(parts[id].Recipe);
                        RemovePart(id);
                    }
                    break;
                case FactoryAction.PhysicsProp:
                case FactoryAction.Instruction:
                    if (!HandsBusy(actor))
                    {
                        var prop = target.GetComponentInParent<FactoryPhysicsProp>();
                        if (prop && propIds.TryGetValue(prop, out int propId)) TakeProp(actor, propId);
                        else if (prop && kitchen && kitchen.OwnsHeld(prop)) UseKitchen(role, actor, target);
                    }
                    break;
            }
        }

        void ReleaseWheel(int role)
        {
            if (WheelOwner != role) return;
            if (station.Manipulating) station.EndGrip();
            WheelOwner = 0;
        }

        public void DisconnectGuest(int role)
        {
            var actor = Player(role);
            actor.Active = false;
            ReleaseWheel(role);
            if (craneOwner == role)
            {
                game.craneJob.ReturnToWorkshop(RunnerFor(role));
                ReleaseProxy(role);
                craneOwner = 0;
            }
            if (productionOwner == role)
            {
                game.production.CancelPlayer();
                ReleaseProxy(role);
                productionOwner = 0;
            }
            if (kitchenOwner == role) ReleaseKitchen();
            if (trayCarrier == role)
            {
                game.lunch.CancelCarry();
                trayCarrier = 0;
            }
            ReleaseProp(actor, Vector3.forward, false);
            actor.Hand.Dipping = false;
            DropPart(actor, new Ray(actor.Position + Vector3.up * .65f, Vector3.forward), false);
            actor.Hand = default;
            actor.SewerAccess = actor.Relocation = 0;
            actor.Controls = FactoryCoopControls.None;
            actor.Left = actor.Right = false;
            actor.Jump = actor.RunTime = actor.LastRunTime = 0;
            actor.LastToolShot = float.NegativeInfinity;
            actor.RunReported = false;
        }

        void DropPart(FactoryCoopPlayer actor, Ray aim, bool thrown)
        {
            if (!actor.Hand.HasPart || actor.Hand.Dipping || parts.Count >= MaximumParts) return;
            var reach = aim.GetPoint(.6f);
            if (Physics.Raycast(aim, out var obstacle, .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                reach = aim.GetPoint(Mathf.Max(.15f, obstacle.distance - .12f));
            var node = CreateLoosePart(new FactoryCoopPartState
            {
                Id = ++nextPart, Recipe = actor.Hand.Recipe, Position = reach,
                Rotation = Quaternion.Euler(80, 0, 0)
            }, true);
            if (thrown) node.Body.linearVelocity = aim.direction * 6;
            actor.Hand = default;
        }

        LoosePart CreateLoosePart(FactoryCoopPartState state, bool simulate)
        {
            var mesh = CreateGear(state.Recipe);
            mesh.name = "Shared gear";
            mesh.transform.SetPositionAndRotation(state.Position, state.Rotation);
            var collider = mesh.AddComponent<BoxCollider>();
            collider.size = new Vector3(.95f, .95f, .28f);
            var body = mesh.AddComponent<Rigidbody>();
            body.mass = .65f;
            body.isKinematic = !simulate;
            if (simulate)
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            var target = mesh.AddComponent<FactoryInteractable>();
            target.action = FactoryAction.CoopPart;
            target.prompt = "E - PICK UP GEAR";
            var part = new LoosePart { Body = body, Recipe = state.Recipe, Target = target };
            parts.Add(state.Id, part);
            partTargets.Add(target, state.Id);
            return part;
        }

        void RemovePart(int id)
        {
            var part = parts[id];
            partTargets.Remove(part.Target);
            if (part.Target)
            {
                part.Target.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(part.Target.gameObject);
            }
            parts.Remove(id);
        }

        GameObject CreateGear(FactoryRecipe recipe)
        {
            var gear = new GameObject("Held gear");
            gear.transform.localScale = Vector3.one * .24f;
            FactoryGeometry.GearVisual(gear.transform, recipe, game.gearMeshes, game.hubMeshes,
                recipe.finish > 0 ? game.finishMaterials[recipe.finish - 1] : game.gearPaper, game.gearEdge, game.gearInk);
            return gear;
        }

        public void ShowParts(Camera pointOfView, Transform[] avatars)
        {
            for (int i = 0; i < players.Length; i++)
            {
                bool own = players[i].Role == localRole;
                var parent = own ? pointOfView.transform : avatars[i];
                if (!parent) continue;
                ShowHand(players[i].Hand, parent, ref heldVisuals[i], ref shownRecipes[i],
                    own ? new Vector3(-.28f, -.28f, .64f) : new Vector3(-.3f, 1.05f, .55f), own ? pointOfView : null);
            }
        }

        void ShowHand(FactoryCoopHand hand, Transform parent, ref GameObject visual, ref FactoryRecipe recipe, Vector3 offset, Camera lens)
        {
            bool visible = hand.HasPart && !hand.Dipping && !game.Ended;
            if (visual && (!visible || !hand.Recipe.Equals(recipe)))
            {
                visual.SetActive(false);
                UnityEngine.Object.Destroy(visual);
                visual = null;
            }
            if (!visible || visual) return;
            recipe = hand.Recipe;
            visual = CreateGear(recipe);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = offset;
            visual.transform.localRotation = Quaternion.Euler(12, 10, 0);
            if (lens) visual.AddComponent<FactoryHeldGearMotion>().Initialize(game, lens);
        }

        public void CaptureWorld()
        {
            world.Station = station.ReadCoopState();
            for (int i = 0; i < players.Length; i++)
            {
                var actor = players[i];
                world.Players[i] = new FactoryCoopPlayerState
                {
                    Hand = actor.Hand, Prop = actor.Prop, SewerAccess = actor.SewerAccess, Relocation = actor.Relocation
                };
            }
            world.Order = game.CurrentOrder;
            world.Repairs = game.Repairs;
            world.Mistakes = game.Mistakes;
            world.Remaining = game.ShiftRemaining;
            world.State = game.State;
            world.Notice = Time.time <= game.MessageUntil ? game.Message : "";
            world.WheelOwner = WheelOwner;
            world.Round = Round;
            world.Parts = parts.Select(pair => new FactoryCoopPartState
            {
                Id = pair.Key, Position = pair.Value.Body.position, Rotation = pair.Value.Body.rotation, Recipe = pair.Value.Recipe
            }).ToArray();
            for (int i = 0; i < doors.Length; i++) world.Doors[i] = doors[i].ReadCoopState();
            for (int i = 0; i < workers.Length; i++)
                world.Workers[i] = new FactoryCoopPose { Position = workers[i].transform.position, Rotation = workers[i].transform.rotation };
            ReadSharedObjects();
            ReadCrane();
            ReadProduction();
            ReadKitchen();
            if (game.lunch)
            {
                world.Lunch.Carrier = trayCarrier;
                game.lunch.ReadCoopState(world.Lunch);
            }
            if (game.captureSequence)
            {
                var capture = game.captureSequence;
                world.Capture = capture.Phase;
                world.CaptureTime = capture.PhaseTime;
                world.CaptureCamera = new FactoryCoopPose { Position = capture.CameraPosition, Rotation = capture.CameraRotation };
            }
            if (game.ending)
            {
                world.EndingView = game.ending.CameraControlled;
                world.EndingFade = game.ending.CoopFade;
                world.EndingGlow = game.ending.glow && game.ending.glow.gameObject.activeSelf;
                world.EndingCamera = new FactoryCoopPose { Position = game.ending.CameraPosition, Rotation = game.ending.CameraRotation };
            }
            if (game.ratHunt)
            {
                world.RatsCaught = game.ratHunt.Food;
                world.RatStrikes = game.ratHunt.Strikes;
                world.RatsDelivered = game.ratHunt.Delivered;
                for (int i = 0; i < world.Rats.Length; i++) world.Rats[i] = game.ratHunt.targets[i].ReadCoopState();
            }
        }

        public FactoryCoopWorld SnapshotForPeer(int receivedStrokes, int receivedKitchenMesh)
        {
            SelectStrokes(receivedStrokes);
            SelectKitchenMesh(receivedKitchenMesh);
            return world;
        }

        public bool ApplySnapshot(FactoryCoopWorld incoming, bool playedLeverSound = false)
        {
            if (incoming == null || incoming.Doors?.Length != doors.Length || incoming.Workers?.Length != workers.Length ||
                incoming.Props?.Length != props.Length ||
                incoming.Parts == null || incoming.Parts.Length > MaximumParts ||
                incoming.Order < 0 || incoming.Order > game.gears.Length || incoming.Repairs < 0 ||
                incoming.Repairs > game.RepairGoal || incoming.Mistakes < 0 || incoming.Mistakes > CardboardFactoryGame.MistakeLimit ||
                incoming.Players?.Length != MaximumPlayers || !Array.TrueForAll(incoming.Players, actor =>
                    actor.Prop >= 0 && actor.Prop <= props.Length && actor.SewerAccess >= 0 &&
                    actor.SewerAccess <= sewerAccesses.Length && ValidHand(actor.Hand)))
                return false;

            if (Round != incoming.Round)
            {
                ResetSharedObjects();
                ResetKitchenReplication();
            }
            Round = incoming.Round;
            game.ApplyCoopShift(incoming.Order, incoming.Repairs, incoming.Mistakes, incoming.Remaining, incoming.State, incoming.Notice);
            station.ApplyCoopState(incoming.Station, localRole, playedLeverSound);
            for (int i = 0; i < players.Length; i++)
            {
                var state = incoming.Players[i];
                players[i].Hand = state.Hand;
                players[i].Prop = state.Prop;
                players[i].SewerAccess = state.SewerAccess;
                players[i].Relocation = state.Relocation;
            }
            if (game.ratHunt)
            {
                if (incoming.Rats?.Length != game.ratHunt.targets.Length || incoming.RatsCaught < 0 ||
                    incoming.RatsCaught > FactoryRatHunt.Quota) return false;
                game.ratHunt.ApplyCoopProgress(incoming.RatsCaught, incoming.RatStrikes, incoming.RatsDelivered);
                for (int i = 0; i < incoming.Rats.Length; i++) game.ratHunt.targets[i].ApplyCoopState(incoming.Rats[i]);
            }
            WheelOwner = incoming.WheelOwner;
            game.CoopCaptureView = incoming.Capture >= CapturePhase.Lifting || incoming.EndingView;
            game.CoopCaptureCamera = incoming.EndingView ? incoming.EndingCamera : incoming.CaptureCamera;
            if (game.ending) game.ending.ApplyCoopPresentation(incoming.EndingFade, incoming.EndingGlow);
            if (game.captureSequence && game.captureSequence.grade)
            {
                if (incoming.Capture >= CapturePhase.Grade) game.captureSequence.grade.Show(incoming.CaptureTime);
                else game.captureSequence.grade.Hide();
            }
            if (game.lunch)
            {
                if (!game.lunch.ApplyCoopState(incoming.Lunch) || incoming.Lunch.Carrier < 0 ||
                    incoming.Lunch.Carrier > MaximumPlayers) return false;
                trayCarrier = incoming.Lunch.Carrier;
            }
            for (int i = 0; i < doors.Length; i++) doors[i].ApplyCoopState(incoming.Doors[i]);
            for (int i = 0; i < workers.Length; i++)
            {
                var worker = workers[i];
                var pose = incoming.Workers[i];
                float travel = Vector3.Distance(worker.transform.position, pose.Position);
                worker.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                worker.Pose(Time.time * 4 + i, Mathf.Clamp01(travel * 13));
            }
            foreach (var part in incoming.Parts)
            {
                if (part.Recipe.finish < 0 || part.Recipe.finish > game.finishMaterials.Length) return false;
                if (!parts.TryGetValue(part.Id, out var instance)) instance = CreateLoosePart(part, false);
                instance.Body.transform.SetPositionAndRotation(part.Position, part.Rotation);
            }
            foreach (int id in parts.Keys.ToArray())
                if (!incoming.Parts.Any(part => part.Id == id)) RemovePart(id);
            return ApplySharedObjects(incoming) && ApplyCrane(incoming.Crane) && ApplyProduction(incoming.Production) &&
                ApplyKitchen(incoming.Kitchen);
        }

        bool ValidHand(FactoryCoopHand hand) =>
            !hand.HasPart || hand.Recipe.finish >= 0 && hand.Recipe.finish <= game.finishMaterials.Length;
    }
}
#endif
