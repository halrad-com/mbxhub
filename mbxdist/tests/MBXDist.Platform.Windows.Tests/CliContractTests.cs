using System.Diagnostics;
using System.Text.Json;
using MBXDist.App;
using MBXDist.Core.Model;

namespace MBXDist.Platform.Windows.Tests;

public class CliContractTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-cli-" + Guid.NewGuid().ToString("N"));
    private string Config => Path.Combine(_dir, "state.json");
    public CliContractTests() => Directory.CreateDirectory(_dir);

    private async Task<(int Code, string Output, string Error)> Run(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(typeof(AppInfo).Assembly.Location);
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add("--config"); psi.ArgumentList.Add(Config);
        psi.ArgumentList.Add("--no-self-update");
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    [Fact]
    public async Task Feed_option_before_verb_is_parsed_as_an_option()
    {
        var result = await Run("--feed", "http://127.0.0.1:9/", "version", "--json");
        Assert.Equal(0, result.Code);
        Assert.Equal(AppInfo.Version, JsonDocument.Parse(result.Output).RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Unknown_flag_is_an_error_not_an_update()
    {
        var result = await Run("update", "--not-a-real-flag", "--json");
        Assert.Equal(1, result.Code);
        Assert.False(File.Exists(Config));
        Assert.True(JsonDocument.Parse(result.Output).RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Dry_run_does_not_create_state_or_logs()
    {
        var result = await Run("update", "--dry-run", "--json");
        Assert.Equal(0, result.Code);
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
        Assert.True(JsonDocument.Parse(result.Output).RootElement.TryGetProperty("dryRun", out _));
    }

    [Fact]
    public async Task Unknown_package_is_rejected_without_changing_state()
    {
        var result = await Run("update", "unknown.package", "--json");
        Assert.Equal(1, result.Code);
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public async Task Placeholder_embedded_feed_is_refused_before_installation()
    {
        var result = await Run("validate-feed", "--feed-root", _dir, "--json");
        Assert.Equal(30, result.Code);
        Assert.True(JsonDocument.Parse(result.Output).RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task General_update_does_not_install_unselected_catalog_members()
    {
        var result = await Run("update", "--json");
        Assert.Equal(0, result.Code);
        Assert.Empty(JsonDocument.Parse(result.Output).RootElement.GetProperty("packages").EnumerateArray());
    }

    [Fact]
    public async Task Internal_interactive_mode_is_not_an_explicit_automation_verb()
    {
        var result=await Run("ux","--json");
        Assert.Equal(1,result.Code);
        Assert.True(JsonDocument.Parse(result.Output).RootElement.TryGetProperty("error",out _));
        Assert.False(File.Exists(Config));
    }

    public void Dispose() => Directory.Delete(_dir, true);
}
