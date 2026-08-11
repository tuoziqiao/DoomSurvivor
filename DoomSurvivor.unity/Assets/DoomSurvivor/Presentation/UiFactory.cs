using UnityEngine;
using UnityEngine.UIElements;

namespace DoomSurvivor.Presentation
{
    internal static class UiFactory
    {
        public static VisualElement Screen(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.035f, 0.055f, 0.06f, 0.96f);
            var screen = new VisualElement();
            screen.style.flexGrow = 1;
            screen.style.paddingLeft = 36;
            screen.style.paddingRight = 36;
            screen.style.paddingTop = 28;
            screen.style.paddingBottom = 28;
            root.Add(screen);
            return screen;
        }

        public static Label Label(string text, int size = 18, Color? color = null)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color ?? new Color(0.88f, 0.94f, 0.9f);
            label.style.marginBottom = 8;
            return label;
        }

        public static Button Button(string text, System.Action clicked, int width = 260)
        {
            var button = new Button(clicked) { text = text };
            button.style.width = width;
            button.style.height = 42;
            button.style.marginTop = 5;
            button.style.marginBottom = 5;
            button.style.fontSize = 17;
            button.style.backgroundColor = new Color(0.18f, 0.32f, 0.25f);
            button.style.color = Color.white;
            return button;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        public static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.marginBottom = 14;
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.115f, 0.94f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            return card;
        }
    }
}
