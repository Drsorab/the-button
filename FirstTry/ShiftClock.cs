using Godot;
using System;

public partial class ShiftClock : Control
{
    public event Action<ShiftMode> OnShiftModeEvaluated;
    public event Action<int> OnShiftScheduled;

    [Export] public int ShiftStartHour = 11;          // Ενεργή βάρδια σήμερα
    [Export] public int ScheduledShiftStartHour = 8; // Κατοχυρωμένη για αύριο
    public const int ShiftDurationHours = 8;
    public const int MinRestHours = 8;
    private const float DegsPerHour = 15.0f;

    // UI Elements
    private TextureProgressBar _greenShiftRing;
    private TextureProgressBar _scheduledRing;
    private Node2D _hourHand;
    private Control _clockCenter;
    private ColorRect _dimBackdrop;
    private Button _clickDetector;
    private Button _confirmButton;
    private Button _cancelButton;

    // Modal & Dragging States
    private Vector2 _miniPosition;
    private Vector2 _miniScale = Vector2.One;
    [Export] public float ExpandedScale = 2.4f;
    private bool _isExpanded = false;
    private bool _isDragging = false;

    private int _pendingShiftStartHour;
    private bool _hasScheduledShift = false;
    private ShiftMode _currentReportedMode = ShiftMode.SkeletonCrew;
    private int _lastEvaluatedDay = -1;

    public override void _Ready()
    {
        _greenShiftRing = GetNode<TextureProgressBar>("ClockCenter/GreenRing");
        _scheduledRing = GetNode<TextureProgressBar>("ClockCenter/ScheduledRing");
        _hourHand = GetNode<Node2D>("ClockCenter/Pivot");
        _clockCenter = GetNode<Control>("ClockCenter");
        _dimBackdrop = GetNode<ColorRect>("DimBackdrop");
        _clickDetector = GetNode<Button>("ClickDetector");
        _confirmButton = GetNode<Button>("ConfirmButton");
        _cancelButton = GetNode<Button>("CancelButton");

        _clockCenter.PivotOffset = _clockCenter.Size / 2.0f;
        _miniPosition = _clockCenter.Position;
        _miniScale = _clockCenter.Scale;

        _dimBackdrop.TopLevel = true;
        _dimBackdrop.ZIndex = 40;
        _dimBackdrop.Visible = false;

        // Σύνδεση σημάτων
        _clickDetector.Pressed += OpenClockModal;
        _confirmButton.Pressed += OnConfirmButtonPressed;
        _cancelButton.Pressed += CloseClockModal;
        _dimBackdrop.GuiInput += OnBackdropInput;

        _dimBackdrop.Visible = false;
        _confirmButton.Visible = false;
        _cancelButton.Visible = false;
        _scheduledRing.Visible = false;
        _confirmButton.TopLevel = true;
        _confirmButton.ZIndex = 50;

        _cancelButton.TopLevel = true;
        _cancelButton.ZIndex = 50;
        _pendingShiftStartHour = ScheduledShiftStartHour;

        UpdateRingVisuals();
        EvaluateCurrentShift(DateTime.Now);
    }

    public override void _Process(double delta)
    {
        DateTime now = DateTime.Now;

        // Έλεγχος αλλαγής μέρας στα μεσάνυχτα
        if (_lastEvaluatedDay != -1 && now.Day != _lastEvaluatedDay)
        {
            ApplyMidnightShiftTransition(now);
        }
        _lastEvaluatedDay = now.Day;

        // Κίνηση δείκτη
        float currentHourDecimal = now.Hour + (now.Minute / 60.0f) + (now.Second / 3600.0f);
        _hourHand.RotationDegrees = currentHourDecimal * DegsPerHour;

        EvaluateCurrentShift(now);
    }

    // Διαχείριση αλλαγής ημέρας
    private void ApplyMidnightShiftTransition(DateTime time)
    {
        if (_hasScheduledShift)
        {
            ShiftStartHour = ScheduledShiftStartHour;
            _hasScheduledShift = false;
            GD.Print($"[CLOCK] Νέα μέρα! Η βάρδια ενημερώθηκε σε: {ShiftStartHour:00}:00");
        }

        _scheduledRing.Visible = false; // Σβήσιμο του πορτοκαλί ring
        UpdateRingVisuals();
        EvaluateCurrentShift(time);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isExpanded) return;

        // Αν το κλικ/tap πέσει πάνω σε κουμπιά, αγνόησε το dragging
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (_confirmButton.Visible && _confirmButton.GetGlobalRect().HasPoint(mb.Position)) return;
            if (_cancelButton.Visible && _cancelButton.GetGlobalRect().HasPoint(mb.Position)) return;

