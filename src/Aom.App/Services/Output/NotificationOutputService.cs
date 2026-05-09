using System.Collections.Concurrent;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using Aom.App.Services.Ai;
using Aom.Core.Runtime;
using NAudio.Wave;

namespace Aom.App.Services.Output;

public sealed class NotificationOutputService : IDisposable
{
    private const int SpeechFlagsAsyncPurge = 3;
    private readonly BlockingCollection<SpeechRequest> speechQueue = new();
    private readonly Thread speechThread;
    private readonly AiPartnerDiagnosticsLog aiPartnerDiagnosticsLog = new();
    private object? speechVoice;
    private Type? speechVoiceType;

    public NotificationOutputService()
    {
        speechThread = new Thread(RunSpeechLoop)
        {
            IsBackground = true,
            Name = nameof(NotificationOutputService),
        };
        speechThread.SetApartmentState(ApartmentState.STA);
        speechThread.Start();
    }

    public void SpeakCheckSixSpeedCue(CheckSixSpeedCue cue)
    {
        var message = BuildCheckSixSpeedCueMessage(cue);
        if (message is null)
        {
            return;
        }

        QueueSpeech(message);
    }

    public void SpeakFlapsReminder()
    {
        QueueSpeech(BuildFlapsReminderMessage());
    }

    public void SpeakPresetLoaded(string presetDisplayName)
    {
        QueueSpeech(BuildPresetLoadedMessage(presetDisplayName));
    }

    public void SpeakAiPartnerReply(string? reply, byte[]? aiSpeechAudioBytes = null, string? aiSpeechAudioFormat = null)
    {
        var normalizedReply = string.IsNullOrWhiteSpace(reply)
            ? null
            : reply.Trim();

        if (normalizedReply is null)
        {
            return;
        }

        QueueSpeech(new SpeechRequest(normalizedReply, aiSpeechAudioBytes, aiSpeechAudioFormat));
    }

    public void SpeakTuneModeStatus(string? statusMessage, string? cycleTrigger = null)
    {
        var message = BuildTuneModeStatusMessage(statusMessage, cycleTrigger);
        if (message is null)
        {
            return;
        }

        QueueSpeech(message);
    }

    public static string BuildPresetLoadedMessage(string presetDisplayName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(presetDisplayName)
            ? null
            : presetDisplayName.Trim();

        return normalizedName is null
            ? "Preset loaded"
            : $"{normalizedName} preset loaded";
    }

    public static string BuildFlapsReminderMessage() => "Flaps.";

    public static string? BuildCheckSixSpeedCueMessage(CheckSixSpeedCue cue) => cue switch
    {
        CheckSixSpeedCue.Low => "low",
        CheckSixSpeedCue.Optimal => "optimal",
        CheckSixSpeedCue.Danger => "danger",
        _ => null,
    };

    public static string? BuildTuneModeStatusMessage(string? statusMessage, string? cycleTrigger = null)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(statusMessage)
            ? null
            : statusMessage.Trim();

        if (normalizedStatus is null)
        {
            return null;
        }

        if (string.Equals(normalizedStatus, "TuneMode: Please select tune mode.", StringComparison.Ordinal))
        {
            var spokenTrigger = BuildSpokenBindingTrigger(cycleTrigger);
            return spokenTrigger is null
                ? "Please select tune mode."
                : $"Please select tune mode by pressing {spokenTrigger}.";
        }

