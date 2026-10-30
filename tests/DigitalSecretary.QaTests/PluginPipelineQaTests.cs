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
        ids.Should().Contain(new[]
        {
            "launcher", "calculator", "clipboard-history", "email-downloader",
            "gmail-downloader", "google-drive-downloader", "dedupe-files", "storage-studio",
            "similar-pictures"
        });
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

    [Fact]
    public void Gmail_feature_resolves_its_own_private_MailKit()
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == "gmail-downloader");
        var asmPath = Path.Combine(m.Directory, m.EntryAssembly);

        var ctx = new PluginLoadContext(asmPath);
        ctx.LoadFromAssemblyPath(asmPath);
        var mailkit = ctx.LoadFromAssemblyName(new AssemblyName("MailKit"));

        Path.GetDirectoryName(mailkit.Location).Should().Be(m.Directory,
            "MailKit must come from the feature's own folder, not the host");
    }

    [Fact]
    public void Drive_feature_resolves_its_own_private_Google_Apis()
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == "google-drive-downloader");
        var asmPath = Path.Combine(m.Directory, m.EntryAssembly);

        var ctx = new PluginLoadContext(asmPath);
        ctx.LoadFromAssemblyPath(asmPath);
        var drive = ctx.LoadFromAssemblyName(new AssemblyName("Google.Apis.Drive.v3"));

        Path.GetDirectoryName(drive.Location).Should().Be(m.Directory,
            "Google.Apis.Drive.v3 must come from the feature's own folder, not the host");
    }

    [Fact]
    public void Storage_Studio_map_drills_into_a_folder_when_its_tile_is_clicked()
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == "storage-studio");

        Sta.Run(() =>
        {
            var asmPath = Path.Combine(m.Directory, m.EntryAssembly);
            var asm = new PluginLoadContext(asmPath).LoadFromAssemblyPath(asmPath);
            var module = (IFeatureModule)Activator.CreateInstance(asm.GetType(m.EntryType)!)!;
            using var view = module.CreateView(new QaContext(m.Id));
            view.Size = new System.Drawing.Size(1200, 800);

            // Show the placeholder sample tree, as the app does after a scan.
            var sample = asm.GetType("DigitalSecretary.Features.StorageStudio.SampleSpaceTree")!
                .GetMethod("Build")!.Invoke(null, [DateTime.UtcNow])!;
            view.GetType().GetMethod("ShowTree")!.Invoke(view, [sample]);

            var map = Descendants(view).Single(c => c.GetType().Name == "TreemapControl");
            map.Size = new System.Drawing.Size(700, 450);
            Prop(map, "FocusNode").Should().BeSameAs(sample, "the map shows the root after ShowTree");

            // Click just inside the first top-level folder tile.
            var cells = (System.Collections.IEnumerable)map.GetType()
                .GetField("_cells", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(map)!;
            var folderCell = cells.Cast<object>().First(c =>
                Prop(c, "Node") is { } n && (bool)Prop(n, "IsFolder")! && (int)Prop(c, "Depth")! == 1);
            var rect = (System.Drawing.RectangleF)Prop(folderCell, "Rect")!;
            var click = new MouseEventArgs(MouseButtons.Left, 1, (int)(rect.Left + 5), (int)(rect.Top + 5), 0);
            map.GetType().GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(map, [click]);

            Prop(map, "FocusNode").Should().BeSameAs(Prop(folderCell, "Node"), "clicking a folder tile drills into it");
        });
    }

    [Fact]
    public void Similar_Pictures_groups_the_sample_and_a_click_toggles_a_pictures_mark()
    {
        var m = QaEnvironment.Manifests().First(x => x.Id == "similar-pictures");

        Sta.Run(() =>
        {
            var asmPath = Path.Combine(m.Directory, m.EntryAssembly);
            var asm = new PluginLoadContext(asmPath).LoadFromAssemblyPath(asmPath);
            var module = (IFeatureModule)Activator.CreateInstance(asm.GetType(m.EntryType)!)!;
            using var view = module.CreateView(new QaContext(m.Id));
            view.Size = new System.Drawing.Size(1200, 800);

            // Show the placeholder sample set (generated pictures), as DocShots does.
            var sampleType = asm.GetType("DigitalSecretary.Features.SimilarPictures.SamplePictureSet")!;
            var built = sampleType.GetMethod("Build")!.Invoke(null, [DateTime.UtcNow])!;
            var records = built.GetType().GetField("Item1")!.GetValue(built)!;
            var thumbs = (Dictionary<string, System.Drawing.Bitmap>)built.GetType().GetField("Item2")!.GetValue(built)!;
            Func<string, System.Drawing.Bitmap?> source = p => thumbs.TryGetValue(p, out var b) ? new System.Drawing.Bitmap(b) : null;
            var root = (string)sampleType.GetField("RootPath")!.GetValue(null)!;
            view.GetType().GetMethod("ShowRecords")!.Invoke(view, [root, records, source]);

            // Grouping runs in the background; pump messages until the first group's cards appear.
            var cards = WaitFor(() => Descendants(view).Where(c => c.GetType().Name == "PictureCardControl").ToList(), l => l.Count >= 2);
            cards.Count.Should().BeGreaterThanOrEqualTo(2, "the first group shows its keeper and at least one other picture");

            var marked = (System.Collections.Generic.ICollection<string>)view.GetType()
                .GetField("_marked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
            var before = marked.Count;
            var other = cards[1];
            other.GetType().GetMethod("OnAnyClick", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(other, [other, new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0)]);

            marked.Count.Should().NotBe(before, "clicking a picture that is not the keeper toggles whether it moves");
            foreach (var bmp in thumbs.Values)
            {
                bmp.Dispose();
            }
        });
    }

    private static T WaitFor<T>(Func<T> read, Func<T, bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        var value = read();
        while (!done(value) && DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(20);
            value = read();
        }
        return value;
    }

    private static object? Prop(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target);

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var grandchild in Descendants(child))
            {
                yield return grandchild;
            }
        }
    }
}
