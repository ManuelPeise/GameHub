using System.IO.Compression;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>
/// Expected remaining points under optimal play for every game situation that matters for decisions:
/// which categories are filled (13 bits), the upper section sum capped at 63, and whether the Kniffel box
/// holds 50 (further Kniffel bonuses). Computed once by <see cref="KniffelValueTableGenerator"/> and shipped as
/// embedded resource (gzip, values × 100 as UInt16).
/// </summary>
public static class KniffelValueTable
{
    public const string ResourceName = "Logic.Kniffel.Ai.kniffel-values.bin.gz";
    public const int StateCount = 8192 * 64 * 2;
    public const double Scale = 100d;

    private static readonly Lazy<float[]> Values = new(Load);

    /// <summary>The raw table (index see <see cref="Index"/>).</summary>
    internal static float[] Table => Values.Value;

    public static int Index(int filledMask, int upperSum, bool kniffelBonusEligible) =>
        (((filledMask * 64) + Math.Min(upperSum, KniffelScoring.UpperBonusThreshold)) * 2) + (kniffelBonusEligible ? 1 : 0);

    /// <summary>Expected points still to come from this situation on (0 when the sheet is full).</summary>
    public static double Value(int filledMask, int upperSum, bool kniffelBonusEligible) =>
        filledMask == (1 << KniffelRules.CategoryCount) - 1 ? 0 : Values.Value[Index(filledMask, upperSum, kniffelBonusEligible)];

    public static int FilledMask(KniffelSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var mask = 0;
        for (var category = 0; category < KniffelRules.CategoryCount; category++)
        {
            if (sheet.Scores[category] is not null)
            {
                mask |= 1 << category;
            }
        }

        return mask;
    }

    /// <summary>Writes a table in the resource format (used by the generator test).</summary>
    public static void Write(float[] values, Stream target)
    {
        ArgumentNullException.ThrowIfNull(values);
        using var gzip = new GZipStream(target, CompressionLevel.SmallestSize, leaveOpen: true);
        var buffer = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            var scaled = (ushort)Math.Clamp(Math.Round(values[i] * Scale), 0, ushort.MaxValue);
            buffer[2 * i] = (byte)(scaled & 0xFF);
            buffer[(2 * i) + 1] = (byte)(scaled >> 8);
        }

        gzip.Write(buffer);
    }

    private static float[] Load()
    {
        using var resource = typeof(KniffelValueTable).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        var buffer = new byte[StateCount * 2];
        gzip.ReadExactly(buffer);

        var values = new float[StateCount];
        for (var i = 0; i < StateCount; i++)
        {
            values[i] = (float)((buffer[2 * i] | (buffer[(2 * i) + 1] << 8)) / Scale);
        }

        return values;
    }
}