        return normalizedStatus.EndsWith(".", StringComparison.Ordinal)
            ? normalizedStatus
            : $"{normalizedStatus}.";
    }

    private static string? BuildSpokenBindingTrigger(string? trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return null;
        }

        var parts = trigger
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(HumanizeBindingToken)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0
            ? null
            : string.Join(" plus ", parts);
    }

    private static string HumanizeBindingToken(string token) => token switch
    {
        "LeftAlt" => "Left Alt",
        "RightAlt" => "Right Alt",
        "LeftControl" => "Left Control",
        "RightControl" => "Right Control",
        "LeftShift" => "Left Shift",
        "RightShift" => "Right Shift",
        "UpArrow" => "Up Arrow",
        "DownArrow" => "Down Arrow",
        "LeftArrow" => "Left Arrow",
        "RightArrow" => "Right Arrow",
        "PageUp" => "Page Up",
        "PageDown" => "Page Down",
        "ScrollLock" => "Scroll Lock",
        "NumPadPeriod" => "Numpad Decimal",
        _ when token.StartsWith("NumPad", StringComparison.Ordinal) && token.Length > "NumPad".Length => $"Numpad {token["NumPad".Length..]}",
        _ => token,
    };

    public void Dispose()
    {
        speechQueue.CompleteAdding();

        if (speechThread.IsAlive && Thread.CurrentThread != speechThread)
        {
            speechThread.Join(TimeSpan.FromMilliseconds(500));
        }

        ReleaseSpeechVoice();
        speechQueue.Dispose();
    }

    private void QueueSpeech(string text)
    {
        QueueSpeech(new SpeechRequest(text, null, null));
    }

    private void QueueSpeech(SpeechRequest request)
    {
        if ((string.IsNullOrWhiteSpace(request.Text) && (request.AudioBytes is null || request.AudioBytes.Length == 0))
            || speechQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            speechQueue.Add(request);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RunSpeechLoop()
    {
        foreach (var request in speechQueue.GetConsumingEnumerable())
        {
            try
            {
                if (request.AudioBytes is { Length: > 0 })
                {
                    aiPartnerDiagnosticsLog.WriteInfo($"Starting OpenAI voice playback. Format: {request.AudioFormat ?? "unknown"}. Audio bytes: {request.AudioBytes.Length}.");
                    PlayAiSpeechAudioCore(request.AudioBytes, request.AudioFormat);
                    aiPartnerDiagnosticsLog.WriteInfo("OpenAI voice playback completed.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.Text))
                {
                    SpeakCore(request.Text);
                }
            }
            catch (Exception exception)
            {
                aiPartnerDiagnosticsLog.WriteError("OpenAI voice playback failed. Falling back to local speech if text is available.", exception);
                if (!string.IsNullOrWhiteSpace(request.Text))
                {
                    try
                    {
                        SpeakCore(request.Text);
                        aiPartnerDiagnosticsLog.WriteInfo("Local SAPI fallback playback completed.");
                        continue;
                    }
                    catch (Exception fallbackException)
                    {
                        aiPartnerDiagnosticsLog.WriteError("Local SAPI fallback playback failed.", fallbackException);
                    }
                }

                SystemSounds.Asterisk.Play();
            }
        }

        ReleaseSpeechVoice();
    }

    private void SpeakCore(string text)
    {
        speechVoiceType ??= Type.GetTypeFromProgID("SAPI.SpVoice");
        if (speechVoiceType is null)
        {
            SystemSounds.Asterisk.Play();
            return;
        }

        speechVoice ??= Activator.CreateInstance(speechVoiceType);
        speechVoiceType.InvokeMember(
            "Speak",
            BindingFlags.InvokeMethod,
            binder: null,
            target: speechVoice,
            args: new object[] { text, SpeechFlagsAsyncPurge });
    }

    private static void PlayAiSpeechAudioCore(byte[] audioBytes, string? audioFormat)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"aom-ai-voice-{Guid.NewGuid():N}{ResolveAudioFileExtension(audioBytes, audioFormat)}");

        try
        {
            File.WriteAllBytes(tempFilePath, audioBytes);
            using var reader = new MediaFoundationReader(tempFilePath);
            using var outputDevice = new WaveOutEvent();
            using var playbackStopped = new ManualResetEventSlim(false);

            Exception? playbackException = null;
            outputDevice.PlaybackStopped += (_, args) =>
            {
                playbackException = args.Exception;
                playbackStopped.Set();
            };

            outputDevice.Init(reader);
            outputDevice.Play();

            if (!playbackStopped.Wait(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    outputDevice.Stop();
                }
                catch
                {
                }

                throw new TimeoutException("OpenAI voice playback timed out.");
            }

            if (playbackException is not null)
            {
                throw playbackException;
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
            }
        }
    }

    private static string ResolveAudioFileExtension(byte[] audioBytes, string? audioFormat)
    {
        if (!string.IsNullOrWhiteSpace(audioFormat))
        {
            return audioFormat.Trim().ToLowerInvariant() switch
            {
                "mp3" => ".mp3",
                "wav" => ".wav",
                "opus" => ".opus",
                "flac" => ".flac",
                _ => ".bin",
            };
        }

        if (audioBytes.Length >= 3 && audioBytes[0] == (byte)'I' && audioBytes[1] == (byte)'D' && audioBytes[2] == (byte)'3')
        {
            return ".mp3";
        }

        if (audioBytes.Length >= 4 && audioBytes[0] == (byte)'R' && audioBytes[1] == (byte)'I' && audioBytes[2] == (byte)'F' && audioBytes[3] == (byte)'F')
        {
            return ".wav";
        }

        if (audioBytes.Length >= 2 && audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0)
        {
            return ".mp3";
        }

        return ".bin";
    }

    private void ReleaseSpeechVoice()
    {
        if (speechVoice is null)
        {
            return;
        }

        if (Marshal.IsComObject(speechVoice))
        {
            Marshal.FinalReleaseComObject(speechVoice);
        }

        speechVoice = null;
    }

    private sealed record SpeechRequest(string? Text, byte[]? AudioBytes, string? AudioFormat);
}