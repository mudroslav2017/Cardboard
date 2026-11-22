using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard
{
    [DisallowMultipleComponent]
    public sealed class FactoryBeesTrend : MonoBehaviour, IFactoryTrendRecording
    {
        public const int FrameRate = 30;
        public const int FrameCount = 333;
        public const int CloseFrame = 189;
        public const int ContactFrame = 285;

        const float Duration = (float)FrameCount / FrameRate;
        const float Aspect = 9f / 16f;
        const float HopInterval = .9f;
        const float AdvancePerHop = .94f;
        const float CloseStart = (float)CloseFrame / FrameRate;
        const float Contact = (float)ContactFrame / FrameRate;
        const float RiseStart = 6.49f;
        const float LevelStart = 8.38f;
        const int CameraSampleRate = 120;
        const float BeeOrbitSpeed = .42f * .60f;
        const float BeeScale = .88f * 1.30f;
        const float FullTurn = 2 * Mathf.PI;

        static readonly Vector3 Nose = new Vector3(-.052f, 1.59f, .04345f);
        static readonly float[] HopHeights = { .34f, .48f, .28f, .52f, .36f, .43f, .56f };
        static readonly float[] BeeSizes = { .83f, .72f, .95f, .69f, .79f };

        readonly Transform[] bees = new Transform[BeeSizes.Length];
        CardboardFactoryGame game;
        Camera lens;
        Transform stage, nastya;
        Light key, rim;
        FactoryWorkerAppearance appearance;
        Vector3[] soleVertices, trackingEyes, trackingAims;
        float beeNoseOffset;
        VolumeProfile focusProfile;
        DepthOfField focus;
        bool previewing, playing;
        float time;

        public bool Active { get; private set; }
        public Camera Lens => lens;
        public AudioClip Soundtrack => null;

        void Awake() => game = GetComponent<CardboardFactoryGame>();

        public bool Begin(bool preview)
        {
            if (Active) return true;

            var beePrefab = Resources.Load<GameObject>("BeesTrend/Bee");
            if (!beePrefab)
            {
                Debug.LogError("Missing the BeesTrend/Bee recording prefab.");
                return false;
            }
            if (!game || !game.filmSession.Begin(preserveSceneLighting: true)) return false;

            Active = true;
            try
            {
                lens = Camera.main;
                stage = new GameObject("Bees • factory aisle").transform;
                stage.SetPositionAndRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX - 8.2f, -.1475f, .96f),
                    Quaternion.Euler(0, 90, 0));
                game.filmSession.HideOtherRecordingProps();

                nastya = Node("Настя");
                var model = ImportedCardboardCharacters.Create(nastya, 2);
                appearance = model.GetComponent<FactoryWorkerAppearance>();
                soleVertices = Array.FindAll(model.GetComponent<MeshFilter>().sharedMesh.vertices, vertex => vertex.y < .22f);
                for (int i = 0; i < soleVertices.Length; i++)
                    soleVertices[i] = nastya.InverseTransformPoint(model.TransformPoint(soleVertices[i]));

                for (int i = 0; i < bees.Length; i++)
                {
                    bees[i] = Instantiate(beePrefab, stage, false).transform;
                    bees[i].name = "Bee " + (i + 1);
                    bees[i].localScale *= BeeSizes[i] * BeeScale;
                }

                beeNoseOffset = MeasureBeeNose(bees[0]);
                BuildTrackingShot();

                key = AddLight("Warm key", new Color(1, .90f, .74f), 3.1f);
                rim = AddLight("Amber rim", new Color(1, .72f, .36f), 2.1f);
                AddFocus();
                lens.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                lens.aspect = Aspect;
                previewing = playing = preview;
                Pose(0);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop();
                return false;
            }
        }

        Light AddLight(string lightName, Color color, float intensity)
        {
            var light = Node(lightName).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        void AddFocus()
        {
            focusProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            focus = focusProfile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Bokeh);
            focus.focusDistance.Override(5);
            focus.aperture.Override(5f);
            focus.focalLength.Override(67);
            var volume = Node("Tracking focus").gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 80;
            volume.sharedProfile = focusProfile;
        }

        void Pose(float seconds)
        {
            time = seconds;
            if (seconds < CloseStart)
            {
                appearance.SetState(FactoryFaceState.Normal);
                PoseHops(seconds);
                PoseSwarm(seconds);
                PoseTrackingShot(seconds);
                return;
            }

            PoseCloseup(seconds);
        }

        void PoseHops(float seconds)
        {
            float beat = HopBeat(seconds);
            float step = Mathf.Repeat(beat, 1);
            float height = HopHeight(beat);
            float settle = 1 - Ease(seconds, 5.99f, CloseStart);
            float lean = settle * Mathf.Sin(Mathf.PI * beat);
            float windup = Pulse(step, .04f, .15f, .24f);
            float rebound = Pulse(step, .73f, .80f, .95f);
            var tilt = Quaternion.Euler(settle * (7 * windup - 7 * rebound + 4 * height),
                6 * lean, (13 + 7 * height) * lean);

            float sole = float.PositiveInfinity;
            foreach (var vertex in soleVertices)
                sole = Mathf.Min(sole, (tilt * vertex).y);
            // Rock the whole sheet around its point of support.
            nastya.SetLocalPositionAndRotation(HopPosition(beat) + Vector3.up * (height - sole), tilt);
        }

        void PoseSwarm(float seconds)
        {
            const float angularSpeed = FullTurn * BeeOrbitSpeed;
            Vector3 centerVelocity = Vector3.forward * (AdvancePerHop / HopInterval);
            Vector3 center = centerVelocity * seconds + Vector3.right * .18f * Mathf.Sin(seconds * .65f);
            centerVelocity += Vector3.right * (.18f * .65f * Mathf.Cos(seconds * .65f));
            for (int i = 0; i < bees.Length; i++)
            {
                bees[i].gameObject.SetActive(true);
                float theta = angularSpeed * seconds + FullTurn * i / bees.Length;
                float radius = .83f + .12f * (i % 3);
                float wave = theta + i * .8f;
                float y = .75f + .37f * i + .17f * Mathf.Sin(wave);
                var offset = new Vector3(radius * Mathf.Sin(theta), y, .83f * Mathf.Cos(theta));
                var tangent = new Vector3(radius * Mathf.Cos(theta), .17f * Mathf.Cos(wave),
                    -.83f * Mathf.Sin(theta));
                bees[i].SetLocalPositionAndRotation(center + offset,
                    Quaternion.LookRotation(centerVelocity + tangent * angularSpeed));
            }
        }

        void BuildTrackingShot()
        {
            int sampleCount = Mathf.CeilToInt(CloseStart * CameraSampleRate) + 1;
            trackingEyes = new Vector3[sampleCount];
            trackingAims = new Vector3[sampleCount];
            TrackingTargets(0, out var eye, out var aim);
            trackingEyes[0] = eye;
            trackingAims[0] = aim;
            Vector3 eyeVelocity = Vector3.zero, aimVelocity = Vector3.zero;
            for (int i = 1; i < sampleCount; i++)
            {
                TrackingTargets((float)i / CameraSampleRate, out eye, out aim);
                trackingEyes[i] = FollowSpring(trackingEyes[i - 1], eye, ref eyeVelocity, 4.4f);
                trackingAims[i] = FollowSpring(trackingAims[i - 1], aim, ref aimVelocity, 6.2f);
            }
        }

        static void TrackingTargets(float seconds, out Vector3 eye, out Vector3 aim)
        {
            float beat = HopBeat(seconds);
            var position = HopPosition(beat);
            float height = HopHeight(beat);
            float sweep = Mathf.Sin(seconds * .91f - .38f);
            eye = position + new Vector3(.46f * sweep, .91f + .31f * height, 5.50f + .33f * sweep);
            aim = position + new Vector3(-.15f * Mathf.Sin(Mathf.PI * beat), 1.26f + .67f * height, 0);
        }

        // Bake the spring at a fixed rate so seeking and rendering give the same camera pose.
        static Vector3 FollowSpring(Vector3 position, Vector3 target, ref Vector3 velocity, float frequency)
        {
            const float dt = 1f / CameraSampleRate;
            const float damping = .75f;
            velocity += ((target - position) * (frequency * frequency) - velocity * (2 * damping * frequency)) * dt;
            return position + velocity * dt;
        }

        void PoseTrackingShot(float seconds)
        {
            float sample = seconds * CameraSampleRate;
            int index = Mathf.FloorToInt(sample);
            float blend = sample - index;
            var eye = Vector3.Lerp(trackingEyes[index], trackingEyes[index + 1], blend);
            var aim = Vector3.Lerp(trackingAims[index], trackingAims[index + 1], blend);
            SetCamera(eye, aim, 48f);
            SetLights(nastya.localPosition);
            SetFocus(stage.InverseTransformPoint(nastya.TransformPoint(Nose)),
                .15f + .09f * Mathf.Sin(seconds * 1.52f), 5f);
        }

        void PoseCloseup(float seconds)
        {
            Vector3 mark = HopPosition(HopBeat(CloseStart));
            float recoil = Pulse(seconds, Contact, Contact + .16f, Contact + .57f);
            float look = Ease(seconds, Contact + .24f, Contact + .83f);
            nastya.SetLocalPositionAndRotation(mark + new Vector3(0, .016f * recoil, -.065f * recoil),
                Quaternion.Euler(-5 * recoil, -5 * look, 3.4f * recoil - 1.8f * look));
            appearance.SetState(seconds >= Contact ? FactoryFaceState.Suspicious : FactoryFaceState.Normal);

            for (int i = 1; i < bees.Length; i++)
                bees[i].gameObject.SetActive(false);

            var contact = mark + Nose;
            Vector3 tip, heading;
            if (seconds < LevelStart)
            {
                float t = Ease(seconds, RiseStart, LevelStart);
                Approach(t, new Vector3(-.46f, -1.04f, .94f), new Vector3(-.44f, -.35f, .87f),
                    new Vector3(-.34f, 0, .75f), new Vector3(-.25f, 0, .62f), out tip, out heading);
            }
            else if (seconds < Contact)
            {
                float t = Ease(seconds, LevelStart, Contact);
                Approach(t, new Vector3(-.25f, 0, .62f), new Vector3(-.16f, 0, .49f),
                    new Vector3(0, 0, .13f), Vector3.zero, out tip, out heading);
            }
            else
            {
                float away = Ease(seconds, Contact, Contact + .27f);
                float floatBack = Ease(seconds, Contact + .30f, Duration);
                tip = new Vector3(.045f * away + .37f * floatBack, .065f * recoil - .25f * floatBack,
                    .24f * away + .24f * floatBack);
                heading = new Vector3(-.12f * floatBack, -.12f * recoil, -1);
            }

            var bee = bees[0];
            var rotation = Quaternion.LookRotation(heading);
            bee.SetLocalPositionAndRotation(contact + tip - rotation * Vector3.forward * beeNoseOffset, rotation);
            float push = Ease(seconds, CloseStart, Contact + .36f);
            var eye = mark + new Vector3(-1.49f - .08f * push, 1.69f + .055f * push, 2.16f - .10f * push);
            var aim = mark + new Vector3(.045f, 1.69f + .025f * push, .02f);
            SetCamera(eye, aim, 50f);
            SetLights(mark);

            float rack = Pulse(seconds, RiseStart + .25f, LevelStart, Contact + .47f);
            Vector3 focusTarget = Vector3.Lerp(stage.InverseTransformPoint(nastya.TransformPoint(Nose)),
                bee.localPosition, .47f * rack);
            SetFocus(focusTarget, .01f, 10f);
        }

        static void Approach(float t, Vector3 start, Vector3 control1, Vector3 control2, Vector3 end,
            out Vector3 position, out Vector3 heading)
        {
            var first = Vector3.Lerp(start, control1, t);
            var middle = Vector3.Lerp(control1, control2, t);
            var last = Vector3.Lerp(control2, end, t);
            var from = Vector3.Lerp(first, middle, t);
            var to = Vector3.Lerp(middle, last, t);
            position = Vector3.Lerp(from, to, t);
            heading = to - from;
        }

        static float MeasureBeeNose(Transform bee)
        {
            var filter = bee.GetComponentInChildren<MeshFilter>();
            var bounds = filter.sharedMesh.bounds;
            var toBee = bee.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            float forward = toBee.MultiplyPoint3x4(bounds.center).z
                + Mathf.Abs(toBee.MultiplyVector(Vector3.right * bounds.extents.x).z)
                + Mathf.Abs(toBee.MultiplyVector(Vector3.up * bounds.extents.y).z)
                + Mathf.Abs(toBee.MultiplyVector(Vector3.forward * bounds.extents.z).z);
            return forward * bee.localScale.z;
        }

        void SetCamera(Vector3 eye, Vector3 aim, float fieldOfView)
        {
            lens.transform.SetPositionAndRotation(stage.TransformPoint(eye),
                Quaternion.LookRotation(stage.TransformVector(aim - eye), stage.up));
            lens.fieldOfView = fieldOfView;
        }

        void SetLights(Vector3 position)
        {
            key.transform.localPosition = position + new Vector3(-1.20f, 2.65f, 1.65f);
            rim.transform.localPosition = position + new Vector3(1.05f, 2.58f, -.95f);
        }

        void SetFocus(Vector3 point, float pull, float aperture)
        {
            focus.focusDistance.value = Vector3.Dot(stage.TransformPoint(point) - lens.transform.position,
                lens.transform.forward) - pull;
            focus.aperture.value = aperture;
        }

        static float HopBeat(float seconds) => seconds / HopInterval + .15f;

        static Vector3 HopPosition(float beat)
        {
            float flight = FlightProgress(beat);
            float stride = Mathf.Min(HopHeights.Length, Mathf.Floor(beat) + Mathf.SmoothStep(0, 1, flight));
            float side = .28f * Mathf.Sin(stride * .64f) - .105f * Mathf.Sin(Mathf.PI * beat);
            return new Vector3(side, 0, AdvancePerHop * stride);
        }

        static float HopHeight(float beat)
        {
            float flight = FlightProgress(beat);
            float height = HopHeights[Mathf.Min(Mathf.FloorToInt(beat), HopHeights.Length - 1)];
            return height * 4 * flight * (1 - flight);
        }

        static float FlightProgress(float beat) => Mathf.InverseLerp(.25f, .74f, Mathf.Repeat(beat, 1));

        static float Ease(float value, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

        static float Pulse(float value, float start, float peak, float end) =>
            Ease(value, start, peak) * (1 - Ease(value, peak, end));

        Transform Node(string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(stage, false);
            return node;
        }

        void Update()
        {
            if (!Active || !previewing) return;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f7Key.wasPressedThisFrame) { Pose(0); playing = true; }
                if (keys.spaceKey.wasPressedThisFrame) playing = !playing;
            }
            if (playing) Pose(Mathf.Repeat(time + Time.unscaledDeltaTime, Duration));
        }

        void LateUpdate()
        {
            if (!Active || lens.targetTexture) return;
            var picture = Picture();
            lens.rect = new Rect(picture.x / Screen.width, picture.y / Screen.height,
                picture.width / Screen.width, picture.height / Screen.height);
            lens.aspect = Aspect;
        }

        static Rect Picture()
        {
            float width = Mathf.Min(Screen.width, Screen.height * Aspect);
            float height = width / Aspect;
            return new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        }

        void OnGUI()
        {
            if (!Active || !previewing) return;
            var frame = Picture();
            var color = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, frame.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.xMax, 0, Screen.width - frame.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, 0, frame.width, frame.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.yMax, frame.width, Screen.height - frame.yMax), Texture2D.whiteTexture);
            GUI.color = color;
        }

        public void Seek(float seconds)
        {
            if (!Active) return;
            playing = false;
            Pose(Mathf.Clamp(seconds, 0, Duration));
        }

        public void Stop(bool restoreMenu = true)
        {
            if (!Active) return;
            Active = false;
            playing = false;
            if (stage) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
            if (focusProfile) Destroy(focusProfile);
            if (game && game.filmSession) game.filmSession.Stop(restoreMenu);
        }

        void OnDisable() => Stop(false);
    }
}
