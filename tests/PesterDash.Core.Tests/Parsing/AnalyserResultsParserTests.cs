using PesterDash.Core.Parsing;

namespace PesterDash.Core.Tests.Parsing;

public class AnalyserResultsParserTests
{
    [Fact]
    public void Parse_ParsesFinding_WithNullLineAndColumn()
    {
        const string json = """
            [
              {
                "RuleName": "PSAvoidUsingWriteHost",
                "Severity": "Warning",
                "ScriptName": "C:\\repo\\script.ps1",
                "Line": null,
                "Column": null,
                "Message": "Avoid using Write-Host"
              }
            ]
            """;

        var findings = AnalyserResultsParser.Parse(json);

        Assert.Single(findings);
        Assert.Equal("PSAvoidUsingWriteHost", findings[0].RuleName);
        Assert.Equal("C:\\repo\\script.ps1", findings[0].ScriptPath);
        Assert.Equal(0, findings[0].Line);
        Assert.Equal(0, findings[0].Column);
    }

    [Fact]
    public void Parse_ParsesFinding_WithNullExtent()
    {
        const string json = """
            [
              {
                "RuleName": "PSUseBOMForUnicodeEncodedFile",
                "Severity": 1,
                "ScriptName": "LogAnalytics.psm1",
                "ScriptPath": "C:\\repo\\LogAnalytics.psm1",
                "Extent": null,
                "Line": null,
                "Column": null,
                "Message": "Missing BOM encoding"
              }
            ]
            """;

        var findings = AnalyserResultsParser.Parse(json);

        Assert.Single(findings);
        Assert.Equal("PSUseBOMForUnicodeEncodedFile", findings[0].RuleName);
        Assert.Equal("Warning", findings[0].Severity);
        Assert.Equal(0, findings[0].Line);
    }

    [Fact]
    public void Parse_ParsesRealWorldResultsFile()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "scm-data-gateway-onprem",
            ".pester-dash",
            "analyser",
            "results.json");

        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var findings = AnalyserResultsParser.Parse(json);

        Assert.NotEmpty(findings);
    }
}
