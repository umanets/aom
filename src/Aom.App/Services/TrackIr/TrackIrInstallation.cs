namespace Aom.App.Services.TrackIr;

public sealed record TrackIrInstallation(string DllPath, string Source, IReadOnlyList<string> Candidates);