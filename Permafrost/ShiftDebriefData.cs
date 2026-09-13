public struct ShiftDebriefData
{
    public double ShiftDurationSeconds;
    public int CyclesCompleted;
    public float FinalIntegrity;
    public float RemainingCoalKg;
    public float RemainingBattery;
    public int ScrapSalvaged;

    // Πληθυσμιακά στοιχεία
    public int ActiveWorkforce;
    public int TotalWorkforce;
    public int SickCount;
    public int GravelySickCount;
    public int GeneralPopulation;
    public int CasualtiesThisShift;
    public float FinalEfficiency;
}