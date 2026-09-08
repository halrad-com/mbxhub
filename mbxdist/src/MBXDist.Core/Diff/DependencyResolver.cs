using MBXDist.Core.Model;
using MBXDist.Core.Versioning;
namespace MBXDist.Core.Diff;

/// <summary>Validates and orders an install plan with prerequisites first.</summary>
public sealed class DependencyResolver
{
    private readonly Func<string, Manifest> _manifestOf;
    public DependencyResolver(Func<string, Manifest> manifestOf) => _manifestOf = manifestOf;
    public IReadOnlyList<string> ResolveClosure(IEnumerable<string> roots, LocalState state)
    {
        var result = new List<string>();
        var requested = roots.ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>();
        var active = new HashSet<string>();
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!active.Add(id)) throw new InvalidDataException($"dependency cycle at '{id}'");
            foreach (var dep in _manifestOf(id).Dependencies)
            {
                bool satisfied = state.Packages.TryGetValue(dep.Id, out var installed)
                    && !state.InProgress.ContainsKey(dep.Id)
                    && PackageVersion.Parse(installed.Version).Satisfies(PackageVersion.Parse(dep.MinVersion));
                if (satisfied && !requested.Contains(dep.Id)) continue;
                var candidate = _manifestOf(dep.Id);
                if (!PackageVersion.Parse(candidate.Version).Satisfies(PackageVersion.Parse(dep.MinVersion)))
                    throw new InvalidDataException($"available '{dep.Id}' {candidate.Version} cannot satisfy {dep.MinVersion}");
                Visit(dep.Id);
            }
            active.Remove(id); visited.Add(id); result.Add(id);
        }
        foreach (var root in requested) Visit(root);
        return result;
    }
}
