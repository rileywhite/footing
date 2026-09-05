namespace Footing.Tests.E2E;

/// <summary>
/// The tool page's localStorage contract, in the exact shape FootingAnalysisEditor itself
/// writes. Shared so a seed can only be wrong in one place.
/// </summary>
internal static class ToolStorage
{
    /// <summary>FootingAnalysisEditor.FootingAnalysisStorageKey.</summary>
    public const string AnalysisKey = "3794bdc6-f064-43e6-9a1e-8bb2c03d16cb";

    /// <summary>Written by the theme toggle, read by the &lt;head&gt; restore snippet.</summary>
    public const string ThemeKey = "ft-theme";

    /// <summary>The name of the single income entry in <see cref="OneIncomeEntry"/>.</summary>
    public const string SeededEntryName = "Salary";

    /// <summary>
    /// A returning user with one $2000/month income entry.
    ///
    /// This is NOT hand-written -- it was captured from a real write, by driving the UI to add
    /// the entry and reading the key back. Two details make a hand-written seed silently wrong,
    /// and the earlier one in this suite was:
    ///
    ///   * `Amount` is an OBJECT, not a number. MonetaryAmount is a struct whose own property
    ///     is also called Amount, so it serializes as {"Amount":2000}. A bare 2000 deserializes
    ///     to a zero amount.
    ///   * `Period` is the enum's NUMERIC value (Monthly = 4), not the name. "Monthly" does not
    ///     deserialize.
    ///
    /// Neither failure is loud: the analysis still deserializes, HasAnyEntries is still true, so
    /// the returning-user tree still renders and card-count assertions still pass -- while the
    /// entry itself carries a $0 amount and the Net Total reads $0/week. Any test asserting on
    /// amounts or entry chips against such a seed is testing nothing.
    ///
    /// WeeklyTotalMoneyFlow and HasAnyEntries appear in what the app writes but are omitted
    /// here: both are get-only computed properties, so they are written and never read back.
    /// </summary>
    public const string OneIncomeEntry = """
        {"Inflows":[{"Id":"00000000-0000-0000-0000-000000000001","Name":"Salary","Amount":{"Amount":2000},"Period":4}],"RecurringBills":[],"HouseholdBudgets":[],"PersonalBudgets":[],"EventBudgets":[]}
        """;

    public static Dictionary<string, string> ReturningUser() => new()
    {
        [AnalysisKey] = OneIncomeEntry,
    };

    public static Dictionary<string, string> ReturningUserWithTheme(string theme) => new()
    {
        [AnalysisKey] = OneIncomeEntry,
        [ThemeKey] = theme,
    };
}
