using Godot;
using System;
using System.Text;

public enum TerminalTab
{
    ShiftLogs,
    GeneratorStats,
    CityStats,
    ResourceAccumulation
}

public partial class DiagnosticTerminal : Control
{
    [ExportGroup("Subsystem Dependencies")]
    [Export] private ResourceSubsystem _resourceSubsystem;
    [Export] private PressureSubsystem _pressureSubsystem;
    [Export] private PopulationSubsystem_old _populationSubsystem;
    [Export] private SkeletonCrewSubsystem _skeletonCrewSubsystem;

    [ExportGroup("Layout Coordinates")]
    [Export] public Rect2 DockedRect = new(135, 1180, 290, 360);
    [Export] public Rect2 ExpandedRect = new(80, 420, 560, 920);

    private ColorRect _dimmer;
    private PanelContainer _frame;
    private HBoxContainer _navTabBar;
    private Button _minimizeBtn;
    private Button _maximizeTrigger;

    private ScrollContainer _viewLogs;
    private RichTextLabel _logsText;
    private RichTextLabel _genStatsText;
    private RichTextLabel _cityStatsText;
    private RichTextLabel _resStatsText;

    private readonly StringBuilder _logHistory = new();
    private TerminalTab _activeTab = TerminalTab.ShiftLogs;
    private bool _isExpanded = false;
    private Tween _activeTween;

