using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aom.Core.Bindings;

namespace Aom.App.ViewModels;

public sealed class BindingEntryViewModel : INotifyPropertyChanged
{
    private string trigger;
    private bool isLearning;

    public BindingEntryViewModel(string actionId, string name, string trigger, BindingActivationMode activationMode)
    {
        ActionId = actionId;
        Name = name;
        this.trigger = trigger;
        ActivationMode = activationMode;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ActionId { get; }

    public string Name { get; }

    public BindingActivationMode ActivationMode { get; }

    public string ActivationModeLabel => ActivationMode == BindingActivationMode.Hold ? "Hold" : "Press";

    public string Trigger
    {
        get => trigger;
        private set
        {
            trigger = value;
            OnPropertyChanged();
        }
    }

    public bool IsLearning
    {
        get => isLearning;
        private set
        {
            isLearning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LearnActionLabel));
        }
    }

    public string LearnActionLabel => IsLearning ? "Listening..." : "Learn";

    public void SetTrigger(string value)
    {
        Trigger = value;
    }

    public void SetLearning(bool value)
    {
        IsLearning = value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}