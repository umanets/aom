namespace Aom.Core.Bindings;

public sealed record BindingGroup(string Title, string Description, IReadOnlyList<BindingDefinition> Bindings);