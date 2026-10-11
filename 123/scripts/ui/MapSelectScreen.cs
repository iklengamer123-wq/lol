using System;
using System.Collections.Generic;
using Godot;
using CustomRhythia.Map;
using CustomRhythia.Rp;
using CustomRhythia.Util;

namespace CustomRhythia.UI;

// Map picker. Each row shows: (optional heart) then a square thumbnail
// then the map title. Selecting a row highlights it, no animation.
// Modifiers: NoFail, SuddenDeath, AutoPlay. NoFail and SuddenDeath are
// mutually exclusive.
public partial class MapSelectScreen : Control
{
	public static readonly (string Tag, double Value)[] SpeedPresets =
	{
		("S----", 0.65),
		("S---",  0.75),
		("S--",   0.80),
		("S-",    0.87),
		("NM",    1.00),
		("S+",    1.15),
		("S++",   1.25),
		("S+++",  1.35),
		("S++++", 1.45),
	};

	public Map.Map InitialSelection { get; set; }

	private const float LeftPanelWidth = 560f;

	private const int ThumbSize = 48;
	private const int ThumbRenderSize = 96;
	private const int ThumbPadding = 8;

	private const int BigCoverRenderSize = 284;

	private const int HeartSize = 26;
	private const float ThumbAlphaIdle = 0.55f;
	private const float ThumbAlphaSelected = 1.0f;

	private VBoxContainer _list;
	private LineEdit _search;
	private Label _infoTitle;
	private Label _infoSub;
	private Label _infoLen;
	private TextureRect _cover;
	private Button _playButton;
	private Map.Map _selected;

	private HSlider _startSlider;
	private LineEdit _startEdit;
	private Label _startFromValue;
	private AudioStreamPlayer _preview;
	private double _startFromMs = 0;
	private bool _updatingStartUi = false;

	private Label _speedValueLabel;
	private LineEdit _customSpeedEdit;
	private bool _updatingSpeedUi = false;

	private Button _noFailButton;
	private Button _suddenDeathButton;
	private Button _autoPlayButton;

	private Label _rpPotentialLabel;
	private Label _rpBestLabel;

	private List<Map.Map> _visibleMaps = new();

	private FileDialog _dialog;
	private ConfirmationDialog _confirm;
	private PopupMenu _contextMenu;
	private Map.Map _contextMap;

	private readonly List<Button> _speedButtons = new();
	private readonly Dictionary<string, ImageTexture> _coverCache = new();

	private string _lastCoverFor = null;
	private readonly Random _rng = new();

	private VBoxContainer _passBlock;
	private Label _currentPassGrade;
	private Label _currentPassStatus;
	private Label _currentPassStats;

	private VBoxContainer _bestBlock;
	private Label _bestAttemptGrade;
	private Label _bestAttemptStatus;
	private Label _bestAttemptStats;

	private Label _noPassLabel;

	public override void _Ready()
	{
		ScreenUtil.MakeFullScreen(this);

		AddChild(UiStyle.FullScreen(UiStyle.MakeBackground()));

		_preview = new AudioStreamPlayer { Bus = "Master" };
		_preview.VolumeDb = ComputePreviewDb();
		AddChild(_preview);

		BuildTopBar();
		BuildSplit();
		BuildContextMenu();
		BuildFileDialogs();

		MapManager.MapsChanged += RefreshList;
		RefreshList();

		ApplyInitialSelection();
	}

	public override void _ExitTree()
	{
		MapManager.MapsChanged -= RefreshList;
		if (_preview != null) _preview.Stop();
	}

	private void ApplyInitialSelection()
	{
		if (InitialSelection == null) return;

		Map.Map target = null;
		foreach (var m in MapManager.Maps)
		{
			if (m.FolderPath == InitialSelection.FolderPath)
			{
				target = m;
				break;
			}
		}
		if (target == null) target = InitialSelection;

		SelectMap(target);
	}

	private static float ComputePreviewDb()
	{
		double vol = SettingsManager.Current.VolumeMusic;
		if (vol <= 0) return -80f;
		return (float)(-80 + 70 * Math.Pow(vol / 100.0, 0.1));
	}

