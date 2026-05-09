using Aom.App.ViewModels;
using Aom.Core.Presets;
using Xunit;

namespace Aom.App.Tests;

public sealed class PresetListItemViewModelTests
{
    [Fact]
    public void Constructor_UsesStoredDisplayNameOverride()
    {
        var preset = new CameraPreset("lagg", "Lagg", Array.Empty<PresetParameter>());

        var viewModel = new PresetListItemViewModel(preset, "My Lagg");

        Assert.Equal("My Lagg", viewModel.DisplayName);
        Assert.True(viewModel.HasCustomDisplayName);
    }

    [Fact]
    public void CommitRename_StoresTrimmedCustomName()
    {
        var preset = new CameraPreset("lagg", "Lagg", Array.Empty<PresetParameter>());
        PresetListItemViewModel? committed = null;
        var viewModel = new PresetListItemViewModel(preset, null, item => committed = item);

        viewModel.BeginRename();
        viewModel.EditableDisplayName = "  Frontline Lagg  ";
        viewModel.CommitRename();

        Assert.Equal("Frontline Lagg", viewModel.DisplayName);
        Assert.True(viewModel.HasCustomDisplayName);
        Assert.Same(viewModel, committed);
    }

    [Fact]
    public void CommitRename_BlankValueRestoresDefaultDisplayName()
    {
        var preset = new CameraPreset("lagg", "Lagg", Array.Empty<PresetParameter>());
        var viewModel = new PresetListItemViewModel(preset, "Custom Lagg");

        viewModel.BeginRename();
        viewModel.EditableDisplayName = "   ";
        viewModel.CommitRename();

        Assert.Equal("Lagg", viewModel.DisplayName);
        Assert.False(viewModel.HasCustomDisplayName);
        Assert.Null(viewModel.PersistedDisplayName);
    }

    [Fact]
    public void Delete_InvokesCallbackWhenPresetCanBeDeleted()
    {
        var preset = new CameraPreset("custom-1", "Unknown", Array.Empty<PresetParameter>());
        PresetListItemViewModel? deleted = null;
        var viewModel = new PresetListItemViewModel(
            preset,
            displayNameOverride: null,
            deleteRequested: item => deleted = item,
            canDelete: true);

        viewModel.Delete();

        Assert.Same(viewModel, deleted);
    }

    [Fact]
    public void UpdatePreset_ReplacesUnderlyingPresetAndDisplayName()
    {
        var preset = new CameraPreset("custom-1", "Unknown", Array.Empty<PresetParameter>());
        var updatedPreset = new CameraPreset("custom-1", "Unknown (2)", Array.Empty<PresetParameter>());
        var viewModel = new PresetListItemViewModel(preset, null);

        viewModel.UpdatePreset(updatedPreset);

        Assert.Equal("Unknown (2)", viewModel.DisplayName);
        Assert.Same(updatedPreset, viewModel.Preset);
    }
}