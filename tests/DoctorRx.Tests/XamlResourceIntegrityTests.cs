using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using Xunit;

namespace DoctorRx.Tests;

public class XamlResourceIntegrityTests
{
    private static readonly Regex StaticOrDynamicResourceRegex = new(
        @"\{(StaticResource|DynamicResource)\s+([A-Za-z0-9_.-]+)\}",
        RegexOptions.Compiled);

    private static string GetPresentationDirectory()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "DoctorRx.sln")))
        {
            currentDir = currentDir.Parent;
        }
        string projectRoot = currentDir?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine(projectRoot, "src", "DoctorRx.Presentation");
    }

    [Fact]
    public void AppResourceDictionaries_LoadSuccessfullyInWpfContext()
    {
        StaTestRunner.Run(() =>
        {
            var colors = new ResourceDictionary
            {
                Source = new Uri("/DoctorRx.Presentation;component/Theme/Colors.xaml", UriKind.RelativeOrAbsolute)
            };
            Assert.NotEmpty(colors.Keys);

            var icons = new ResourceDictionary
            {
                Source = new Uri("/DoctorRx.Presentation;component/Theme/Icons.xaml", UriKind.RelativeOrAbsolute)
            };
            Assert.NotEmpty(icons.Keys);

            var styles = new ResourceDictionary
            {
                Source = new Uri("/DoctorRx.Presentation;component/Theme/Styles.xaml", UriKind.RelativeOrAbsolute)
            };
            Assert.NotEmpty(styles.Keys);
        });
    }

    [Fact]
    public void AllXamlFiles_HaveAllStaticAndDynamicResourcesResolved()
    {
        string presentationDir = GetPresentationDirectory();
        var xamlFiles = Directory.GetFiles(presentationDir, "*.xaml", SearchOption.AllDirectories);

        Assert.NotEmpty(xamlFiles);

        // 1. Collect global application keys (from Colors, Icons, Styles, App.xaml)
        var globalKeys = new HashSet<string>(StringComparer.Ordinal);

        // Known converters registered in App.xaml
        globalKeys.Add("BoolToVisConverter");
        globalKeys.Add("InverseBoolToVisConverter");
        globalKeys.Add("CountToVisConverter");
        globalKeys.Add("FollowUpModeDisplayConverter");
        globalKeys.Add("InverseBoolConverter");
        globalKeys.Add("FileSizeConverter");
        globalKeys.Add("CountToZeroVisConverter");

        // Parse App.xaml, Colors.xaml, Icons.xaml, Styles.xaml for defined keys
        string[] themeFiles = ["Colors.xaml", "Icons.xaml", "Styles.xaml", "App.xaml"];
        foreach (var themeFileName in themeFiles)
        {
            var matchFile = xamlFiles.FirstOrDefault(f => Path.GetFileName(f).Equals(themeFileName, StringComparison.OrdinalIgnoreCase));
            if (matchFile != null)
            {
                CollectDefinedKeys(matchFile, globalKeys);
            }
        }

        var missingResourceErrors = new List<string>();

        foreach (var xamlFile in xamlFiles)
        {
            // Collect keys defined locally in this file
            var fileLocalKeys = new HashSet<string>(globalKeys, StringComparer.Ordinal);
            CollectDefinedKeys(xamlFile, fileLocalKeys);

            string content = File.ReadAllText(xamlFile);
            var matches = StaticOrDynamicResourceRegex.Matches(content);

            foreach (Match match in matches)
            {
                string key = match.Groups[2].Value;

                // Ignore standard WPF / system prefix references if any
                if (key.StartsWith("SystemColors.", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("SystemFonts.", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("x:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!fileLocalKeys.Contains(key))
                {
                    missingResourceErrors.Add($"[{Path.GetFileName(xamlFile)}] Missing resource key: '{key}' referenced in {xamlFile}");
                }
            }
        }

        Assert.True(missingResourceErrors.Count == 0,
            $"Found {missingResourceErrors.Count} unresolved XAML resource reference(s):\n" +
            string.Join("\n", missingResourceErrors));
    }

    private static void CollectDefinedKeys(string filePath, HashSet<string> keySet)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            foreach (var elem in doc.Descendants())
            {
                foreach (var attr in elem.Attributes())
                {
                    if (attr.Name.LocalName == "Key")
                    {
                        keySet.Add(attr.Value);
                    }
                }
            }
        }
        catch
        {
            // If XML parsing fails on specialized XAML markup, fall back to regex
            string text = File.ReadAllText(filePath);
            var keyMatches = Regex.Matches(text, @"x:Key\s*=\s*""([^""]+)""");
            foreach (Match m in keyMatches)
            {
                keySet.Add(m.Groups[1].Value);
            }
        }
    }
}