	private void BuildTopBar()
	{
		Panel topBar = UiStyle.MakePanel(UiStyle.Bg, UiStyle.Border);
		topBar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		topBar.OffsetBottom = 72;
		AddChild(topBar);

		HBoxContainer box = new HBoxContainer();
		box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		box.OffsetLeft = 20;
		box.OffsetRight = -20;
		box.OffsetTop = 12;
		box.OffsetBottom = -12;
		box.AddThemeConstantOverride("separation", 10);
		topBar.AddChild(box);

		Button back = UiStyle.MakeButton("Back", 15);
		back.CustomMinimumSize = new Vector2(110, 44);
		back.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		back.Pressed += OnBackPressed;
		box.AddChild(back);

		Label heading = UiStyle.MakeLabel("Map Select", 22);
		heading.VerticalAlignment = VerticalAlignment.Center;
		box.AddChild(heading);

		_search = new LineEdit
		{
			PlaceholderText = "Search maps...",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 44),
			ClearButtonEnabled = true,
		};
		_search.AddThemeFontSizeOverride("font_size", 15);
		_search.TextChanged += OnSearchChanged;
		box.AddChild(_search);

		Button random = UiStyle.MakeButton("Random", 15);
		random.CustomMinimumSize = new Vector2(120, 44);
		random.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		random.TooltipText = "Pick a random map (F2)";
		random.Pressed += PickRandom;
		box.AddChild(random);

		Button import = UiStyle.MakeButton("Import", 15, true);
		import.CustomMinimumSize = new Vector2(120, 44);
		import.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		import.Pressed += OnImportPressed;
		box.AddChild(import);

