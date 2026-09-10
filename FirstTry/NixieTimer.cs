using Godot;
using System;

public partial class NixieTimer : Control
{
    [Export] public double CoverOpenThreshold = 10.0;
    [Export] public double GracePeriod = 10.0;

    // Events για επικοινωνία με το Main.cs
    public event Action<bool> OnCoverThresholdChanged;
    public event Action OnTimerZeroReached;
    public event Action OnGracePeriodExpired;
    public double CurrentTime => _currentTime;

    private Sprite2D _minHundrends;
    private Sprite2D _minTens;
    private Sprite2D _minUnits;
    private Sprite2D _secTens;
    private Sprite2D _secUnits;
    private Control _digitsContainer;

    private double _currentTime;
    private bool _isRunning = false;
    private bool _wasCoverOpen = false;
    private bool _isSkeletonCrew = false;
    public double TimeScale { get; set; } = 1.0;
    public override void _Ready()
    {
        // Προσοχή στο όνομα του κόμβου όπως υπάρχει στο Scene Tree
        _minHundrends = GetNode<Sprite2D>("DigitsContainer/DigitMinuteHundrends");
        _minTens = GetNode<Sprite2D>("DigitsContainer/DigitMinuteTens");
        _minUnits = GetNode<Sprite2D>("DigitsContainer/DigitMinuteUnits");
        _secTens = GetNode<Sprite2D>("DigitsContainer/DigitSecondTens");
        _secUnits = GetNode<Sprite2D>("DigitsContainer/DigitSecondUnits");
        _digitsContainer = GetNode<Control>("DigitsContainer");
    }

    public override void _Process(double delta)
    {
        if (!_isRunning || _isSkeletonCrew)
            return;

        double previousTime = _currentTime;
        // Εφαρμογή του TimeScale (επιτάχυνση αντίστροφης μέτρησης)
        _currentTime -= (delta * TimeScale);

        // 1. Έλεγχος για άνοιγμα / κλείσιμο καπακιού
        bool shouldOpen = _currentTime <= CoverOpenThreshold;
        if (shouldOpen != _wasCoverOpen)
        {
            _wasCoverOpen = shouldOpen;
            OnCoverThresholdChanged?.Invoke(shouldOpen);
        }

        // 2. Trigger τη στιγμή που ο χρόνος μηδενίζει
        if (previousTime > 0 && _currentTime <= 0)
        {
            OnTimerZeroReached?.Invoke();
        }

        // 3. Έλεγχος οριστικής λήξης (Game Over μετά τη χάρη)
        if (_currentTime < -GracePeriod)
        {
            _isRunning = false;
            OnGracePeriodExpired?.Invoke();
            return;
        }

        UpdateVisuals();
    }

    // Κατάσταση Skeleton Crew: 000:00, κανονικό χρώμα, κουμπί κλειδωμένο
    public void SetSkeletonCrewMode()
    {
        _isSkeletonCrew = true;
        _isRunning = false;
        _currentTime = 0;

        _digitsContainer.Visible = true;
        _digitsContainer.Modulate = Colors.White;
        SetDigitFrames(0, 0);

        _wasCoverOpen = false;
        OnCoverThresholdChanged?.Invoke(false);
    }

    // Έναρξη Βάρδιας: Παραμένει στο 000:00 και ξεκλειδώνει το κουμπί
    public void ResumeActiveShift(double durationInSeconds)
    {
        //_isSkeletonCrew = false;
        //_isRunning = false;
        //_currentTime = 0;

        //_digitsContainer.Visible = true;
        //_digitsContainer.Modulate = Colors.White;
        //SetDigitFrames(0, 0);

        //_wasCoverOpen = true;
        //OnCoverThresholdChanged?.Invoke(true);

        _isSkeletonCrew = false;
        StartCountdown(durationInSeconds);
    }

    public void StartCountdown(double durationInSeconds)
    {
        _currentTime = durationInSeconds;
        _isRunning = true;
        _wasCoverOpen = false;
        _digitsContainer.Visible = true;

        OnCoverThresholdChanged?.Invoke(false);
        UpdateVisuals();
    }

    public void StopCountdown()
    {
        _isRunning = false;
    }

    public void SetFailureDisplay()
    {
        _isRunning = false;
        _digitsContainer.Visible = false;
    }

    private void UpdateVisuals()
    {
        if (_isSkeletonCrew)
        {
            _digitsContainer.Modulate = Colors.White;
            SetDigitFrames(0, 0);
            return;
        }

        double displaySeconds;

        if (_currentTime > 0)
        {
            displaySeconds = _currentTime;
            _digitsContainer.Modulate = Colors.White;
        }
        else
        {
            double remainingGrace = GracePeriod + _currentTime;
            displaySeconds = Math.Max(0, remainingGrace);
            _digitsContainer.Modulate = new Color(2.5f, 0.15f, 0.15f, 1.0f);
        }

        int totalSeconds = (int)Math.Ceiling(displaySeconds);
        int totalMinutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        SetDigitFrames(totalMinutes, seconds);
    }

    // Τοποθέτηση των frames και στα 5 ψηφία (έως 999:59)
    private void SetDigitFrames(int totalMinutes, int seconds)
    {
        // 3 ψηφία για τα λεπτά (0-999)
        _minHundrends.Frame = Mathf.Clamp(totalMinutes / 100, 0, 9);
        _minTens.Frame = Mathf.Clamp((totalMinutes % 100) / 10, 0, 9);
        _minUnits.Frame = Mathf.Clamp(totalMinutes % 10, 0, 9);

        // 2 ψηφία για τα δευτερόλεπτα (0-59)
        _secTens.Frame = Mathf.Clamp(seconds / 10, 0, 9);
        _secUnits.Frame = Mathf.Clamp(seconds % 10, 0, 9);
    }
}