using System.IO;
using NAudio.Wave;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Audio;

public static class OfflineMixExporter
{
    public static void ExportMix(
        IEnumerable<TrackState> trackStates,
        string destinationFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var activeTracks = trackStates.Where(t => File.Exists(t.FilePath)).ToList();
        if (activeTracks.Count == 0)
        {
            throw new InvalidOperationException("No hay pistas válidas para exportar.");
        }

        bool anySolo = activeTracks.Any(t => t.IsSolo);

        var readers = new List<AudioFileReader>();
        WaveFormat masterFormat = null!;
        long maxTotalSamples = 0;

        try
        {
            foreach (var t in activeTracks)
            {
                var reader = new AudioFileReader(t.FilePath);
                readers.Add(reader);
                if (masterFormat == null)
                {
                    masterFormat = reader.WaveFormat;
                }
                if (reader.Length > maxTotalSamples)
                {
                    maxTotalSamples = reader.Length;
                }
            }

            int channels = masterFormat.Channels;
            int sampleRate = masterFormat.SampleRate;

            string tempOut = destinationFilePath + ".tmp";
            using (var writer = new WaveFileWriter(tempOut, masterFormat))
            {
                int bufferSize = 4096 * channels;
                float[] mixedBuffer = new float[bufferSize];
                float[][] trackBuffers = new float[readers.Count][];
                for (int i = 0; i < readers.Count; i++)
                {
                    trackBuffers[i] = new float[bufferSize];
                }

                long samplesWritten = 0;

                while (samplesWritten < maxTotalSamples && !cancellationToken.IsCancellationRequested)
                {
                    Array.Clear(mixedBuffer, 0, bufferSize);
                    int maxReadThisChunk = 0;

                    for (int i = 0; i < readers.Count; i++)
                    {
                        var trackState = activeTracks[i];
                        bool isAudible = !trackState.IsMuted;
                        if (anySolo && !trackState.IsSolo) isAudible = false;

                        float vol = isAudible ? trackState.Volume : 0.0f;

                        int read = readers[i].Read(trackBuffers[i], 0, bufferSize);
                        if (read > maxReadThisChunk) maxReadThisChunk = read;

                        if (vol > 0.0001f)
                        {
                            for (int s = 0; s < read; s++)
                            {
                                mixedBuffer[s] += trackBuffers[i][s] * vol;
                            }
                        }
                    }

                    if (maxReadThisChunk == 0) break;

                    // Write float samples converted to WaveFileWriter format
                    writer.WriteSamples(mixedBuffer, 0, maxReadThisChunk);
                    samplesWritten += maxReadThisChunk;

                    if (progress != null && maxTotalSamples > 0)
                    {
                        double p = (double)samplesWritten / maxTotalSamples * 100.0;
                        progress.Report(Math.Min(100.0, p));
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
                return;
            }

            if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath);
            File.Move(tempOut, destinationFilePath);
        }
        finally
        {
            foreach (var r in readers)
            {
                r.Dispose();
            }
        }
    }
}
