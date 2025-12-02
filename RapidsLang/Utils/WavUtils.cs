namespace RapidsLang.Utils;

public static class WavUtils
{
    private const int HeaderSize = 44;

    public static double GetDurationSeconds(byte[] file)
    {
        if (file.Length < HeaderSize) return 0;

        // WAV Header: Byte Rate is at offset 28 (4 bytes)
        // Data Size is at offset 40 (4 bytes)
        int byteRate = BitConverter.ToInt32(file, 28);
        int dataSize = BitConverter.ToInt32(file, 40);

        if (byteRate == 0) return 0;
        return (double)dataSize / byteRate;
    }

    public static byte[] CombineWavs(byte[] first, byte[] second)
    {
        if (first.Length < HeaderSize) return second;
        if (second.Length < HeaderSize) return first;

        // 1. Check compatibility (Sample Rate, Bits per sample, Channels)
        // Ideally throw an error if they don't match, or perform conversion.
        // For this example, we assume they match.

        // 2. Extract bodies
        var firstDataSize = BitConverter.ToInt32(first, 40);
        var secondDataSize = BitConverter.ToInt32(second, 40);
        
        // Sanity check: actual array length might differ from header claim if truncated
        var actualFirstBody = first.Length - HeaderSize; 
        var actualSecondBody = second.Length - HeaderSize;

        var newFile = new byte[HeaderSize + actualFirstBody + actualSecondBody];

        // 3. Copy Header from First
        Array.Copy(first, 0, newFile, 0, HeaderSize);

        // 4. Copy Body from First
        Array.Copy(first, HeaderSize, newFile, HeaderSize, actualFirstBody);

        // 5. Copy Body from Second
        Array.Copy(second, HeaderSize, newFile, HeaderSize + actualFirstBody, actualSecondBody);

        // 6. Fix the Header (Update ChunkSize and Subchunk2Size)
        var newTotalFileSize = newFile.Length - 8;
        var newDataSize = newFile.Length - HeaderSize;

        // Offset 4: ChunkSize (File Size - 8)
        BitConverter.GetBytes(newTotalFileSize).CopyTo(newFile, 4);
        
        // Offset 40: Subchunk2Size (Data Size)
        BitConverter.GetBytes(newDataSize).CopyTo(newFile, 40);

        return newFile;
    }
    
    // LLM Slop code
    
    public static byte[] MatchFormat(byte[] masterFile, byte[] targetFile)
    {
        // 1. Parse Headers
        var hA = ParseHeader(masterFile);
        var hB = ParseHeader(targetFile);

        // 2. Extract 16-bit samples from target
        short[] samples = BytesToShorts(targetFile);

        // 3. Match Channels (Mono <-> Stereo)
        if (hA.Channels != hB.Channels)
        {
            samples = ConvertChannels(samples, hB.Channels, hA.Channels);
        }

        // 4. Match Sample Rate (Resampling)
        if (hA.SampleRate != hB.SampleRate)
        {
            samples = Resample(samples, hB.Channels, hB.SampleRate, hA.SampleRate);
        }

        // 5. Rebuild WAV byte array
        return PackWav(samples, hA.Channels, hA.SampleRate);
    }
    
    private static short[] ConvertChannels(short[] input, int sourceChannels, int targetChannels)
    {
        if (sourceChannels == targetChannels) return input;

        // Mono to Stereo: Duplicate sample
        if (sourceChannels == 1 && targetChannels == 2)
        {
            var output = new short[input.Length * 2];
            for (int i = 0; i < input.Length; i++)
            {
                output[i * 2] = input[i];     // Left
                output[i * 2 + 1] = input[i]; // Right
            }
            return output;
        }

        // Stereo to Mono: Average samples
        if (sourceChannels == 2 && targetChannels == 1)
        {
            var output = new short[input.Length / 2];
            for (int i = 0; i < output.Length; i++)
            {
                // (Left + Right) / 2
                int sum = input[i * 2] + input[i * 2 + 1];
                output[i] = (short)(sum / 2);
            }
            return output;
        }

        // Fallback (rare cases like 5.1 surround): Return original
        return input;
    }

    private static short[] Resample(short[] input, int channels, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate) return input;

        // Calculate the ratio (e.g., 44100 / 48000 = 0.91875)
        double ratio = (double)sourceRate / targetRate;
        
        // Calculate new size
        int newSampleCount = (int)(input.Length / ratio);
        
        // Ensure we align to channel boundaries (must be multiple of channels)
        if (newSampleCount % channels != 0) newSampleCount -= (newSampleCount % channels);

        short[] output = new short[newSampleCount];

        // Linear Interpolation Algorithm
        for (int i = 0; i < output.Length / channels; i++)
        {
            // Where are we in the source array?
            double sourceIndex = i * ratio;
            int indexFloor = (int)sourceIndex;
            int indexCeil = indexFloor + 1;
            
            // Decimal part for weighting (how close are we to the next sample?)
            double weight = sourceIndex - indexFloor; 

            // Prevent array out of bounds
            if (indexCeil * channels >= input.Length) break;

            for (int c = 0; c < channels; c++)
            {
                short val1 = input[(indexFloor * channels) + c];
                short val2 = input[(indexCeil * channels) + c];

                // Interpolate: value = val1 + (difference * weight)
                output[(i * channels) + c] = (short)(val1 + (val2 - val1) * weight);
            }
        }

        return output;
    }
    
    private static WavHeaderInfo ParseHeader(byte[] data)
    {
        return new WavHeaderInfo
        {
            Channels = BitConverter.ToInt16(data, 22),
            SampleRate = BitConverter.ToInt32(data, 24),
            BitsPerSample = BitConverter.ToInt16(data, 34)
        };
    }

    private static short[] BytesToShorts(byte[] bytes)
    {
        // 16-bit PCM = 2 bytes per sample
        // Skip Header (44 bytes)
        int sampleCount = (bytes.Length - HeaderSize) / 2;
        short[] samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(bytes, HeaderSize + (i * 2));
        }
        return samples;
    }

    private static byte[] PackWav(short[] samples, int channels, int sampleRate)
    {
        int headerSize = 44;
        int dataSize = samples.Length * 2;
        byte[] bytes = new byte[headerSize + dataSize];

        // --- Write Header ---
        // RIFF header
        "RIFF"u8.ToArray().CopyTo(bytes, 0);
        BitConverter.GetBytes(dataSize + 36).CopyTo(bytes, 4); // File size
        "WAVE"u8.ToArray().CopyTo(bytes, 8);
        "fmt "u8.ToArray().CopyTo(bytes, 12);
        BitConverter.GetBytes(16).CopyTo(bytes, 16); // Header length
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20); // PCM format
        BitConverter.GetBytes((short)channels).CopyTo(bytes, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
        BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(bytes, 28); // ByteRate
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(bytes, 32); // BlockAlign
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34); // BitsPerSample
        "data"u8.ToArray().CopyTo(bytes, 36);
        BitConverter.GetBytes(dataSize).CopyTo(bytes, 40);

        // --- Write Data ---
        Buffer.BlockCopy(samples, 0, bytes, 44, dataSize);

        return bytes;
    }

    private struct WavHeaderInfo { public int Channels; public int SampleRate; public int BitsPerSample; }
}