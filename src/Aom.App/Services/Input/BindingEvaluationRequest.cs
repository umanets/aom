using Aom.Core.Bindings;

namespace Aom.App.Services.Input;

public sealed record BindingEvaluationRequest(string ActionId, string Trigger, BindingActivationMode ActivationMode);