            if (mb.Pressed && IsTouchInsideClock(mb.Position))
            {
                _isDragging = true;
                UpdatePendingHourFromPosition(mb.Position);
            }
            else if (!mb.Pressed)
            {
                _isDragging = false;
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            UpdatePendingHourFromPosition(mm.Position);
        }

        if (@event is InputEventScreenTouch touch)
        {
            if (_confirmButton.Visible && _confirmButton.GetGlobalRect().HasPoint(touch.Position)) return;
            if (_cancelButton.Visible && _cancelButton.GetGlobalRect().HasPoint(touch.Position)) return;

            if (touch.Pressed && IsTouchInsideClock(touch.Position))
            {
                _isDragging = true;
                UpdatePendingHourFromPosition(touch.Position);
            }
            else if (!touch.Pressed)
            {
                _isDragging = false;
            }
        }
        else if (@event is InputEventScreenDrag drag && _isDragging)
        {
            UpdatePendingHourFromPosition(drag.Position);
        }
    }

    private void UpdatePendingHourFromPosition(Vector2 touchPos)
    {
        Vector2 clockGlobalCenter = GetViewportRect().Size / 2.0f;
        Vector2 direction = touchPos - clockGlobalCenter;

        float angleRad = Mathf.Atan2(direction.Y, direction.X);
        float degrees = Mathf.RadToDeg(angleRad) + 90.0f;
        if (degrees < 0.0f) degrees += 360.0f;

        int selectedHour = Mathf.FloorToInt(degrees / DegsPerHour) % 24;

        if (selectedHour != _pendingShiftStartHour)
        {
            _pendingShiftStartHour = selectedHour;
            _scheduledRing.RadialInitialAngle = _pendingShiftStartHour * DegsPerHour;
            UpdateButtonState();
        }
    }

    // Ο ακριβής μαθηματικός έλεγχος χωρίς modulo σφάλματα
    private bool CheckRestPeriodValidity(int currentStart, int nextStart)
    {
        int todayEndFromMidnight = currentStart + ShiftDurationHours;
        int tomorrowStartFromMidnight = 24 + nextStart;
        int restHours = tomorrowStartFromMidnight - todayEndFromMidnight;

        return restHours >= MinRestHours;
    }

    private void UpdateButtonState()
    {
        int endHour = (_pendingShiftStartHour + ShiftDurationHours) % 24;
        bool isValid = CheckRestPeriodValidity(ShiftStartHour, _pendingShiftStartHour);

        if (isValid)
        {
            _confirmButton.Disabled = false;
            _confirmButton.Text = $"CONFIRM [{_pendingShiftStartHour:00}:00 - {endHour:00}:00]";
        }
        else
        {
            _confirmButton.Disabled = true;
            _confirmButton.Text = "REST REQ. (MIN 8H)";
        }
    }

    public void OpenClockModal()
    {
        if (_isExpanded) return;
        _isExpanded = true;
        _isDragging = false;


        _dimBackdrop.GlobalPosition = Vector2.Zero;
        _dimBackdrop.Size = GetViewportRect().Size;
        _dimBackdrop.Visible = true;
        _dimBackdrop.Modulate = new Color(1, 1, 1, 0); 

        _pendingShiftStartHour = _hasScheduledShift ? ScheduledShiftStartHour : ShiftStartHour;
        _scheduledRing.Visible = true;
        _scheduledRing.RadialInitialAngle = _pendingShiftStartHour * DegsPerHour;
        UpdateButtonState();

        _clickDetector.Visible = false;
        _dimBackdrop.Visible = true;
        _dimBackdrop.Modulate = new Color(1, 1, 1, 0);

        Vector2 screenCenter = GetViewportRect().Size / 2.0f;
        Vector2 localCenter = screenCenter - GlobalPosition;
        Vector2 targetCenterPosition = localCenter - _clockCenter.PivotOffset;

        // Ακτίνα του μεγεθυμένου ρολογιού + περιθώριο ασφαλείας
        float clockRadiusScaled = (_clockCenter.Size.Y / 2.0f) * ExpandedScale;
        float confirmButtonY = screenCenter.Y + clockRadiusScaled + 30.0f; // 30px κάτω από το καντράν

        float buttonWidth = 320.0f;
        float buttonHeight = 44.0f;
        float verticalGap = 12.0f;
        float buttonX = screenCenter.X - (buttonWidth / 2.0f); // Κεντράρισμα ακριβώς στον άξονα Χ της οθόνης

        // 1. Confirm Button
        _confirmButton.Size = new Vector2(buttonWidth, buttonHeight);
        _confirmButton.Position = new Vector2(buttonX, confirmButtonY);
        _confirmButton.Modulate = new Color(1, 1, 1, 0);
        _confirmButton.Visible = true;

        // 2. Cancel Button (Ακριβώς από κάτω)
        float cancelButtonY = confirmButtonY + buttonHeight + verticalGap;
        _cancelButton.Size = new Vector2(buttonWidth, buttonHeight);
        _cancelButton.Position = new Vector2(buttonX, cancelButtonY);
        _cancelButton.Modulate = new Color(1, 1, 1, 0);
        _cancelButton.Visible = true;

        Tween tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_clockCenter, "position", targetCenterPosition, 0.35);
        tween.TweenProperty(_clockCenter, "scale", Vector2.One * ExpandedScale, 0.35);
        tween.TweenProperty(_dimBackdrop, "modulate:a", 0.70f, 0.2);
        tween.TweenProperty(_confirmButton, "modulate:a", 1.0f, 0.3);
        tween.TweenProperty(_cancelButton, "modulate:a", 1.0f, 0.3);

        ZIndex = 50;
    }

    private void OnConfirmButtonPressed()
    {
        ScheduledShiftStartHour = _pendingShiftStartHour;
        _hasScheduledShift = true;
        _scheduledRing.Visible = true;

        UpdateRingVisuals();
        OnShiftScheduled?.Invoke(ScheduledShiftStartHour);

        GD.Print($"[CLOCK] Κατοχυρώθηκε για αύριο: {ScheduledShiftStartHour:00}:00");
        CloseClockModal();
    }

    public void CloseClockModal()
    {
        if (!_isExpanded) return;
        _isDragging = false;

        // Επαναφορά προηγούμενης ορατότητας και θέσης
        _scheduledRing.Visible = _hasScheduledShift;
        UpdateRingVisuals();

        Tween tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_clockCenter, "position", _miniPosition, 0.25);
        tween.TweenProperty(_clockCenter, "scale", _miniScale, 0.25);
        tween.TweenProperty(_dimBackdrop, "modulate:a", 0.0f, 0.2);
        tween.TweenProperty(_confirmButton, "modulate:a", 0.0f, 0.2);
        tween.TweenProperty(_cancelButton, "modulate:a", 0.0f, 0.2);

        tween.Chain().TweenCallback(Callable.From(() =>
        {
            _dimBackdrop.Visible = false;
            _confirmButton.Visible = false;
            _cancelButton.Visible = false;
            _clickDetector.Visible = true;
            _isExpanded = false;
            ZIndex = 0;
        }));
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && !_isDragging)
        {
            CloseClockModal();
        }
    }

    public void UpdateRingVisuals()
    {
        _greenShiftRing.RadialInitialAngle = ShiftStartHour * DegsPerHour;
        _scheduledRing.RadialInitialAngle = ScheduledShiftStartHour * DegsPerHour;
    }

    private bool IsTouchInsideClock(Vector2 pos)
    {
        Vector2 clockGlobalCenter = GetViewportRect().Size / 2.0f;
        float maxRadius = (_clockCenter.Size.X / 2.0f) * ExpandedScale;
        float minRadius = maxRadius * 0.25f;
        float distance = pos.DistanceTo(clockGlobalCenter);
        return distance >= minRadius && distance <= maxRadius;
    }

    public void ForceEvaluate()
    {
       EvaluateCurrentShift(DateTime.Now);
        OnShiftModeEvaluated?.Invoke(_currentReportedMode);
    }

    private void EvaluateCurrentShift(DateTime time)
    {
        int currentHour = time.Hour;
        int shiftEndHour = (ShiftStartHour + ShiftDurationHours) % 24;

        bool isActive = ShiftStartHour < shiftEndHour
            ? (currentHour >= ShiftStartHour && currentHour < shiftEndHour)
            : (currentHour >= ShiftStartHour || currentHour < shiftEndHour);

        ShiftMode evaluatedMode = isActive ? ShiftMode.ActiveShift : ShiftMode.SkeletonCrew;

        if (evaluatedMode != _currentReportedMode)
        {
            _currentReportedMode = evaluatedMode;
            OnShiftModeEvaluated?.Invoke(_currentReportedMode);
        }
    }

    // Debug μέθοδος για άμεση προσομοίωση αλλαγής ημέρας
    public void SimulateMidnightTransition()
    {
        DateTime simulatedTomorrow = DateTime.Now.AddDays(1);
        ApplyMidnightShiftTransition(simulatedTomorrow);
    }
}