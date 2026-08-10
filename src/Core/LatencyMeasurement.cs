namespace EffectsLatencyTester.Core;

public readonly record struct LatencyResult(
    bool HasResult,
    int LatencySamples,
    double LatencyMilliseconds,
    int SampleRate,
    float[] OutputSamples,
    float[] InputSamples);

public static class LatencyMeasurement
{
    private const int TestDurationMilliseconds = 3200;
    private const int PulseOffsetMilliseconds = 500;
    private const int PulseSpacingMilliseconds = 1000;
    private const float DetectionThreshold = 0.12f;
    private const int DetectionRefractorySamples = 1000;

    public static async Task<LatencyResult> RunAsync(
        IAudioBackend backend,
        AudioStreamOptions options,
        CancellationToken cancellationToken = default)
    {
        var durationSamples = checked(options.SampleRate * TestDurationMilliseconds / 1000);
        var outputSamples = new float[Math.Max(durationSamples, 1)];
        var inputSamples = new float[Math.Max(durationSamples, 1)];
        var pulsePositions = new[]
        {
            options.SampleRate * PulseOffsetMilliseconds / 1000,
            options.SampleRate * (PulseOffsetMilliseconds + PulseSpacingMilliseconds) / 1000,
            options.SampleRate * (PulseOffsetMilliseconds + 2 * PulseSpacingMilliseconds) / 1000,
        };

        using var stream = backend.OpenStream(options);
        var samplePosition = 0;
        var writtenInputSamples = 0;
        Exception? callbackError = null;
        var started = false;

        void ProcessAudio(float[] input, float[] output, int sampleCount)
        {
            try
            {
                var count = Math.Min(sampleCount, Math.Min(input.Length, output.Length));
                for (var index = 0; index < count; index++)
                {
                    var position = samplePosition + index;
                    output[index] = position == pulsePositions[0] ||
                                    position == pulsePositions[1] ||
                                    position == pulsePositions[2]
                        ? 0.8f
                        : 0.0f;

                    if (position >= 0 && position < outputSamples.Length)
                    {
                        outputSamples[position] = output[index];
                    }

                    if (writtenInputSamples < inputSamples.Length)
                    {
                        inputSamples[writtenInputSamples++] = input[index];
                    }
                }

                if (count < sampleCount)
                {
                    Array.Clear(output, count, output.Length - count);
                }

                samplePosition += count;
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref callbackError, exception, null);
                Array.Clear(output, 0, output.Length);
            }
        }

        try
        {
            stream.Start(ProcessAudio);
            started = true;
            await Task.Delay(TestDurationMilliseconds, cancellationToken).ConfigureAwait(false);
            stream.Stop();
            started = false;

            if (callbackError is not null)
            {
                throw new AudioBackendException("Audio callback failed.", callbackError);
            }

            return DetectLatency(
                inputSamples.AsSpan(0, Math.Min(writtenInputSamples, inputSamples.Length)).ToArray(),
                outputSamples.AsSpan(0, Math.Min(samplePosition, outputSamples.Length)).ToArray(),
                pulsePositions,
                options.SampleRate);
        }
        finally
        {
            if (started)
            {
                try
                {
                    stream.Stop();
                }
                catch
                {
                    // Preserve the original measurement or cancellation error.
                }
            }
        }
    }

    private static LatencyResult DetectLatency(
        float[] inputSamples,
        float[] outputSamples,
        IReadOnlyList<int> expectedPulses,
        int sampleRate)
    {
        var detections = new List<int>();
        var nextAllowed = 0;
        for (var index = 1; index < inputSamples.Length; index++)
        {
            if (index < nextAllowed ||
                Math.Abs(inputSamples[index]) < DetectionThreshold ||
                inputSamples[index] < inputSamples[index - 1])
            {
                continue;
            }

            detections.Add(index);
            nextAllowed = index + DetectionRefractorySamples;
        }

        var latencies = new List<int>();
        foreach (var detection in detections)
        {
            var expected = expectedPulses
                .OrderBy(pulse => Math.Abs(detection - pulse))
                .First();
            var latency = detection - expected;
            if (latency >= 0 && latency < sampleRate / 2)
            {
                latencies.Add(latency);
            }
        }

        if (latencies.Count == 0)
        {
            return new LatencyResult(false, 0, 0, sampleRate, outputSamples, inputSamples);
        }

        latencies.Sort();
        var median = latencies[latencies.Count / 2];
        return new LatencyResult(
            true,
            median,
            median * 1000.0 / sampleRate,
            sampleRate,
            outputSamples,
            inputSamples);
    }
}