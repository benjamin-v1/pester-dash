using System.Globalization;
using System.Xml.Linq;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Core.Parsing;

/// <summary>Parses JaCoCo and Cobertura coverage reports.</summary>
public sealed class CoverageParser : ICoverageParser
{
    /// <inheritdoc />
    public async Task<CoverageResult> ParseAsync(string coverageFile, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(coverageFile))
        {
            throw new FileNotFoundException($"Coverage file not found: {coverageFile}", coverageFile);
        }

        await using var stream = File.OpenRead(coverageFile);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        var root = document.Root ?? throw new InvalidOperationException("Coverage file has no root element.");

        return root.Name.LocalName.Equals("coverage", StringComparison.OrdinalIgnoreCase)
            ? ParseCobertura(root, coverageFile)
            : ParseJaCoCo(root, coverageFile);
    }

    private static CoverageResult ParseJaCoCo(XElement root, string coverageFile)
    {
        var lineCounter = ReadCounter(root, "LINE");
        var branchCounter = ReadCounter(root, "BRANCH");
        var files = root
            .Descendants("sourcefile")
            .Select(ParseJaCoCoSourceFile)
            .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CoverageResult
        {
            ReportFile = coverageFile,
            CoveredLines = lineCounter.covered,
            TotalLines = lineCounter.missed + lineCounter.covered,
            CoveredBranches = branchCounter.covered,
            TotalBranches = branchCounter.missed + branchCounter.covered,
            Files = files,
        };
    }

    private static CoverageFileEntry ParseJaCoCoSourceFile(XElement element)
    {
        var lineCounter = ReadCounter(element, "LINE");
        var branchCounter = ReadCounter(element, "BRANCH");
        var lines = element
            .Elements("line")
            .Select(ParseJaCoCoLine)
            .OrderBy(line => line.LineNumber)
            .ToList();

        return new CoverageFileEntry
        {
            FileName = (string?)element.Attribute("name") ?? "unknown",
            CoveredLines = lineCounter.covered,
            TotalLines = lineCounter.missed + lineCounter.covered,
            CoveredBranches = branchCounter.covered,
            TotalBranches = branchCounter.missed + branchCounter.covered,
            Lines = lines,
        };
    }

    private static CoverageLineEntry ParseJaCoCoLine(XElement element)
    {
        var missed = ReadIntAttribute(element, "mi") ?? 0;
        var covered = ReadIntAttribute(element, "ci") ?? 0;

        var status = missed == 0 && covered == 0
            ? CoverageLineStatus.NotExecutable
            : covered > 0 && missed == 0
                ? CoverageLineStatus.Covered
                : covered > 0
                    ? CoverageLineStatus.Partial
                    : CoverageLineStatus.Uncovered;

        return new CoverageLineEntry
        {
            LineNumber = ReadIntAttribute(element, "nr") ?? 0,
            Status = status,
            HitCount = covered,
        };
    }

    private static CoverageResult ParseCobertura(XElement root, string coverageFile)
    {
        var classFiles = root
            .Descendants("class")
            .Select(ParseCoberturaClass)
            .ToList();

        var files = classFiles
            .GroupBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeCoverageFiles(group))
            .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linesCovered = ReadIntAttribute(root, "lines-covered") ?? files.Sum(file => file.CoveredLines);
        var linesValid = ReadIntAttribute(root, "lines-valid") ?? files.Sum(file => file.TotalLines);
        var branchesCovered = ReadIntAttribute(root, "branches-covered") ?? files.Sum(file => file.CoveredBranches);
        var branchesValid = ReadIntAttribute(root, "branches-valid") ?? files.Sum(file => file.TotalBranches);

        return new CoverageResult
        {
            ReportFile = coverageFile,
            CoveredLines = linesCovered,
            TotalLines = linesValid,
            CoveredBranches = branchesCovered,
            TotalBranches = branchesValid,
            Files = files,
        };
    }

    private static CoverageFileEntry ParseCoberturaClass(XElement element)
    {
        var fileName = (string?)element.Attribute("filename") ?? "unknown";
        var lineElements = element.Elements("lines").Elements("line").ToList();
        var lines = lineElements
            .Select(ParseCoberturaLine)
            .OrderBy(line => line.LineNumber)
            .ToList();
        var covered = lines.Count(line => line.Status == CoverageLineStatus.Covered);

        return new CoverageFileEntry
        {
            FileName = fileName,
            CoveredLines = covered,
            TotalLines = lines.Count,
            CoveredBranches = 0,
            TotalBranches = 0,
            Lines = lines,
        };
    }

    private static CoverageLineEntry ParseCoberturaLine(XElement element)
    {
        var hits = ReadIntAttribute(element, "hits") ?? 0;
        return new CoverageLineEntry
        {
            LineNumber = ReadIntAttribute(element, "number") ?? 0,
            Status = hits > 0 ? CoverageLineStatus.Covered : CoverageLineStatus.Uncovered,
            HitCount = hits,
        };
    }

    private static CoverageFileEntry MergeCoverageFiles(IEnumerable<CoverageFileEntry> files)
    {
        var list = files.ToList();
        var mergedLines = list
            .SelectMany(file => file.Lines)
            .GroupBy(line => line.LineNumber)
            .Select(group => group.First())
            .OrderBy(line => line.LineNumber)
            .ToList();

        return new CoverageFileEntry
        {
            FileName = list[0].FileName,
            CoveredLines = list.Sum(file => file.CoveredLines),
            TotalLines = list.Sum(file => file.TotalLines),
            CoveredBranches = list.Sum(file => file.CoveredBranches),
            TotalBranches = list.Sum(file => file.TotalBranches),
            Lines = mergedLines,
        };
    }

    private static (int missed, int covered) ReadCounter(XElement root, string type)
    {
        var counter = root.Elements("counter")
            .FirstOrDefault(element =>
                string.Equals((string?)element.Attribute("type"), type, StringComparison.OrdinalIgnoreCase));

        return (
            ReadIntAttribute(counter, "missed") ?? 0,
            ReadIntAttribute(counter, "covered") ?? 0);
    }

    private static int? ReadIntAttribute(XElement? element, string name)
    {
        var value = (string?)element?.Attribute(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
