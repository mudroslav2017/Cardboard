using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryKitchenVerification
    {
        const string Pending = "Cardboard.VerifyKitchen";

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
            if (!game || !CardboardRunner.Player || !game.kitchen.board.Prop.Body) return;
            SessionState.EraseBool(Pending);
            try
            {
                Verify(game);
                Debug.Log("Kitchen verification passed: loose board and pan placement, mesh cuts, peeling, frying and both soup trays.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void Verify(CardboardFactoryGame game)
        {
            game.mainMenu.CloseForFilming();
            game.ResetShift();
            var kitchen = game.kitchen;
            var camera = Camera.main.transform;
            var player = CardboardRunner.Player;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CaptureBench(camera.GetComponent<Camera>(), kitchen.transform);
            UnityEngine.Random.InitState(139);
            player.transform.position = kitchen.transform.position + Vector3.back * 2.1f;
            camera.SetPositionAndRotation(player.transform.position + Vector3.up * 1.62f, Quaternion.identity);
            Physics.simulationMode = SimulationMode.Script;

            void Simulate(float time, bool supportHeldObject = false)
            {
                Physics.SyncTransforms();
                for (int step = 0; step < Mathf.RoundToInt(time / .02f); step++)
                {
                    if (supportHeldObject) game.objectCarry.TickGrip();
                    Physics.Simulate(.02f);
                    kitchen.TickCooking(.02f);
                }
            }

            void AimAt(KitchenAction action)
            {
                var target = kitchen.GetComponentsInChildren<FactoryKitchenControl>().Single(control => control.action == action);
                var bounds = target.GetComponentInChildren<Collider>().bounds;
                Vector3 eye = bounds.center + new Vector3(0, .55f, -1.3f);
                Check(game.TargetForRay(new Ray(eye, bounds.center - eye)) == target.GetComponent<FactoryInteractable>(),
                    "Aim reaches kitchen control " + action);
            }

            Vector2 knifeCursor = Vector2.zero;
            bool capturedKnife = false;
            void GripKnife()
            {
                Check(kitchen.Use(KitchenAction.Knife) && kitchen.Preparing && game.PlayerBlocked, "Knife takes over mouse and movement.");
                knifeCursor = Vector2.zero;
            }

            void ClickCut(Vector2 cutAt, float angle = 0)
            {
                kitchen.SetKnifeAngle(angle);
                kitchen.MovePrepTool((cutAt - knifeCursor) / .0026f, false);
                kitchen.MovePrepTool(Vector2.zero, true);
                knifeCursor = cutAt;
            }

            void Slice()
            {
                GripKnife();
                Check(kitchen.knifeDashes.All(dash => dash.gameObject.activeSelf), "Dashes show the projected blade in knife mode.");
                if (!capturedKnife && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    Vector3 position = camera.position;
                    Quaternion rotation = camera.rotation;
                    camera.SetPositionAndRotation(kitchen.PrepCameraPosition, kitchen.PrepCameraRotation);
                    CaptureCamera(camera.GetComponent<Camera>(), "Logs/KitchenKnifePreview.png");
                    camera.SetPositionAndRotation(position, rotation);
                    capturedKnife = true;
                }
                ClickCut(new Vector2(-.076f, 0), -6);
                ClickCut(new Vector2(.072f, 0), 11);
                Check(kitchen.Preparing, "Two individual clicks cut at arbitrary rotations without leaving knife mode.");
                kitchen.EndPrep();
                Simulate(.28f);
            }

            FactoryKitchenFood[] Food(bool rat) => kitchen.GetComponentsInChildren<FactoryKitchenFood>().Where(piece => piece.Rat == rat).ToArray();

            void PeelPotato()
            {
                Check(kitchen.Use(KitchenAction.Potato), "A raw potato is taken directly into the player's hands.");
                var potato = game.objectCarry.Held.GetComponent<FactoryKitchenFood>();
                Check(!potato.Peeled && potato.Model == kitchen.potatoModel && kitchen.Use(KitchenAction.Peeler),
                    "The held potato moves to the separate peeling stand.");
                for (int i = 0; i < potato.PeelCount; i++)
                {
                    kitchen.MovePrepTool(new Vector2(0, 172), false);
                    kitchen.MovePrepTool(new Vector2(0, -172), true);
                }
                Check(Food(false).Contains(potato) && potato.Peeled && potato.Model == kitchen.potatoModel && !kitchen.Preparing,
                    "Peeling changes the skin of the same physical potato while keeping the mesh.");
                Check(potato.Prop.Take(game) && kitchen.Use(KitchenAction.Board) && !game.objectCarry.Held,
                    "Re-pick up the peeled potato and place it on the chopping board.");
                Simulate(.33f);
            }

            void MoveBoardForThrow(FactoryKitchenFood[] food, Vector3 position)
            {
                var offsets = food.Select(piece => kitchen.board.transform.InverseTransformPoint(piece.Prop.Body.position)).ToArray();
                var body = kitchen.board.Prop.Body;
                body.position = position;
                body.rotation = Quaternion.identity;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                kitchen.board.transform.SetPositionAndRotation(position, Quaternion.identity);
                for (int i = 0; i < food.Length; i++)
                {
                    Vector3 newPosition = kitchen.board.transform.TransformPoint(offsets[i]);
                    food[i].transform.position = newPosition;
                    food[i].Prop.Body.position = newPosition;
                    food[i].Prop.Body.linearVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();
            }

            AimAt(KitchenAction.Knife);
            AimAt(KitchenAction.Burner);
            AimAt(KitchenAction.Pot);
            Check(kitchen.GetComponentsInChildren<TextMesh>(true).Length == 0, "The cooking table has no world-space text.");
            Check(!kitchen.Use(KitchenAction.Rat), "Rat stock starts empty.");
            kitchen.ReceiveRats(FactoryRatHunt.Quota);
            Check(kitchen.Use(KitchenAction.Rat) && kitchen.RatStock == 9, "Kitchen withdraws one rat from the diver's delivery.");
            Check(Food(true).Single().Model == kitchen.ratModel && kitchen.ratModel.vertexCount > 30, "Whole rat is the baked original FBX mesh.");
            Slice();
            var rats = Food(true);
            Check(rats.Length == 3 && rats.All(piece => kitchen.board.Contains(piece.Prop.Body.position)), "Three independent rigidbodies land on the board.");
            Check(Mathf.Abs(rats.Sum(piece => piece.Amount) - 1) < .0001f && rats.All(piece => piece.Sliced), "Free slices preserve ingredient amount.");
            Check(rats.All(piece => piece.Model != kitchen.ratModel) && rats.Any(piece => piece.Model.GetIndexCount(piece.Model.subMeshCount-1)>0),
                "Original rat triangles are retained with newly generated exposed cut faces.");
            camera.position = kitchen.board.transform.position + new Vector3(0, .64f, -1.12f);
            Check(kitchen.Use(KitchenAction.Board), "The loaded board can be taken in hand.");
            Simulate(.25f, true);
            for (int step = 0; step < 37; step++)
            {
                camera.position += Vector3.right * (.036f);
                Simulate(.02f, true);
            }
            Simulate(.25f, true);
            Check(rats.All(piece => kitchen.board.Contains(piece.Prop.Body.position)),
                "At walking speed the held board supports its loose ingredients from bench to stove.");
            MoveBoardForThrow(rats, kitchen.pan.transform.position + new Vector3(0, -.033f, -.93f));
            Check(kitchen.Toss(kitchen.board, new Vector2(0, 4)) == 0, "A sub-threshold movement leaves the contents on the board.");
            Check(kitchen.Toss(kitchen.board, new Vector2(0, 68)) == 3, "Flick applies a physical launch to every piece on the board.");
            Simulate(.82f);
            int caught = rats.Count(piece => kitchen.pan.Contains(piece.Prop.Body.position));
            Check(caught >= 1, "A centered flick lands in the physical pan. Positions: " + string.Join(", ", rats.Select(piece => piece.Prop.Body.position)));
            Check(kitchen.Use(KitchenAction.Prep), "Board returns to the prep bench after the toss.");

            var miss = rats.OrderByDescending(piece => piece.Amount).First();
            miss.Prop.Body.position = miss.transform.position = kitchen.transform.position + new Vector3(.05f, .85f, -2f);
            miss.Prop.Body.linearVelocity = Vector3.right;
            Simulate(.92f);
            Check(miss.Dusty && miss.Prop.Body.position.y < .22f, "A missed piece hits the floor and picks up visible dirt.");
            foreach (var piece in rats.Where(piece => !kitchen.pan.Contains(piece.Prop.Body.position)))
            {
                Check(piece.Prop.Take(game) && kitchen.Use(KitchenAction.Burner), "A stray piece can be picked up and dropped into the pan.");
                Simulate(.42f);
            }
            Check(kitchen.Use(KitchenAction.Burner), "Burner lights.");
            Simulate(4.85f);
            Check(rats.All(piece => piece.SideReady && !piece.Fried), "One side browns and raw back sides still need turning. " +
                string.Join(", ", rats.Select(piece => piece.Prop.Body.position + " ready=" + piece.SideReady)));
            Check(kitchen.Use(KitchenAction.Spatula), "Spatula flips pieces in the pan.");
            Simulate(4.95f);
            Check(rats.All(piece => piece.Fried), "The second side browns every piece.");
            Check(kitchen.Use(KitchenAction.Pan), "The frying pan can be picked up with cooked pieces inside.");
            Check(kitchen.Use(KitchenAction.Pot), "The frying pan tips cooked food into the pot.");
            Simulate(.82f);
            Check(kitchen.RatInPot >= FactoryKitchen.AmountPerIngredient && kitchen.Portions == 0, "The fried portions fill the rat requirement and the pot waits for potatoes. " +
                "Pot count: " + kitchen.RatInPot + ". Leftover positions: " + string.Join(", ", Food(true).Select(piece => piece.Prop.Body.position)));
            Check(kitchen.Use(KitchenAction.Burner), "The pan can be returned to the burner.");

            PeelPotato();
            Slice();
            Check(Food(false).Length == 3 && kitchen.Use(KitchenAction.Board), "Sliced potato has three loose pieces.");
            MoveBoardForThrow(Food(false), kitchen.pot.position + new Vector3(0, .07f, -.9f));
            Check(kitchen.Use(KitchenAction.Pot), "The loaded board can tip potatoes into the stockpot.");
            Simulate(.80f);
            Check(kitchen.PotatoInPot >= FactoryKitchen.AmountPerIngredient && kitchen.Portions == 0, "Potatoes start the boil when the fried rat is already in the pot.");
            Check(kitchen.Use(KitchenAction.Prep), "Board docks after pouring.");
            Simulate(7.1f);
            Check(kitchen.Portions == 2 && kitchen.Use(KitchenAction.Serve), "Finished soup yields two portions and can be ladled onto the tray.");
            Check(game.lunch.KitchenMeal && kitchen.traySoup.activeSelf && kitchen.trayDust.activeSelf && kitchen.Portions == 1,
                "The real tray has a soup bowl with the recovered piece's dust.");
            Check(!kitchen.Use(KitchenAction.Serve) && kitchen.Portions == 1, "The occupied counter keeps the second portion in the pot.");
            player.transform.position = game.lunch.pickup.position + Vector3.down + Vector3.back * 1.1f;
            Check(game.lunch.TryTake(), "The existing balance mechanic takes the kitchen tray.");
            player.transform.position = game.lunch.destination.position + Vector3.down * .8f;
            Check(game.lunch.TryServe() && kitchen.Use(KitchenAction.Serve) && kitchen.Portions == 0,
                "Delivering the tray frees the counter for the second bowl.");
            game.ResetShift();
            Check(kitchen.RatStock == 0 && !game.lunch.KitchenMeal && !kitchen.traySoup.activeSelf && kitchen.board.ReadyAtStation && kitchen.pan.ReadyAtStation,
                "Restart returns both tools, ingredients and tray to their initial state.");

            kitchen.ReceiveRats(FactoryRatHunt.Quota);
            Check(kitchen.Use(KitchenAction.Rat), "Load a rat for the unrestricted cut stress test.");
            GripKnife();
            for (int i = -11; i <= 11; i++) ClickCut(new Vector2(i * .018f, 0));
            for (int i = -11; i <= 11; i++) ClickCut(new Vector2(0, i * .011f), 90);
            var diced = Food(true);
            Check(diced.Length >= 100 && Mathf.Abs(diced.Sum(piece => piece.Amount) - 1) < .0001f,
                $"Single clicks made {diced.Length} physical pieces from the original rat mesh without losing volume.");
            kitchen.EndPrep();
            Simulate(.2f);
            Check(diced.All(piece => piece.Prop.Body.detectCollisions && !piece.Prop.Body.isKinematic), "All diced pieces use active physics.");

            var testRat = diced.First();
            Check(testRat.Prop.Take(game) && kitchen.Use(KitchenAction.Burner), "A tiny cut piece can be put into the pan.");
            game.ResetShift();
            kitchen.ReceiveRats(FactoryRatHunt.Quota);
            Check(kitchen.Use(KitchenAction.Rat), "Load a rat for the scorch test.");
            Slice();
            var burnRat = Food(true)[1];
            foreach (var piece in Food(true))
            {
                Vector3 target = piece == burnRat ? kitchen.pan.transform.position + Vector3.up * .08f : kitchen.transform.position + Vector3.back * 3;
                piece.transform.position = piece.Prop.Body.position = target;
                piece.Prop.Body.linearVelocity = Vector3.zero;
            }
            PeelPotato();
            Slice();
            var burnPotato = Food(false)[1];
            Check(burnPotato.Prop.Take(game) && kitchen.Use(KitchenAction.Burner), "Potatoes can be dropped into the pan too.");
            Check(kitchen.Use(KitchenAction.Burner), "Light the shared pan for the scorch test.");
            Simulate(9.2f);
            Check(burnRat.Burnt && burnPotato.Burnt && !burnPotato.ReadyForPot && !burnRat.ReadyForPot && kitchen.scorchSmoke.isPlaying,
                "Both ingredients char, produce smoke and become unusable in soup.");

            void PlaceOffCenter(FactoryKitchenTool tool, KitchenAction pickup, Vector3 offset, float angle)
            {
                Check(kitchen.Use(pickup) && !tool.ReadyAtStation, $"{pickup} is inactive while held.");
                var body = tool.Prop.Body;
                Vector3 position = tool.home.TransformPoint(offset);
                Quaternion rotation = tool.home.rotation * Quaternion.Euler(0, angle, 0);
                tool.transform.SetPositionAndRotation(position, rotation);
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                game.objectCarry.TickInput(false, true);
                Simulate(.85f);
                Check(tool.ReadyAtStation && !body.isKinematic && Vector3.Distance(body.position, tool.home.position) > .12f &&
                    Quaternion.Angle(body.rotation, tool.home.rotation) > 5f,
                    $"{pickup} dropped with RMB stays off-center and rotated but works at its station. Position: {body.position}");
            }

            game.ResetShift();
            PlaceOffCenter(kitchen.board, KitchenAction.Board, new Vector3(-.035f, .09f, -.19f), 12);
            Vector3 looseBoardPosition = kitchen.board.Prop.Body.position;
            kitchen.ReceiveRats(FactoryRatHunt.Quota);
            Check(kitchen.Use(KitchenAction.Rat) && kitchen.board.Contains(Food(true).Single().Prop.Body.position),
                "The rat is placed on the actual board instead of the old docking location.");
            GripKnife();
            ClickCut(Vector2.zero);
            Check(Food(true).Length == 2 && Vector3.Distance(kitchen.board.Prop.Body.position, looseBoardPosition) < .01f,
                "The knife cuts on the loose board without centering it.");
            kitchen.EndPrep();
            Check(!kitchen.board.Prop.Body.isKinematic, "The loose board resumes physics after using the knife.");

            PlaceOffCenter(kitchen.pan, KitchenAction.Pan, new Vector3(.14f, .09f, .16f), 22);
            var loosePanFood = Food(true).OrderBy(piece => piece.Amount).First();
            Check(loosePanFood.Prop.Take(game) && kitchen.Use(KitchenAction.Burner) && kitchen.Use(KitchenAction.Burner),
                "Food can be put into the loose pan and the burner lights.");
            Simulate(5.2f);
            Check(loosePanFood.SideReady, "Food browns in the off-center pan.");
            Check(kitchen.Use(KitchenAction.Pan) && !kitchen.pan.ReadyAtStation, "Picking up the loose pan stops heating.");
            var panBody = kitchen.pan.Prop.Body;
            kitchen.pan.transform.position = panBody.position = kitchen.pan.home.position + Vector3.right * 1.1f;
            game.objectCarry.TickInput(false, true);
            Check(!kitchen.pan.ReadyAtStation, "The pan stops working when placed beyond the burner area.");
        }

        static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            Debug.Log("PASS " + message);
        }

        static void CaptureBench(Camera camera, Transform kitchen)
        {
            var position = kitchen.TransformPoint(new Vector3(0, 1.90f, -3.49f));
            var focus = kitchen.TransformPoint(new Vector3(0, .84f, 0));
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
            CaptureCamera(camera, "Logs/KitchenBench.png");
        }

        static void CaptureCamera(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
