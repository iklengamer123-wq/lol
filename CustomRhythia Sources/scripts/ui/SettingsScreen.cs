using System;
using System.Globalization;
using System.IO;
using Godot;

namespace CustomRhythia.UI;

// Settings overlay. Values persist to the active profile.
// Layout mirrors the Profile screen: a centered card with an internal
// scroll area and a fixed footer, so nothing overflows the viewport.
public partial class SettingsScreen : Control
{
    private readonly System.Collections.Generic.List<(string Key, HSlider Slider, Label Value, Func<double, string> Format)> _sliderRefs = new();
    private readonly System.Collections.Generic.List<(string Key, CheckButton Toggle, Action<bool> Setter)> _toggleRefs = new();

    private const int CardWidth = 840;
    private const int CardHeight = 720;

    private Label _hitSoundValue;

    public override void _Ready()
    {
        ScreenUtil.MakeFullScreen(this);

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
        AddChild(bg);
        UiStyle.FullScreen(bg);

        var card = UiStyle.MakePanel(UiStyle.Surface);
        card.AnchorLeft = 0.5f;
        card.AnchorRight = 0.5f;
        card.AnchorTop = 0.5f;
        card.AnchorBottom = 0.5f;
        card.OffsetLeft = -CardWidth / 2f;
        card.OffsetRight = CardWidth / 2f;
        card.OffsetTop = -CardHeight / 2f;
        card.OffsetBottom = CardHeight / 2f;
        AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        BuildHeader(root);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);

        BuildGameplay(content);
        BuildVisual(content);
        BuildAudio(content);
        BuildCursor(content);
        BuildPerformance(content);

        BuildFooter(root);

