namespace Aom.Core.Presets;

public sealed record CameraPreset(string Id, string DisplayName, IReadOnlyList<PresetParameter> Parameters)
{
    public int ParameterCount => Parameters.Count;

    public double GetValueOrDefault(string parameterName, double defaultValue = 0)
    {
        var parameter = Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, parameterName, StringComparison.Ordinal));
        return parameter?.Value ?? defaultValue;
    }

    public CameraPreset WithParameterValue(string parameterName, double value)
    {
        var updatedParameters = Parameters.ToList();
        var index = updatedParameters.FindIndex(parameter => string.Equals(parameter.Name, parameterName, StringComparison.Ordinal));

        if (index >= 0)
        {
            updatedParameters[index] = updatedParameters[index] with { Value = value };
        }
        else
        {
            updatedParameters.Add(new PresetParameter(parameterName, value));
        }

        return this with { Parameters = updatedParameters };
    }
}