using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;

namespace DoctorRx.Tests;

public class RenderingSpikeFontTests
{
    private readonly ITestOutputHelper _output;

    public RenderingSpikeFontTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public record FontCoverageReport(
        string FontFamilyName,
        bool IsInstalled,
        int TotalCharactersTested,
        int SupportedCount,
        int MissingCount,
        List<string> MissingDescriptions
    );

    [Fact]
    public void VerifyCandidateFonts_UrduAndLatinGlyphCoverage()
    {
        // 39 standard Urdu alphabet letters
        var urduLetters = new (int Code, string Name)[]
        {
            (0x0627, "Alif (ا)"),
            (0x0628, "Bay (ب)"),
            (0x067E, "Pay (پ)"),
            (0x062A, "Tay (ت)"),
            (0x0679, "Ttay (ٹ)"),
            (0x062B, "Say (ث)"),
            (0x062C, "Jeem (ج)"),
            (0x0686, "Chay (چ)"),
            (0x062D, "Bari Hay (ح)"),
            (0x062E, "Khay (خ)"),
            (0x062F, "Daal (د)"),
            (0x0688, "Ddaal (ڈ)"),
            (0x0630, "Zaal (ذ)"),
            (0x0631, "Ray (ر)"),
            (0x0691, "Rray (ڑ)"),
            (0x0632, "Zay (ز)"),
            (0x0698, "Zhay (ژ)"),
            (0x0633, "Seen (س)"),
            (0x0634, "Sheen (ش)"),
            (0x0635, "Swaad (ص)"),
            (0x0636, "Zwaad (ض)"),
            (0x0637, "Toyen (ط)"),
            (0x0638, "Zoyen (ظ)"),
            (0x0639, "Ain (ع)"),
            (0x063A, "Ghain (غ)"),
            (0x0641, "Fay (ف)"),
            (0x0642, "Qaaf (ق)"),
            (0x06A9, "Kaaf (ک)"),
            (0x06AF, "Gaaf (گ)"),
            (0x0644, "Laam (ل)"),
            (0x0645, "Meem (م)"),
            (0x0646, "Noon (ن)"),
            (0x06BA, "Noon Ghunna (ں)"),
            (0x0648, "Wao (و)"),
            (0x06C1, "Choti Hay (ہ)"),
            (0x06BE, "Do Chashmi Hay (ھ)"),
            (0x0621, "Hamza (ء)"),
            (0x06CC, "Choti Yay (ی)"),
            (0x06D2, "Bari Yay (ے)")
        };

        // 10 Urdu / Extended Arabic-Indic digits (U+06F0 - U+06F9)
        var urduDigits = Enumerable.Range(0x06F0, 10)
            .Select(c => (Code: c, Name: $"Urdu Digit {(char)c} (U+{c:X4})"))
            .ToArray();

        // 5 Urdu punctuation marks
        var urduPunctuation = new (int Code, string Name)[]
        {
            (0x060C, "Urdu Comma (،)"),
            (0x061B, "Urdu Semicolon (؛)"),
            (0x06D4, "Urdu Full Stop / Khatma (۔)"),
            (0x061F, "Urdu Question Mark (؟)"),
            (0x066A, "Urdu Percent Sign (٪)")
        };

        // Standard Latin ASCII (A-Z, a-z, 0-9, common punctuation)
        var latinChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;/-()"
            .Select(c => (Code: (int)c, Name: $"ASCII '{c}' (U+{(int)c:X4})"))
            .ToArray();

        var testSet = urduLetters
            .Concat(urduDigits)
            .Concat(urduPunctuation)
            .Concat(latinChars)
            .ToArray();

        var candidateFonts = new[]
        {
            "Segoe UI",
            "Urdu Typesetting",
            "Tahoma",
            "Arial",
            "Calibri",
            "Times New Roman",
            "Microsoft Sans Serif",
            "Noto Nastaliq Urdu",
            "Jameel Noori Nastaliq"
        };

        var reports = new List<FontCoverageReport>();

        foreach (var fontName in candidateFonts)
        {
            var fontFamily = new FontFamily(fontName);
            GlyphTypeface? glyphTypeface = null;

            foreach (var tf in fontFamily.GetTypefaces())
            {
                if (tf.TryGetGlyphTypeface(out var gtf))
                {
                    glyphTypeface = gtf;
                    break;
                }
            }

            if (glyphTypeface == null)
            {
                reports.Add(new FontCoverageReport(fontName, false, testSet.Length, 0, testSet.Length, new List<string> { "Font not installed or has no accessible GlyphTypeface" }));
                continue;
            }

            var map = glyphTypeface.CharacterToGlyphMap;
            var missing = new List<string>();

            foreach (var item in testSet)
            {
                if (!map.ContainsKey(item.Code))
                {
                    missing.Add(item.Name);
                }
            }

            reports.Add(new FontCoverageReport(
                fontName,
                true,
                testSet.Length,
                testSet.Length - missing.Count,
                missing.Count,
                missing
            ));
        }

        _output.WriteLine("=== CANDIDATE FONT GLYPH COVERAGE RESULTS ===");
        foreach (var r in reports)
        {
            _output.WriteLine($"Font: {r.FontFamilyName,-22} | Installed: {r.IsInstalled,-5} | Tested: {r.TotalCharactersTested} | Supported: {r.SupportedCount} | Missing: {r.MissingCount}");
            if (r.MissingCount > 0 && r.MissingCount <= 20)
            {
                _output.WriteLine($"   Missing: {string.Join(", ", r.MissingDescriptions)}");
            }
        }

        // Segoe UI and Arial are present on clean Windows
        var segoe = reports.FirstOrDefault(r => r.FontFamilyName == "Segoe UI");
        Assert.NotNull(segoe);
        Assert.True(segoe.IsInstalled);

        // At least one font must have complete 100% coverage
        var fullCoverageFont = reports.FirstOrDefault(r => r.IsInstalled && r.MissingCount == 0);
        Assert.NotNull(fullCoverageFont);
    }
}
