using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

/// <summary>
/// TODO: improve
/// </summary>
public class TextureManager : IGameSystem
{
    private readonly Dictionary<FGuid, Texture> _textures = [];
    private readonly Dictionary<FGuid, BindlessTexture> _bindless = [];
    
    private readonly Dictionary<FGuid, List<(int SectionId, string Key)>> _textureToSections = [];
    private readonly Dictionary<int, (PrimitiveSection Section, int RemainingTextures)> _sectionPendingTextures = [];
    
    public event Action<PrimitiveSection>? OnSectionReady;
    
    private void Add(PrimitiveSection section, string key, Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        
        var guid = texture.Guid;
        var sectionId = section.SectionId;
        
        if (_textures.ContainsKey(guid) || _texturesToLoad.Contains(texture))
        {
            if (!_textureToSections.TryGetValue(guid, out var list))
            {
                list = [];
                _textureToSections[guid] = list;
            }

            // Avoid duplicate section+key pair
            if (!list.Exists(entry => entry.SectionId == sectionId && entry.Key == key))
                list.Add((sectionId, key));
        
            return;
        }
        
        _texturesToLoad.Enqueue(texture);
        _textureToSections[guid] = [(sectionId, key)];
    }
    
    public void AddRange(PrimitiveSection[] sections)
    {
        foreach (var section in sections)
        {
            if (section.DrawDataContainer is null) continue;
            
            var textures = section.DrawDataContainer.GetTextures();
            _sectionPendingTextures[section.SectionId] = (section, textures.Count);
            
            foreach (var kvp in textures)
            {
                Add(section, kvp.Key, kvp.Value);
            }
        }
    }

    public void Load() => throw new NotImplementedException();
    public void Update(float delta) => DequeueTextures(1);
    public void Render(CameraComponent camera) => throw new NotImplementedException();
    
    private readonly Queue<Texture> _texturesToLoad = [];
    private void DequeueTextures(int limit = 0)
    {
        var count = 0;
        while (_texturesToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            // from wherever this is called, this will async decode the texture and upload it to the GPU on the main thread
            // once the texture is GPU ready, it will create a BindlessTexture representation of it and pass it to the event
            
            var texture = _texturesToLoad.Dequeue();
            var guid = texture.Guid;
            texture.TextureReadyForBindless += () =>
            {
                Log.Debug("Texture {Name} with GUID {Guid} is ready for bindless usage.", texture.Name, guid);
                
                var bindless = new BindlessTexture(texture);
                _bindless.Add(guid, bindless);
            
                if (_textureToSections.TryGetValue(guid, out var mappings))
                {
                    foreach (var (sectionId, key) in mappings)
                    {
                        if (!_sectionPendingTextures.TryGetValue(sectionId, out var entry))
                            continue;

                        var (section, remaining) = entry;

                        section.DrawDataContainer?.SetBindlessTexture(key, bindless);

                        remaining--;
                        if (remaining <= 0)
                        {
                            _sectionPendingTextures.Remove(sectionId);
                            OnSectionReady?.Invoke(section);
                        }
                        else
                        {
                            _sectionPendingTextures[sectionId] = (section, remaining);
                        }
                    }

                    _textureToSections.Remove(guid);
                }
            };
            texture.Generate();
            
            _textures.Add(guid, texture);
            count++;
        }
    }
    
    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }
        
        foreach (var texture in _bindless.Values)
        {
            texture.Dispose();
        }
        
        _textures.Clear();
        _bindless.Clear();
    }
}