using FluentAssertions;
using Footing.Client.Services;
using Xunit;

namespace Footing.Tests.Unit.Services;

public class MimeTypeTests
{
    [Fact]
    public void ExcelXlsxFile_HasCorrectMimeType() =>
        MimeType.ExcelXlsxFile.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
}
