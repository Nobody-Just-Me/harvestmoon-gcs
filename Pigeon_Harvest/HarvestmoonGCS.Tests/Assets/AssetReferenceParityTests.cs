using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace HarvestmoonGCS.Tests.Assets;

public class AssetReferenceParityTests
{
    [Fact]
    public void MsAppxAssetReferences_ShouldResolveToExistingFiles_WithExactCase()
    {
        var appRoot = ResolveAppProjectRoot();
        var assetsRoot = Path.Combine(appRoot, "Assets");
        Directory.Exists(assetsRoot).Should().BeTrue("Assets folder must exist.");

        var regex = new Regex("ms-appx:///Assets/([A-Za-z0-9_./ -]+)", RegexOptions.Compiled);
        var files = Directory
            .EnumerateFiles(appRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/bin/") || normalized.Contains("/obj/") || normalized.Contains("/.sisyphus/"))
                {
                    return false;
                }

                var ext = Path.GetExtension(path);
                return string.Equals(ext, ".xaml", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var failures = new List<string>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in regex.Matches(content))
            {
                var relative = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                var name = Path.GetFileName(relative);
                if (string.IsNullOrWhiteSpace(name) || !name.Contains('.', StringComparison.Ordinal))
                {
                    continue;
                }

                var expectedPath = Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(expectedPath))
                {
                    continue;
                }

                var parent = Path.GetDirectoryName(expectedPath);
                if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
                {
                    failures.Add($"{GetRelative(file, appRoot)} -> {relative} (missing folder)");
                    continue;
                }

                var caseInsensitiveMatch = Directory
                    .EnumerateFiles(parent)
                    .FirstOrDefault(candidate =>
                        string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase));

                failures.Add(
                    caseInsensitiveMatch == null
                        ? $"{GetRelative(file, appRoot)} -> {relative} (missing file)"
                        : $"{GetRelative(file, appRoot)} -> {relative} (case mismatch, actual: {Path.GetFileName(caseInsensitiveMatch)})");
            }
        }

        failures.Should().BeEmpty("all ms-appx asset references must map to real packaged files.");
    }

    private static string ResolveAppProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "HarvestmoonGCS", "HarvestmoonGCS.csproj");
            if (File.Exists(candidate))
            {
                return Path.Combine(current.FullName, "HarvestmoonGCS");
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HarvestmoonGCS app project root.");
    }

    private static string GetRelative(string fullPath, string root)
    {
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }
}
