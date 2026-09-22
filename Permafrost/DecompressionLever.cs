using Godot;
using System;

public partial class DecompressionLever : Control
{
    [Export] private HotWeldOverdrive _overdriveSubsystem;

    public event Action<VentMode_old> OnVentTriggered;
    public event Action<VentMode_old> OnNotchChanged;

    [Export] public VentMode_old CurrentMode { get; private set; } = VentMode_old.SafeRelease;

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

        _notchCycleButton.Pressed += OnNotchCyclePressed;
        _hotWeldButton.Pressed += OnHotWeldToggled;
        _commitVentButton.Pressed += OnCommitVentPressed;

        // --- Σύνδεση με το Subsystem για αυτόματη απενεργοποίηση (Blowout / No Scrap) ---
        if (_overdriveSubsystem != null)
        {
            _overdriveSubsystem.OnOverdriveToggled += HandleExternalOverdriveToggled;
            _overdriveSubsystem.OnOverdriveBlowout += HandleExternalOverdriveBlowout;
        }

        UpdateVisualState();
        SetLockedState(true);
    }

    public override void _ExitTree()
    {
        // Αποσύνδεση των C# events κατά την καταστροφή του κόμβου
        if (_overdriveSubsystem != null)
        {
            _overdriveSubsystem.OnOverdriveToggled -= HandleExternalOverdriveToggled;
            _overdriveSubsystem.OnOverdriveBlowout -= HandleExternalOverdriveBlowout;
        }
    }

    // Καλείται αυτόματα όταν το σύστημα σβήσει μόνο του (π.χ. έλλειψη Scrap ή ForceShutdown)
    private void HandleExternalOverdriveToggled(bool isActive)
    {
        if (!isActive && _isHotWeldActive)
        {
            _isHotWeldActive = false;
            RevertToMechanicalNotch();
            UpdateVisualState();
            OnNotchChanged?.Invoke(CurrentMode);
        }
    }

    // Καλείται σε περίπτωση έκρηξης / υπερθέρμανσης
    private void HandleExternalOverdriveBlowout()
    {
        _isHotWeldActive = false;
        RevertToMechanicalNotch();
        UpdateVisualState();
        OnNotchChanged?.Invoke(CurrentMode);

        // Προαιρετικό: feedback στο κουμπί του Hot-Weld
        if (_hotWeldButton != null)
        {
            _hotWeldButton.Text = "[BLOWOUT!]";
        }
    }

    public void SetLockedState(bool locked)
    {
        _isUnlocked = !locked;
        _commitVentButton.Disabled = locked;

        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        if (locked)
        {
            _coverShield.Visible = true;
            tween.TweenProperty(_coverShield, "modulate:a", 1.0f, 0.25f);
        }
        else
        {
            tween.TweenProperty(_coverShield, "modulate:a", 0.0f, 0.25f);
            tween.Chain().TweenCallback(Callable.From(() => _coverShield.Visible = false));
        }
    }

    private void OnNotchCyclePressed()
    {
        // Αν ήταν ενεργό το Hot-Weld, η αλλαγή notch το σβήνει και στο Subsystem
        if (_isHotWeldActive)
        {
            _isHotWeldActive = false;
            _overdriveSubsystem?.ToggleOverdrive(false);
        }

        _currentNotchIndex = (_currentNotchIndex + 1) % 3;
        RevertToMechanicalNotch();

        UpdateVisualState();
        OnNotchChanged?.Invoke(CurrentMode);
    }

    private void OnHotWeldToggled()
    {
        _isHotWeldActive = !_isHotWeldActive;

        if (_isHotWeldActive)
        {
            CurrentMode = VentMode_old.HotWeld;
            _overdriveSubsystem?.ToggleOverdrive(true);
        }
        else
        {
            // Απενεργοποίηση και στο Subsystem
            _overdriveSubsystem?.ToggleOverdrive(false);
            RevertToMechanicalNotch();
        }

        UpdateVisualState();
        OnNotchChanged?.Invoke(CurrentMode);
    }

    private void RevertToMechanicalNotch()
    {
        CurrentMode = _currentNotchIndex switch
        {
            0 => VentMode_old.SafeRelease,
            1 => VentMode_old.HeatSurge,
            2 => VentMode_old.ScrapScour,
            _ => VentMode_old.SafeRelease
        };
    }

    private void OnCommitVentPressed()
    {
        if (!_isUnlocked) return;
        OnVentTriggered?.Invoke(CurrentMode);
        SetLockedState(true);
    }

    private void UpdateVisualState()
    {
        if (_leverTrackSprite != null)
        {
            _leverTrackSprite.Frame = _currentNotchIndex;
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
                VentMode_old.SafeRelease => "NOTCH 1: SAFE (108m)",
                VentMode_old.HeatSurge => "NOTCH 2: HEAT (72m)",
                VentMode_old.ScrapScour => "NOTCH 3: SCRAP (45m)",
                VentMode_old.HotWeld => "BYPASS: HOT-WELD",
                _ => "SELECT NOTCH"
            };
        }
    }
}