using Aom.App.Services.Input;
using Xunit;

namespace Aom.App.Tests;

public sealed class KeyboardLearnTriggerBufferTests
{
    [Fact]
    public void TryConsume_WaitsForShortcutToSettle()
    {
        var buffer = new KeyboardLearnTriggerBuffer(TimeSpan.FromMilliseconds(200));
        var start = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

        buffer.Queue("UpArrow", start);

        var consumedTooEarly = buffer.TryConsume(start.AddMilliseconds(150), out _);

        Assert.False(consumedTooEarly);
    }

    [Fact]
    public void TryConsume_ReturnsUpdatedShortcutAfterSecondKey()
    {
        var buffer = new KeyboardLearnTriggerBuffer(TimeSpan.FromMilliseconds(200));
        var start = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

        buffer.Queue("UpArrow", start);
        buffer.Queue("UpArrow + NumPad0", start.AddMilliseconds(80));

        var consumed = buffer.TryConsume(start.AddMilliseconds(320), out var trigger);

        Assert.True(consumed);
        Assert.Equal("UpArrow + NumPad0", trigger);
    }

    [Fact]
    public void Clear_DropsPendingShortcut()
    {
        var buffer = new KeyboardLearnTriggerBuffer(TimeSpan.FromMilliseconds(100));
        var start = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

        buffer.Queue("UpArrow + NumPad0", start);
        buffer.Clear();

        var consumed = buffer.TryConsume(start.AddMilliseconds(200), out _);

        Assert.False(consumed);
    }
}