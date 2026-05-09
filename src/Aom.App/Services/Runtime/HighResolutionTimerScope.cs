using System.Runtime.InteropServices;

namespace Aom.App.Services.Runtime;

public sealed class HighResolutionTimerScope : IDisposable
{
    private readonly uint periodMilliseconds;
    private readonly bool isActive;

    public HighResolutionTimerScope(uint periodMilliseconds = 1)
    {
        this.periodMilliseconds = periodMilliseconds;
        isActive = TimeBeginPeriod(periodMilliseconds) == 0;
    }

    public void Dispose()
    {
        if (isActive)
        {
            TimeEndPeriod(periodMilliseconds);
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint periodMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint periodMilliseconds);
}