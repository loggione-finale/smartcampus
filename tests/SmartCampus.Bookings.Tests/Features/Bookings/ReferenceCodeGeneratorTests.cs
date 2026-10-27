using System.Text.RegularExpressions;
using SmartCampus.Bookings.Api.Features.Bookings;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class ReferenceCodeGeneratorTests
{
    [Fact]
    public void Generates_a_readable_code_based_on_the_start_date()
    {
        var code = ReferenceCodeGenerator.Generate(new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc));

        Assert.Matches(new Regex("^SC-261028-[A-Z2-9]{4}$"), code);
    }

    [Fact]
    public void Generates_different_codes()
    {
        var start = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);

        var codes = Enumerable.Range(0, 50).Select(_ => ReferenceCodeGenerator.Generate(start)).ToHashSet();

        Assert.True(codes.Count > 40);
    }
}
