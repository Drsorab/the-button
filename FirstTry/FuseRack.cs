using Godot;
using System.Collections.Generic;

public partial class FuseRack : Control
{
    private readonly List<FuseSlot> _slots = new();

    public override void _Ready()
    {
        for (int i = 0; i < 4; i++)
        {
            FuseSlot slot = GetNodeOrNull<FuseSlot>($"Slot{i}");
            if (slot != null)
            {
                _slots.Add(slot);
            }
        }
    }

    // 1. Αγορά επόμενης θέσης (Socket)
    public bool TryBuySlot()
    {
        FuseSlot lockedSlot = _slots.Find(s => s.CurrentCondition == SlotCondition.Locked);
        if (lockedSlot != null)
        {
            lockedSlot.UnlockSlot();
            return true;
        }
        GD.Print("Όλες οι θέσεις (3/3) είναι ήδη ξεκλείδωτες.");
        return false;
    }

    // 2. Αγορά ασφάλειας σε διαθέσιμη άδεια θέση
    public bool TryBuyFuse()
    {
        FuseSlot emptySlot = _slots.Find(s => s.CurrentCondition == SlotCondition.Empty);
        if (emptySlot != null)
        {
            emptySlot.InstallFuse();
            return true;
        }
        GD.Print("Δεν υπάρχει άδεια ξεκλείδωτη θέση για ασφάλεια.");
        return false;
    }

    // 3. Αυτόματη χρήση/κάψιμο ασφάλειας στο 0
    public bool TryConsumeFuse(out double durationBonus)
    {
        FuseSlot activeSlot = _slots.Find(s => s.CurrentCondition == SlotCondition.Active);
        if (activeSlot != null)
        {
            durationBonus = activeSlot.BurnFuse();
            return true;
        }

        durationBonus = 0;
        return false;
    }
}