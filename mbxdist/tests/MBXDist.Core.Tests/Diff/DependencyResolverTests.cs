using MBXDist.Core.Diff;
using MBXDist.Core.Model;

namespace MBXDist.Core.Tests.Diff;

public class DependencyResolverTests
{
    private static Manifest M(string id, params (string dep, string min)[] deps)
    {
        var m = new Manifest { Id = id, Version = "1.0" };
        foreach (var (dep, min) in deps) m.Dependencies.Add(new Dependency { Id = dep, MinVersion = min });
        return m;
    }

    [Fact]
    public void Root_with_no_deps_returns_just_itself()
    {
        var manifests = new Dictionary<string, Manifest> { ["a"] = M("a") };
        var r = new DependencyResolver(id => manifests[id]);
        var closure = r.ResolveClosure(new[] { "a" }, new LocalState());
        Assert.Equal(new[] { "a" }, closure);
    }

    [Fact]
    public void Missing_dependency_is_pulled_in()
    {
        var manifests = new Dictionary<string, Manifest> { ["a"] = M("a", ("b", "1.0")), ["b"] = M("b") };
        var r = new DependencyResolver(id => manifests[id]);
        var closure = r.ResolveClosure(new[] { "a" }, new LocalState());
        Assert.Contains("a", closure);
        Assert.Contains("b", closure);
    }

    [Fact]
    public void Satisfied_dependency_is_skipped()
    {
        var manifests = new Dictionary<string, Manifest> { ["a"] = M("a", ("b", "1.0")), ["b"] = M("b") };
        var state = new LocalState();
        state.Packages["b"] = new PackageState { Version = "1.0" }; // satisfies min 1.0
        var r = new DependencyResolver(id => manifests[id]);
        var closure = r.ResolveClosure(new[] { "a" }, state);
        Assert.Contains("a", closure);
        Assert.DoesNotContain("b", closure);
    }

    [Fact]
    public void Cycle_is_rejected_before_any_installation()
    {
        var manifests = new Dictionary<string, Manifest> { ["a"] = M("a", ("b", "1.0")), ["b"] = M("b", ("a", "1.0")) };
        var r = new DependencyResolver(id => manifests[id]);
        Assert.Throws<InvalidDataException>(() => r.ResolveClosure(new[] { "a" }, new LocalState()));
    }
}
