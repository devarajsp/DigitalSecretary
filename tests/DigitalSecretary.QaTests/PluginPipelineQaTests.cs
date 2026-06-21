using System.Reflection;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.QaTests;

/// <summary>
/// End-to-end QA of the plugin pipeline: every feature DLL on disk must load in isolation,
/// instantiate, and build a usable view — the same path the running app takes.
/// </summary>
public sealed class PluginPipelineQaTests
{
    public static IEnumerable<object[]> AllManifestIds =>
        QaEnvironment.Manifests().Select(m => new object[] { m.Id }).ToList();

    [Fact]
    public void Plugins_folder_exists_and_contains_features()
    {
        Directory.Exists(QaEnvironment.PluginsRoot)
            .Should().BeTrue($"the solution must be built first; expected plugins at {QaEnvironment.PluginsRoot}");
        QaEnvironment.Manifests().Should().NotBeEmpty();
    }

    [Fact]
    public void All_expected_features_are_installed()
    {
        var ids = QaEnvironment.Manifests().Select(m => m.Id).ToList();
        ids.Should().Contain(new[] { "launcher", "calculator", "clipboard-history", "email-downloader" });
    }

    [Theory]
    [MemberData(nameof(AllManifestIds))]
    public void Manifest_points_at_an_existing_assembly(string id)
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == id);

        m.EntryAssembly.Should().NotBeNullOrWhiteSpace();
        m.EntryType.Should().NotBeNullOrWhiteSpace();
        File.Exists(Path.Combine(m.Directory, m.EntryAssembly)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllManifestIds))]
    public void Feature_loads_instantiates_and_builds_a_view(string id)
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == id);

        Sta.Run(() =>
        {
            var asmPath = Path.Combine(m.Directory, m.EntryAssembly);
            var ctx = new PluginLoadContext(asmPath);
            var asm = ctx.LoadFromAssemblyPath(asmPath);

            var type = asm.GetType(m.EntryType);
            type.Should().NotBeNull($"entry type {m.EntryType} should exist");

            var module = Activator.CreateInstance(type!) as IFeatureModule;
            module.Should().NotBeNull("the entry type must implement IFeatureModule");

            var view = module!.CreateView(new QaContext(m.Id));
            view.Should().BeAssignableTo<Control>();

            (view as IDisposable)?.Dispose();
        });
    }

    [Fact]
    public void Email_feature_resolves_its_own_private_MailKit()
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == "email-downloader");
        var asmPath = Path.Combine(m.Directory, m.EntryAssembly);

        var ctx = new PluginLoadContext(asmPath);
        ctx.LoadFromAssemblyPath(asmPath);
        var mailkit = ctx.LoadFromAssemblyName(new AssemblyName("MailKit"));

        Path.GetDirectoryName(mailkit.Location).Should().Be(m.Directory,
            "MailKit must come from the feature's own folder, not the host");
    }
}
