using NAudio.Wave;

namespace Aom.App.Services.Ai;

public sealed class MicrophoneLoopbackService : IDisposable
{
    private WaveInEvent? captureDevice;
    private WaveOutEvent? playbackDevice;
    private BufferedWaveProvider? buffer;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        if (WaveIn.DeviceCount == 0)
        {
            throw new InvalidOperationException("No microphone input device was found.");
        }

        captureDevice = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 60,
            NumberOfBuffers = 3,
        };
        captureDevice.DataAvailable += OnDataAvailable;

        buffer = new BufferedWaveProvider(captureDevice.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
        };

        playbackDevice = new WaveOutEvent
        {
            DesiredLatency = 60,
        };
        playbackDevice.Init(buffer);
        playbackDevice.Play();

        captureDevice.StartRecording();
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        captureDevice!.DataAvailable -= OnDataAvailable;

        try
        {
            captureDevice.StopRecording();
        }
        catch
        {
        }

        playbackDevice?.Stop();
        playbackDevice?.Dispose();
        captureDevice?.Dispose();

        playbackDevice = null;
        captureDevice = null;
        buffer = null;
        IsRunning = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }
}