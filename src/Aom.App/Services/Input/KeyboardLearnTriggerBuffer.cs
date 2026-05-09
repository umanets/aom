namespace Aom.App.Services.Input;

public sealed class KeyboardLearnTriggerBuffer
{
    private readonly TimeSpan settleDelay;
    private string? pendingTrigger;
    private DateTimeOffset pendingUpdatedAt;

    public KeyboardLearnTriggerBuffer(TimeSpan? settleDelay = null)
    {
        this.settleDelay = settleDelay ?? TimeSpan.FromMilliseconds(250);
    }

    public void Queue(string? trigger, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return;
        }

        pendingTrigger = trigger;
        pendingUpdatedAt = timestamp;
    }

    public bool TryConsume(DateTimeOffset timestamp, out string trigger)
    {
        if (string.IsNullOrWhiteSpace(pendingTrigger) || timestamp - pendingUpdatedAt < settleDelay)
        {
            trigger = string.Empty;
            return false;
        }

        trigger = pendingTrigger;
        pendingTrigger = null;
        return true;
    }

    public void Clear()
    {
        pendingTrigger = null;
        pendingUpdatedAt = default;
    }
}