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

    /// <summary>
    /// The five category names, in the order <c>FootingAnalysisEditor</c> renders them, and
    /// the ids their card headers carry. Shared so a test cannot assert against four.
    /// </summary>
    public static readonly string[] SectionNames =
        ["income", "recurringBills", "householdBudgets", "personalBudgets", "eventBudgets"];

    /// <summary>
    /// A returning user with one entry in EVERY category -- the state W-05 needs, because a
    /// single-category seed leaves four of the five cards summing to $0 and is not the tree a
    /// real returning user sees.
    ///
    /// Built on exactly the shape <see cref="OneIncomeEntry"/> was captured in, for the reason
    /// F-11 records: `Amount` is an object because `MonetaryAmount` wraps a property of the
    /// same name, and `Period` is the enum's numeric value (Weekly = 1, Monthly = 4,
    /// Annually = 7). Getting either wrong is silent -- the tree still renders, the card count
    /// still passes, and every amount reads $0.
    ///
    /// That silence is why <c>NarrowViewportOverflowTests.ReturningUserSeed_PopulatesEveryCategory</c>
    /// exists: it asserts each of the five headers shows a NON-zero weekly total, so a
    /// degenerate seed fails loudly here instead of quietly weakening the overflow tests that
    /// depend on it.
    ///
    /// Entry names are deliberately ordinary ("Salary", "Rent"). A long adversarial name would
    /// overflow a narrow viewport on its own and confound OQ-01's ruling with content the app
    /// never ships.
    /// </summary>
    public const string EntryInEveryCategory = """
        {"Inflows":[{"Id":"00000000-0000-0000-0000-000000000001","Name":"Salary","Amount":{"Amount":2000},"Period":4}],"RecurringBills":[{"Id":"00000000-0000-0000-0000-000000000002","Name":"Rent","Amount":{"Amount":1200},"Period":4}],"HouseholdBudgets":[{"Id":"00000000-0000-0000-0000-000000000003","Name":"Groceries","Amount":{"Amount":150},"Period":1}],"PersonalBudgets":[{"Id":"00000000-0000-0000-0000-000000000004","Name":"Lunches","Amount":{"Amount":60},"Period":1}],"EventBudgets":[{"Id":"00000000-0000-0000-0000-000000000005","Name":"Christmas","Amount":{"Amount":800},"Period":7}]}
        """;

    public static Dictionary<string, string> ReturningUser() => new()
    {
        [AnalysisKey] = OneIncomeEntry,
    };

    /// <summary>A returning user carrying <see cref="EntryInEveryCategory"/>.</summary>
    public static Dictionary<string, string> ReturningUserWithEveryCategory() => new()
    {
        [AnalysisKey] = EntryInEveryCategory,
    };

    public static Dictionary<string, string> ReturningUserWithTheme(string theme) => new()
    {
        [AnalysisKey] = OneIncomeEntry,
        [ThemeKey] = theme,
    };
}
