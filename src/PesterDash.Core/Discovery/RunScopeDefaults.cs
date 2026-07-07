namespace PesterDash.Core.Discovery;

/// <summary>Default run-scope selections for first-time setup and CI runs.</summary>
public static class RunScopeDefaults
{
  /// <summary>
  /// Test files selected by default in the scope picker (excludes obvious runner/helper scripts).
  /// </summary>
  public static IReadOnlyList<string> SelectDefaultTestFiles(IEnumerable<string> candidates) =>
      candidates.Where(path => !IsExcludedTestRunner(Path.GetFileName(path))).ToList();

  /// <summary>All discovered source files are selected by default.</summary>
  public static IReadOnlyList<string> SelectDefaultSourceFiles(IEnumerable<string> candidates) =>
      candidates.ToList();

  public static bool IsExcludedTestRunner(string fileName)
  {
    if (fileName.StartsWith("run-", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    if (fileName.EndsWith(".helper.ps1", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".bootstrap.ps1", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return false;
  }
}
