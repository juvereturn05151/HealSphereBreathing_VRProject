using System;
using System.IO;
using UnityEngine;

namespace HealSphere.Creation
{
    public static class SpeechWavCodec
    {
        public static byte[] EncodeMono(AudioClip clip, int frameCount)
        {
            if (clip == null || frameCount < clip.frequency / 10 || frameCount > clip.frequency * 20 || clip.channels > 2) throw new InvalidOperationException("Record between a moment and 20 seconds.");
            var source = new float[clip.samples * clip.channels]; clip.GetData(source, 0);
            using var stream = new MemoryStream(44 + frameCount * 2); using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frameCount * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(clip.frequency); writer.Write(clip.frequency * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frameCount * 2);
            for (int i = 0; i < frameCount; i++) { float sample = 0; for (int c = 0; c < clip.channels; c++) sample += source[i * clip.channels + c]; writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample / clip.channels, -1, 1) * 32767)); }
            return stream.ToArray();
        }
        public static AudioClip Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 4 * 1024 * 1024 || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || System.Text.Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") throw new InvalidOperationException("Invalid narration audio.");
            int channels = 0, rate = 0, start = 0, count = 0;
            for (int pos = 12; pos + 8 <= bytes.Length;)
            {
                var kind = System.Text.Encoding.ASCII.GetString(bytes, pos, 4); int size = BitConverter.ToInt32(bytes, pos + 4); int data = pos + 8;
                if (size < 0 || (long)data + size > bytes.Length) throw new InvalidOperationException("Invalid audio chunk.");
                if (kind == "fmt " && size >= 16) { if (BitConverter.ToInt16(bytes, data) != 1 || BitConverter.ToInt16(bytes, data + 14) != 16) throw new InvalidOperationException("Narration requires PCM16 WAV."); channels = BitConverter.ToInt16(bytes, data + 2); rate = BitConverter.ToInt32(bytes, data + 4); }
                if (kind == "data") { start = data; count = size; } pos = data + size + (size % 2);
            }
            if (channels < 1 || channels > 2 || rate < 16000 || rate > 48000 || count < 2 || count % (channels * 2) != 0 || (float)count / (channels * 2 * rate) > 45) throw new InvalidOperationException("Narration exceeds audio bounds.");
            var values = new float[count / 2]; for (int i = 0; i < values.Length; i++) values[i] = BitConverter.ToInt16(bytes, start + i * 2) / 32768f;
            var clip = AudioClip.Create("Rabbit narration", values.Length / channels, channels, rate, false); clip.SetData(values, 0); return clip;
        }
    }
}
