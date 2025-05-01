using UnityEngine;
using System.Text;
using System.IO;
using System;

/// <summary>
/// WAV utility for recording and audio playback functions in Unity.
/// Updated to support direct float[] conversion for save operations.
/// </summary>
public class WavUtility
{
    const int BlockSize_16Bit = 2;

    public static byte[] FromAudioClip(AudioClip audioClip)
    {
        string file;
        return FromAudioClip(audioClip, out file, false);
    }

    public static byte[] FromAudioClip(AudioClip audioClip, out string filepath, bool saveAsFile = true, string dirname = "recordings")
    {
        filepath = null;

        float[] data = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(data, 0);

        return FromFloatArray(data, audioClip.channels, audioClip.frequency, out filepath, saveAsFile, dirname);
    }

    public static byte[] FromFloatArray(float[] data, int channels, int sampleRate)
    {
        string _; // discard path
        return FromFloatArray(data, channels, sampleRate, out _, false);
    }

    public static byte[] FromFloatArray(float[] data, int channels, int sampleRate, out string filepath, bool saveAsFile = true, string dirname = "recordings")
    {
        filepath = null;

        MemoryStream stream = new MemoryStream();
        const int headerSize = 44;
        UInt16 bitDepth = 16;

        int fileSize = data.Length * BlockSize_16Bit + headerSize;

        WriteFileHeader(ref stream, fileSize);
        WriteFileFormat(ref stream, channels, sampleRate, bitDepth);
        WriteFileData(ref stream, data, bitDepth);

        byte[] bytes = stream.ToArray();

        if (saveAsFile)
        {
            filepath = string.Format("{0}/{1}/{2}.{3}", Application.persistentDataPath, dirname, DateTime.UtcNow.ToString("yyMMdd-HHmmss-fff"), "wav");
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
            File.WriteAllBytes(filepath, bytes);
        }

        stream.Dispose();
        return bytes;
    }

    #region Header Writing

    private static int WriteFileHeader(ref MemoryStream stream, int fileSize)
    {
        stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(fileSize - 8), 0, 4);
        stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        return 12;
    }

    private static int WriteFileFormat(ref MemoryStream stream, int channels, int sampleRate, UInt16 bitDepth)
    {
        stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4); // Subchunk1 size
        stream.Write(BitConverter.GetBytes((ushort)1), 0, 2); // Audio format = 1 (PCM)
        stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
        stream.Write(BitConverter.GetBytes(sampleRate * channels * bitDepth / 8), 0, 4); // Byte rate
        stream.Write(BitConverter.GetBytes((ushort)(channels * bitDepth / 8)), 0, 2); // Block align
        stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);
        return 24;
    }

    private static int WriteFileData(ref MemoryStream stream, float[] data, UInt16 bitDepth)
    {
        byte[] id = Encoding.ASCII.GetBytes("data");
        stream.Write(id, 0, 4);

        int subchunk2Size = data.Length * BlockSize_16Bit;
        stream.Write(BitConverter.GetBytes(subchunk2Size), 0, 4);

        Int16 maxValue = Int16.MaxValue;
        byte[] bytes = new byte[data.Length * 2];

        for (int i = 0; i < data.Length; i++)
        {
            Int16 value = (short)Mathf.Clamp(data[i] * maxValue, short.MinValue, short.MaxValue);
            byte[] bytePair = BitConverter.GetBytes(value);
            bytes[i * 2] = bytePair[0];
            bytes[i * 2 + 1] = bytePair[1];
        }

        stream.Write(bytes, 0, bytes.Length);
        return bytes.Length + 8;
    }

    #endregion
}
