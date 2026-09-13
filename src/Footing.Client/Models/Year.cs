namespace Footing.Models;

public static class Year
{
    public const decimal DaysPerWeek = 7m;

    // The average Gregorian year: 97 leap days every 400 years, so 365 + 97/400 days.
    public const decimal Days = 365.2425m;

    public const decimal Weeks = Days / DaysPerWeek;
}
