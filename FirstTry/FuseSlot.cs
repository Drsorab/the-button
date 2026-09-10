using Godot;
using System;

public enum SlotCondition
{
    Locked,  // Η θέση δεν έχει αγοραστεί (αόρατη βάση)
    Empty,   // Η θέση αγοράστηκε (φαίνεται η βάση, αλλά είναι άδεια)
    Active,  // Έχει ενεργή ασφάλεια
    Burned   // Η ασφάλεια κάηκε (clickable για αφαίρεση)
}

public partial class FuseSlot : TextureRect
{
    [Export] public Texture2D ActiveFuseTexture;
    [Export] public Texture2D BurnedFuseTexture;
    [Export] public double FuseBackupDuration = 20.0;

    public event Action OnFuseCleared;

    private TextureButton _fuseBulb;

    public SlotCondition CurrentCondition { get; private set; } = SlotCondition.Locked;

    public override void _Ready()
    {
        // Βρίσκουμε το παιδί με το ακριβές όνομα που έχεις στη σκηνή
        _fuseBulb = GetNode<TextureButton>("FuseBuld");
        _fuseBulb.Pressed += HandleFuseClick;
        _fuseBulb.IgnoreTextureSize = true;
        _fuseBulb.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        SetCondition(SlotCondition.Locked);
    }

    public void UnlockSlot()
    {
        if (CurrentCondition == SlotCondition.Locked)
        {
            SetCondition(SlotCondition.Empty);
        }
    }

    public void InstallFuse()
    {
        if (CurrentCondition == SlotCondition.Empty)
        {
            SetCondition(SlotCondition.Active);
        }
    }

    public double BurnFuse()
    {
        if (CurrentCondition == SlotCondition.Active)
        {
            SetCondition(SlotCondition.Burned);
            return FuseBackupDuration;
        }
        return 0;
    }

    private void HandleFuseClick()
    {
        // Όταν πατάς την καμένη ασφάλεια, φεύγει και η θέση αδειάζει
        if (CurrentCondition == SlotCondition.Burned)
        {
            SetCondition(SlotCondition.Empty);
            OnFuseCleared?.Invoke();
        }
    }

    private void SetCondition(SlotCondition condition)
    {
        CurrentCondition = condition;

        switch (condition)
        {
            case SlotCondition.Locked:
                Visible = false;            // Κρύβεται ολόκληρη η βάση
                _fuseBulb.Visible = false;
                _fuseBulb.Disabled = true;
                break;

            case SlotCondition.Empty:
                Visible = true;             // Εμφανίζεται η βάση
                _fuseBulb.Visible = false;  // Δεν υπάρχει λάμπα μέσα
                _fuseBulb.Disabled = true;
                break;

            case SlotCondition.Active:
                Visible = true;
                _fuseBulb.TextureNormal = ActiveFuseTexture;
                _fuseBulb.Visible = true;
                _fuseBulb.Disabled = true;  // Δεν πατιέται όσο λειτουργεί
                break;

            case SlotCondition.Burned:
                Visible = true;
                _fuseBulb.TextureNormal = BurnedFuseTexture;
                _fuseBulb.Visible = true;
                _fuseBulb.Disabled = false; // Ενεργό για αφαίρεση με κλικ
                break;
        }
    }
}