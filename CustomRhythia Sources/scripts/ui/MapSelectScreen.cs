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
		bestStatusRow.AddThemeConstantOverride("separation", 6);
		_bestBlock.AddChild(bestStatusRow);

		_bestAttemptGrade = UiStyle.MakeLabel("", 18, UiStyle.TextPrimary);
		bestStatusRow.AddChild(_bestAttemptGrade);

		bestStatusRow.AddChild(UiStyle.MakeLabel("-", 18, UiStyle.TextMuted));

		_bestAttemptStatus = UiStyle.MakeLabel("", 18, UiStyle.TextPrimary);
		bestStatusRow.AddChild(_bestAttemptStatus);

		_bestAttemptStats = UiStyle.MakeLabel("", 11);
		_bestAttemptStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_bestBlock.AddChild(_bestAttemptStats);
	}

	private void BuildContextMenu()
	{
		_contextMenu = new PopupMenu();
		_contextMenu.AddItem("Toggle Favorite", 0);
		_contextMenu.AddItem("Delete Map", 1);
		_contextMenu.IdPressed += OnContextMenuAction;
		AddChild(_contextMenu);
	}

	private void BuildFileDialogs()
	{
		_dialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFiles,
			Access = FileDialog.AccessEnum.Filesystem,
			Title = "Import Maps",
			Filters = new[] { "*.sspm ; Sound Space Plus Map" },
			UseNativeDialog = true,
		};
		_dialog.FilesSelected += OnFilesSelected;
		AddChild(_dialog);

		_confirm = new ConfirmationDialog
		{
			Title = "Delete Map",
			DialogText = "Delete this map permanently?",
		};
		_confirm.Confirmed += OnDeleteConfirmed;
		AddChild(_confirm);
	}

	private void OnContextMenuAction(long id)
	{
		if (_contextMap == null) return;

		if (id == 0)
		{
			MapManager.ToggleFavorite(_contextMap);
		}
		else if (id == 1)
		{
			_confirm.DialogText = $"Delete \"{_contextMap.PrettyTitle}\" permanently?";
			_confirm.PopupCentered();
		}
	}

	private void OnDeleteConfirmed()
	{
		if (_contextMap == null) return;
		MapManager.Delete(_contextMap);
		_contextMap = null;
	}

	private void RefreshList()
	{
		foreach (Node child in _list.GetChildren()) child.QueueFree();
		_visibleMaps.Clear();

		string query = _search?.Text?.Trim()?.ToLowerInvariant() ?? "";

		foreach (var m in MapManager.Maps)
		{
			if (query.Length > 0)
			{
				string haystack = (m.PrettyTitle + " " + m.PrettyMappers + " " + m.DifficultyName)
					.ToLowerInvariant();
				if (!haystack.Contains(query)) continue;
			}

			_visibleMaps.Add(m);
		}

		if (_visibleMaps.Count == 0)
		{
			var empty = UiStyle.MakeLabel(
				MapManager.Maps.Count == 0
					? "No maps imported yet. Drop .sspm files or click Import."
					: "No maps match your search.",
				13, UiStyle.TextMuted);
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			empty.CustomMinimumSize = new Vector2(0, 80);
			_list.AddChild(empty);
			return;
		}

		foreach (var m in _visibleMaps)
		{
			_list.AddChild(MakeMapRow(m));
		}
	}

	private Control MakeMapRow(Map.Map map)
	{
		Button row = new Button
		{
			CustomMinimumSize = new Vector2(0, ThumbSize + ThumbPadding * 2),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			Text = "",
			ClipText = true,
		};
		UiStyle.StyleListButton(row);

		HBoxContainer hbox = new HBoxContainer();
		hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		hbox.OffsetLeft = 10;
		hbox.OffsetRight = -10;
		hbox.AddThemeConstantOverride("separation", 8);
		hbox.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(hbox);

		if (map.Favorite)
		{
			var heart = new Label
			{
				Text = "\u2665",
				MouseFilter = MouseFilterEnum.Ignore,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			heart.AddThemeFontSizeOverride("font_size", HeartSize);
			heart.AddThemeColorOverride("font_color", new Color("ff4466"));
			heart.CustomMinimumSize = new Vector2(HeartSize + 4, 0);
			hbox.AddChild(heart);
		}

		Panel thumbFrame = UiStyle.MakePanel(UiStyle.BgDeep, UiStyle.Border, 6);
		thumbFrame.CustomMinimumSize = new Vector2(ThumbSize, ThumbSize);
		thumbFrame.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		thumbFrame.MouseFilter = MouseFilterEnum.Ignore;
		hbox.AddChild(thumbFrame);

		TextureRect thumb = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, ThumbAlphaIdle),
		};
		thumb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		thumb.OffsetLeft = 2; thumb.OffsetRight = -2;
		thumb.OffsetTop = 2; thumb.OffsetBottom = -2;
		thumbFrame.AddChild(thumb);

		thumb.Texture = GetCover(map, ThumbRenderSize);

		VBoxContainer textCol = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		textCol.AddThemeConstantOverride("separation", 1);
		hbox.AddChild(textCol);

		var titleLbl = UiStyle.MakeLabel(map.PrettyTitle, 14, UiStyle.TextPrimary);
		titleLbl.ClipText = true;
		titleLbl.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(titleLbl);

		string sub = map.DifficultyName + "  |  " + map.Notes.Length + " notes";
		var subLbl = UiStyle.MakeLabel(sub, 10, UiStyle.TextMuted);
		subLbl.ClipText = true;
		subLbl.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(subLbl);

		row.GuiInput += (@event) =>
		{
			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.Left)
				{
					SelectMap(map);
				}
				else if (mb.ButtonIndex == MouseButton.Right)
				{
					_contextMap = map;
					_contextMenu.Position = (Vector2I)GetGlobalMousePosition();
					_contextMenu.Popup();
				}
			}
		};

		row.SetMeta("map_ref", map.FolderPath);
		return row;
	}

	private ImageTexture GetCover(Map.Map map, int size)
	{
		string key = map.FolderPath + "|" + size;
		if (_coverCache.TryGetValue(key, out var cached)) return cached;

		if (!map.HasCover) return null;

		Image img = Images.LoadFromFile(map.CoverPath);
		if (img == null) return null;

		int w = img.GetWidth();
		int h = img.GetHeight();
		if (w <= 0 || h <= 0) return null;

		int s = Mathf.Min(w, h);
		int cx = (w - s) / 2;
		int cy = (h - s) / 2;
		Image cropped = (w == h) ? img : img.GetRegion(new Rect2I(cx, cy, s, s));

		if (cropped.GetWidth() != size || cropped.GetHeight() != size)
		{
			cropped.Resize(size, size, Image.Interpolation.Bilinear);
		}

		var tex = ImageTexture.CreateFromImage(cropped);
		_coverCache[key] = tex;
		return tex;
	}

	private void SelectMap(Map.Map map)
	{
		if (map == null) return;
		_selected = map;

		foreach (Node child in _list.GetChildren())
		{
			if (child is Button row && row.HasMeta("map_ref"))
			{
				bool isSelected = (string)row.GetMeta("map_ref") == map.FolderPath;
				if (row.GetChildCount() > 0 && row.GetChild(0) is HBoxContainer hb
					&& hb.GetChildCount() > 0)
				{
					for (int i = 0; i < hb.GetChildCount(); i++)
					{
						if (hb.GetChild(i) is Panel pf && pf.GetChildCount() > 0
							&& pf.GetChild(0) is TextureRect tr)
						{
							tr.Modulate = new Color(1, 1, 1, isSelected
								? ThumbAlphaSelected : ThumbAlphaIdle);
							break;
						}
					}
				}
			}
		}

		_cover.Texture = GetCover(map, BigCoverRenderSize);

		_infoTitle.Text = map.PrettyTitle;
		_infoSub.Text = "Mapped by " + map.PrettyMappers;
		_infoLen.Text = map.DifficultyName + "  |  " + map.Notes.Length + " notes  |  "
			+ Strings.FormatTime(map.Length / 1000.0);

		_startSlider.MaxValue = Math.Max(1, map.Length - 1000);
		_startSlider.Value = 0;
		_startFromMs = 0;
		UpdateStartFromDisplay();

		RefreshSpeedButtons();
		RefreshRpInfo();
		RefreshPassStats();
		PlayPreview();

		_playButton.Disabled = false;
	}

	private void RefreshSpeedButtons()
	{
		_updatingSpeedUi = true;

		foreach (var b in _speedButtons)
		{
			b.ButtonPressed = Math.Abs((double)b.GetMeta("speed_val") - Main.SelectedSpeed) < 0.001;
		}

		string speedStr = Main.SelectedSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
		_speedValueLabel.Text = speedStr;
		_customSpeedEdit.Text = speedStr;

		_updatingSpeedUi = false;
	}

	private void RefreshRpInfo()
	{
		if (_selected == null)
		{
			_rpPotentialLabel.Text = "-";
			_rpBestLabel.Text = "-";
			return;
		}

		var stats = _selected.Stats;
		bool counting = !Main.NoFailEnabled && !Main.AutoPlayEnabled;

		if (counting)
		{
			double max = RpCalculator.MaxRpForSpeed(stats, Main.SelectedSpeed);
			_rpPotentialLabel.Text = max.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " RP";
			_rpPotentialLabel.AddThemeColorOverride("font_color", UiStyle.Accent);
		}
		else
		{
			_rpPotentialLabel.Text = "0 RP (assist)";
			_rpPotentialLabel.AddThemeColorOverride("font_color", UiStyle.TextMuted);
		}

		double best = PlayerProfile.GetMapMaxRP(_selected.Name);
		_rpBestLabel.Text = best.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " RP";
	}

	private void RefreshPassStats()
	{
		if (_selected == null)
		{
			_passBlock.Visible = false;
			_bestBlock.Visible = false;
			_noPassLabel.Visible = true;
			_noPassLabel.Text = "No map selected.";
			return;
		}

		var lb = Leaderboard.Load(_selected.Name);
		var rec = lb.For(Main.SelectedSpeed);

		if (rec.Last == null && rec.Best == null)
		{
			_passBlock.Visible = false;
			_bestBlock.Visible = false;
			_noPassLabel.Visible = true;
			_noPassLabel.Text = "No attempts at this speed yet.";
			return;
		}

		_noPassLabel.Visible = false;

		if (rec.Last != null)
		{
			_passBlock.Visible = true;
			_currentPassGrade.Text = rec.Last.Grade;
			_currentPassGrade.AddThemeColorOverride("font_color", GradeColor(rec.Last.Grade));
			_currentPassStatus.Text = rec.Last.Passed ? "Passed" : "Failed";
			_currentPassStatus.AddThemeColorOverride("font_color",
				rec.Last.Passed ? UiStyle.Success : new Color("ff7d3d"));

			string acc = rec.Last.Accuracy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "%";
			string from = "from " + Strings.FormatTime(rec.Last.StartFrom / 1000.0);
			_currentPassStats.Text = acc + "  |  " + rec.Last.Misses + " misses  |  " + from;
		}
		else
		{
			_passBlock.Visible = false;
		}

		if (rec.Best != null)
		{
			_bestBlock.Visible = true;
			_bestAttemptGrade.Text = rec.Best.Grade;
			_bestAttemptGrade.AddThemeColorOverride("font_color", GradeColor(rec.Best.Grade));
			_bestAttemptStatus.Text = rec.Best.Passed ? "Passed" : "Failed";
			_bestAttemptStatus.AddThemeColorOverride("font_color",
				rec.Best.Passed ? UiStyle.Success : new Color("ff7d3d"));

			string acc = rec.Best.Accuracy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "%";
			string from = "from " + Strings.FormatTime(rec.Best.StartFrom / 1000.0);
			_bestAttemptStats.Text = acc + "  |  " + rec.Best.Misses + " misses  |  " + from;
		}
		else
		{
			_bestBlock.Visible = false;
		}
	}

	private static Color GradeColor(string grade)
	{
		switch (grade)
		{
			case "SS": return new Color("ffd700");
			case "S":  return new Color("8fe4ff");
			case "A":  return new Color("6ee7a8");
			case "B":  return new Color("8fbcff");
			case "C":  return new Color("ffd166");
			case "D":  return new Color("ff9d33");
			default:   return new Color("ff4d6d");
		}
	}

	private void UpdateStartFromDisplay()
	{
		double total = _selected?.Length ?? 0;
		string cur = Strings.FormatTime(_startFromMs / 1000.0);
		string tot = Strings.FormatTime(total / 1000.0);
		_startFromValue.Text = _startFromMs > 1 ? "Starting at " + cur + " of " + tot : "";
		_startEdit.Text = Strings.FormatTime(_startFromMs / 1000.0);
	}

	private void OnStartFromSliderChanged(double value)
	{
		if (_updatingStartUi) return;
		_startFromMs = Math.Round(value / 100.0) * 100.0;
		UpdateStartFromDisplay();
	}

	private void OnStartFromSubmitted(string text)
	{
		CommitStartFromEdit();
	}

	private void CommitStartFromEdit()
	{
		if (_selected == null) return;

		string txt = _startEdit.Text.Trim();
		double seconds = ParseTime(txt);
		double ms = seconds * 1000.0;
		if (ms < 0) ms = 0;
		double maxMs = Math.Max(0, _selected.Length - 1000);
		if (ms > maxMs) ms = maxMs;

		_startFromMs = Math.Round(ms / 100.0) * 100.0;
		_updatingStartUi = true;
		_startSlider.Value = _startFromMs;
		_updatingStartUi = false;
		UpdateStartFromDisplay();
	}

	private static double ParseTime(string txt)
	{
		if (string.IsNullOrEmpty(txt)) return 0;
		txt = txt.Trim();

		if (txt.Contains(':'))
		{
			var parts = txt.Split(':');
			if (parts.Length == 2
				&& double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out double m)
				&& double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out double s))
			{
				return m * 60 + s;
			}
			return 0;
		}

		if (double.TryParse(txt, System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out double v))
		{
			return v;
		}
		return 0;
	}

	private Button MakeSpeedButton(string label, double value, ButtonGroup group)
	{
		Button b = new Button
		{
			Text = label,
			ToggleMode = true,
			ButtonGroup = group,
			CustomMinimumSize = new Vector2(0, 32),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		b.AddThemeFontSizeOverride("font_size", 10);
		b.SetMeta("speed_val", value);

		var normal = UiStyle.PanelStyle(UiStyle.SurfaceHi, UiStyle.Border, 6);
		normal.ContentMarginLeft = 4; normal.ContentMarginRight = 4;
		normal.ContentMarginTop = 6; normal.ContentMarginBottom = 6;

		var active = UiStyle.PanelStyle(UiStyle.Accent, UiStyle.Accent, 6);
		active.ContentMarginLeft = 4; active.ContentMarginRight = 4;
		active.ContentMarginTop = 6; active.ContentMarginBottom = 6;

		b.AddThemeStyleboxOverride("normal", normal);
		b.AddThemeStyleboxOverride("hover", UiStyle.PanelStyle(UiStyle.SurfaceHi.Lightened(0.06f), UiStyle.Accent, 6));
		b.AddThemeStyleboxOverride("pressed", active);
		b.AddThemeStyleboxOverride("focus", normal);

		b.AddThemeColorOverride("font_color", UiStyle.TextMuted);
		b.AddThemeColorOverride("font_hover_color", UiStyle.TextPrimary);
		b.AddThemeColorOverride("font_pressed_color", Colors.White);

		b.Toggled += (on) =>
		{
			if (_updatingSpeedUi) return;
			if (!on) return;
			Main.SelectedSpeed = value;
			_customSpeedEdit.Text = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
			_speedValueLabel.Text = _customSpeedEdit.Text;
			RefreshRpInfo();
			RefreshPassStats();
			PlayPreview();
		};

		return b;
	}

	private void UpdateSpeedButtons()
	{
		RefreshSpeedButtons();
	}

	private void OnCustomSpeedSubmitted(string text)
	{
		CommitCustomSpeed();
	}

	private void CommitCustomSpeed()
	{
		string txt = _customSpeedEdit.Text.Trim().TrimEnd('x', 'X');
		if (!double.TryParse(txt, System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out double val))
		{
			RefreshSpeedButtons();
			return;
		}

		val = Math.Clamp(val, 0.1, 5.0);
		Main.SelectedSpeed = val;

		_updatingSpeedUi = true;
		foreach (var b in _speedButtons)
		{
			b.ButtonPressed = Math.Abs((double)b.GetMeta("speed_val") - val) < 0.001;
		}
		_updatingSpeedUi = false;

		_customSpeedEdit.Text = val.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
		_speedValueLabel.Text = _customSpeedEdit.Text;

		RefreshRpInfo();
		RefreshPassStats();
		PlayPreview();
	}

	private void PlayPreview()
	{
		_preview.Stop();

		if (_selected == null || !_selected.HasAudio) return;

		try
		{
			AudioStream stream = _selected.AudioExt == "ogg"
				? AudioStreamOggVorbis.LoadFromFile(_selected.AudioPath)
				: AudioStreamMP3.LoadFromFile(_selected.AudioPath);

			if (stream == null) return;

			_preview.Stream = stream;
			_preview.PitchScale = (float)Main.SelectedSpeed;
			_preview.VolumeDb = ComputePreviewDb();

			double startSec = _startFromMs / 1000.0;
			_preview.Play((float)startSec);
		}
		catch { }
	}

	private void OnBackPressed()
	{
		_preview.Stop();
		Main.Instance.ShowMainMenu();
	}

	private void OnSearchChanged(string text)
	{
		RefreshList();
	}

	private void OnImportPressed()
	{
		_dialog.PopupCentered(new Vector2I(720, 480));
	}

	private async void OnFilesSelected(string[] files)
	{
		int n = await MapManager.ImportAsync(files);
		if (n > 0) GD.Print("Imported " + n + " map(s).");
	}

	private void OnRefreshPressed()
	{
		MapManager.Reload();
	}

	private void PickRandom()
	{
		if (MapManager.Maps.Count == 0) return;
		var m = MapManager.Maps[_rng.Next(MapManager.Maps.Count)];
		SelectMap(m);

		var scroll = _list.GetParent() as ScrollContainer;
		if (scroll != null) scroll.ScrollVertical = 0;
	}

	private void PlaySelected()
	{
		if (_selected == null) return;
		_preview.Stop();

		Main.SelectedStartFrom = _startFromMs;
		Main.Instance.StartGame(_selected, Main.SelectedSpeed, _startFromMs);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.PhysicalKeycode == Key.Escape)
			{
				OnBackPressed();
				GetViewport().SetInputAsHandled();
			}
			else if (key.PhysicalKeycode == Key.F2)
			{
				PickRandom();
				GetViewport().SetInputAsHandled();
			}
		}
	}
}
