using Godot;
using CustomRhythia.Map;
using CustomRhythia.Rp;

namespace CustomRhythia.UI;

// Title screen: Play / Profile / Settings / Quit. Handles drag & drop
// map import and hosts the profile widget in the top-right corner.
public partial class MainMenuScreen : Control
{
    public const string GameTagline = "Aim-Based Rhythm Game by kage";

    private AcceptDialog _nicknameDialog;
    private LineEdit _nicknameEdit;

    public override void _Ready()
    {
        ScreenUtil.MakeFullScreen(this);

        AddChild(UiStyle.FullScreen(UiStyle.MakeBackground()));

        BuildCard();
        BuildTopRightWidget();

        GetWindow().Connect("files_dropped", Callable.From<string[]>(OnFilesDropped));

        if (PlayerProfile.IsFirstLaunch)
        {
            CallDeferred(nameof(ShowNicknameDialog));
        }
    }

    private void BuildCard()
    {
        var card = UiStyle.MakePanel(UiStyle.Surface);
        card.AnchorLeft = 0.5f;
        card.AnchorRight = 0.5f;
        card.AnchorTop = 0.5f;
        card.AnchorBottom = 0.5f;
        card.OffsetLeft = -190;
        card.OffsetRight = 190;
        card.OffsetTop = -220;
        card.OffsetBottom = 220;
        AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 32);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var title = UiStyle.MakeLabel("CustomRhythia", 40, UiStyle.TextPrimary);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var underline = new ColorRect
        {
            Color = UiStyle.Accent,
            CustomMinimumSize = new Vector2(0, 2),
        };
        vbox.AddChild(underline);

        var subtitle = UiStyle.MakeLabel(GameTagline, 13, UiStyle.TextMuted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(subtitle);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        var play = UiStyle.MakeButton("PLAY", 20, accent: true);
        play.Pressed += OnPlayPressed;
        vbox.AddChild(play);

        var profile = UiStyle.MakeButton("PROFILE");
        profile.Pressed += OnProfilePressed;
        vbox.AddChild(profile);

        var settings = UiStyle.MakeButton("SETTINGS");
        settings.Pressed += OnSettingsPressed;
        vbox.AddChild(settings);

        var quit = UiStyle.MakeButton("QUIT");
        quit.Pressed += OnQuitPressed;
        vbox.AddChild(quit);

        vbox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
    }

    private void BuildTopRightWidget()
    {
        var widget = new ProfileWidget();
        AddChild(widget);
    }

    private void OnPlayPressed()
    {
        Main.Instance.ShowMapSelect();
    }

    private void OnProfilePressed()
    {
        Main.Instance.ShowProfile();
    }

    private void OnSettingsPressed()
    {
        Main.Instance.ShowSettings();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void ShowNicknameDialog()
    {
        _nicknameDialog = new AcceptDialog
        {
            Title = "Welcome",
            OkButtonText = "Continue",
        };

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        vbox.AddThemeConstantOverride("separation", 8);
        _nicknameDialog.AddChild(vbox);

        var lbl = UiStyle.MakeLabel("Choose a nickname. You can change it later in Profile.", 12, UiStyle.TextMuted);
        lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(lbl);

        _nicknameEdit = new LineEdit
        {
            PlaceholderText = "Player",
            CustomMinimumSize = new Vector2(0, 34),
            Text = "",
        };
        _nicknameEdit.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_nicknameEdit);

        _nicknameDialog.Confirmed += OnNicknameConfirmed;

        AddChild(_nicknameDialog);
        _nicknameDialog.PopupCentered();
        _nicknameEdit.GrabFocus();
    }

    private void OnNicknameConfirmed()
    {
        string nick = _nicknameEdit == null ? "" : _nicknameEdit.Text.Trim();
        if (string.IsNullOrEmpty(nick)) nick = "Player";
        PlayerProfile.SetNickname(nick);
    }

    private async void OnFilesDropped(string[] files)
    {
        int n = await MapManager.ImportAsync(files);
        if (n > 0)
        {
            GD.Print("Imported " + n + " map(s).");
            Main.Instance.ShowMapSelect();
        }
    }
}
