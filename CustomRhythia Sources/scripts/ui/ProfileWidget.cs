using Godot;
using CustomRhythia.Rp;

namespace CustomRhythia.UI;

/// <summary>
/// Small top-right card on the main menu showing the player's nickname and
/// total RP. Clicking it opens the profile screen.
/// </summary>
public partial class ProfileWidget : Button
{
    private Label _nick;
    private Label _rp;

    private const int WidgetWidth = 200;
    private const int WidgetHeight = 68;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Text = "";

        // Anchor top-right with a margin.
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 0;
        OffsetLeft = -WidgetWidth - 16;
        OffsetRight = -16;
        OffsetTop = 16;
        OffsetBottom = 16 + WidgetHeight;

        ApplyStyles();

        _nick = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true,
        };
        _nick.AddThemeFontSizeOverride("font_size", 17);
        _nick.AddThemeColorOverride("font_color", UiStyle.TextPrimary);
        _nick.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _nick.OffsetLeft = 12; _nick.OffsetRight = -12;
        _nick.OffsetTop = 6; _nick.OffsetBottom = 36;
        AddChild(_nick);

        _rp = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true,
        };
        _rp.AddThemeFontSizeOverride("font_size", 14);
        _rp.AddThemeColorOverride("font_color", UiStyle.Accent);
        _rp.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _rp.OffsetLeft = 12; _rp.OffsetRight = -12;
        _rp.OffsetTop = 36; _rp.OffsetBottom = 62;
        AddChild(_rp);

        Pressed += () => Main.Instance.ShowProfile();

        Refresh();
        PlayerProfile.Changed += Refresh;
    }

    public override void _ExitTree()
    {
        PlayerProfile.Changed -= Refresh;
    }

    private void ApplyStyles()
    {
        var normal = UiStyle.PanelStyle(UiStyle.Surface, UiStyle.Border, 10);
        normal.ContentMarginLeft = 0; normal.ContentMarginRight = 0;
        normal.ContentMarginTop = 0; normal.ContentMarginBottom = 0;

        var hover = UiStyle.PanelStyle(UiStyle.SurfaceHi, UiStyle.Accent, 10);
        hover.ContentMarginLeft = 0; hover.ContentMarginRight = 0;
        hover.ContentMarginTop = 0; hover.ContentMarginBottom = 0;

        var pressed = UiStyle.PanelStyle(UiStyle.SurfaceHi.Darkened(0.1f), UiStyle.Accent, 10);
        pressed.ContentMarginLeft = 0; pressed.ContentMarginRight = 0;
        pressed.ContentMarginTop = 0; pressed.ContentMarginBottom = 0;

        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", pressed);
        AddThemeStyleboxOverride("focus", hover);
    }

    private void Refresh()
    {
        string nick = PlayerProfile.Data.Nickname;
        _nick.Text = string.IsNullOrEmpty(nick) ? "Player" : nick;

        double rp = PlayerProfile.Data.TotalRP;
        _rp.Text = rp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " RP";
    }
}
