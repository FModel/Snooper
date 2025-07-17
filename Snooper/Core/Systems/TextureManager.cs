using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public class TextureManager : IGameSystem
{
    private readonly Dictionary<FGuid, Texture> _textures = [];
    private readonly Dictionary<FGuid, BindlessTexture> _bindless = [];
    
    private readonly Dictionary<FGuid, (PrimitiveSection Section, string Key)> _textureToSection = [];
    private readonly Dictionary<PrimitiveSection, int> _sectionPendingTextures = [];
    
    public event Action<PrimitiveSection>? OnSectionReady;
    
    private void Add(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (_textures.ContainsKey(texture.Guid))
        {
            throw new InvalidOperationException($"Texture with GUID {texture.Guid} already exists.");
        }
        
        if (_texturesToLoad.Contains(texture))
        {
            return;
            throw new InvalidOperationException($"Texture with GUID {texture.Guid} is already queued for loading.");
        }
        
        _texturesToLoad.Enqueue(texture);
    }
    
    public void AddRange(PrimitiveSection[] sections)
    {
        foreach (var section in sections)
        {
            if (section.DrawDataContainer is null) continue;
            
            var textures = section.DrawDataContainer.GetTextures();
            _sectionPendingTextures[section] = textures.Count;
            
            foreach (var (key, texture) in textures)
            {
                Add(texture);
                _textureToSection[texture.Guid] = (section, key);
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
                var bindless = new BindlessTexture(texture);
                bindless.Generate();
                bindless.MakeResident();
                _bindless[guid] = bindless;
                
                if (_textureToSection.TryGetValue(guid, out var mapping))
                {
                    mapping.Section.DrawDataContainer?.SetBindlessTexture(mapping.Key, bindless);
                    if (_sectionPendingTextures.TryGetValue(mapping.Section, out var count))
                    {
                        count--;
                        if (count <= 0)
                        {
                            _sectionPendingTextures.Remove(mapping.Section);
                            OnSectionReady?.Invoke(mapping.Section);
                        }
                        else
                        {
                            _sectionPendingTextures[mapping.Section] = count;
                        }
                    }
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