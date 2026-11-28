using UnityEngine;

namespace Cardboard
{
    public sealed class FactoryRatHunt : MonoBehaviour
    {
        public const int Quota = 10;
        public const int StrikeLimit = 3;

        public CardboardFactoryGame game;
        public FactoryRatTarget[] targets;
        public Transform grabber, basket, leftJaw, rightJaw;
        public GameObject[] basketContents;

        public int Food { get; private set; }
        public int Strikes { get; private set; }
        public bool Delivered { get; private set; }
        public bool CanGrab => ActiveGameplay && InSewer && Food < Quota && Strikes < StrikeLimit;
        public bool CanDeliver => ActiveGameplay && !InSewer && Food == Quota && !Delivered;
        bool InSewer => CardboardRunner.Player && CardboardRunner.Player.InSewer;
        bool ActiveGameplay => game && game.State == FactoryState.Working && !game.PlayerBlocked &&
            !game.IntroActive && !game.CaptureActive;
        bool started;
        string feedback;
        float feedbackUntil, strikeTime;
        GUIStyle title, description, aim;
        Vector3 restPosition;

        void Awake() { restPosition = grabber.localPosition; }

        public void ResetHunt()
        {
            Food = Strikes = 0;
            Delivered = started = false;
            feedback = "";
            strikeTime = feedbackUntil = float.NegativeInfinity;
            foreach (var target in targets) target.ResetTarget();
            foreach (var rat in basketContents) rat.SetActive(false);
            grabber.gameObject.SetActive(false);
            basket.gameObject.SetActive(false);
        }

        public bool TryCollect(FactoryRatTarget target)
        {
            return Collect(target, CanGrab);
        }
        internal bool TryCollectCoop(FactoryRatTarget target, bool underground) => Collect(target,
            underground && game.State == FactoryState.Working && Food < Quota && Strikes < StrikeLimit);

        bool Collect(FactoryRatTarget target, bool canGrab)
        {
            if (!canGrab || !target || !target.Available) return false;
            target.Remove();
            started = true;
            strikeTime = Time.time;
            if (target.food == FactoryRatFood.LiveRat)
            {
                Food++;
                int visibleRats = Mathf.CeilToInt(Food * basketContents.Length / (float)Quota);
                for (int i = 0; i < visibleRats; i++) basketContents[i].SetActive(true);
                game.PlayCaptureSound("good");
                SetFeedback(Food == Quota ? "Quota filled. Take the basket to the canteen." : "Live rat caught.");
            }
            else
            {
                Strikes++;
                string item = target.food == FactoryRatFood.RottenRat ? "Rotten rat" : "Droppings";
                SetFeedback(item + " discarded. Strike " + Strikes + " / " + StrikeLimit + ".");
                game.PlayCaptureSound("wrong");
                if (Strikes == StrikeLimit) game.Capture(FactoryDeath.Press);
            }
            return true;
        }

        public bool Deliver()
        {
            return Deliver(CanDeliver);
        }
        internal bool DeliverCoop() => Deliver(game.State == FactoryState.Working && Food == Quota && !Delivered);

        bool Deliver(bool canDeliver)
        {
            if (!canDeliver) return false;
            Delivered = true;
            if (game.kitchen) game.kitchen.ReceiveRats(Food);
            foreach (var rat in basketContents) rat.SetActive(false);
            game.PlayCaptureSound("good");
            SetFeedback("The kitchen received all 10 rats.");
            return true;
        }

        void SetFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.time + 4f;
        }

        void LateUpdate()
        {
            if (!game.MultiplayerPrototype) Tick(Time.deltaTime);
            bool showTools = ActiveGameplay && !game.CameraOverride && !CardboardMarker.DrawingMode &&
                !game.HandsBusy && !game.HasPart && (!game.R82C4916Item || !game.R82C4916Item.Drawn) &&
                !Delivered && Strikes < StrikeLimit;
            grabber.gameObject.SetActive(showTools && InSewer);
            basket.gameObject.SetActive(showTools && (InSewer || Food == Quota));
            if (!grabber.gameObject.activeSelf) return;

            float elapsed = Time.time - strikeTime;
            float thrust = Mathf.Sin(Mathf.Clamp01(elapsed / .32f) * Mathf.PI);
            float squeeze = Mathf.Sin(Mathf.Clamp01((elapsed - .07f) / .29f) * Mathf.PI);
            var camera = Camera.main;
            var sway = camera && camera.TryGetComponent(out CardboardCamera view)
                ? camera.transform.InverseTransformVector(view.WalkingOffset) : Vector3.zero;
            grabber.localPosition = restPosition - sway * 1.7f + new Vector3(-.065f, .04f, .16f) * thrust;
            leftJaw.localRotation = Quaternion.Euler(0, 14 * squeeze, 0);
            rightJaw.localRotation = Quaternion.Euler(0, -14 * squeeze, 0);
        }

