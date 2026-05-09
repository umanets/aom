using System.Linq;
using Aom.Core.Bindings;
using Xunit;

namespace Aom.Core.Tests;

public sealed class BindingCatalogTests
{
    [Fact]
    public void All_ContainsOverlayToggleBinding()
    {
        var overlayBinding = BindingCatalog.All
            .SelectMany(group => group.Bindings)
            .SingleOrDefault(binding => binding.ActionId == BindingActionIds.ToggleOverlay);

        Assert.NotNull(overlayBinding);
        Assert.Equal("Toggle overlay", overlayBinding!.Name);
        Assert.Equal(BindingActivationMode.Press, overlayBinding.ActivationMode);
    }
}