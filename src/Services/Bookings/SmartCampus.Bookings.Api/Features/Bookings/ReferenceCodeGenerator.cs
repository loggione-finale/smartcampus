using System.Security.Cryptography;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public static class ReferenceCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SuffixLength = 4;

    public static string Generate(DateTime start)
    {
        var suffix = RandomNumberGenerator.GetString(Alphabet, SuffixLength);
        return $"SC-{start:yyMMdd}-{suffix}";
    }
}
