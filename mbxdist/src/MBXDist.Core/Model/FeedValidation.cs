using System.Text.RegularExpressions;
using MBXDist.Core.Versioning;
namespace MBXDist.Core.Model;

/// <summary>Authoring checks before filesystem work; metadata is trusted only from the signed client.</summary>
public static class FeedValidation
{
    public static void Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\z") || value.EndsWith('.'))
            throw new InvalidDataException($"invalid name '{value}'");
    }
    public static void RelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOfAny(new[] { ':', '?', '#', '%', '<', '>', '|', '"', '*', '\0' }) >= 0)
            throw new InvalidDataException($"invalid relative path '{path}'");
        foreach (var part in path.Replace('\\', '/').Split('/'))
        {
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(char.IsControl))
                throw new InvalidDataException($"invalid path segment in '{path}'");
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" || Regex.IsMatch(stem, @"\A(COM|LPT)[1-9]\z"))
                throw new InvalidDataException($"reserved filename in '{path}'");
        }
    }
    public static string UnderRoot(string root, string relative)
    {
        RelativePath(relative);
        if (!Path.IsPathFullyQualified(root)) throw new InvalidDataException("target root must be absolute");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("target escapes its root");
        for (var current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"reparse point in target path '{current}'");
        return full;
    }
    public static void Manifest(Manifest manifest)
    {
        Name(manifest.Id); PackageVersion.Parse(manifest.Version);
        if (manifest.Resources is null || manifest.Resources.Count == 0 || manifest.Dependencies is null)
            throw new InvalidDataException("manifest must contain resources and dependencies");
        var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in manifest.Resources)
        {
            Name(resource.Filename); RelativePath(resource.Filename);
            if (!filenames.Add(resource.Filename)) throw new InvalidDataException("duplicate resource filename");
            RelativePath(resource.Url); Name(resource.Target.Root); RelativePath(resource.Target.Path);
            var targetKey = resource.Target.Root + "/" + resource.Target.Path.Replace('\\', '/');
            if (!targets.Add(targetKey) || !targets.Add(targetKey + ".pending"))
                throw new InvalidDataException("duplicate resource target");
            if (resource.Sha256 is null || !Regex.IsMatch(resource.Sha256, @"\A[0-9a-fA-F]{64}\z") || resource.Sha256.All(c => c == '0'))
                throw new InvalidDataException("invalid or placeholder sha256");
            if (resource.Size <= 0) throw new InvalidDataException("resource size must be positive");
        }
        var dependencies = new HashSet<string>();
        foreach (var dependency in manifest.Dependencies)
        { Name(dependency.Id); PackageVersion.Parse(dependency.MinVersion); if (!dependencies.Add(dependency.Id)) throw new InvalidDataException("duplicate dependency"); }
    }
    public static void Catalog(Catalog catalog, Func<string, Manifest> manifestOf)
    {
        if (catalog.SchemaVersion != 1 || catalog.Packages is null) throw new InvalidDataException("unsupported catalog schema");
        var ids = new HashSet<string>();
        foreach (var entry in catalog.Packages)
        {
            Name(entry.Id); PackageVersion.Parse(entry.Version);
            if (!ids.Add(entry.Id)) throw new InvalidDataException("duplicate catalog package");
            var manifest = manifestOf(entry.Id); Manifest(manifest);
            if (manifest.Id != entry.Id || manifest.Version != entry.Version) throw new InvalidDataException("catalog/manifest identity or version mismatch");
        }
        foreach (var entry in catalog.Packages)
            foreach (var dep in manifestOf(entry.Id).Dependencies)
                if (!ids.Contains(dep.Id)) throw new InvalidDataException($"dependency '{dep.Id}' is absent from catalog");
        new Diff.DependencyResolver(manifestOf).ResolveClosure(ids, new LocalState());
    }
}
