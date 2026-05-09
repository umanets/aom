using System.IO;
using NAudio.Wave;

namespace Aom.App.Services.Ai;

public sealed class PushToTalkAudioCaptureService : IDisposable
{
    private static readonly TimeSpan RecordingStopGracePeriod = TimeSpan.FromMilliseconds(250);
    private WaveInEvent? captureDevice;
    private MemoryStream? pcmStream;
    private WaveFormat? waveFormat;
    private TaskCompletionSource<Exception?>? recordingStopped;
    private bool stopRequested;

    public bool IsRecording { get; private set; }

    public bool LastStopTimedOut { get; private set; }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        if (WaveIn.DeviceCount == 0)
        {
            throw new InvalidOperationException("No microphone input device was found.");
        }

        pcmStream = new MemoryStream();
        waveFormat = new WaveFormat(16000, 16, 1);
        recordingStopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
    stopRequested = false;
    LastStopTimedOut = false;

        captureDevice = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = waveFormat,
            BufferMilliseconds = 80,
            NumberOfBuffers = 3,
        };
        captureDevice.DataAvailable += OnDataAvailable;
        captureDevice.RecordingStopped += OnRecordingStopped;
        captureDevice.StartRecording();
        IsRecording = true;
    }

    public byte[]? Stop()
    {
        if (!IsRecording)
        {
            return null;
        }

        try
        {
            stopRequested = true;
            captureDevice!.StopRecording();
            if (recordingStopped!.Task.Wait(RecordingStopGracePeriod))
            {
                var stopException = recordingStopped.Task.GetAwaiter().GetResult();
                if (stopException is not null)
                {
                    throw stopException;
                }

                LastStopTimedOut = false;
            }
            else
            {
                LastStopTimedOut = true;
            }

            var pcmBytes = pcmStream!.ToArray();
            if (pcmBytes.Length == 0)
            {
                return null;
            }

            return EncodeWave(pcmBytes, waveFormat!);
        }
        finally
        {
            Cleanup();
        }
    }

    public void Dispose()
    {
        Cleanup();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        pcmStream?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        recordingStopped?.TrySetResult(e.Exception);
    }

    private static byte[] EncodeWave(byte[] pcmBytes, WaveFormat format)
    {
        using var buffer = new NonClosingMemoryStream();
        using (var writer = new WaveFileWriter(buffer, format))
        {
            writer.Write(pcmBytes, 0, pcmBytes.Length);
        }

        return buffer.ToArray();
    }

    private void Cleanup()
    {
        if (captureDevice is not null)
        {
            captureDevice.DataAvailable -= OnDataAvailable;
            captureDevice.RecordingStopped -= OnRecordingStopped;

            try
            {
                if (IsRecording && !stopRequested)
                {
                    captureDevice.StopRecording();
                }
            }
            catch
            {
            }

            captureDevice.Dispose();
            captureDevice = null;
        }

        pcmStream?.Dispose();
        pcmStream = null;
        waveFormat = null;
        recordingStopped = null;
        stopRequested = false;
        IsRecording = false;
    }

    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
        }
    }
}