        public void Tick(float dt)
        {
            if (!ActiveGameplay || !InSewer) return;
            started = true;
            foreach (var target in targets) target.Tick(dt, CardboardRunner.Player.transform.position);
        }

        internal void TickCoop(float dt, Vector3 position)
        {
            if (game.State != FactoryState.Working || Delivered) return;
            started = true;
            foreach (var target in targets) target.Tick(dt, position);
        }

        internal void ApplyCoopProgress(int food, int strikes, bool delivered)
        {
            Food = food;
            Strikes = strikes;
            Delivered = delivered;
            int visible = Mathf.CeilToInt(Food * basketContents.Length / (float)Quota);
            for (int i = 0; i < basketContents.Length; i++)
                basketContents[i].SetActive(!Delivered && i < visible);
        }

        void OnGUI()
        {
            if (!game || game.MenuActive || game.Ended || game.FilmActive || game.CameraOverride ||
                (game.CaptureActive && Strikes != StrikeLimit)) return;
            bool showingFeedback = Time.time < feedbackUntil;
            if (game.CaptureActive && !showingFeedback) return;
            var focus = game.Focus;
            bool atHatch = focus && focus.action == FactoryAction.SewerAccess;
            bool showTask = started && (!Delivered || showingFeedback);
            bool showAim = !CardboardMarker.DrawingMode && (!game.R82C4916Item || !game.R82C4916Item.Drawn) &&
                (InSewer || atHatch || CanDeliver && focus &&
                focus.action == FactoryAction.RatDelivery);
            if (!showTask && !showAim) return;

            title ??= Style(22, FontStyle.Bold, TextAnchor.MiddleLeft);
            description ??= Style(15, FontStyle.Normal, TextAnchor.MiddleLeft);
            aim ??= Style(16, FontStyle.Bold, TextAnchor.MiddleCenter);
            float scale = Mathf.Min(Screen.width / 1080f, Screen.height / 660f);
            if (scale <= 0) return;
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            if (showTask)
            {
                // The repair counter occupies the first 104 units on the 1000-high HUD canvas.
                float top = height * .125f;
                Panel(new Rect(22, top, 334, 108));
                GUI.Label(new Rect(37, top + 5, 304, 32), $"Kitchen food   {Food} / {Quota}", title);
                GUI.Label(new Rect(37, top + 40, 304, 26), $"Strikes   {Strikes} / {StrikeLimit}", description);
                string instruction = Strikes == StrikeLimit ? "The guards are coming." : Delivered ? "Food delivered" :
                    Food == Quota ? "Deliver the basket to the canteen" :
                    InSewer ? "Aim and press E. Catch live rats only." : "Return to the sewer to catch live rats";
                GUI.Label(new Rect(37, top + 71, 304, 28), instruction, description);
            }
            if (showAim)
            {
                GUI.Label(new Rect(width / 2 - 12, height / 2 - 12, 24, 24), "+", aim);
                string prompt = game.FocusPrompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    var label = new Rect(width / 2 - 185, height / 2 + 22, 370, 32);
                    Panel(label);
                    GUI.Label(label, prompt, aim);
                }
            }
            if (showingFeedback)
            {
                var label = new Rect(width / 2 - 220, height - 93, 440, 37);
                Panel(label);
                GUI.Label(label, feedback, aim);
            }
            GUI.matrix = matrix;
            GUI.color = color;
        }

        static GUIStyle Style(int size, FontStyle weight, TextAnchor align)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = CardboardTypography.DefaultFont, fontSize = size, fontStyle = weight, alignment = align
            };
            style.normal.textColor = new Color(.99f, .91f, .76f);
            return style;
        }

        static void Panel(Rect rect)
        {
            GUI.color = new Color(.09f, .12f, .11f, .94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
