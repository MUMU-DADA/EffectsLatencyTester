using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using NAudio.Wave;

namespace LatencyTester;

internal sealed record MeasurementExportData(
    string LastTestName,
    string DriverName,
    int SampleRate,
    int BufferSize,
    int OutputChannel,
    string OutputChannelName,
    int InputChannel,
    string InputChannelName,
    double? BaselineMilliseconds,
    double? EffectsBoardLatencyMilliseconds,
    LatencyResult? BaselineResult,
    LatencyResult? EffectsResult);

internal static class MeasurementExport
{
    public static void CreateZip(string destinationPath, MeasurementExportData data)
    {
        try
        {
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

            WriteTextEntry(archive, "result.csv", BuildCsv(data));
            if (data.BaselineResult is { HasResult: true } baselineResult)
            {
                WriteWaveEntry(archive, "correction-output.wav", baselineResult.OutputSamples, data.SampleRate);
                WriteWaveEntry(archive, "correction-input.wav", baselineResult.InputSamples, data.SampleRate);
            }

            if (data.EffectsResult is { HasResult: true } effectsResult)
            {
                WriteWaveEntry(archive, "final-output.wav", effectsResult.OutputSamples, data.SampleRate);
                WriteWaveEntry(archive, "final-input.wav", effectsResult.InputSamples, data.SampleRate);
            }
        }
        catch
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch
            {
                // Keep the original export exception when cleanup also fails.
            }

            throw;
        }
    }

    private static string BuildCsv(MeasurementExportData data)
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv, "Field", "Value");
        AppendCsvRow(csv, "Last test", data.LastTestName);
        AppendCsvRow(csv, "Driver", data.DriverName);
        AppendCsvRow(csv, "Sample rate (Hz)", data.SampleRate.ToString(CultureInfo.InvariantCulture));
        AppendCsvRow(csv, "Buffer size (samples)", data.BufferSize.ToString(CultureInfo.InvariantCulture));
        AppendCsvRow(csv, "Output channel", $"{data.OutputChannel}: {data.OutputChannelName}");
        AppendCsvRow(csv, "Input channel", $"{data.InputChannel}: {data.InputChannelName}");
        AppendCsvRow(csv, "Has correction result", (data.BaselineResult?.HasResult ?? false).ToString());
        AppendCsvRow(csv, "Correction latency (samples)", FormatOptional(data.BaselineResult?.LatencySamples));
        AppendCsvRow(csv, "Correction latency (ms)", FormatOptional(data.BaselineResult?.LatencyMilliseconds));
        AppendCsvRow(csv, "Has final result", (data.EffectsResult?.HasResult ?? false).ToString());
        AppendCsvRow(csv, "Final latency (samples)", FormatOptional(data.EffectsResult?.LatencySamples));
        AppendCsvRow(csv, "Final latency (ms)", FormatOptional(data.EffectsResult?.LatencyMilliseconds));
        AppendCsvRow(csv, "Direct baseline (ms)", FormatOptional(data.BaselineMilliseconds));
        AppendCsvRow(csv, "Effects-board latency (ms)", FormatOptional(data.EffectsBoardLatencyMilliseconds));
        AppendCsvRow(csv, "Correction output samples", FormatOptional(data.BaselineResult?.OutputSamples.Length));
        AppendCsvRow(csv, "Correction input samples", FormatOptional(data.BaselineResult?.InputSamples.Length));
        AppendCsvRow(csv, "Final output samples", FormatOptional(data.EffectsResult?.OutputSamples.Length));
        AppendCsvRow(csv, "Final input samples", FormatOptional(data.EffectsResult?.InputSamples.Length));
        AppendCsvRow(csv, "Exported UTC", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return csv.ToString();
    }

    private static string FormatOptional(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatOptional(double? value)
    {
        return value?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static void AppendCsvRow(StringBuilder csv, string field, string value)
    {
        csv.Append(EscapeCsv(field));
        csv.Append(',');
        csv.Append(EscapeCsv(value));
        csv.AppendLine();
    }

    private static string EscapeCsv(string value)
    {
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.Write(content);
    }

    private static void WriteWaveEntry(
        ZipArchive archive,
        string entryName,
        IReadOnlyList<float> samples,
        int sampleRate)
    {
        byte[] waveData;
        using (var waveStream = new MemoryStream())
        {
            using (var writer = new WaveFileWriter(
                       waveStream,
                       WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
            {
                var sampleArray = samples.ToArray();
                writer.WriteSamples(sampleArray, 0, sampleArray.Length);
            }

            waveData = waveStream.ToArray();
        }

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(waveData, 0, waveData.Length);
    }
}
