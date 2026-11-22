using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactorySewerScaleCheckTemporary
    {
        static readonly string[] Layout = {"A┬┬┐┌─┬┐"," ├┴┤├─┤│","┌┤┌┘│┌┤│","│├┴┬┴┤├┘","└┴─┴─┴┴B"};

        public static void BuildAndVerify()
        {
            FactorySewerBuilder.BuildBatch();
            Verify();
        }

        public static void Verify()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FactoryShift.unity");
            var game = Object.FindAnyObjectByType<CardboardFactoryGame>();
            var sewer = game.transform.Find("Sewer • maintenance tunnels");
            var tiles = sewer.Find("Tunnel tiles");
            var accessGroup = sewer.Find("Maintenance hatches");
            var player = game.GetComponentsInChildren<CardboardRunner>().Single(runner => runner.isPlayer);
            CardboardRunner.Player = player;
            var capsule = player.GetComponent<CharacterController>();
            using var output = new StreamWriter("Logs/SewerScaleVerification.txt");

            var renderers = tiles.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            output.WriteLine($"Tiles: {tiles.childCount}, bounds: {bounds.size}, center: {bounds.center}");
            var westLanding = accessGroup.Find("A • sewer landing");
            var eastLanding = accessGroup.Find("B • sewer landing");
            float floor = westLanding.position.y - .18f;
            var centers = tiles.Cast<Transform>().ToDictionary(tile => tile.name.Substring(0, 5), tile => tile.position);
            Vector3 Point(int c, int r)
            {
                if (Layout[r][c] == 'A') return westLanding.position;
                if (Layout[r][c] == 'B') return eastLanding.position;
                var point = centers[$"{r + 1:D2}.{c + 1:D2}"];
                point.y = floor + .18f;
                return point;
            }

            bool Walk(Vector3 start, Vector3 goal, string edge)
            {
                capsule.enabled = false;
                capsule.transform.position = start;
                capsule.enabled = true;
                int frames = Mathf.CeilToInt(Vector3.Distance(start, goal) / .079f) + 45;
                float maxDepth = 0;
                for (int i = 0; i < frames; i++)
                {
                    Vector3 remaining = goal - capsule.transform.position;
                    remaining.y = 0;
                    if (remaining.magnitude < .08f && maxDepth < .9f) return true;
                    capsule.Move(Vector3.ClampMagnitude(remaining, .085f) + Vector3.down * .038f);
                    maxDepth = Mathf.Max(maxDepth, floor - capsule.transform.position.y);
                }
                output.WriteLine($"Blocked {edge} from {start} to {goal}, settled at {capsule.transform.position}, depth {maxDepth}");
                return false;
            }

            Physics.SyncTransforms();
            float originalStep = capsule.stepOffset;
            capsule.stepOffset = .32f;
            int total = 0, passed = 0;
            void CheckEdge(int c, int r, int toC, int toR)
            {
                total++;
                Vector3 start = Point(c, r), end = Point(toC, toR);
                string edge = $"{r + 1},{c + 1} to {toR + 1},{toC + 1}";
                if (Walk(start, end, edge) & Walk(end, start, edge)) passed++;
            }
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 8; c++)
            {
                char tile = Layout[r][c];
                if ("A┬┌─├┴└".Contains(tile)) CheckEdge(c, r, c + 1, r);
                if ("┌┐┬├│┤".Contains(tile)) CheckEdge(c, r, c, r + 1);
            }
            capsule.stepOffset = originalStep;
            output.WriteLine($"Walked {passed}/{total} connections in both directions.");

            bool allAccesses = true;
            var focus = typeof(CardboardFactoryGame).GetProperty(nameof(CardboardFactoryGame.Focus));
            foreach (string name in new[] {"A","B"})
            {
                var hatch = accessGroup.Find($"{name} • workshop hatch");
                var landing = accessGroup.Find($"{name} • sewer landing");
                var climb = accessGroup.Find($"{name} • climb to workshop");
                var upperLanding = hatch.Find("Workshop landing");
                var upperView = upperLanding.position + Vector3.up * 1.62f;
                var descent = game.TargetForRay(new Ray(upperView, hatch.position + Vector3.up * .075f - upperView));
                focus.SetValue(game, descent);
                game.Interact();
                bool entered = descent && descent.transform == hatch && player.InSewer &&
                    Vector3.Distance(player.transform.position, landing.position) < .03f;
                for (int i = 0; i < 100; i++) player.TickMovement(.02f);
                bool grounded = player.InSewer && capsule.isGrounded &&
                    Mathf.Abs(player.transform.position.y - landing.position.y) < .2f;
                var lowerView = player.transform.position + Vector3.up * 1.62f;
                var ascent = game.TargetForRay(new Ray(lowerView, climb.position - lowerView));
                focus.SetValue(game, ascent);
                game.Interact();
                bool exited = ascent && ascent.transform == climb && !player.InSewer &&
                    Vector3.Distance(player.transform.position, upperLanding.position) < .03f;
                allAccesses &= entered && grounded && exited;
                output.WriteLine($"{name}: entered {entered}, settled at {landing.position} underground {grounded}, exited {exited}; upper hatch {hatch.position}, target down {descent?.name}, target up {ascent?.name}.");
                player.LeaveSewer();
            }

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                var preview = new GameObject("Sewer scale preview").AddComponent<Camera>();
                preview.clearFlags = CameraClearFlags.SolidColor;
                preview.backgroundColor = new Color(.035f,.045f,.05f);
                preview.fieldOfView = CardboardCamera.GameplayFov;
                preview.nearClipPlane = .05f;
                preview.transform.SetPositionAndRotation(Point(2,0) + Vector3.up * 1.62f, Quaternion.LookRotation(new Vector3(-1, .045f, -.10f)));
                Render(preview, "SewerScaledTunnel");
                preview.transform.SetPositionAndRotation(westLanding.position + Vector3.up * 1.62f, westLanding.rotation);
                Render(preview, "SewerScaledEntry");
                preview.orthographic = true;
                preview.orthographicSize = bounds.size.z * .57f;
                preview.nearClipPlane = 5 - (floor + 2.65f);
                preview.farClipPlane = 45;
                preview.transform.SetPositionAndRotation(new Vector3(bounds.center.x, 5, bounds.center.z), Quaternion.Euler(90,0,0));
                Render(preview, "SewerScaledPlan");
            }
            if (passed != total || !allAccesses)
                throw new InvalidOperationException("Sewer paths or hatches failed verification. See Logs/SewerScaleVerification.txt.");
        }

        static void Render(Camera camera, string name)
        {
            var render = new RenderTexture(1200, 740, 24);
            var image = new Texture2D(1200, 740, TextureFormat.RGB24, false);
            camera.targetTexture = render;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = render });
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, 1200, 740), 0, 0);
            image.Apply();
            File.WriteAllBytes($"Logs/{name}.png", image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(render);
        }
    }
}
