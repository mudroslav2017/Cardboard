using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard
{
    [DefaultExecutionOrder(-70), DisallowMultipleComponent]
    public sealed class FactoryNastyaLeraTrend : MonoBehaviour, IFactoryTrendRecording
    {
        public const int FrameRate = 30;
        public const int SwapFrame = 134;
        public const int FrameCount = 269;
        public const float Duration = (float)FrameCount / FrameRate;
        const float Aspect = 9f / 16;
        const int StageLayer = 30;
        const string AudioResource = "NastyaLeraTrend/Reference audio";

        sealed class Actor
        {
            public Transform Root;
            public FactoryCardboardBend Bend;
        }

        readonly List<Material> stageMaterials = new List<Material>();
        CardboardFactoryGame game;
        Camera lens;
        Transform stage;
        AudioSource soundtrack;
        Actor nastya, lera;
        int previousMask;
        bool showControls, playing;
        float time;

        public bool Active { get; private set; }
        public Camera Lens => lens;
        public AudioClip Soundtrack => soundtrack ? soundtrack.clip : null;

        void Awake() => game = GetComponent<CardboardFactoryGame>();

        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.f8Key.wasPressedThisFrame)
            {
                if (Active) Stop();
                else Begin(true);
            }
            if (!Active) return;

            if (keys != null)
            {
                if (keys.hKey.wasPressedThisFrame) showControls = !showControls;
                if (keys.f7Key.wasPressedThisFrame)
                {
                    Seek(0);
                    SetPlaying(true);
                    return;
                }
                if (keys.spaceKey.wasPressedThisFrame) SetPlaying(!playing);
            }

            if (!playing) return;
            Pose(Mathf.Min(time + Time.unscaledDeltaTime, Duration));
            if (time >= Duration) SetPlaying(false);
        }

        public bool Begin(bool preview)
        {
            if (Active) return true;
            if (!game || !game.filmSession.Begin()) return false;

            lens = Camera.main;
            previousMask = lens.cullingMask;
            Active = true;
            showControls = preview;
            try
            {
                stage = new GameObject("Nastya and Lera • TikTok stage").transform;
                stage.gameObject.layer = StageLayer;
                BuildSet();
                nastya = Cast("Настя", 2);
                lera = Cast("Лера", 0);

                soundtrack = stage.gameObject.AddComponent<AudioSource>();
                soundtrack.clip = Resources.Load<AudioClip>(AudioResource);
                if (!soundtrack.clip) throw new InvalidOperationException("Nastya and Lera soundtrack is missing.");
                soundtrack.playOnAwake = false;
                soundtrack.ignoreListenerPause = true;
                soundtrack.spatialBlend = 0;

                lens.cullingMask = 1 << StageLayer;
                lens.backgroundColor = new Color(.12f, .085f, .065f);
                lens.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.48f, .42f, .35f);
                lens.aspect = Aspect;
                Pose(0);
                if (preview) SetPlaying(true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop(false);
                return false;
            }
        }

        void LateUpdate()
        {
            if (!Active || lens.targetTexture) return;
            Rect frame = PortraitPixels;
            lens.rect = new Rect(frame.x / Screen.width, frame.y / Screen.height, frame.width / Screen.width, frame.height / Screen.height);
            lens.aspect = Aspect;
        }

        static Rect PortraitPixels
        {
            get
            {
                float width = Mathf.Min(Screen.width, Screen.height * Aspect);
                float height = width / Aspect;
                return new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            }
        }

        void OnGUI()
        {
            if (!Active) return;
            var frame = PortraitPixels;
            var color = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, frame.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.xMax, 0, Screen.width - frame.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, 0, frame.width, frame.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.yMax, frame.width, Screen.height - frame.yMax), Texture2D.whiteTexture);
            GUI.color = color;
            if (!showControls) return;

            float left = frame.x + 10;
            float top = frame.yMax - 47;
            float width = frame.width - 20;
            GUI.Box(new Rect(left, top, width, 36), GUIContent.none);
            if (GUI.Button(new Rect(left + 5, top + 5, 64, 25), playing ? "Пауза" : "Играть"))
                SetPlaying(!playing);
            float next = GUI.HorizontalSlider(new Rect(left + 77, top + 11, Mathf.Max(20, width - 160), 18), time, 0, Duration);
            if (!Mathf.Approximately(next, time)) Seek(next);
            if (GUI.Button(new Rect(left + width - 67, top + 5, 62, 25), "F8  Выход")) Stop();
        }

        public void Seek(float seconds)
        {
            if (!Active) return;
            SetPlaying(false);
            Pose(Mathf.Clamp(seconds, 0, Duration));
        }

        void SetPlaying(bool value)
        {
            if (value == playing) return;
            playing = value;
            if (!value)
            {
                if (soundtrack) soundtrack.Stop();
                return;
            }

            if (time >= Duration) Pose(0);
            soundtrack.timeSamples = Mathf.Min(soundtrack.clip.samples - 1, Mathf.RoundToInt(time * soundtrack.clip.frequency));
            soundtrack.Play();
        }

        static float Ease(float t, float from, float to) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));

        void Pose(float seconds)
        {
            time = seconds;
            float cut = (float)SwapFrame / FrameRate;
            bool swapped = time >= cut;
            float shot = swapped ? time - cut : time;
            var close = swapped ? lera : nastya;
            var far = swapped ? nastya : lera;

            // The repeated three accents in the source audio are 0.48 seconds apart.
            float firstHop = swapped ? 6.939f : 3.137f;
            float secondHop = firstHop + .478f;
            float thirdHop = secondHop + .478f;
            float turn = Ease(time, firstHop - .79f, firstHop - .11f);
            float ready = Mathf.Sin(turn * Mathf.PI);
            float sway = 1.5f * Mathf.Sin(shot * 5.4f) * (1 - turn);
            float first = Hop(time, firstHop);
            float second = Hop(time, secondHop);
            float third = Hop(time, thirdHop);
            float descent = Mathf.Clamp01((first + second + third) / 3);

            // The pointing accents happen while both actors still face the camera.
            float firstPoke = swapped ? 5.046f : .760f;
            const float pokeSpacing = .476f;
            Vector2 poke = (Poke(time - firstPoke) * .85f
                + Poke(time - firstPoke - pokeSpacing)
                + Poke(time - firstPoke - 2 * pokeSpacing) * .92f) * (1 - turn);

            Vector3 closePosition = new Vector3(Mathf.Lerp(.34f, .43f, descent), 0, 3.14f + .08f * turn);
            Vector3 rearPosition = new Vector3(-.63f, 0, 4.98f);
            Vector3 towardRear = (rearPosition - closePosition).normalized;
            Vector3 pokeAxis = Vector3.Cross(Vector3.up, towardRear);
            var closeRotation = Quaternion.AngleAxis(23 * poke.x, pokeAxis)
                * Quaternion.Euler(0, Mathf.LerpAngle(187 + sway + 9 * poke.x, -60, turn), 2 * sway - 2 * ready);
            close.Bend.Apply(0, 47 * first + 36 * second + 46 * third - 10 * ready, 0);
            if (Mathf.Abs(poke.x) > .001f)
            {
                closePosition += towardRear * (.055f * poke.x);
                closePosition.y = close.Bend.GroundLift(closeRotation);
            }
            close.Root.SetPositionAndRotation(closePosition, closeRotation);

            // Each hit adds a new crease, starting at the shoulders and working down to the legs.
            const float responseDelay = .028f;
            float rearFirst = Hop(time, firstHop + responseDelay);
            float rearSecond = Hop(time, secondHop + responseDelay);
            float rearThird = Hop(time, thirdHop + responseDelay);
            float rearTurn = Ease(time, firstHop - .08f, firstHop + .27f);
            far.Bend.Apply(106 * rearThird, -156 * rearSecond - 16 * rearThird,
                118 * rearFirst + 4 * rearSecond + 52 * rearThird - 13 * poke.y);
            float rearFacing = Mathf.LerpAngle(173, 158, rearTurn) - 16 * Mathf.Clamp01(rearThird);
            var rearRotation = Quaternion.AngleAxis(23 * poke.y, pokeAxis)
                * Quaternion.Euler(0, rearFacing + 10 * poke.y, -1.7f * Mathf.Sin(shot * 4.1f) * (1 - rearTurn));
            rearPosition += towardRear * (.18f * poke.y);
            rearPosition.y = Mathf.Abs(poke.y) > .001f
                ? far.Bend.GroundLift(rearRotation)
                : Mathf.Max(0, -far.Bend.Mesh.bounds.min.y);
            far.Root.SetPositionAndRotation(rearPosition, rearRotation);

            float drift = Mathf.Sin(shot * 1.42f);
            Vector3 eye = new Vector3(.016f * drift, 1.49f - .08f * descent, 0);
            Vector3 aim = new Vector3(.005f - .06f * (second - third), 1.26f - .61f * descent + .018f * drift, 4.4f);
            lens.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            lens.fieldOfView = 48 - 2 * descent + .45f * Mathf.Sin(shot * 1.2f);
        }

        static float Pulse(float time, float start, float peak, float end)
            => Ease(time, start, peak) * (1 - Ease(time, peak, end));

        static Vector2 Poke(float time)
        {
            float lean = Pulse(time, -.068f, .02f, .174f)
                - .14f * Pulse(time, -.124f, -.085f, -.054f)
                - .13f * Pulse(time, .17f, .22f, .32f);
            float recoil = Pulse(time, .026f, .119f, .275f)
                - .19f * Pulse(time, .211f, .27f, .36f);
            return new Vector2(lean, recoil);
        }

        static float Hop(float time, float beat)
        {
            float dip = Ease(time, beat - .07f, beat + .14f);
            float rebound = Mathf.InverseLerp(beat + .14f, beat + .36f, time);
            return dip + .05f * Mathf.Sin(rebound * 2 * Mathf.PI) * (1 - rebound);
        }

        Actor Cast(string label, int variant)
        {
            var anchor = new GameObject(label).transform;
            anchor.SetParent(stage, false);
            anchor.gameObject.layer = StageLayer;
            var model = ImportedCardboardCharacters.Create(anchor, variant);
            model.gameObject.layer = StageLayer;
            var appearance = model.GetComponent<FactoryWorkerAppearance>();
            if (appearance) appearance.SetState(FactoryFaceState.Normal);
            return new Actor { Root = anchor, Bend = new FactoryCardboardBend(model.GetComponent<MeshFilter>()) };
        }

        void BuildSet()
        {
            var paper = Paper(new Color(.48f, .32f, .21f));
            var edge = Paper(new Color(.28f, .16f, .092f));
            var trim = Paper(new Color(.62f, .42f, .24f));
            Material floor = null;
            foreach (var renderer in game.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Main workshop floor") continue;
                floor = renderer.sharedMaterial;
                break;
            }
            Block("Workshop floor", new Vector3(0, -.07f, 4.8f), new Vector3(16, .14f, 15), floor ? floor : trim);
            Block("Cardboard backdrop", new Vector3(0, 2.5f, 7.42f), new Vector3(9, 5, .24f), paper);
            for (int i = -4; i <= 4; i++)
                Block("Folded wall seam", new Vector3(i * .79f, 2.5f, 7.283f), new Vector3(.016f, 5, .04f), edge);
            Block("Bottom edge", new Vector3(0, .09f, 7.23f), new Vector3(9, .18f, .20f), edge);

            Block("Box left", new Vector3(-1.69f, .25f, 6.65f), new Vector3(.61f, .5f, .51f), trim, -8);
            Block("Box left seam", new Vector3(-1.69f, .25f, 6.386f), new Vector3(.07f, .5f, .013f), edge, -8);
            Block("Box right", new Vector3(1.28f, .23f, 6.79f), new Vector3(.72f, .46f, .6f), paper, 10);
            Block("Box right top", new Vector3(1.37f, .59f, 6.87f), new Vector3(.52f, .26f, .44f), trim, -9);

            var key = new GameObject("Soft workshop key").AddComponent<Light>();
            key.transform.SetParent(stage, false);
            key.transform.rotation = Quaternion.Euler(43, -22, 0);
            key.type = LightType.Directional;
            key.color = new Color(1, .87f, .69f);
            key.intensity = 1.62f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .68f;

            var fill = new GameObject("Cool rim").AddComponent<Light>();
            fill.transform.SetParent(stage, false);
            fill.transform.position = new Vector3(1.7f, 3.12f, 5.8f);
            fill.type = LightType.Point;
            fill.range = 7;
            fill.color = new Color(.58f, .74f, 1);
            fill.intensity = 2.5f;
        }

        Material Paper(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            material.SetFloat("_Smoothness", 0);
            stageMaterials.Add(material);
            return material;
        }

        void Block(string name, Vector3 position, Vector3 scale, Material material, float yaw = 0)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = StageLayer;
            cube.transform.SetParent(stage, false);
            cube.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        public void Stop(bool restoreMenu = true)
        {
            if (!Active) return;
            SetPlaying(false);
            Active = false;
            if (stage)
            {
                stage.gameObject.SetActive(false);
                Destroy(stage.gameObject);
            }
            if (nastya != null) Destroy(nastya.Bend.Mesh);
            if (lera != null) Destroy(lera.Bend.Mesh);
            nastya = lera = null;
            foreach (var material in stageMaterials) Destroy(material);
            stageMaterials.Clear();

            if (lens) lens.cullingMask = previousMask;
            game.filmSession.Stop(restoreMenu);
        }

        void OnDisable() => Stop(false);
    }
}
