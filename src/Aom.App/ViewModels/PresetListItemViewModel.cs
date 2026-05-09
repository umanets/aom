using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Aom.Core.Presets;

namespace Aom.App.ViewModels;

public sealed class PresetListItemViewModel : INotifyPropertyChanged
{
    private readonly Action<PresetListItemViewModel> renameCommitted;
    private readonly Action<PresetListItemViewModel> deleteRequested;
    private CameraPreset preset;
    private string displayName;
    private string editableDisplayName;
    private bool isEditing;

    public PresetListItemViewModel(
        CameraPreset preset,
        string? displayNameOverride,
        Action<PresetListItemViewModel>? renameCommitted = null,
        Action<PresetListItemViewModel>? deleteRequested = null,
        bool canDelete = false)
    {
        this.preset = preset;
        this.renameCommitted = renameCommitted ?? (_ => { });
        this.deleteRequested = deleteRequested ?? (_ => { });
        CanDelete = canDelete;
        displayName = NormalizeDisplayName(displayNameOverride) ?? preset.DisplayName;
        editableDisplayName = displayName;
        BeginRenameCommand = new RelayCommand<object>(_ => BeginRename());
        CommitRenameCommand = new RelayCommand<object>(_ => CommitRename());
        CancelRenameCommand = new RelayCommand<object>(_ => CancelRename());
        DeleteCommand = new RelayCommand<object>(_ => Delete(), _ => CanDelete);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CameraPreset Preset => preset;

    public string Id => Preset.Id;

    public bool CanDelete { get; }

    public string DisplayName
    {
        get => displayName;
        private set
        {
            displayName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCustomDisplayName));
            OnPropertyChanged(nameof(PersistedDisplayName));
        }
    }

    public string EditableDisplayName
    {
        get => editableDisplayName;
        set
        {
            editableDisplayName = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => isEditing;
        private set
        {
            isEditing = value;
            OnPropertyChanged();
        }
    }

    public bool HasCustomDisplayName => !string.Equals(DisplayName, Preset.DisplayName, StringComparison.Ordinal);

    public string? PersistedDisplayName => HasCustomDisplayName ? DisplayName : null;

    public ICommand BeginRenameCommand { get; }

    public ICommand CommitRenameCommand { get; }

    public ICommand CancelRenameCommand { get; }

    public ICommand DeleteCommand { get; }

    public void BeginRename()
    {
        EditableDisplayName = DisplayName;
        IsEditing = true;
    }

    public void CommitRename()
    {
        var normalizedDisplayName = NormalizeDisplayName(EditableDisplayName) ?? Preset.DisplayName;
        var displayNameChanged = !string.Equals(DisplayName, normalizedDisplayName, StringComparison.Ordinal);

        DisplayName = normalizedDisplayName;
        EditableDisplayName = normalizedDisplayName;
        IsEditing = false;

        if (displayNameChanged)
        {
            renameCommitted(this);
        }
    }

    public void CancelRename()
    {
        EditableDisplayName = DisplayName;
        IsEditing = false;
    }

    public void Delete()
    {
        if (!CanDelete)
        {
            return;
        }

        deleteRequested(this);
    }

    public void UpdatePreset(CameraPreset value)
    {
        preset = value;
        displayName = value.DisplayName;
        editableDisplayName = value.DisplayName;

        OnPropertyChanged(nameof(Preset));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(EditableDisplayName));
        OnPropertyChanged(nameof(HasCustomDisplayName));
        OnPropertyChanged(nameof(PersistedDisplayName));
    }

    private static string? NormalizeDisplayName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}