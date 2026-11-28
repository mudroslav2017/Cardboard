#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cardboard
{
    internal sealed class FactoryPrototypeMenu
    {
        static readonly Color Paper = new Color32(241, 230, 206, 255);
        static readonly Color Panel = new Color32(28, 44, 39, 250);
        static readonly Color Control = new Color32(56, 77, 64, 255);
        static readonly Color Gold = new Color32(224, 182, 110, 255);

        public readonly Button Entry, Host, Join, Resume;
        public readonly VisualElement Setup, Pause;
        public readonly TextField Address, Port;
        public readonly Label SetupStatus, SessionHeader, SessionStatus;
        readonly PanelSettings settings;

        public FactoryPrototypeMenu(Transform parent, Action open, Action back, Action host, Action join,
            Action resume, Action leave)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FactoryPrototypeTheme");
            settings.sortingOrder = 100;
            var node = new GameObject("Multiplayer menu");
            node.SetActive(false);
            node.transform.SetParent(parent, false);
            var document = node.AddComponent<UIDocument>();
            document.panelSettings = settings;
            node.SetActive(true);

            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root.style.color = Paper;
            root.style.fontSize = 15;

            Entry = AddButton(root, "Multiplayer prototype", open);
            Entry.style.position = Position.Absolute;
            Entry.style.top = 18;
            Entry.style.right = 20;
            Entry.style.width = 196;

            Setup = AddPanel(root);
            AddLabel(Setup, "Multiplayer prototype", 21);
            AddLabel(Setup, "Host address");
            Address = AddField(Setup, "127.0.0.1", 253);
            AddLabel(Setup, "TCP port");
            Port = AddField(Setup, "7777", 5);
            var actions = AddActions(Setup);
            Host = AddButton(actions, "Host", host);
            Join = AddButton(actions, "Join", join);
            AddButton(actions, "Back", back);
            SetupStatus = AddLabel(Setup, "Up to 4 players. Use the same port on all computers.");

            Pause = AddPanel(root);
            SessionHeader = AddLabel(Pause, "", 21);
            SessionStatus = AddLabel(Pause, "");
            actions = AddActions(Pause);
            Resume = AddButton(actions, "Resume", resume);
            AddButton(actions, "Leave session", leave);

            Entry.style.display = Setup.style.display = Pause.style.display = DisplayStyle.None;
        }

        public void Dispose() => UnityEngine.Object.Destroy(settings);

        static VisualElement AddPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.width = 370;
            panel.style.maxWidth = Length.Percent(94);
            panel.style.paddingTop = panel.style.paddingBottom = 16;
            panel.style.paddingLeft = panel.style.paddingRight = 18;
            panel.style.backgroundColor = Panel;
            panel.style.borderTopWidth = 3;
            panel.style.borderTopColor = Gold;
            parent.Add(panel);
            return panel;
        }

        static VisualElement AddActions(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 12;
            parent.Add(row);
            return row;
        }

        static Label AddLabel(VisualElement parent, string text, int size = 15)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 5;
            label.style.marginBottom = 5;
            parent.Add(label);
            return label;
        }

        static Button AddButton(VisualElement parent, string text, Action click)
        {
            var button = new Button(click) { text = text };
            button.style.height = 36;
            button.style.flexGrow = 1;
            button.style.marginRight = 4;
            button.style.backgroundColor = Control;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.color = Gold);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.color = Paper);
            parent.Add(button);
            return button;
        }

        static TextField AddField(VisualElement parent, string value, int limit)
        {
            var field = new TextField { value = value, maxLength = limit };
            field.style.height = 33;
            field.style.backgroundColor = Control;
            field.style.borderBottomWidth = 1;
            field.style.borderBottomColor = Paper;
            var input = field.Q(className: TextField.inputUssClassName);
            input.style.flexGrow = 1;
            input.style.backgroundColor = Control;
            input.style.color = Paper;
            input.style.paddingLeft = 8;
            input.style.unityTextAlign = TextAnchor.MiddleLeft;
            field.RegisterCallback<FocusInEvent>(_ => field.style.borderBottomColor = Gold);
            field.RegisterCallback<FocusOutEvent>(_ => field.style.borderBottomColor = Paper);
            parent.Add(field);
            return field;
        }
    }
}
#endif
