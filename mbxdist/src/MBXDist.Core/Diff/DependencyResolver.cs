using MBXDist.Core.Model;
using MBXDist.Core.Versioning;

namespace MBXDist.Core.Diff;

/// <summary>Resolves the dependency closure of a set of root package ids, skipping deps already satisfied by local state.</summary>
public sealed class DependencyResolver
{
    private readonly Func<string, Manifest> _manifestOf;

    public DependencyResolver(Func<string, Manifest> manifestOf) => _manifestOf = manifestOf;

    public IReadOnlyList<string> ResolveClosure(IEnumerable<string> roots, LocalState state)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();
        var stack = new Stack<string>(roots);

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!seen.Add(id)) continue;   // cycle / duplicate guard
            result.Add(id);

            foreach (var dep in _manifestOf(id).Dependencies)
            {
                bool satisfied = state.Packages.TryGetValue(dep.Id, out var dps) && dps is not null
                    && PackageVersion.Parse(dps.Version).Satisfies(PackageVersion.Parse(dep.MinVersion));
                if (!satisfied && !seen.Contains(dep.Id))
                    stack.Push(dep.Id);
            }
        }
        return result;
    }
}
