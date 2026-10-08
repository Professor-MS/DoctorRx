using System;
using DoctorRx.Application.Common;
using Xunit;

namespace DoctorRx.Tests;

public class SearchNormalizerTests
{
    [Fact]
    public void Normalize_UrduAndArabicVariants_MapsToStandardUrduLetters()
    {
        // ي (Arabic Yeh U+064A) and ى (Alef Maksura U+0649) -> ی (Urdu Yeh U+06CC)
        // ك (Arabic Kaf U+0643) -> ک (Urdu Keheh U+06A9)
        // ة (Teh Marbuta U+0629) -> ہ (Urdu Heh Gol U+06C1)
        var input = "علي ى كمال فاطمة";
        var expected = "علی ی کمال فاطمہ";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_ArabicHarakatAndDiacritics_StripsAllDiacritics()
    {
        // مَحْمُودٌ (Mahmood with Fatha, Sukun, Damma, Tanwin Damm) -> محمود
        var input = "مَحْمُودٌ بِسْمِ اللهِ الرَّحْمٰنِ";
        var expected = "محمود بسم الله الرحمن";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_TatweelKashida_StripsTatweel()
    {
        // صـــــاحب (Sahib with Kashida) -> صاحب
        var input = "صـــــاحب کـــــتاب";
        var expected = "صاحب کتاب";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_ZeroWidthCharacters_RemovesAllZeroWidthChars()
    {
        // Contains ZWNJ (\u200C), ZWJ (\u200D), BOM (\uFEFF)
        var input = "بے\u200Cچین\u200D\uFEFF مریض";
        var expected = "بےچین مریض";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_ArabicIndicAndPersianDigits_ConvertsToAsciiDigits()
    {
        // ٠١٢٣٤٥٦٧٨٩ (Arabic-Indic) and ۰۱۲۳۴۵۶۷۸۹ (Extended Urdu/Persian)
        var input = "فون ٠٣٠٠١٢٣٤٥٦٧ اور ۰۳۲۱۷۶۵۴۳۲۱";
        var expected = "فون 03001234567 اور 03217654321";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_MixedEnglishAndUrdu_CollapsesWhitespaceAndNormalizesCasing()
    {
        var input = "   Dr.   Tariq   طـــــارق   100   MG   ";
        var expected = "dr. tariq طارق 100 mg";

        var actual = SearchNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizePhoneDigits_ExtractsNumericDigitsWithEasternDigitConversion()
    {
        var phone = "+92 (۳۰۰) ۱۲۳-۴۵۶۷";
        var expected = "923001234567";

        var actual = SearchNormalizer.NormalizePhoneDigits(phone);

        Assert.Equal(expected, actual);
    }
}
