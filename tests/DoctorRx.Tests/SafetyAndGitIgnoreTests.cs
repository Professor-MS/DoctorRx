using System;
using System.IO;
using System.Linq;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using Xunit;

namespace DoctorRx.Tests;

public class SafetyAndGitIgnoreTests
{
    [Fact]
    public void GitIgnore_ContainsRules_ForBinObjVsAndLargeMedia()
    {
        // Locate .gitignore
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? gitIgnorePath = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".gitignore");
            if (File.Exists(candidate))
            {
                gitIgnorePath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(gitIgnorePath);
        Assert.True(File.Exists(gitIgnorePath), $"Could not find .gitignore at {gitIgnorePath}");

        var content = File.ReadAllText(gitIgnorePath);

        // Verify rules
        Assert.Contains("[Bb]in/", content);
        Assert.Contains("[Oo]bj/", content);
        Assert.Contains(".vs/", content);
        Assert.Contains("*.gif", content);
        Assert.Contains("wallpaper", content);
        Assert.Contains("Professor_AI_JARVIS_Smooth_Infinite_Loop.gif", content);
    }

    [Fact]
    public void DefaultInvestigationCatalogue_EveryEntry_HasNullDefaultInstructions()
    {
        Assert.NotEmpty(DefaultInvestigationCatalogue.InitialEntries);

        foreach (var entry in DefaultInvestigationCatalogue.InitialEntries)
        {
            Assert.Null(entry.DefaultInstructions);
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Category));
        }
    }

    [Fact]
    public void MedicineDto_NeverAuthorsClinicalIndications_WhenDescriptionIsNull()
    {
        var med = new MedicineDto(1, "Panadol", "Paracetamol", "Tablet", "500 mg", true, 0, null);
        Assert.Equal(string.Empty, med.DescriptionDisplay);

        var genericMed = new MedicineDto(2, "Acyclovir", "Acyclovir", "Tablet", "400 mg", true, 0, null);
        Assert.Equal(string.Empty, genericMed.DescriptionDisplay);
        Assert.Equal(string.Empty, genericMed.GenericFormulaDisplay);
    }
}

