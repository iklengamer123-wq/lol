using System;
using System.Collections.Generic;
using Godot;
using CustomRhythia.Rp;

namespace CustomRhythia.UI;

/// <summary>
/// Account screen. Shows the editable nickname, total RP, the top 20 scores
/// by RP, and the 10 most recent completed passes.
/// </summary>
public partial class ProfileScreen : Control
{
    private LineEdit _nickEdit;
    private Label _totalRpLabel;
    private VBoxContainer _bestList;
    private VBoxContainer _recentList;

    private const int TopCount = 20;
    private const int RecentCount = 10;
    private const string RankPrefix = "No. ";

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
        card.OffsetLeft = -400;
        card.OffsetRight = 400;
        card.OffsetTop = -340;
        card.OffsetBottom = 340;
        AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        card.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        BuildHeader(root);
        BuildLists(root);
        BuildFooter(root);

        RefreshLists();
    }

    private void BuildHeader(VBoxContainer root)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 14);
        root.AddChild(header);

        var nickCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nickCol.AddThemeConstantOverride("separation", 4);
        header.AddChild(nickCol);

        var nickLabel = UiStyle.MakeLabel("Nickname", 11, UiStyle.TextMuted);
        nickCol.AddChild(nickLabel);

        _nickEdit = new LineEdit();
        _nickEdit.Text = PlayerProfile.Data.Nickname;
        _nickEdit.CustomMinimumSize = new Vector2(0, 34);
        _nickEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nickEdit.AddThemeFontSizeOverride("font_size", 16);
        _nickEdit.TextSubmitted += OnNicknameSubmitted;
        _nickEdit.FocusExited += CommitNickname;
        nickCol.AddChild(_nickEdit);

        var rpCol = new VBoxContainer();
        rpCol.AddThemeConstantOverride("separation", 4);
        header.AddChild(rpCol);

        var rpLabel = UiStyle.MakeLabel("Total RP", 11, UiStyle.TextMuted);
        rpLabel.HorizontalAlignment = HorizontalAlignment.Right;
        rpCol.AddChild(rpLabel);

        _totalRpLabel = UiStyle.MakeLabel("0", 30, UiStyle.Accent);
        _totalRpLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _totalRpLabel.CustomMinimumSize = new Vector2(160, 0);
        rpCol.AddChild(_totalRpLabel);

        var rule = new ColorRect();
        rule.Color = UiStyle.Border;
        rule.CustomMinimumSize = new Vector2(0, 1);
        root.AddChild(rule);
    }

    private void BuildLists(VBoxContainer root)
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(content);

        content.AddChild(UiStyle.MakeLabel("Best Scores", 15, UiStyle.Accent));
        content.AddChild(UiStyle.MakeLabel("Top " + TopCount + " by RP", 10, UiStyle.TextMuted));

        _bestList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bestList.AddThemeConstantOverride("separation", 2);
        content.AddChild(_bestList);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 12);
        content.AddChild(spacer);

        content.AddChild(UiStyle.MakeLabel("Recent Scores", 15, UiStyle.Accent));
        content.AddChild(UiStyle.MakeLabel("Last " + RecentCount + " passes", 10, UiStyle.TextMuted));

        _recentList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _recentList.AddThemeConstantOverride("separation", 2);
        content.AddChild(_recentList);
    }

    private void BuildFooter(VBoxContainer root)
    {
        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.End;
        root.AddChild(footer);

        var close = UiStyle.MakeButton("Close", 14, true);
        close.CustomMinimumSize = new Vector2(160, 42);
        close.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        close.Pressed += OnClosePressed;
        footer.AddChild(close);
    }

    private void OnClosePressed()
    {
        CommitNickname();
        Main.Instance.ShowMainMenu();
    }

    private void OnNicknameSubmitted(string text)
    {
        CommitNickname();
    }

    private void CommitNickname()
    {
        if (_nickEdit == null) return;
        string txt = _nickEdit.Text.Trim();
        if (string.IsNullOrEmpty(txt)) return;
        if (txt == PlayerProfile.Data.Nickname) return;
        PlayerProfile.SetNickname(txt);
    }

    private void RefreshLists()
    {
        _totalRpLabel.Text = PlayerProfile.Data.TotalRP.ToString(
            "N0", System.Globalization.CultureInfo.InvariantCulture);

        ClearChildren(_bestList);
        ClearChildren(_recentList);

        FillBestScores();
        FillRecentScores();
    }

    private void FillBestScores()
    {
        var best = PlayerProfile.TopScores(TopCount);
        if (best.Count == 0)
        {
            _bestList.AddChild(MakeEmptyLabel("No scored attempts yet."));
            return;
        }

        for (int i = 0; i < best.Count; i++)
        {
            _bestList.AddChild(MakeScoreRow(i + 1, best[i], true));
        }
    }

    private void FillRecentScores()
    {
        var recent = PlayerProfile.RecentScores(RecentCount);
        if (recent.Count == 0)
        {
            _recentList.AddChild(MakeEmptyLabel("No completed passes yet."));
            return;
        }

        for (int i = 0; i < recent.Count; i++)
        {
            _recentList.AddChild(MakeScoreRow(0, recent[i], false));
        }
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static Label MakeEmptyLabel(string text)
    {
        var lbl = UiStyle.MakeLabel(text, 12, UiStyle.TextMuted);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.CustomMinimumSize = new Vector2(0, 40);
        return lbl;
    }

    /// <summary>
    /// Score row. Uses a PanelContainer (a real Container) so the HBox
    /// inside gets the full row width and the ExpandFill title can shrink
    /// without pushing the fixed right-hand columns on top of each other.
    /// </summary>
    private static PanelContainer MakeScoreRow(int rank, RpScoreEntry s, bool showRank)
    {
        var row = new PanelContainer();
        var sb = UiStyle.PanelStyle(UiStyle.SurfaceHi, UiStyle.Border, 6);
        sb.ContentMarginLeft = 10;
        sb.ContentMarginRight = 10;
        sb.ContentMarginTop = 6;
        sb.ContentMarginBottom = 6;
        row.AddThemeStyleboxOverride("panel", sb);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        row.AddChild(hbox);

        if (showRank)
        {
            hbox.AddChild(MakeRankLabel(rank));
        }

        hbox.AddChild(MakeTitleLabel(s.MapTitle));

        string speedStr = s.Speed.ToString("0.##",
            System.Globalization.CultureInfo.InvariantCulture) + "x";
        hbox.AddChild(MakeRightLabel(speedStr, 11, UiStyle.TextMuted, 50));

        string accStr = s.Accuracy.ToString("F2",
            System.Globalization.CultureInfo.InvariantCulture) + "%";
        hbox.AddChild(MakeRightLabel(accStr, 11, UiStyle.TextMuted, 66));

        Color missColor = s.Misses == 0 ? UiStyle.Success : new Color("ff8d6d");
        string missStr = s.Misses + " miss";
        hbox.AddChild(MakeRightLabel(missStr, 11, missColor, 60));

        hbox.AddChild(MakeGradeLabel(s.Grade));

        string rpStr = s.RP.ToString("F0",
            System.Globalization.CultureInfo.InvariantCulture) + " RP";
        hbox.AddChild(MakeRightLabel(rpStr, 13, UiStyle.Accent, 80));

        if (!showRank)
        {
            hbox.AddChild(MakeRightLabel(FormatAgo(s.Timestamp), 11, UiStyle.TextMuted, 80));
        }

        return row;
    }

    private static Label MakeRankLabel(int rank)
    {
        var lbl = UiStyle.MakeLabel(RankPrefix + rank, 12, UiStyle.TextMuted);
        lbl.CustomMinimumSize = new Vector2(40, 0);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        return lbl;
    }

    private static Label MakeTitleLabel(string title)
    {
        var lbl = UiStyle.MakeLabel(title, 13, UiStyle.TextPrimary);
        lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lbl.VerticalAlignment = VerticalAlignment.Center;
        lbl.ClipText = true;
        return lbl;
    }

    private static Label MakeRightLabel(string text, int fontSize, Color color, int width)
    {
        var lbl = UiStyle.MakeLabel(text, fontSize, color);
        lbl.CustomMinimumSize = new Vector2(width, 0);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        return lbl;
    }

    private static Label MakeGradeLabel(string grade)
    {
        var lbl = UiStyle.MakeLabel(grade, 15, GradeColor(grade));
        lbl.CustomMinimumSize = new Vector2(34, 0);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        return lbl;
    }

    private static string FormatAgo(double unixSeconds)
    {
        double now = Time.GetUnixTimeFromSystem();
        double delta = now - unixSeconds;
        if (delta < 0) delta = 0;

        if (delta < 60) return ((int)delta) + "s ago";
        if (delta < 3600) return ((int)(delta / 60)) + "m ago";
        if (delta < 86400) return ((int)(delta / 3600)) + "h ago";
        return ((int)(delta / 86400)) + "d ago";
    }

    private static Color GradeColor(string grade)
    {
        switch (grade)
        {
            case "SS": return new Color("ffd700");
            case "S": return new Color("8fe4ff");
            case "A": return new Color("6ee7a8");
            case "B": return new Color("8fbcff");
            case "C": return new Color("ffd166");
            case "D": return new Color("ff9d33");
            default: return new Color("ff4d6d");
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!(@event is InputEventKey key)) return;
        if (!key.Pressed || key.Echo) return;
        if (key.PhysicalKeycode != Key.Escape) return;

        if (GetViewport().GuiGetFocusOwner() is LineEdit)
        {
            CommitNickname();
            GetViewport().GuiReleaseFocus();
            return;
        }

        CommitNickname();
        Main.Instance.ShowMainMenu();
    }
}
