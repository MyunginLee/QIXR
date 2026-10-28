using System;
using System.IO;
using System.Text;

namespace Qixr.Companion
{
    public static class QuantumWav
    {
        public static byte[] Encode(float[] mono, int sampleRate)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + mono.Length*2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
                writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate*2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(mono.Length*2);
                foreach (float value in mono) writer.Write((short)(Math.Max(-1,Math.Min(1,value))*32767));
                return stream.ToArray();
            }
        }
        public static float[] Decode(byte[] data, out int sampleRate)
        {
            sampleRate = 0;
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new FormatException("Expected RIFF");
                reader.ReadInt32();
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new FormatException("Expected WAVE");
                short format=0, channels=0, bits=0;
                while (stream.Position+8 <= stream.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4)); int length=reader.ReadInt32();
                    long end=stream.Position+length;
                    if (length < 0 || end > stream.Length) throw new FormatException("Truncated WAV");
                    if (id == "fmt ")
                    {
                        if (length < 16) throw new FormatException("Invalid WAV format");
                        format=reader.ReadInt16(); channels=reader.ReadInt16(); sampleRate=reader.ReadInt32();
                        reader.ReadInt32(); reader.ReadInt16(); bits=reader.ReadInt16();
                    }
                    else if (id == "data")
                    {
                        if (format!=1 || channels!=1 || bits!=16 || sampleRate<8000 || sampleRate>96000 || length%2!=0)
                            throw new FormatException("Expected mono PCM16");
                        var samples = new float[length/2];
                        for (int i=0;i<samples.Length;i++) samples[i]=reader.ReadInt16()/32768f;
                        return samples;
                    }
                    stream.Position=end+(length%2);
                }
                throw new FormatException("WAV data missing");
            }
        }
    }
}
