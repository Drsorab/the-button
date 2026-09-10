using Godot;
using System;

public partial class ResetButton : TextureButton
{
    public event Action OnButtonPressed;

    public override void _Ready()
    {
        Pressed += () => OnButtonPressed?.Invoke();
        SetEnabled(false);
    }

    // Ενεργοποίηση / Απενεργοποίηση κουμπιού βάσει χρόνου
    public void SetEnabled(bool isEnabled)
    {
        MouseFilter = isEnabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Disabled = !isEnabled;

        // Οπτικό feedback: σκοτεινό όταν είναι κλειδωμένο, φωτεινό όταν ξεκλειδώνει
        if (isEnabled)
        {
            Modulate = Colors.White;

            // Μικρό scale pulse animation όταν ενεργοποιείται
            Tween tween = CreateTween();
            tween.TweenProperty(this, "scale", new Vector2(1.05f, 1.05f), 0.1);
            tween.TweenProperty(this, "scale", Vector2.One, 0.1);
        }
        else
        {
            Modulate = new Color(0.45f, 0.45f, 0.45f, 0.85f); // Σκοτεινό / μη πατήσιμο
        }
    }
}