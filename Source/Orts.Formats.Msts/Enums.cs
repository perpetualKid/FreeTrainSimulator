namespace Orts.Formats.Msts
{
    public enum EventType
    {
        AllStops = 0,
        AssembleTrain,
        AssembleTrainAtLocation,
        DropOffWagonsAtLocation,
        PickUpPassengers,
        PickUpWagons,
        ReachSpeed
    }

    public enum ActivityMode
    {
        IntroductoryTrainRide = 0,
        Player = 2,
        Tutorial = 3,
    }

    public enum SeasonType
    {
        Spring = 0,
        Summer,
        Autumn,
        Winter
    }

    public enum WeatherType
    {
        Clear = 0, Snow, Rain
    }

    public enum Difficulty
    {
        Easy = 0, Medium, Hard
    }

}