        ApplyCurrentValues();
    }

    private void BuildHeader(VBoxContainer root)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        root.AddChild(header);

        var title = UiStyle.MakeLabel("Settings", 26, UiStyle.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(title);

        var rule = new ColorRect { Color = UiStyle.Border, CustomMinimumSize = new Vector2(0, 1) };
        root.AddChild(rule);
    }

    private void BuildGameplay(VBoxContainer root)
    {
        AddSectionHeader(root, "Gameplay");

        AddSlider(root, "Sensitivity", 0.05, 0.1, 5.0, "Sensitivity", v =>
            SettingsManager.Current.Sensitivity = v, FormatFloat2);

        AddSlider(root, "CameraParallax", 0.005, 0.0, 0.5, "Camera Parallax", v =>
            SettingsManager.Current.CameraParallax = v, FormatFloat3);

        AddSlider(root, "ApproachRate", 0.1, 5.0, 50.0, "Approach Rate", v =>
            SettingsManager.Current.ApproachRate = v, FormatFloat1);

        AddSlider(root, "ApproachDistance", 0.1, 5.0, 50.0, "Approach Distance", v =>
            SettingsManager.Current.ApproachDistance = v, FormatFloat1);

        AddToggle(root, "Pushback", "Note Pushback",
            () => SettingsManager.Current.Pushback,
            v => SettingsManager.Current.Pushback = v);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }

    private void BuildVisual(VBoxContainer root)
    {
        AddSectionHeader(root, "Visual");

        AddSlider(root, "NoteSize", 0.01, 0.25, 1.5, "Note Size", v =>
            SettingsManager.Current.NoteSize = v, FormatFloat2);

        AddSlider(root, "NoteOutlineThickness", 0.01, 0.0, 0.3, "Note Outline Width", v =>
            SettingsManager.Current.NoteOutlineThickness = v, FormatFloat2);

        AddSlider(root, "NoteOpacity", 0.01, 0.0, 1.0, "Note Opacity", v =>
            SettingsManager.Current.NoteOpacity = v, FormatFloat2);

        var colorsetRow = new HBoxContainer();
        colorsetRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(colorsetRow);

        var colorsetLabel = UiStyle.MakeLabel("Note Colorset", 12, UiStyle.TextMuted);
        colorsetLabel.CustomMinimumSize = new Vector2(170, 0);
        colorsetLabel.VerticalAlignment = VerticalAlignment.Center;
        colorsetRow.AddChild(colorsetLabel);

        var colorsetOpt = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
        };
        colorsetOpt.AddThemeFontSizeOverride("font_size", 13);
        PopulateColorsetOptions(colorsetOpt);
        colorsetOpt.ItemSelected += idx =>
        {
            string name = colorsetOpt.GetItemText((int)idx);
            SettingsManager.Current.NoteColorset = name == "Default" ? "" : name;
        };
        colorsetRow.AddChild(colorsetOpt);

        var colorsetFolder = UiStyle.MakeButton("Folder", 12);
        colorsetFolder.CustomMinimumSize = new Vector2(90, 30);
        colorsetFolder.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        colorsetFolder.Pressed += () => OS.ShellOpen(Constants.ColorsetsFolder);
        colorsetRow.AddChild(colorsetFolder);

        var hint = UiStyle.MakeLabel(
            "Drop .txt files with hex colors (e.g. ff0059,ffd8e6) into the Colorsets folder.",
            10, UiStyle.TextMuted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(hint);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }

    private void BuildAudio(VBoxContainer root)
    {
        AddSectionHeader(root, "Audio");

        AddSlider(root, "VolumeMusic", 0.01, 0.0, 1.0, "Music Volume", v =>
            SettingsManager.Current.VolumeMusic = v, FormatFloat2);

        AddSlider(root, "VolumeHitSound", 0.01, 0.0, 1.0, "Hit Sound Volume", v =>
            SettingsManager.Current.VolumeHitSound = v, FormatFloat2);

        // Hit sound file picker. Default is empty = no hit sound.
        var hsRow = new HBoxContainer();
        hsRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(hsRow);

        var hsLabel = UiStyle.MakeLabel("Hit Sound", 12, UiStyle.TextMuted);
        hsLabel.CustomMinimumSize = new Vector2(170, 0);
        hsLabel.VerticalAlignment = VerticalAlignment.Center;
        hsRow.AddChild(hsLabel);

        _hitSoundValue = UiStyle.MakeLabel(
            string.IsNullOrEmpty(SettingsManager.Current.HitSoundFile)
                ? "None"
                : SettingsManager.Current.HitSoundFile,
            12, UiStyle.TextMuted);
        _hitSoundValue.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hitSoundValue.VerticalAlignment = VerticalAlignment.Center;
        _hitSoundValue.ClipText = true;
        hsRow.AddChild(_hitSoundValue);

        var hsChoose = UiStyle.MakeButton("Choose...", 12);
        hsChoose.CustomMinimumSize = new Vector2(90, 30);
        hsChoose.Pressed += () =>
        {
            var dialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                CurrentDir = Constants.SoundsFolder,
                Filters = new[] { "*.mp3,*.ogg,*.wav ; Audio" },
            };
            dialog.FileSelected += path =>
            {
                try
                {
                    string fileName = Path.GetFileName(path);
                    string dest = $"{Constants.SoundsFolder}/{fileName}";
                    if (path != dest) File.Copy(path, dest, true);
                    SettingsManager.Current.HitSoundFile = fileName;
                    _hitSoundValue.Text = fileName;
                }
                catch (Exception ex) { GD.PushError(ex.Message); }
            };
            AddChild(dialog);
            dialog.PopupCentered();
        };
        hsRow.AddChild(hsChoose);

        var hsDefault = UiStyle.MakeButton("None", 12);
        hsDefault.CustomMinimumSize = new Vector2(90, 30);
        hsDefault.Pressed += () =>
        {
            SettingsManager.Current.HitSoundFile = "";
            _hitSoundValue.Text = "None";
        };
        hsRow.AddChild(hsDefault);

        var hsFolder = UiStyle.MakeButton("Folder", 12);
        hsFolder.CustomMinimumSize = new Vector2(90, 30);
        hsFolder.Pressed += () =>
        {
            Directory.CreateDirectory(Constants.SoundsFolder);
            OS.ShellOpen(Constants.SoundsFolder);
        };
        hsRow.AddChild(hsFolder);

        var hint = UiStyle.MakeLabel(
            "Drop .mp3 / .ogg / .wav files into the Sounds folder. Default: no hit sound.",
            10, UiStyle.TextMuted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(hint);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }

    private void BuildCursor(VBoxContainer root)
    {
        AddSectionHeader(root, "Cursor");

        AddSlider(root, "CursorScale", 0.01, 0.25, 3.0, "Cursor Size", v =>
            SettingsManager.Current.CursorScale = v, FormatFloat2);

        AddSlider(root, "CursorOpacity", 0.01, 0.0, 1.0, "Cursor Opacity", v =>
            SettingsManager.Current.CursorOpacity = v, FormatFloat2);

        var cursorRow = new HBoxContainer();
        cursorRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(cursorRow);

        var cursorLabel = UiStyle.MakeLabel("Cursor Image", 12, UiStyle.TextMuted);
        cursorLabel.CustomMinimumSize = new Vector2(170, 0);
        cursorLabel.VerticalAlignment = VerticalAlignment.Center;
        cursorRow.AddChild(cursorLabel);

        var cursorValue = UiStyle.MakeLabel(
            string.IsNullOrEmpty(SettingsManager.Current.CursorFile)
                ? "Default (circle)"
                : SettingsManager.Current.CursorFile,
            12, UiStyle.TextMuted);
        cursorValue.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cursorValue.VerticalAlignment = VerticalAlignment.Center;
        cursorValue.ClipText = true;
        cursorRow.AddChild(cursorValue);

        var choose = UiStyle.MakeButton("Choose...", 12);
        choose.CustomMinimumSize = new Vector2(90, 30);
        choose.Pressed += () =>
        {
            var dialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                CurrentDir = Constants.CursorsFolder,
                Filters = new[] { "*.png,*.jpg,*.jpeg,*.bmp,*.webp ; Images" },
            };
            dialog.FileSelected += path =>
            {
                try
                {
                    string fileName = Path.GetFileName(path);
                    string dest = $"{Constants.CursorsFolder}/{fileName}";
                    if (path != dest) File.Copy(path, dest, true);
                    SettingsManager.Current.CursorFile = fileName;
                    cursorValue.Text = fileName;
                }
                catch (Exception ex) { GD.PushError(ex.Message); }
            };
            AddChild(dialog);
            dialog.PopupCentered();
        };
        cursorRow.AddChild(choose);

        var defaultBtn = UiStyle.MakeButton("Default", 12);
        defaultBtn.CustomMinimumSize = new Vector2(90, 30);
        defaultBtn.Pressed += () =>
        {
            SettingsManager.Current.CursorFile = "";
            cursorValue.Text = "Default (circle)";
        };
        cursorRow.AddChild(defaultBtn);

        var cursorFolder = UiStyle.MakeButton("Folder", 12);
        cursorFolder.CustomMinimumSize = new Vector2(90, 30);
        cursorFolder.Pressed += () =>
        {
            Directory.CreateDirectory(Constants.CursorsFolder);
            OS.ShellOpen(Constants.CursorsFolder);
        };
        cursorRow.AddChild(cursorFolder);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }

    private void BuildPerformance(VBoxContainer root)
    {
        AddSectionHeader(root, "Performance");

        AddToggle(root, "ShowFpsCounter", "Show FPS Counter",
            () => SettingsManager.Current.ShowFpsCounter,
            v => SettingsManager.Current.ShowFpsCounter = v);

        AddToggle(root, "LockFps", "Lock FPS",
            () => SettingsManager.Current.LockFps,
            v => SettingsManager.Current.LockFps = v);

        AddSlider(root, "FpsLimit", 1, 30, 1000, "FPS Limit", v =>
            SettingsManager.Current.FpsLimit = (int)v, v =>
            v.ToString("0", CultureInfo.InvariantCulture));

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }

    private void BuildFooter(VBoxContainer root)
    {
        var rule = new ColorRect { Color = UiStyle.Border, CustomMinimumSize = new Vector2(0, 1) };
        root.AddChild(rule);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        var reset = UiStyle.MakeButton("Reset", 14);
        reset.CustomMinimumSize = new Vector2(160, 44);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        reset.Pressed += () =>
        {
            SettingsManager.Reset();
            RefreshAfterReset();
        };
        footer.AddChild(reset);

        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var save = UiStyle.MakeButton("Save & Close", 14, true);
        save.CustomMinimumSize = new Vector2(200, 44);
        save.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        save.Pressed += OnClosePressed;
        footer.AddChild(save);
    }

    private static void AddSectionHeader(VBoxContainer parent, string text)
    {
        var lbl = UiStyle.MakeLabel(text, 15, UiStyle.Accent);
        parent.AddChild(lbl);
    }

    private void AddSlider(VBoxContainer parent, string key, double step, double min, double max,
        string label, Action<double> onChanged, Func<double, string> format)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        var lbl = UiStyle.MakeLabel(label, 12, UiStyle.TextMuted);
        lbl.CustomMinimumSize = new Vector2(170, 0);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = min,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 22),
            FocusMode = FocusModeEnum.None,
        };
        row.AddChild(slider);

        var valueLbl = UiStyle.MakeLabel("", 12, UiStyle.Accent);
        valueLbl.CustomMinimumSize = new Vector2(70, 0);
        valueLbl.HorizontalAlignment = HorizontalAlignment.Right;
        valueLbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(valueLbl);

        slider.ValueChanged += v =>
        {
            onChanged(v);
            valueLbl.Text = format(v);
        };

        _sliderRefs.Add((key, slider, valueLbl, format));
    }

    private void AddToggle(VBoxContainer parent, string key, string label,
        Func<bool> getter, Action<bool> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        var lbl = UiStyle.MakeLabel(label, 12, UiStyle.TextMuted);
        lbl.CustomMinimumSize = new Vector2(170, 0);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(lbl);

        var toggle = new CheckButton
        {
            ButtonPressed = getter(),
            FocusMode = FocusModeEnum.None,
        };
        row.AddChild(toggle);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        toggle.Toggled += v => setter(v);

        _toggleRefs.Add((key, toggle, setter));
    }

    private void PopulateColorsetOptions(OptionButton opt)
    {
        opt.Clear();
        opt.AddItem("Default");
        if (Directory.Exists(Constants.ColorsetsFolder))
        {
            foreach (string file in Directory.GetFiles(Constants.ColorsetsFolder, "*.txt"))
            {
                opt.AddItem(Path.GetFileNameWithoutExtension(file));
            }
        }

        string current = SettingsManager.Current.NoteColorset;
        if (string.IsNullOrEmpty(current))
        {
            opt.Select(0);
        }
        else
        {
            for (int i = 0; i < opt.ItemCount; i++)
            {
                if (opt.GetItemText(i) == current) { opt.Select(i); break; }
            }
        }
    }

    private void ApplyCurrentValues()
    {
        var s = SettingsManager.Current;

        foreach (var (key, slider, value, format) in _sliderRefs)
        {
            double v = key switch
            {
                "Sensitivity" => s.Sensitivity,
                "CameraParallax" => s.CameraParallax,
                "ApproachRate" => s.ApproachRate,
                "ApproachDistance" => s.ApproachDistance,
                "NoteSize" => s.NoteSize,
                "NoteOutlineThickness" => s.NoteOutlineThickness,
                "NoteOpacity" => s.NoteOpacity,
                "VolumeMusic" => s.VolumeMusic,
                "VolumeHitSound" => s.VolumeHitSound,
                "CursorScale" => s.CursorScale,
                "CursorOpacity" => s.CursorOpacity,
                "FpsLimit" => s.FpsLimit,
                _ => slider.Value,
            };
            slider.Value = v;
            value.Text = format(v);
        }

        foreach (var (key, toggle, _) in _toggleRefs)
        {
            toggle.ButtonPressed = key switch
            {
                "Pushback" => s.Pushback,
                "ShowFpsCounter" => s.ShowFpsCounter,
                "LockFps" => s.LockFps,
                _ => toggle.ButtonPressed,
            };
        }

        if (_hitSoundValue != null)
        {
            _hitSoundValue.Text = string.IsNullOrEmpty(s.HitSoundFile)
                ? "None"
                : s.HitSoundFile;
        }
    }

    private void RefreshAfterReset()
    {
        // The Reset path rebuilds the whole screen so the newly-cleared
        // colorset dropdown and hit-sound label reflect the defaults.
        Main.Instance.ShowSettings();
        QueueFree();
    }

    private static string FormatFloat1(double v) =>
        v.ToString("0.0", CultureInfo.InvariantCulture);

    private static string FormatFloat2(double v) =>
        v.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatFloat3(double v) =>
        v.ToString("0.000", CultureInfo.InvariantCulture);

    private void OnClosePressed()
    {
        SettingsManager.Save();
        Main.Instance.ShowMainMenu();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.PhysicalKeycode == Key.Escape)
        {
            OnClosePressed();
        }
    }
}
