namespace Orts.Menu
{
    #region Menu_Selection enum
    public enum MenuSelectionIndex
    {
        // Base items
        Folder = 0,
        Route = 1,
        // Activity mode items
        Activity = 2,
        Locomotive = 3,
        Consist = 4,
        Path = 5,
        Time = 6,
        // Timetable mode items
        TimetableSet = Activity, //2,
        Timetable = Locomotive,//3,
        Train = Consist, //4,
        Day = Path, //5,
        // Shared items
        Season = 7,
        Weather = 8,
    }
    #endregion
}
