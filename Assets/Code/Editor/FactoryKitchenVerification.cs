using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryKitchenVerification
    {
        const string Pending = "Cardboard.VerifyKitchen";
        const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

        static FactoryKitchenVerification() { EditorApplication.update += VerifyWhenReady; }

        public static void InstallAndRunBatch()
        {
            FactoryKitchenInstaller.InstallBatch();
            RunBatch();
        }

        public static void RunBatch()
        {
            try
            {
                FactoryRatHuntVerification.RunBatch();
                SessionState.SetBool(Pending, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void VerifyWhenReady()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindAnyObjectByType<CardboardFactoryGame>();
            if (!game || !CardboardRunner.Player || !game.kitchen.Service || !game.kitchen.board.Prop.Body || Time.time < .30f) return;
            SessionState.EraseBool(Pending);
            try
            {
                Verify(game);
                Debug.Log("Kitchen verified: 30-second burner cooking, ladling with real cut pieces, ten servings, tray collection and shift reset.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static object Call(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Internal).Invoke(target, arguments);

        static void Verify(CardboardFactoryGame game)
        {
            game.mainMenu.CloseForFilming();
            game.ResetShift();
            var kitchen = game.kitchen;
            var service = kitchen.Service;
            var camera = Camera.main.transform;
            var player = CardboardRunner.Player;
            var cuttingBoard = kitchen.board.transform;
            SimulationMode originalSimulation = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                player.transform.position = cuttingBoard.position + Vector3.back * .95f - Vector3.up * cuttingBoard.position.y;
                camera.SetPositionAndRotation(player.transform.position + Vector3.up * 1.62f, Quaternion.Euler(7, 0, 0));
                Check(!kitchen.Use(KitchenAction.Rat), "Rat stock starts empty.");
                kitchen.ReceiveRats(FactoryRatHunt.Quota);
                Check(kitchen.Use(KitchenAction.Rat) && kitchen.RatStock == 9, "A catch puts a whole rat on the board.");
                Step(game, .15f);
                Check(kitchen.Use(KitchenAction.Knife) && !game.PlayerBlocked && !kitchen.Preparing,
                    "Taking the knife leaves free movement and camera look.");
                var knife = game.objectCarry.Held.GetComponent<FactoryKitchenUtensil>();
                Check(knife && !knife.transform.Find("Right grip") && !knife.transform.Find("Left grip") &&
                    !knife.Prop.Body.isKinematic && string.IsNullOrEmpty(game.FocusPrompt),
                    "The knife stays physical and hides both hands and its prompt.");
                Step(game, .20f, true);
                for (int pitch = 10; pitch <= 42; pitch += 2)
                {
                    camera.rotation = Quaternion.Euler(pitch, 0, 0);
                    Step(game, .04f, true);
                }
                Check(LooseFood(kitchen, true).Length == 1, "A slow lowering of the knife does not slice.");
                var rat = LooseFood(kitchen, true).Single();
                Place(rat, cuttingBoard.position + Vector3.up * (.032f - rat.Model.bounds.min.y), cuttingBoard.rotation);
                camera.rotation = Quaternion.Euler(7, 0, 0);
                Step(game, .25f, true);
                Call(knife, "RecordKnifeMotion", 41f);
                camera.rotation = Quaternion.Euler(48, 0, 0);
                Step(game, .20f, true);
                var slices = LooseFood(kitchen, true);
                Check(slices.Length == 2 && slices.All(piece => piece.Sliced && !piece.Prop.Body.isKinematic) &&
                    Mathf.Abs(slices.Sum(piece => piece.Amount) - 1f) < .0001f,
                    "A fast camera flick cuts on physical contact, conserving the original amount.");
                Capture(camera.GetComponent<Camera>(), camera.position, camera.position + camera.forward, "KitchenFlickKnife");
                game.Interact();
                Check(!game.objectCarry.Held, "E returns the knife to the rack.");

                var fried = slices.OrderByDescending(piece => piece.Amount).First();
                var raw = slices.First(piece => piece != fried);
                Check(Vector3.Distance(kitchen.board.home.position, kitchen.pan.home.position) > 2.5f &&
                    Vector3.Distance(kitchen.pan.home.position, kitchen.pot.position) > 1.8f,
                    "The preparation, frying and stockpot stations have clear separation.");
                Vector3 benchView = kitchen.transform.position + new Vector3(1.7f, 1.04f, 0);
                Capture(camera.GetComponent<Camera>(), benchView + new Vector3(0, 1.15f, -5.85f), benchView, "KitchenWorkAreas");
                Check(fried.FryDuration > 20 && fried.FryDuration <= 44 && raw.FryDuration != fried.FryDuration,
                    $"Cut thickness affects the full roasting time ({fried.FryDuration} vs {raw.FryDuration}).");
                Step(game, .26f);
                fried.Prop.Body.linearVelocity = cuttingBoard.right * .35f;
                Step(game, .16f);
                Check((bool)Call(fried, "OnSurface", kitchen.board) && fried.Prop.Body.linearVelocity.magnitude < .22f,
                    "A cut piece on the board settles with light grip.");
                Check(kitchen.Use(KitchenAction.Board), "Take the board with the cuts still on it.");
                camera.position = kitchen.pan.transform.position + new Vector3(0, .52f, -1.35f);
                Aim(camera, kitchen.pan.transform.position + Vector3.up * .02f);
                InteractWith(game, camera, KitchenAction.Pan, KitchenAction.Burner);
                Step(game, 1.28f);
                Check(slices.All(piece => kitchen.pan.Contains(piece.Prop.Body.position)),
                    "E transfers both cuts from the board to the pan beyond the old reach limit.");
                Check(kitchen.Use(KitchenAction.Prep), "Set the empty board back down.");

                Check(kitchen.Use(KitchenAction.Burner), "The burner lights.");
                Place(raw, cuttingBoard.position + Vector3.up * (.032f - raw.Model.bounds.min.y), cuttingBoard.rotation);
                Place(fried, kitchen.pan.transform.position + Vector3.up * (.029f - fried.Model.bounds.min.y), Quaternion.identity);
                Step(game, fried.FryDuration * .45f);
                Check(fried.FryProgress > .4f && !fried.Fried && !fried.Burnt, "A sitting slice shows progress before it is ready.");
                Step(game, fried.FryDuration * .55f + .18f);
                Check(fried.Fried && fried.ReadyForPot && fried.FryProgress == 1f && kitchen.Prompt(KitchenAction.Pan).Contains("READY"),
                    "One continuous roast is enough, and the pan says the rat is ready.");
                Check(kitchen.Use(KitchenAction.Spatula) && !game.PlayerBlocked &&
                      game.objectCarry.Held.GetComponent<FactoryKitchenUtensil>().Tool == FactoryKitchenUtensil.Kind.Spatula,
                    "The physical spatula can still move cooked food.");
                TurnWithSpatula(game, camera, fried);
                game.Interact();
                Check(kitchen.Use(KitchenAction.Pan), "Take the pan with a cooked slice.");
                camera.position = kitchen.pot.position + new Vector3(0, .58f, -1.20f);
                Aim(camera, kitchen.pot.position + Vector3.up * .11f);
                InteractWith(game, camera, KitchenAction.Pot);
                Step(game, 1.3f);
                Check(Mathf.Abs(kitchen.RatInPot - fried.Amount) < .0001f,
                    "E pours straight into the stockpot from the pan's position.");
                Check(kitchen.Use(KitchenAction.Burner), "Return the empty pan.");
                Place(raw, kitchen.pan.transform.position + Vector3.up * (.029f - raw.Model.bounds.min.y), Quaternion.identity);
                Step(game, raw.FryDuration + 7f);
                Check(raw.Burnt && !raw.ReadyForPot, "Leaving a done piece on the hot pan burns it.");

                game.ResetShift();
                kitchen.ReceiveRats(FactoryRatHunt.Quota);
                for (int i = 0; i < 7; i++)
                {
                    Check(kitchen.Use(KitchenAction.Rat), "Load a rat for the full batch.");
                    var wholeRat = LooseFood(kitchen, true).Single();
                    SliceForBatch(kitchen, wholeRat);
                    foreach (var piece in LooseFood(kitchen, true))
                    {
                        piece.Fry(piece.FryDuration + .01f);
                        PlaceInPot(kitchen, piece, i);
                    }
                }
                Check(kitchen.Portions == 0 && Mathf.Abs(kitchen.RatInPot - 7) < .001f,
                    "The stockpot accounts for seven sliced and fried rats by original amount.");
                for (int i = 0; i < 7; i++)
                {
                    Check(kitchen.Use(KitchenAction.Potato), "Take a potato.");
                    var potato = game.objectCarry.Held.GetComponent<FactoryKitchenFood>();
                    Check(kitchen.Use(KitchenAction.Peeler), "Start peeling.");
                    PeelPotato(kitchen, potato, i == 0);
                    Check(potato.Peeled && potato.Prop.Take(game) && kitchen.Use(KitchenAction.Board), "Peel and place the potato.");
                    SliceForBatch(kitchen, potato);
                    foreach (var piece in LooseFood(kitchen, false)) PlaceInPot(kitchen, piece, i);
                }
                VerifyStockpot(game, camera);
                Check(kitchen.Portions == 10 && service.BatchCooked && Mathf.Abs(kitchen.PotatoInPot - 7) < .001f,
                    "The stockpot finishes cooking and yields ten servings.");
                Check(kitchen.Use(KitchenAction.Serve), "Take the ladle when soup is ready.");
                var ladle = game.objectCarry.Held.GetComponent<FactoryKitchenUtensil>();
                var bowls = kitchen.GetComponentsInChildren<FactoryKitchenControl>().Where(control => control.action == KitchenAction.Bowl)
                    .OrderBy(control => control.slot).ToArray();
                for (int i = 0; i < bowls.Length; i++)
                {
                    WalkLadleTo(game, camera, kitchen.pot.position + new Vector3(0, .54f, -1.16f));
                    Aim(camera, kitchen.pot.position + Vector3.up * .07f);
                    Check((bool)Call(kitchen, "UseLadle", camera), "Aimed click dips the ladle into the cooked stockpot.");
                    Step(game, .80f, true);
                    AssertServedPieces(ladle.transform, "One scoop carries broth and real cuts of both ingredients.", false);
                    Check(service.HasScoop && kitchen.Portions == 9 - i, "One scoop uses exactly one serving.");
                    if (i == 1)
                    {
                        PositionAtBowl(game, camera, bowls[0].transform);
                        Check(!(bool)Call(kitchen, "UseLadle", camera) && service.HasScoop,
                            "A filled bowl cannot waste the next scoop.");
                    }
                    PositionAtBowl(game, camera, bowls[i].transform);
                    Check((bool)Call(kitchen, "UseLadle", camera), "Aimed click pours into the selected empty bowl.");
                    if (i == 0)
                    {
                        Step(game, .63f, true);
                        Capture(camera.GetComponent<Camera>(), camera.position, camera.position + camera.forward, "KitchenLadlePour");
                        Step(game, .31f, true);
                    }
                    else Step(game, .88f, true);
                    Check(service.Filled == i + 1 && !service.HasScoop, "Ladle pours exactly one serving.");
                    AssertServedPieces(bowls[i].transform, "Both cut ingredients visibly settle inside the bowl.", true);
                }
                game.Interact();
                Check(kitchen.Portions == 0 && !game.lunch.diners.Active, "Ten filled bowls alone do not call the workers.");
                for (int i = 0; i < bowls.Length; i++)
                {
                    Check(bowls[i].Use() && kitchen.Use(KitchenAction.Tray, bowls.Length - 1 - i), "Pick up and plate a bowl on any empty tray.");
                    AssertServedPieces(bowls[i].transform, "Both ingredients stay in the bowl when it is moved onto a tray.", true);
                    Check(game.lunch.diners.Active == (i == bowls.Length - 1), "Only the last tray starts lunch.");
                }
                Vector3 serviceView = bowls[3].transform.position + Vector3.right * .9f;
                Capture(camera.GetComponent<Camera>(), serviceView + new Vector3(.31f, 1.35f, -2.12f),
                    serviceView, "KitchenServingTrays");
                VerifyWorkers(game);
                game.ResetShift();
                Check(kitchen.RatStock == 0 && kitchen.Portions == 0 && service.Filled == 0 && service.Plated == 0 &&
                    !service.BatchCooked && !game.lunch.diners.Active && !game.objectCarry.Held &&
                    kitchen.board.ReadyAtStation && kitchen.pan.ReadyAtStation, "Shift reset clears the entire batch and restores the tools.");
            }
            finally { Physics.simulationMode = originalSimulation; }
        }

        static void VerifyStockpot(CardboardFactoryGame game, Transform camera)
        {
            var kitchen = game.kitchen;
            var stove = kitchen.GetComponentsInChildren<FactoryKitchenControl>().Single(control => control.action == KitchenAction.SoupBurner);
            var rim = kitchen.pot.GetComponentsInChildren<MeshFilter>().First(mesh => mesh.name == "Cut rat in soup");
            Vector3 rest = kitchen.pot.position;
            Vector3 storedPiece = rim.transform.localPosition;
            kitchen.TickCooking(31f);
            Check(kitchen.Portions == 0 && kitchen.Prompt(KitchenAction.Pot).Contains("30 S"),
                "A full stockpot stays cold on the trivet.");

            void PickUpPot()
            {
                camera.position = kitchen.pot.position + new Vector3(0, .52f, -1.35f);
                Aim(camera, kitchen.pot.position + Vector3.up * .06f);
                InteractWith(game, camera, KitchenAction.Pot);
                Check(game.objectCarry.Held && game.objectCarry.Held.transform == kitchen.pot, "E picks up the stockpot.");
                Check((bool)Call(kitchen, "IgnoreHeldCollider", kitchen.pot.GetComponent<Collider>()),
                    "The stockpot in hand does not block aiming at the burner.");
            }

            void UseBurner()
            {
                camera.position = stove.transform.position + new Vector3(0, .46f, -.95f);
                Aim(camera, stove.transform.position + new Vector3(0, 0, -.39f));
                InteractWith(game, camera, KitchenAction.SoupBurner);
            }

            PickUpPot();
            UseBurner();
            Check(!game.objectCarry.Held && (rest - kitchen.pot.position).magnitude > .7f && rim.transform.localPosition == storedPiece,
                "The full stockpot and its cut ingredients dock together on the burner.");
            kitchen.TickCooking(40f);
            Check(kitchen.Portions == 0 && kitchen.Prompt(KitchenAction.Pot).Contains("LIGHT SOUP BURNER"),
                "The cold burner does not cook the soup.");

            UseBurner();
            kitchen.TickCooking(12f);
            Check(kitchen.Portions == 0 && kitchen.Prompt(KitchenAction.Pot).Contains("18 S"),
                "The lit burner cooks for twelve of the thirty seconds.");
            Capture(camera.GetComponent<Camera>(), kitchen.pot.position + new Vector3(.46f, .69f, -1.61f),
                kitchen.pot.position, "KitchenStockpotOnBurner");
            PickUpPot();
            kitchen.TickCooking(35f);
            Check(kitchen.Portions == 0 && kitchen.Prompt(KitchenAction.SoupBurner) == "E - SET STOCKPOT ON BURNER",
                "Lifting the pot pauses the cooking timer.");
            UseBurner();
            kitchen.TickCooking(17.95f);
            Check(kitchen.Portions == 0, "Cooking has not finished before thirty seconds of heat.");
            kitchen.TickCooking(.06f);
            Check(kitchen.Portions == 10, "Cooking finishes after thirty seconds of accumulated heat.");
        }

        static void PositionAtBowl(CardboardFactoryGame game, Transform camera, Transform bowl)
        {
            WalkLadleTo(game, camera, bowl.position + new Vector3(0, .54f, -1.08f));
            Aim(camera, bowl.position + Vector3.up * .026f);
        }

        static void WalkLadleTo(CardboardFactoryGame game, Transform camera, Vector3 destination)
        {
            camera.rotation = Quaternion.Euler(18f, 0, 0);
            while (camera.position != destination)
            {
                camera.position = Vector3.MoveTowards(camera.position, destination, .055f);
                Step(game, .02f, true);
            }
            Check(game.objectCarry.Held && game.objectCarry.Held.GetComponent<FactoryKitchenUtensil>().Tool == FactoryKitchenUtensil.Kind.Ladle,
                "The ladle stays held while walking between the stockpot and bowls.");
        }

        static void AssertServedPieces(Transform vessel, string message, bool inBowl)
        {
            var chunks = vessel.GetComponentsInChildren<MeshFilter>().Where(mesh =>
                mesh.name == "Cut rat in soup" || mesh.name == "Cut potato in soup").ToArray();
            Check(chunks.Any(mesh => mesh.name == "Cut rat in soup") && chunks.Any(mesh => mesh.name == "Cut potato in soup") &&
                chunks.All(mesh => mesh.sharedMesh && !mesh.GetComponent<Rigidbody>() && (!inBowl ||
                    LocalBowlFit(mesh, vessel))), message);
        }

        static bool LocalBowlFit(MeshFilter chunk, Transform bowl)
        {
            var center = bowl.InverseTransformPoint(chunk.transform.TransformPoint(chunk.sharedMesh.bounds.center));
            var extents = chunk.sharedMesh.bounds.extents * chunk.transform.localScale.x;
            return new Vector2(center.x, center.z).magnitude + new Vector2(extents.x, extents.z).magnitude < .104f &&
                center.y > .04f && center.y < .068f;
        }

        static void VerifyWorkers(CardboardFactoryGame game)
        {
            var crowd = game.lunch.diners;
            var doors = Object.FindObjectsByType<FactoryDoor>(FindObjectsInactive.Exclude);
            const float step = .04f;
            int mostEating = 0;
            for (int i = 0; i < 1400 && mostEating < FactoryKitchen.BowlsPerPot; i++)
            {
                crowd.Tick(step);
                foreach (var worker in crowd.workers) worker.TickMovement(step);
                foreach (var door in doors) door.Tick(step);
                Physics.SyncTransforms();
                mostEating = Mathf.Max(mostEating, crowd.EatingCount);
            }
            if (mostEating != FactoryKitchen.BowlsPerPot)
                foreach (var worker in crowd.workers)
                    Debug.Log($"{worker.name}: {worker.transform.position} {crowd.Describe(worker)}");
            Check(mostEating == FactoryKitchen.BowlsPerPot,
                $"All ten dedicated diners collect a tray and take it to a table (found {mostEating}).");
        }

        static void InteractWith(CardboardFactoryGame game, Transform camera, params KitchenAction[] actions)
        {
            Physics.SyncTransforms();
            var found = game.TargetForRay(new Ray(camera.position, camera.forward));
            Check(found && found.TryGetComponent(out FactoryKitchenControl control) && actions.Contains(control.action),
                "The intended vessel is reachable with loaded cookware in hand.");
            typeof(CardboardFactoryGame).GetProperty(nameof(CardboardFactoryGame.Focus)).SetValue(game, found);
            game.Interact();
        }

        static void PeelPotato(FactoryKitchen kitchen, FactoryKitchenFood potato, bool verifyCoverage)
        {
            var pointer = new Vector2(0, .13f);
            void Move(Vector2 target, bool cutting)
            {
                kitchen.MovePrepTool((target - pointer) / .0026f, cutting);
                pointer = target;
            }

            if (verifyCoverage)
            {
                Move(new Vector2(-.18f, .11f), false);
                Move(new Vector2(-.18f, -.11f), true);
                Check(potato.PeelProgress == 0, "A scrape beside the potato does not peel any skin.");
                Move(new Vector2(0, .11f), false);
                Move(new Vector2(0, -.11f), true);
                float partial = potato.PeelProgress;
                var display = potato.GetComponent<MeshFilter>().sharedMesh;
                Check(partial > 0 && partial < .35f && display.GetIndexCount(0) > 0 && display.GetIndexCount(1) > 0,
                    "One stroke reveals a local patch of flesh.");
                Capture(Camera.main, kitchen.PrepCameraPosition, kitchen.peelingStand.position, "KitchenPotatoPeeling");
                Move(new Vector2(0, .11f), false);
                Move(new Vector2(0, -.11f), true);
                Check(Mathf.Approximately(potato.PeelProgress, partial), "Re-scraping the same patch does not advance peeling.");
                kitchen.EndPrep();
                Check(potato.Prop.Take(kitchen.game) && kitchen.Use(KitchenAction.Peeler) && Mathf.Approximately(potato.PeelProgress, partial),
                    "A partially peeled potato keeps its exposed patches after being picked up.");
                pointer = new Vector2(0, .13f);
            }

            for (int rotation = 0; rotation < 12 && !potato.Peeled; rotation++)
            {
                for (int row = -1; row <= 1 && !potato.Peeled; row++)
                {
                    Move(new Vector2(-.17f, row * .05f), false);
                    Move(new Vector2(.17f, row * .05f), true);
                }
                if (!potato.Peeled) Call(kitchen, "TurnPotato", 30f);
            }
            Check(potato.Peeled && potato.PeelProgress == 1 && !kitchen.Preparing,
                $"Turning and scraping the rest of the surface completes peeling ({potato.PeelProgress:P1}).");
        }

        static void SetUtensil(FactoryKitchenUtensil tool, float tilt, bool dipping)
        {
            typeof(FactoryKitchenUtensil).GetProperty(nameof(FactoryKitchenUtensil.Dipping)).SetValue(tool, dipping);
            typeof(FactoryKitchenUtensil).GetProperty(nameof(FactoryKitchenUtensil.Tilt)).SetValue(tool, tilt);
        }

        static void TurnWithSpatula(CardboardFactoryGame game, Transform camera, FactoryKitchenFood food)
        {
            var tool = game.objectCarry.Held.GetComponent<FactoryKitchenUtensil>();
            var pan = game.kitchen.pan.transform;
            Vector3 level = pan.position + Vector3.up * .032f;
            Vector3 piece = food.Prop.Body.position;
            Vector3 firstFace = Quaternion.Inverse(food.Prop.Body.rotation) * Vector3.up;
            camera.position = new Vector3(piece.x, 1.62f, pan.position.z - 1.02f);
            Aim(camera, new Vector3(piece.x, level.y, piece.z - .145f));
            Step(game, .30f, true);
            SetUtensil(tool, 0, true);
            Step(game, .26f, true);
            Aim(camera, new Vector3(piece.x, level.y, piece.z + .006f));
            Step(game, .30f, true);
            var candidate = typeof(FactoryKitchenUtensil).GetField("spatulaContact", Internal).GetValue(tool);
            Vector3 afterSlide = food.Prop.Body.position - tool.Tip;
            SetUtensil(tool, 0, false);
            Step(game, .36f, true);
            Check(food.Prop.Body.position.y > piece.y + .065f,
                $"Sliding and raising the real spatula lifts the slice ({food.Prop.Body.position.y - piece.y:F3} m, " +
                $"contact {candidate}, difference {afterSlide:F3}, tool {tool.Tip - pan.position:F3}, desired {game.objectCarry.TargetPosition - pan.position:F3}, " +
                $"food {food.Prop.Body.position - pan.position:F3}, joint {food.GetComponent<ConfigurableJoint>()}).");
            SetUtensil(tool, 124f, false);
            Step(game, .8f, true);
            float facing = Vector3.Dot(food.Prop.Body.rotation * firstFace, Vector3.up);
            Check(facing < .62f && game.kitchen.pan.Contains(food.Prop.Body.position),
                $"Rolling the actual spatula lands the slice on a fresh face ({facing:F2}, {food.Prop.Body.position - pan.position:F3}).");
        }

        static void Aim(Transform camera, Vector3 at) => camera.rotation = Quaternion.LookRotation(at - camera.position);

        static void SliceForBatch(FactoryKitchen kitchen, FactoryKitchenFood food)
        {
            Vector3 cut = food.Prop.Body.position + food.Prop.Body.rotation * food.Model.bounds.center;
            Check((bool)Call(kitchen, "TryCut", food, cut, Vector3.forward), "A physical ingredient can be split through its center.");
        }

        static void PlaceInPot(FactoryKitchen kitchen, FactoryKitchenFood piece, int i)
        {
            Vector3 offset = Quaternion.Euler(0, i * 51f, 0) * Vector3.forward * .15f;
            Place(piece, kitchen.pot.position + Vector3.up * .17f + offset, Quaternion.identity);
            kitchen.TickCooking(.02f);
        }

        static FactoryKitchenFood[] LooseFood(FactoryKitchen kitchen, bool rat) =>
            kitchen.GetComponentsInChildren<FactoryKitchenFood>().Where(piece => piece.Rat == rat && piece.transform.parent == kitchen.transform).ToArray();

        static void Place(FactoryKitchenFood piece, Vector3 position, Quaternion rotation)
        {
            var body = piece.Prop.Body;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            piece.transform.SetPositionAndRotation(position, rotation);
        }

        static void Step(CardboardFactoryGame game, float seconds, bool grip = false)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++)
            {
                if (grip) game.objectCarry.TickGrip();
                Physics.Simulate(.02f);
                game.kitchen.TickCooking(.02f);
                if (game.objectCarry.Held && game.objectCarry.Held.TryGetComponent(out FactoryKitchenUtensil tool) &&
                    tool.Tool == FactoryKitchenUtensil.Kind.Ladle)
                    Call(game.kitchen.Service, "Tick", tool, .02f);
            }
        }

        static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            Debug.Log("PASS " + message);
        }

        static void Capture(Camera camera, Vector3 position, Vector3 lookAt, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var pose = new Pose(camera.transform.position, camera.transform.rotation);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                camera.targetTexture = target;
                foreach (var hand in Object.FindObjectsByType<FactoryOperatorHand>(FindObjectsInactive.Exclude)) hand.SyncPose();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
