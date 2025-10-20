using System.Diagnostics;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Sound.Node;
using CUE4Parse.UE4.Objects.UObject;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Snooper.Rendering.Cache;

public class AudioCache : IDisposable
{
    private readonly Dictionary<string, int> _buffers = [];

    public int GetOrCreateBuffer(USoundBase sound)
    {
        var soundPath = sound.GetPathName();
        if (_buffers.TryGetValue(soundPath, out var existingBuffer))
        {
            return existingBuffer;
        }

        try
        {
            var buffer = LoadSoundBuffer(sound);
            if (buffer >= 0)
            {
                _buffers[soundPath] = buffer;
            }
            return buffer;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load sound buffer: {Path}", soundPath);
            return 0;
        }
    }

    private int LoadSoundBuffer(USoundBase sound)
    {
        USoundWave? soundWave;
        switch (sound)
        {
            case USoundWave wave:
                soundWave = wave;
                break;
            case USoundCue cue:
            {
                soundWave = FindFirstWaveInCue(cue);
                if (soundWave == null)
                {
                    Log.Warning("No valid sound wave found in cue: {Path}", sound.GetPathName());
                    return -1;
                }
                break;
            }
            default:
                Log.Warning("Unsupported sound type: {Type} for {Path}", sound.GetType().Name, sound.GetPathName());
                return -1;
        }

        try
        {
            var audioData = ExtractAudioData(soundWave, out var format, out var sampleRate);
            if (audioData == null || audioData.Length == 0)
            {
                Log.Warning("No audio data extracted for: {Path}", sound.GetPathName());
                return -1;
            }

            var buffer = AL.GenBuffer();
            AL.BufferData(buffer, format, audioData, sampleRate);
            
            var error = AL.GetError();
            if (error != ALError.NoError)
            {
                Log.Error("OpenAL error loading buffer: {Error}", error);
                AL.DeleteBuffer(buffer);
                return -1;
            }

            Log.Debug("Loaded audio buffer: {Path} ({Size} bytes, {Rate} Hz, {Format})", sound.GetPathName(), audioData.Length, sampleRate, format);
            return buffer;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create audio buffer for: {Path}", sound.GetPathName());
            return -1;
        }
    }

    private USoundWave? FindFirstWaveInCue(USoundCue cue)
    {
        var firstNode = cue.FirstNode?.Load<USoundNode>();
        if (firstNode == null)
        {
            return null;
        }

        var queue = new Queue<USoundNode>();
        queue.Enqueue(firstNode);

        var visited = new HashSet<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            
            var nodePath = node.GetPathName();
            if (!visited.Add(nodePath))
            {
                continue;
            }

            if (node is USoundNodeWavePlayer wavePlayer)
            {
                var wave = TryGetWaveFromPlayer(wavePlayer);
                if (wave != null)
                {
                    Log.Debug("Found sound wave in cue node: {WavePath}", wave.GetPathName());
                    return wave;
                }
            }

            if (node.ChildNodes != null)
            {
                foreach (var childRef in node.ChildNodes)
                {
                    try
                    {
                        var childNode = childRef.Load<USoundNode>();
                        if (childNode != null)
                        {
                            queue.Enqueue(childNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to load child node in sound cue traversal");
                    }
                }
            }
        }

        return null;
    }

    private USoundWave? TryGetWaveFromPlayer(USoundNodeWavePlayer player)
    {
        try
        {
            var ptr = player.GetOrDefault<FSoftObjectPath?>("SoundWaveAssetPtr");
            if (ptr != null)
            {
                var wave = ptr.Value.Load<USoundWave>();
                if (wave != null)
                {
                    return wave;
                }
            }

            var waveRef = player.SoundWave;
            if (waveRef != null)
            {
                var wave = waveRef.Load<USoundWave>();
                if (wave != null)
                {
                    return wave;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to extract wave from player node");
        }

        return null;
    }

    private byte[]? ExtractAudioData(USoundWave soundWave, out ALFormat format, out int sampleRate)
    {
        format = ALFormat.Mono16;
        sampleRate = 48000;
        
        if (File.Exists($"{soundWave.Name}.wav"))
        {
            return File.ReadAllBytes($"{soundWave.Name}.wav");
        }
        
        soundWave.Decode(true, out var audioFormat, out var data);
        if (data == null || data.Length == 0)
        {
            Log.Warning("Failed to decode audio data for: {Path}", soundWave.GetPathName());
            return null;
        }

        switch (audioFormat)
        {
            case "RADA":
            case "BINKA":
            {
                var path = $"{soundWave.Name}.{audioFormat.ToLower()}";
                File.WriteAllBytes(path, data);
                
                if (TryDecode(path, out var rawFilePath))
                {
                    data = File.ReadAllBytes(rawFilePath);
                }

                break;
            }
        }

        return data;
    }

    private bool TryDecode(string filePath, out string rawFilePath)
    {
        rawFilePath = string.Empty;
        var decoderPath = $"{Path.GetExtension(filePath)[1..]}dec.exe";
        if (!File.Exists(decoderPath))
        {
            return false;
        }

        rawFilePath = Path.ChangeExtension(filePath, ".wav");
        var decoderProcess = Process.Start(new ProcessStartInfo
        {
            FileName = decoderPath,
            Arguments = $"-i \"{filePath}\" -o \"{rawFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        decoderProcess?.WaitForExit(5000);

        File.Delete(filePath);
        return decoderProcess?.ExitCode == 0 && File.Exists(rawFilePath);
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers.Values)
        {
            AL.DeleteBuffer(buffer);
        }
        _buffers.Clear();
    }
}
