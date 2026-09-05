using Xunit.Abstractions;

namespace Footing.Tests.E2E;

/// <summary>
/// IXunitSerializable is load-bearing: without it xunit cannot pre-enumerate the theory data
/// and every test name degrades to the type name.
/// </summary>
public sealed class Viewport : IXunitSerializable
{
    public string Name { get; private set; } = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool FullAssertions { get; private set; }

    // Required by IXunitSerializable.
    public Viewport()
    {
    }

    public Viewport(string name, int width, int height, bool fullAssertions)
    {
        Name = name;
        Width = width;
        Height = height;
        FullAssertions = fullAssertions;
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        Name = info.GetValue<string>(nameof(Name));
        Width = info.GetValue<int>(nameof(Width));
        Height = info.GetValue<int>(nameof(Height));
        FullAssertions = info.GetValue<bool>(nameof(FullAssertions));
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Name), Name);
        info.AddValue(nameof(Width), Width);
        info.AddValue(nameof(Height), Height);
        info.AddValue(nameof(FullAssertions), FullAssertions);
    }

    public override string ToString() => $"{Name} {Width}x{Height}";
}

public static class Viewports
{
    public static readonly Viewport Mobile = new("Mobile", 375, 667, fullAssertions: true);
    public static readonly Viewport MobileFloor = new("MobileFloor", 320, 568, fullAssertions: false);
    public static readonly Viewport Tablet = new("Tablet", 768, 1024, fullAssertions: true);
    public static readonly Viewport Desktop = new("Desktop", 1280, 800, fullAssertions: true);

    private static readonly Viewport[] AllViewports = [Mobile, MobileFloor, Tablet, Desktop];
    private static readonly string[] Pages = ["/", "/find-my-footing/"];

    public static IEnumerable<object[]> All =>
        AllViewports.Select(viewport => new object[] { viewport });

    public static IEnumerable<object[]> Full =>
        AllViewports.Where(viewport => viewport.FullAssertions).Select(viewport => new object[] { viewport });

    /// <summary>
    /// The two narrow viewports. Split out because the tool page has a reproduced overflow
    /// defect at exactly these widths -- see FindMyFootingPageTests -- which W-05 rules on
    /// and W-06 fixes. Once it is fixed these fold back into <see cref="All"/>.
    /// </summary>
    public static IEnumerable<object[]> AtMostMobile =>
        new[] { MobileFloor, Mobile }.Select(viewport => new object[] { viewport });

    /// <summary>The complement of <see cref="AtMostMobile"/>.</summary>
    public static IEnumerable<object[]> AtLeastTablet =>
        new[] { Tablet, Desktop }.Select(viewport => new object[] { viewport });

    /// <summary>
    /// Desktop and Mobile only. Used where the two layouts genuinely differ but the middle
    /// of the range adds nothing -- the Excel export button, whose reachability differs
    /// between the wide and narrow arrangements.
    /// </summary>
    public static IEnumerable<object[]> DesktopAndMobile =>
        new[] { Mobile, Desktop }.Select(viewport => new object[] { viewport });

    public static IEnumerable<object[]> AllByPage =>
        from viewport in AllViewports
        from page in Pages
        select new object[] { viewport, page };
}
