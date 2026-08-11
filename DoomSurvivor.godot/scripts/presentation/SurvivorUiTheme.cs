using Godot;

namespace DoomSurvivor.Presentation;

public enum SurvivorButtonTone
{
    Wood,
    Green,
    Blue,
    Orange
}

public static class SurvivorUiTheme
{
    public static readonly Color Parchment = new("#FFF2D2");
    public static readonly Color ParchmentMuted = new("#D9C7A4");
    public static readonly Color Gold = new("#F2C96B");
    public static readonly Color GoldBright = new("#FFE29A");
    public static readonly Color GoldDark = new("#9A672C");
    public static readonly Color Wood = new("#6E4224");
    public static readonly Color WoodDark = new("#2A1B14");
    public static readonly Color Green = new("#3B7D36");
    public static readonly Color GreenBright = new("#83C95B");
    public static readonly Color Blue = new("#236B94");
    public static readonly Color BlueBright = new("#63C5E8");
    public static readonly Color Orange = new("#B95D25");
    public static readonly Color OrangeBright = new("#F5A34C");
    public static readonly Color Ink = new("#142019");

    public static void ApplyCard(Control control, bool opaque = false)
    {
        var card = new StyleBoxFlat
        {
            BgColor = opaque ? new Color(0.07f, 0.09f, 0.07f, 0.96f) : new Color(0.07f, 0.09f, 0.07f, 0.86f),
            BorderColor = GoldDark,
            ShadowColor = new Color(0f, 0f, 0f, 0.42f),
            ShadowSize = 10,
            ContentMarginLeft = 18,
            ContentMarginTop = 18,
            ContentMarginRight = 18,
            ContentMarginBottom = 18
        };
        card.SetBorderWidthAll(2);
        card.SetCornerRadiusAll(14);
        control.AddThemeStyleboxOverride("panel", card);
    }

    public static void ApplyParchment(Control control)
    {
        var parchment = new StyleBoxFlat
        {
            BgColor = new Color(0.98f, 0.91f, 0.72f, 0.95f),
            BorderColor = new Color(0.47f, 0.29f, 0.12f, 0.95f),
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 10,
            ContentMarginLeft = 20,
            ContentMarginTop = 18,
            ContentMarginRight = 20,
            ContentMarginBottom = 18
        };
        parchment.SetBorderWidthAll(3);
        parchment.SetCornerRadiusAll(16);
        control.AddThemeStyleboxOverride("panel", parchment);
    }

