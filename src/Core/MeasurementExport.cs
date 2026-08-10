using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using EffectsLatencyTester.Core;

namespace EffectsLatencyTester;

public sealed record MeasurementExportData(
    string LastTestName,
    string AudioDeviceName,
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

public static class MeasurementExport
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
            }

            throw;
        }
    }

    private static string BuildCsv(MeasurementExportData data)
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv, "Field", "Value");
        AppendCsvRow(csv, "Last test", data.LastTestName);
        AppendCsvRow(csv, "Audio device", data.AudioDeviceName);
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

    private static string FormatOptional(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatOptional(double? value) => value?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty;

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
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        WriteWave(stream, samples, sampleRate);
    }

    private static void WriteWave(Stream stream, IReadOnlyList<float> samples, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 32;
        const short audioFormat = 3; // IEEE float
        const short blockAlign = channels * (bitsPerSample / 8);
        var dataSize = checked(samples.Count * blockAlign);
        var riffSize = checked(36 + dataSize);
        var header = new byte[44];

        WriteAscii(header, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), riffSize);
        WriteAscii(header, 8, "WAVE");
        WriteAscii(header, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(20), audioFormat);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(22), channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), sampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(32), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(34), bitsPerSample);
        WriteAscii(header, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40), dataSize);
        stream.Write(header);

        Span<byte> sampleBytes = stackalloc byte[4];
        foreach (var sample in samples)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                sampleBytes,
                BitConverter.SingleToInt32Bits(Math.Clamp(float.IsFinite(sample) ? sample : 0f, -1f, 1f)));
            stream.Write(sampleBytes);
        }
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
    }
}