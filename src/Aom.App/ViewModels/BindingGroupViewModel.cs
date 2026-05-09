namespace Aom.App.ViewModels;

public sealed class BindingGroupViewModel
{
    public BindingGroupViewModel(string title, string description, IReadOnlyList<BindingEntryViewModel> bindings)
    {
        Title = title;
        Description = description;
        Bindings = bindings;
    }

    public string Title { get; }

    public string Description { get; }

    public IReadOnlyList<BindingEntryViewModel> Bindings { get; }
}