    public static void ApplyParchmentButton(Button button, bool selected = false)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = selected ? new Color(0.38f, 0.61f, 0.16f, 0.96f) : new Color(0.98f, 0.91f, 0.72f, 0.96f),
            BorderColor = selected ? new Color(0.25f, 0.39f, 0.08f, 1f) : new Color(0.58f, 0.38f, 0.17f, 0.95f),
            ShadowColor = new Color(0f, 0f, 0f, 0.25f),
            ShadowSize = 3,
            ContentMarginLeft = 14,
            ContentMarginTop = 7,
            ContentMarginRight = 14,
            ContentMarginBottom = 7
        };
        normal.SetBorderWidthAll(2);
        normal.SetCornerRadiusAll(10);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = selected ? new Color(0.48f, 0.72f, 0.22f, 1f) : new Color(1f, 0.96f, 0.84f, 1f);
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = selected ? new Color(0.29f, 0.5f, 0.1f, 1f) : new Color(0.9f, 0.82f, 0.61f, 1f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeColorOverride("font_color", selected ? Colors.White : Ink);
        button.AddThemeColorOverride("font_hover_color", selected ? Colors.White : new Color(0.18f, 0.1f, 0.03f));
        button.AddThemeColorOverride("font_pressed_color", selected ? Colors.White : Ink);
        button.AddThemeColorOverride("font_outline_color", new Color(1f, 0.95f, 0.8f, 0.45f));
        button.AddThemeConstantOverride("outline_size", 2);
        button.AddThemeFontSizeOverride("font_size", 16);
    }

    public static void ApplyInk(Label label, bool muted = false)
    {
        label.AddThemeColorOverride("font_color", muted ? new Color(0.35f, 0.25f, 0.14f, 0.82f) : Ink);
        label.AddThemeColorOverride("font_outline_color", new Color(1f, 0.95f, 0.8f, 0.45f));
        label.AddThemeConstantOverride("outline_size", 2);
    }

    public static void ApplyLogoPanel(Control control)
    {
        var logo = new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.22f, 0.08f, 0.28f),
            BorderColor = new Color(0.95f, 0.72f, 0.25f, 0.88f),
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 9,
            ContentMarginLeft = 12,
            ContentMarginTop = 12,
            ContentMarginRight = 12,
            ContentMarginBottom = 12
        };
        logo.SetBorderWidthAll(2);
        logo.SetCornerRadiusAll(18);
        control.AddThemeStyleboxOverride("panel", logo);
    }

    public static void ApplyWoodPanel(Control control)
    {
        var wood = new StyleBoxFlat
        {
            BgColor = new Color(0.25f, 0.13f, 0.055f, 0.82f),
            BorderColor = new Color(0.95f, 0.72f, 0.3f, 0.92f),
            ShadowColor = new Color(0f, 0f, 0f, 0.38f),
            ShadowSize = 8,
            ContentMarginLeft = 14,
            ContentMarginTop = 10,
            ContentMarginRight = 14,
            ContentMarginBottom = 10
        };
        wood.SetBorderWidthAll(2);
        wood.SetCornerRadiusAll(16);
        control.AddThemeStyleboxOverride("panel", wood);
    }

    public static void ApplySection(Control control, Color accent)
    {
        var section = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.07f, 0.055f, 0.86f),
            BorderColor = accent,
            ShadowColor = new Color(0f, 0f, 0f, 0.3f),
            ShadowSize = 5,
            ContentMarginLeft = 16,
            ContentMarginTop = 14,
            ContentMarginRight = 16,
            ContentMarginBottom = 14
        };
        section.SetBorderWidthAll(1);
        section.SetCornerRadiusAll(11);
        control.AddThemeStyleboxOverride("panel", section);
    }

    public static void ApplyButton(Button button, SurvivorButtonTone tone, bool compact = false)
    {
        var palette = tone switch
        {
            SurvivorButtonTone.Green => (Green, GreenBright),
            SurvivorButtonTone.Blue => (Blue, BlueBright),
            SurvivorButtonTone.Orange => (Orange, OrangeBright),
            _ => (Wood, GoldBright)
        };

        button.AddThemeStyleboxOverride("normal", CreateButtonBox(palette.Item1, GoldDark, 0.9f));
        button.AddThemeStyleboxOverride("hover", CreateButtonBox(Lighten(palette.Item1, 0.14f), palette.Item2, 1.0f));
        button.AddThemeStyleboxOverride("pressed", CreateButtonBox(Darken(palette.Item1, 0.12f), GoldBright, 0.96f));
        button.AddThemeStyleboxOverride("focus", CreateButtonBox(palette.Item1, GoldBright, 1.0f));
        button.AddThemeStyleboxOverride("disabled", CreateButtonBox(new Color(0.18f, 0.2f, 0.17f, 0.72f), new Color(0.4f, 0.38f, 0.3f, 0.7f), 0.7f));
        button.AddThemeColorOverride("font_color", Parchment);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Parchment);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.62f, 0.61f, 0.54f, 0.78f));
        button.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.55f));
        button.AddThemeConstantOverride("outline_size", 3);
        button.AddThemeFontSizeOverride("font_size", compact ? 15 : 17);
    }

    public static void ApplyHeading(Label label, int size = 24)
    {
        label.AddThemeColorOverride("font_color", GoldBright);
        label.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.04f, 0.01f, 0.8f));
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeFontSizeOverride("font_size", size);
    }

    public static void ApplyBody(Label label, bool muted = false)
    {
        label.AddThemeColorOverride("font_color", muted ? ParchmentMuted : Parchment);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.65f));
        label.AddThemeConstantOverride("outline_size", 2);
    }

    private static StyleBoxFlat CreateButtonBox(Color fill, Color border, float alpha)
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(fill.R, fill.G, fill.B, alpha),
            BorderColor = border,
            ShadowColor = new Color(0f, 0f, 0f, 0.45f),
            ShadowSize = 5,
            ContentMarginLeft = 14,
            ContentMarginTop = 9,
            ContentMarginRight = 14,
            ContentMarginBottom = 9
        };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(9);
        return box;
    }

    private static Color Lighten(Color color, float amount) => new(
        Mathf.Clamp(color.R + amount, 0f, 1f),
        Mathf.Clamp(color.G + amount, 0f, 1f),
        Mathf.Clamp(color.B + amount, 0f, 1f),
        color.A);

    private static Color Darken(Color color, float amount) => new(
        Mathf.Clamp(color.R - amount, 0f, 1f),
        Mathf.Clamp(color.G - amount, 0f, 1f),
        Mathf.Clamp(color.B - amount, 0f, 1f),
        color.A);
}
