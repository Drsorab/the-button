using Godot;
using System;
using System.Text.Json;

namespace PermafrostChieftain.Simulation
{
    public class PlayerStats
    {
        // Μόνιμα στατιστικά που αθροίζονται σε κάθε run
        public int TotalRunsPlayed { get; set; } = 0;
        public double LifetimeGeneratorUptime { get; set; } = 0.0;
        public int LifetimeWorkersLost { get; set; } = 0;

        // Τοπική αποθήκευση. Στο μέλλον εδώ θα μπει το Cloud Sync (π.χ. PlayFab/Firebase)
        private static readonly string SavePath = "user://player_stats.json";

        public static PlayerStats Instance { get; set; } = new PlayerStats();

        public static void Load()
        {
            if (!FileAccess.FileExists(SavePath))
            {
                Instance = new PlayerStats();
                Instance.Save();
                return;
            }

            try
            {
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                string jsonString = file.GetAsText();
                Instance = JsonSerializer.Deserialize<PlayerStats>(jsonString) ?? new PlayerStats();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Σφάλμα φόρτωσης στατιστικών. Δημιουργία νέων: {ex.Message}");
                Instance = new PlayerStats();
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(this, options);
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
                file.StoreString(jsonString);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Σφάλμα αποθήκευσης στατιστικών: {ex.Message}");
            }
        }
    }
}