		Button refresh = UiStyle.MakeButton("Refresh", 15);
		refresh.CustomMinimumSize = new Vector2(120, 44);
		refresh.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		refresh.Pressed += OnRefreshPressed;
		box.AddChild(refresh);
	}

	private void BuildSplit()
	{
		HBoxContainer split = new HBoxContainer();
		split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		split.OffsetTop = 88;
		split.OffsetLeft = 16;
		split.OffsetRight = -16;
		split.OffsetBottom = -16;
		split.AddThemeConstantOverride("separation", 12);
		AddChild(split);

		Panel listCard = UiStyle.MakePanel(UiStyle.Surface);
		listCard.CustomMinimumSize = new Vector2(LeftPanelWidth, 0);
		listCard.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		split.AddChild(listCard);

		ScrollContainer listScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		listScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		listScroll.OffsetLeft = 10;
		listScroll.OffsetRight = -10;
		listScroll.OffsetTop = 10;
		listScroll.OffsetBottom = -10;
		listCard.AddChild(listScroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 4);
		listScroll.AddChild(_list);

		Panel info = UiStyle.MakePanel(UiStyle.Surface);
		info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		split.AddChild(info);

		ScrollContainer infoScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		infoScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		infoScroll.OffsetLeft = 22;
		infoScroll.OffsetRight = -22;
		infoScroll.OffsetTop = 22;
		infoScroll.OffsetBottom = -22;
		info.AddChild(infoScroll);

		VBoxContainer infoBox = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		infoBox.AddThemeConstantOverride("separation", 10);
		infoScroll.AddChild(infoBox);

		BuildCoverSection(infoBox);
		BuildInfoHeader(infoBox);
		BuildRpInfoBlock(infoBox);
		BuildStartFromSection(infoBox);
		BuildSpeedSection(infoBox);
		BuildModifiersSection(infoBox);
		BuildPassStatsSection(infoBox);

		infoBox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

		_playButton = UiStyle.MakeButton("PLAY", 18, true);
		_playButton.CustomMinimumSize = new Vector2(0, 52);
		_playButton.Disabled = true;
		_playButton.Pressed += PlaySelected;
		infoBox.AddChild(_playButton);
	}

	private void BuildCoverSection(VBoxContainer infoBox)
	{
		CenterContainer coverCenter = new CenterContainer { CustomMinimumSize = new Vector2(0, 160) };
		infoBox.AddChild(coverCenter);

		Panel coverFrame = UiStyle.MakePanel(UiStyle.BgDeep, UiStyle.Border);
		coverFrame.CustomMinimumSize = new Vector2(150, 150);
		coverCenter.AddChild(coverFrame);

		_cover = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_cover.OffsetLeft = 4; _cover.OffsetRight = -4;
		_cover.OffsetTop = 4; _cover.OffsetBottom = -4;
		coverFrame.AddChild(_cover);
	}

	private void BuildInfoHeader(VBoxContainer infoBox)
	{
		_infoTitle = UiStyle.MakeLabel("No map selected", 20);
		_infoTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_infoTitle.HorizontalAlignment = HorizontalAlignment.Center;
		infoBox.AddChild(_infoTitle);

		ColorRect accentLine = new ColorRect
		{
			Color = UiStyle.Accent,
			CustomMinimumSize = new Vector2(48, 2),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		infoBox.AddChild(accentLine);

		_infoSub = UiStyle.MakeLabel("", 13, UiStyle.TextMuted);
		_infoSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_infoSub.HorizontalAlignment = HorizontalAlignment.Center;
		infoBox.AddChild(_infoSub);

		_infoLen = UiStyle.MakeLabel("", 12, UiStyle.TextMuted);
		_infoLen.HorizontalAlignment = HorizontalAlignment.Center;
		infoBox.AddChild(_infoLen);
	}

	private void BuildRpInfoBlock(VBoxContainer infoBox)
	{
		var rpBlock = new PanelContainer();
		var rpSb = UiStyle.PanelStyle(UiStyle.SurfaceHi, UiStyle.Border, 8);
		rpSb.ContentMarginLeft = 14; rpSb.ContentMarginRight = 14;
		rpSb.ContentMarginTop = 10; rpSb.ContentMarginBottom = 10;
		rpBlock.AddThemeStyleboxOverride("panel", rpSb);
		infoBox.AddChild(rpBlock);

		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 20);
		grid.AddThemeConstantOverride("v_separation", 4);
		rpBlock.AddChild(grid);

		var potTag = UiStyle.MakeLabel("Max RP at this speed", 11, UiStyle.TextMuted);
		potTag.VerticalAlignment = VerticalAlignment.Center;
		grid.AddChild(potTag);

		_rpPotentialLabel = UiStyle.MakeLabel("-", 18, UiStyle.Accent);
		_rpPotentialLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_rpPotentialLabel.VerticalAlignment = VerticalAlignment.Center;
		_rpPotentialLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		grid.AddChild(_rpPotentialLabel);

		var bestTag = UiStyle.MakeLabel("Your best for this map", 11, UiStyle.TextMuted);
		bestTag.VerticalAlignment = VerticalAlignment.Center;
		grid.AddChild(bestTag);

		_rpBestLabel = UiStyle.MakeLabel("-", 18, UiStyle.TextPrimary);
		_rpBestLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_rpBestLabel.VerticalAlignment = VerticalAlignment.Center;
		_rpBestLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		grid.AddChild(_rpBestLabel);
	}

	private void BuildStartFromSection(VBoxContainer infoBox)
	{
		var startLabel = UiStyle.MakeLabel("Start From", 12, UiStyle.TextMuted);
		startLabel.HorizontalAlignment = HorizontalAlignment.Center;
		infoBox.AddChild(startLabel);

		var startRow = new HBoxContainer();
		startRow.AddThemeConstantOverride("separation", 8);
		infoBox.AddChild(startRow);

		_startSlider = new HSlider
		{
			MinValue = 0,
			MaxValue = 1,
			Step = 100,
			Value = 0,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 24),
			FocusMode = FocusModeEnum.None,
		};
		startRow.AddChild(_startSlider);

		_startEdit = new LineEdit
		{
			Text = "0:00",
			CustomMinimumSize = new Vector2(72, 30),
			Alignment = HorizontalAlignment.Center,
		};
		startRow.AddChild(_startEdit);

		_startFromValue = UiStyle.MakeLabel("", 11, UiStyle.TextMuted);
		_startFromValue.HorizontalAlignment = HorizontalAlignment.Center;
		infoBox.AddChild(_startFromValue);

		_startSlider.ValueChanged += OnStartFromSliderChanged;
		_startEdit.TextSubmitted += OnStartFromSubmitted;
		_startEdit.FocusExited += CommitStartFromEdit;
	}

	private void BuildSpeedSection(VBoxContainer infoBox)
	{
		var speedHeader = new HBoxContainer();
		speedHeader.AddThemeConstantOverride("separation", 8);
		infoBox.AddChild(speedHeader);

		var speedLabel = UiStyle.MakeLabel("Speed", 12, UiStyle.TextMuted);
		speedLabel.VerticalAlignment = VerticalAlignment.Center;
		speedHeader.AddChild(speedLabel);

		_speedValueLabel = UiStyle.MakeLabel("", 12, UiStyle.Accent);
		_speedValueLabel.VerticalAlignment = VerticalAlignment.Center;
		_speedValueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		speedHeader.AddChild(_speedValueLabel);

		var customLbl = UiStyle.MakeLabel("Custom", 11, UiStyle.TextMuted);
		customLbl.VerticalAlignment = VerticalAlignment.Center;
		speedHeader.AddChild(customLbl);

		_customSpeedEdit = new LineEdit
		{
			CustomMinimumSize = new Vector2(70, 26),
			Alignment = HorizontalAlignment.Center,
		};
		_customSpeedEdit.AddThemeFontSizeOverride("font_size", 12);
		_customSpeedEdit.TextSubmitted += OnCustomSpeedSubmitted;
		_customSpeedEdit.FocusExited += CommitCustomSpeed;
		speedHeader.AddChild(_customSpeedEdit);

		var speedRow = new HBoxContainer();
		speedRow.AddThemeConstantOverride("separation", 3);
		infoBox.AddChild(speedRow);

		ButtonGroup group = new ButtonGroup();
		for (int i = 0; i < SpeedPresets.Length; i++)
		{
			Button b = MakeSpeedButton(SpeedPresets[i].Tag, SpeedPresets[i].Value, group);
			speedRow.AddChild(b);
			_speedButtons.Add(b);
		}
	}

	private void BuildModifiersSection(VBoxContainer infoBox)
	{
		var modsRow = new HBoxContainer();
		modsRow.AddThemeConstantOverride("separation", 8);
		infoBox.AddChild(modsRow);

		var modsLbl = UiStyle.MakeLabel("Modifiers", 12, UiStyle.TextMuted);
		modsLbl.VerticalAlignment = VerticalAlignment.Center;
		modsLbl.CustomMinimumSize = new Vector2(70, 0);
		modsRow.AddChild(modsLbl);

		_noFailButton = BuildModToggle("NoFail",
			"NoFail - misses don't cost health. RP is not awarded.",
			Main.NoFailEnabled);
		_noFailButton.Pressed += OnNoFailPressed;
		modsRow.AddChild(_noFailButton);

		_suddenDeathButton = BuildModToggle("SuddenDeath",
			"SuddenDeath - one miss ends the run instantly. Cannot be combined with NoFail.",
			Main.SuddenDeathEnabled);
		_suddenDeathButton.Pressed += OnSuddenDeathPressed;
		modsRow.AddChild(_suddenDeathButton);

		_autoPlayButton = BuildModToggle("AutoPlay",
			"AutoPlay - the cursor plays the map by itself. RP is not awarded.",
			Main.AutoPlayEnabled);
		_autoPlayButton.Pressed += OnAutoPlayPressed;
		modsRow.AddChild(_autoPlayButton);

		modsRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		UpdateSpeedButtons();
		UpdateModifierVisuals();
	}

	private Button BuildModToggle(string label, string tooltip, bool initialState)
	{
		var b = new Button
		{
			Text = label,
			ToggleMode = true,
			CustomMinimumSize = new Vector2(96, 30),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ButtonPressed = initialState,
			TooltipText = tooltip,
		};
		b.AddThemeFontSizeOverride("font_size", 11);
		return b;
	}

	private void OnNoFailPressed()
	{
		Main.NoFailEnabled = _noFailButton.ButtonPressed;

		if (Main.NoFailEnabled && Main.SuddenDeathEnabled)
		{
			Main.SuddenDeathEnabled = false;
			_suddenDeathButton.ButtonPressed = false;
		}

		UpdateModifierVisuals();
	}

	private void OnSuddenDeathPressed()
	{
		Main.SuddenDeathEnabled = _suddenDeathButton.ButtonPressed;

		if (Main.SuddenDeathEnabled && Main.NoFailEnabled)
		{
			Main.NoFailEnabled = false;
			_noFailButton.ButtonPressed = false;
		}

		UpdateModifierVisuals();
	}

	private void OnAutoPlayPressed()
	{
		Main.AutoPlayEnabled = _autoPlayButton.ButtonPressed;
		UpdateModifierVisuals();
	}

	private static StyleBoxFlat MakeModBox(Color bg, Color border, bool glow)
	{
		var sb = UiStyle.PanelStyle(bg, border, 8);
		sb.ContentMarginLeft = 10;
		sb.ContentMarginRight = 10;
		sb.ContentMarginTop = 6;
		sb.ContentMarginBottom = 6;

		if (glow)
		{
			Color accent = UiStyle.Accent;
			sb.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.85f);
			sb.ShadowSize = 6;
			sb.ShadowOffset = Vector2.Zero;
		}

		return sb;
	}

	private static void ApplyModStyle(Button b)
	{
		if (b == null) return;

		bool on = b.ButtonPressed;
		Color accent = UiStyle.Accent;
		Color offSurface = UiStyle.SurfaceHi;
		Color offBorder = UiStyle.Border;

		if (on)
		{
			b.AddThemeStyleboxOverride("normal", MakeModBox(accent, accent.Lightened(0.15f), true));
			b.AddThemeStyleboxOverride("hover", MakeModBox(accent.Lightened(0.08f), accent.Lightened(0.2f), true));
			b.AddThemeStyleboxOverride("pressed", MakeModBox(accent.Darkened(0.15f), accent.Darkened(0.15f), true));
			b.AddThemeStyleboxOverride("focus", MakeModBox(accent.Lightened(0.08f), accent.Lightened(0.2f), true));
			b.AddThemeColorOverride("font_color", Colors.White);
			b.AddThemeColorOverride("font_hover_color", Colors.White);
			b.AddThemeColorOverride("font_pressed_color", Colors.White);
		}
		else
		{
			b.AddThemeStyleboxOverride("normal", MakeModBox(offSurface, offBorder, false));
			b.AddThemeStyleboxOverride("hover", MakeModBox(offSurface.Lightened(0.06f), accent, false));
			b.AddThemeStyleboxOverride("pressed", MakeModBox(offSurface.Darkened(0.1f), accent, false));
			b.AddThemeStyleboxOverride("focus", MakeModBox(offSurface.Lightened(0.06f), accent, false));
			b.AddThemeColorOverride("font_color", UiStyle.TextMuted);
			b.AddThemeColorOverride("font_hover_color", UiStyle.TextPrimary);
			b.AddThemeColorOverride("font_pressed_color", UiStyle.TextPrimary);
		}
	}

	private void UpdateModifierVisuals()
	{
		ApplyModStyle(_noFailButton);
		ApplyModStyle(_suddenDeathButton);
		ApplyModStyle(_autoPlayButton);

		RefreshRpInfo();
	}

	private void BuildPassStatsSection(VBoxContainer infoBox)
	{
		_noPassLabel = UiStyle.MakeLabel("No attempts at this speed yet.", 12, UiStyle.TextMuted);
		_noPassLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_noPassLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_noPassLabel.Visible = false;
		infoBox.AddChild(_noPassLabel);

		var statsRow = new HBoxContainer();
		statsRow.AddThemeConstantOverride("separation", 20);
		infoBox.AddChild(statsRow);

		_passBlock = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_passBlock.AddThemeConstantOverride("separation", 2);
		_passBlock.Visible = false;
		statsRow.AddChild(_passBlock);

		var lastTryHeader = UiStyle.MakeLabel("Last Try", 11, UiStyle.Accent);
		_passBlock.AddChild(lastTryHeader);

		var passStatusRow = new HBoxContainer();
		passStatusRow.AddThemeConstantOverride("separation", 6);
		_passBlock.AddChild(passStatusRow);

		_currentPassGrade = UiStyle.MakeLabel("", 18, UiStyle.TextPrimary);
		passStatusRow.AddChild(_currentPassGrade);

		passStatusRow.AddChild(UiStyle.MakeLabel("-", 18, UiStyle.TextMuted));

		_currentPassStatus = UiStyle.MakeLabel("", 18, UiStyle.TextPrimary);
		passStatusRow.AddChild(_currentPassStatus);

		_currentPassStats = UiStyle.MakeLabel("", 11);
		_currentPassStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_passBlock.AddChild(_currentPassStats);

		_bestBlock = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_bestBlock.AddThemeConstantOverride("separation", 2);
		_bestBlock.Visible = false;
		statsRow.AddChild(_bestBlock);

		var bestHeader = UiStyle.MakeLabel("Best Attempt", 11, UiStyle.Success);
		_bestBlock.AddChild(bestHeader);

		var bestStatusRow = new HBoxContainer();
		bestStatusRow
