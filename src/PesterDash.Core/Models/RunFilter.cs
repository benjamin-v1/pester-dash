namespace PesterDash.Core.Models;

/// <summary>Optional filters applied to a Pester run.</summary>
public sealed class RunFilter
{
    /// <summary>When set, only these test file paths are executed.</summary>
    public IReadOnlyList<string> TestFiles { get; init; } = [];

    /// <summary>Pester <c>Filter.FullName</c> wildcard patterns.</summary>
    public IReadOnlyList<string> FullNameFilters { get; init; } = [];

    public bool HasFileFilter => TestFiles.Count > 0;

    public bool HasNameFilter => FullNameFilters.Count > 0;

    public bool IsActive => HasFileFilter || HasNameFilter;

    public string Describe()
    {
        if (!IsActive)
        {
            return "scope";
        }

        if (HasFileFilter && !HasNameFilter)
        {
            return TestFiles.Count == 1
                ? Path.GetFileName(TestFiles[0])
                : $"{TestFiles.Count} files";
        }

        if (HasNameFilter)
        {
            return FullNameFilters.Count == 1
                ? FullNameFilters[0]
                : $"{FullNameFilters.Count} name filters";
        }

        return "filtered run";
    }
}