    public override void _Ready()
    {
        _dimmer = GetNode<ColorRect>("BackgroundDimmer");
        _frame = GetNode<PanelContainer>("TerminalFrame");
        _navTabBar = GetNode<HBoxContainer>("TerminalFrame/MainVBox/NavTabBar");
        _minimizeBtn = GetNode<Button>("TerminalFrame/MainVBox/HeaderBar/MinimizeButton");
        _maximizeTrigger = GetNode<Button>("TerminalFrame/MaximizeTrigger");

        _viewLogs = GetNode<ScrollContainer>("TerminalFrame/MainVBox/DisplayContent/ViewLogs");
        _logsText = GetNode<RichTextLabel>("TerminalFrame/MainVBox/DisplayContent/ViewLogs/LogsVBox/LogsText");
        _genStatsText = GetNode<RichTextLabel>("TerminalFrame/MainVBox/DisplayContent/ViewGenerator/GenStatsLabel");
        _cityStatsText = GetNode<RichTextLabel>("TerminalFrame/MainVBox/DisplayContent/ViewCity/CityStatsLabel");
        _resStatsText = GetNode<RichTextLabel>("TerminalFrame/MainVBox/DisplayContent/ViewResources/ResStatsLabel");

        // Αποθήκευση της θέσης που ορίσατε χειροκίνητα στον Godot Editor
        DockedRect = new Rect2(_frame.Position, _frame.Size);

        GetNode<Button>("TerminalFrame/MainVBox/NavTabBar/TabLogsBtn").Pressed += () => SwitchTab(TerminalTab.ShiftLogs);
        GetNode<Button>("TerminalFrame/MainVBox/NavTabBar/TabGenBtn").Pressed += () => SwitchTab(TerminalTab.GeneratorStats);
        GetNode<Button>("TerminalFrame/MainVBox/NavTabBar/TabCityBtn").Pressed += () => SwitchTab(TerminalTab.CityStats);
        GetNode<Button>("TerminalFrame/MainVBox/NavTabBar/TabResBtn").Pressed += () => SwitchTab(TerminalTab.ResourceAccumulation);

        _maximizeTrigger.Pressed += ExpandTerminal;
        _minimizeBtn.Pressed += MinimizeTerminal;

        _dimmer.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                MinimizeTerminal();
            }
        };

        AppendLogEntry("ΣΥΣΤΗΜΑ: Τερματικό συνδέθηκε. Παρακολούθηση ενεργή...");

        ApplyDockedLayoutImmediate();
        SwitchTab(TerminalTab.ShiftLogs);
    }

    public override void _Process(double delta)
    {
        switch (_activeTab)
        {
            case TerminalTab.GeneratorStats:
                RefreshGeneratorStats();
                break;
            case TerminalTab.CityStats:
                RefreshCityStats();
                break;
            case TerminalTab.ResourceAccumulation:
                RefreshResourceStats();
                break;
        }
    }

    public void AppendLogEntry(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string formattedLine = $"[color=#8fa3ad][{timestamp}][/color] {message}\n";
        _logHistory.Append(formattedLine);

        _logsText.Text = _logHistory.ToString();

        Callable.From(() =>
        {
            var vScroll = _viewLogs.GetVScrollBar();
            vScroll.Value = vScroll.MaxValue;
        }).CallDeferred();
    }

    public void SwitchTab(TerminalTab tab)
    {
        _activeTab = tab;

        _viewLogs.Visible = (_activeTab == TerminalTab.ShiftLogs);
        _genStatsText.GetParent<Control>().Visible = (_activeTab == TerminalTab.GeneratorStats);
        _cityStatsText.GetParent<Control>().Visible = (_activeTab == TerminalTab.CityStats);
        _resStatsText.GetParent<Control>().Visible = (_activeTab == TerminalTab.ResourceAccumulation);
    }

    public void ExpandTerminal()
    {
        if (_isExpanded) return;
        _isExpanded = true;

        // Φέρνει όλο το Control μπροστά από τα υπόλοιπα στοιχεία του CanvasLayer
        ZIndex = 50;

        _dimmer.Visible = true;
        _dimmer.MouseFilter = MouseFilterEnum.Stop;

        _navTabBar.Visible = true;
        _minimizeBtn.Visible = true;

        // Απενεργοποιούμε το trigger ώστε να λειτουργούν τα κουμπιά και το scroll
        _maximizeTrigger.Visible = false;
        _maximizeTrigger.MouseFilter = MouseFilterEnum.Ignore;

        AnimateFrame(ExpandedRect);
    }

    public void MinimizeTerminal()
    {
        if (!_isExpanded) return;
        _isExpanded = false;

        _dimmer.Visible = false;
        _dimmer.MouseFilter = MouseFilterEnum.Ignore;

        _navTabBar.Visible = false;
        _minimizeBtn.Visible = false;

        // Επαναφορά του trigger σε όλο το πλαίσιο
        _maximizeTrigger.Visible = true;
        _maximizeTrigger.MouseFilter = MouseFilterEnum.Stop;

        AnimateFrame(DockedRect, onComplete: () =>
        {
            ZIndex = 0;
        });
    }

    private void AnimateFrame(Rect2 targetRect, Action onComplete = null)
    {
        _activeTween?.Kill();
        _activeTween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(_frame, "position", targetRect.Position, 0.35f);
        _activeTween.TweenProperty(_frame, "size", targetRect.Size, 0.35f);

        if (onComplete != null)
        {
            _activeTween.Chain().TweenCallback(Callable.From(onComplete));
        }
    }

    private void ApplyDockedLayoutImmediate()
    {
        ZIndex = 0;
        _frame.Position = DockedRect.Position;
        _frame.Size = DockedRect.Size;

        _dimmer.Visible = false;
        _dimmer.MouseFilter = MouseFilterEnum.Ignore;

        _navTabBar.Visible = false;
        _minimizeBtn.Visible = false;

        _maximizeTrigger.Visible = true;
        _maximizeTrigger.MouseFilter = MouseFilterEnum.Stop;
    }

    private void RefreshGeneratorStats()
    {
        float integrity = _pressureSubsystem != null ? _pressureSubsystem.CasingIntegrity : 100.0f;
        float pressure = _pressureSubsystem != null ? _pressureSubsystem.CurrentPressure : 0.0f;
        string stance = _skeletonCrewSubsystem != null ? _skeletonCrewSubsystem.CurrentStance.ToString() : "Day Active";

        _genStatsText.Text = $"[b][color=#e5a93c]-- GENERATOR TELEMETRY --[/color][/b]\n\n" +
                             $"• Casing Structural Integrity: [color=#{GetIntegrityHex(integrity)}]{integrity:F1}%[/color]\n" +
                             $"• Steam Chamber Pressure: {pressure:F2} Bar\n" +
                             $"• Draft Regulator Stance: {stance}\n" +
                             $"• Soot Delay Coefficient: +{(_skeletonCrewSubsystem != null ? _skeletonCrewSubsystem.MorningPressureDelayBonus * 100f : 0f):F0}%\n";
    }

    private void RefreshCityStats()
    {
        int living = _populationSubsystem != null ? _populationSubsystem.TotalLivingPopulation : 0;
        int activeCrew = _populationSubsystem != null ? _populationSubsystem.EffectiveWorkforce : 0;
        int sick = _populationSubsystem != null ? _populationSubsystem.SickWorkers : 0;
        int gravely = _populationSubsystem != null ? _populationSubsystem.GravelySickWorkers : 0;
        float temp = _resourceSubsystem != null ? _resourceSubsystem.CityTemperatureC : -50.0f;

        _cityStatsText.Text = $"[b][color=#6ecbf5]-- SETTLEMENT HABITAT --[/color][/b]\n\n" +
                              $"• Habitation Core Ambient Temp: [color=#{GetTempHex(temp)}]{temp:F1}°C[/color]\n" +
                              $"• Total Living Souls: {living}\n" +
                              $"• Available Day Crew: {activeCrew}\n" +
                              $"• Lightly Injured (Cots): {sick}\n" +
                              $"• Gravely Frostbitten (Ward): {gravely}\n";
    }

    private void RefreshResourceStats()
    {
        float coal = _resourceSubsystem != null ? _resourceSubsystem.CoalStockpileKg : 0.0f;
        int scrap = _resourceSubsystem != null ? _resourceSubsystem.MetalScrapCount : 0;
        float battery = _resourceSubsystem != null ? _resourceSubsystem.BatteryChargePercent : 0.0f;

        _resStatsText.Text = $"[b][color=#88d49e]-- LOGISTICS STOCKPILE --[/color][/b]\n\n" +
                             $"• Coal Bunker Reserve: {coal:F1} kg\n" +
                             $"• High-Grade Machine Scrap: {scrap} units\n" +
                             $"• Auxiliary Relay Battery: {battery:F1}%\n";
    }

    private string GetIntegrityHex(float integrity) => integrity < 30f ? "ff4444" : integrity < 65f ? "ffaa33" : "44ff88";
    private string GetTempHex(float temp) => temp < -65f ? "4488ff" : temp < -45f ? "88ddff" : "ffffff";
}