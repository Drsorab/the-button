using Godot;
using System;

public partial class DecompressionLever : Control
{
    [Export] private HotWeldOverdrive _overdriveSubsystem;
    // Events για ενημέρωση του Manager και του UI
    public event Action<VentMode> OnVentTriggered;
    public event Action<VentMode> OnNotchChanged;

    [Export] public VentMode CurrentMode { get; private set; } = VentMode.SafeRelease;

    // Node references
    private Sprite2D _leverTrackSprite;
    private Button _notchCycleButton;
    private Button _hotWeldButton;
    private Control _coverShield;
    private Button _commitVentButton;

    private bool _isUnlocked = false;
    private bool _isHotWeldActive = false;
    private int _currentNotchIndex = 0; // 0: Safe (108m), 1: Heat (72m), 2: Scrap (45m)

    public override void _Ready()
    {
        _leverTrackSprite = GetNode<Sprite2D>("LeverTrackSprite");
        _notchCycleButton = GetNode<Button>("NotchCycleButton");
        _hotWeldButton = GetNode<Button>("HotWeldNode/HotWeldButton");
        _coverShield = GetNode<Control>("CoverShield");
        _commitVentButton = GetNode<Button>("CommitVentButton");

        // Σύνδεση σημάτων κουμπιών
        _notchCycleButton.Pressed += OnNotchCyclePressed;
        _hotWeldButton.Pressed += OnHotWeldToggled;
        _commitVentButton.Pressed += OnCommitVentPressed;

        UpdateVisualState();
        SetLockedState(true);
    }

    // Κλείδωμα ή ξεκλείδωμα του προστατευτικού καλύμματος (από το NixieTimer threshold)
    public void SetLockedState(bool locked)
    {
        _isUnlocked = !locked;
        _commitVentButton.Disabled = locked;

        // Ομαλό άνοιγμα/κλείσιμο του καλύμματος με animation
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        if (locked)
        {
            // Κλείσιμο: επαναφορά στην αρχική θέση και ενεργοποίηση κάλυψης
            _coverShield.Visible = true;
            tween.TweenProperty(_coverShield, "modulate:a", 1.0f, 0.25f);
        }
        else
        {
            // Άνοιγμα: fade-out και απόκρυψη ώστε να αποκαλυφθεί το κουμπί εκτόνωσης
            tween.TweenProperty(_coverShield, "modulate:a", 0.0f, 0.25f);
            tween.Chain().TweenCallback(Callable.From(() => _coverShield.Visible = false));
        }
    }

    // Εναλλαγή θέσης μοχλού (Notch 1 -> Notch 2 -> Notch 3 -> Notch 1)
    private void OnNotchCyclePressed()
    {
        if (_isHotWeldActive)
        {
            // Αν είναι ενεργό το Hot-Weld, η αλλαγή notch το απενεργοποιεί
            _isHotWeldActive = false;
        }

        _currentNotchIndex = (_currentNotchIndex + 1) % 3;

        CurrentMode = _currentNotchIndex switch
        {
            0 => VentMode.SafeRelease,
            1 => VentMode.HeatSurge,
            2 => VentMode.ScrapScour,
            _ => VentMode.SafeRelease
        };

        UpdateVisualState();
        OnNotchChanged?.Invoke(CurrentMode);
    }

    // Ενεργοποίηση / Απενεργοποίηση της παράκαμψης Hot-Weld
    private void OnHotWeldToggled()
    {
        _isHotWeldActive = !_isHotWeldActive;

        if (_isHotWeldActive)
        {
            CurrentMode = VentMode.HotWeld;
            _overdriveSubsystem?.ToggleOverdrive(_isHotWeldActive);
        }
        else
        {
            // Επαναφορά στο προηγούμενο επιλεγμένο notch
            CurrentMode = _currentNotchIndex switch
            {
                0 => VentMode.SafeRelease,
                1 => VentMode.HeatSurge,
                2 => VentMode.ScrapScour,
                _ => VentMode.SafeRelease
            };
        }

        UpdateVisualState();
        OnNotchChanged?.Invoke(CurrentMode);
    }

    // Πάτημα του κεντρικού μοχλού/κουμπιού εκτόνωσης
    private void OnCommitVentPressed()
    {
        if (!_isUnlocked) return;

        // Αποστολή του επιλεγμένου mode στον DayShiftManager
        OnVentTriggered?.Invoke(CurrentMode);

        // Αυτόματο κλείδωμα αμέσως μετά την εκτόνωση
        SetLockedState(true);
    }

    // Ενημέρωση των γραφικών του μοχλού και των κουμπιών
    private void UpdateVisualState()
    {
        if (_leverTrackSprite != null)
        {
            _leverTrackSprite.Frame = _currentNotchIndex;
            // Αν το Hot-Weld είναι ενεργό, δίνουμε μια πορτοκαλί/κόκκινη απόχρωση στο sprite
            _leverTrackSprite.Modulate = _isHotWeldActive ? new Color(1.5f, 0.6f, 0.2f, 1.0f) : Colors.White;
        }

        if (_hotWeldButton != null)
        {
            _hotWeldButton.Text = _isHotWeldActive ? "[HOT-WELD: ENGAGED]" : "[HOT-WELD: OFF]";
            _hotWeldButton.Modulate = _isHotWeldActive ? new Color(1.8f, 0.4f, 0.2f, 1.0f) : Colors.White;
        }

        if (_notchCycleButton != null)
        {
            _notchCycleButton.Text = CurrentMode switch
            {
                VentMode.SafeRelease => "NOTCH 1: SAFE (108m)",
                VentMode.HeatSurge => "NOTCH 2: HEAT (72m)",
                VentMode.ScrapScour => "NOTCH 3: SCRAP (45m)",
                VentMode.HotWeld => "BYPASS: HOT-WELD (30m)",
                _ => "SELECT NOTCH"
            };
        }
    }
}