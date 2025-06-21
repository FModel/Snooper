using Snooper.Core.Systems;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem
{
    public override uint Order => 21;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;

    public override void Load()
    {
        Shader.Fragment =
"""
#version 460 core
layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;

in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vColor;
} fs_in;

void main()
{
    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = vec3(1.0) * fs_in.vColor;
    gColor.a = 1.0;
}
""";

        base.Load();
    }
}
