using System;

// Καταστάσεις βάρδιας για το ShiftClock
public enum ShiftMode
{
    SkeletonCrew,
    ActiveShift
}

// Θέσεις / επιλογές του Decompression Lever κατά την εκτόνωση
public enum VentMode_old
{
    SafeRelease,   // Επαναφορά στα 108λ, χαμηλή θερμότητα
    HeatSurge,     // 72λ, μετατροπή μπαταρίας σε μέγιστη θερμότητα
    ScrapScour,    // 45λ, συλλογή +15 scrap, μηδενική θερμότητα
    HotWeld        // 30λ, χρήση scrap για άμεση επιδιόρθωση κάσας
}