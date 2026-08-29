// Morph target deformation, selected by SKINNED_MESH_VERTEX alongside the skinning hook.
// Pulled in by Buffers/MeshHooks.glsl, so it reaches CommonMesh.vert and
// Shadows/shadow_cascade.vert alike. Reads Buffers/PerSkinningData.glsl but does not own it:
// Hooks/skinning.glsl is included first and pulls it in, and EmbeddedShader.ResolveIncludes
// has no include guards, so including it again here would be a redefinition error.

#if defined(MESH_VERTEX_STAGE)

// Blends every morph affecting this vertex into the bind pose, before any skinning. The vertex owns
// one contiguous run of deltas, one entry per morph that touches it, so an arbitrary number of morphs
// stack additively in a single pass.
void MorphDeformVertex(PerDrawStatic draw, PerDrawCulled culled, int instance, inout MeshVertex v)
{
    PerMeshSkinningData skin = uSkinMeshDataBuffer[draw.MeshIndex];
    if (skin.MorphCount == 0u) return;

    uint baseOffset = skin.LOD_BaseMorphOffset[culled.Lod];
    if (baseOffset == 0xFFFFFFFFu) return; // the morphs never reach this LOD

    uint vertexIndex = uint(gl_VertexID - gl_BaseVertex);
    uint startIndex = uMorphDeltaOffsetBuffer[baseOffset + vertexIndex];
    uint endIndex = uMorphDeltaOffsetBuffer[baseOffset + vertexIndex + 1u];
    if (startIndex == endIndex) return; // no morph moves this vertex

    uint baseWeight = uSkinInstanceBuffer[instance].BaseMorphWeight;

    vec3 deltaPosition = vec3(0.0);
#if !defined(MESH_DEPTH_ONLY)
    vec3 deltaNormal = vec3(0.0);
#endif

    for (uint i = startIndex; i < endIndex; i++)
    {
        MorphDelta delta = uMorphDeltaBuffer[i];

        float weight = uMorphWeightBuffer[baseWeight + delta.MorphIndex];
        if (weight == 0.0) continue;

        vec2 positionXY = unpackHalf2x16(delta.PositionXY);
        vec2 positionZTangentX = unpackHalf2x16(delta.PositionZ_TangentX);

        deltaPosition += vec3(positionXY, positionZTangentX.x) * weight;
#if !defined(MESH_DEPTH_ONLY)
        deltaNormal += vec3(positionZTangentX.y, unpackHalf2x16(delta.TangentYZ)) * weight;
#endif
    }

    v.Position.xyz += deltaPosition;
#if !defined(MESH_DEPTH_ONLY)
    v.Normal.xyz = normalize(v.Normal.xyz + deltaNormal);
    v.Tangent = normalize(v.Tangent - dot(v.Tangent, v.Normal.xyz) * v.Normal.xyz);
#endif
}

#if !defined(MESH_DEPTH_ONLY)
// Mode 9: total morph displacement. Recomputes the blend because the deformation above keeps it
// local to itself; this is a debug-only path.
bool MorphVertexDebugColor(PerDrawStatic draw, PerDrawCulled culled, int instance, uint mode, inout vec3 color)
{
    if (mode != 9) return false;

    color = vec3(0.0);

    PerMeshSkinningData skin = uSkinMeshDataBuffer[draw.MeshIndex];
    if (skin.MorphCount == 0u) return true;

    uint baseOffset = skin.LOD_BaseMorphOffset[culled.Lod];
    if (baseOffset == 0xFFFFFFFFu) return true;

    uint vertexIndex = uint(gl_VertexID - gl_BaseVertex);
    uint startIndex = uMorphDeltaOffsetBuffer[baseOffset + vertexIndex];
    uint endIndex = uMorphDeltaOffsetBuffer[baseOffset + vertexIndex + 1u];

    uint baseWeight = uSkinInstanceBuffer[instance].BaseMorphWeight;

    vec3 deltaPosition = vec3(0.0);
    for (uint i = startIndex; i < endIndex; i++)
    {
        MorphDelta delta = uMorphDeltaBuffer[i];
        vec2 positionXY = unpackHalf2x16(delta.PositionXY);
        vec2 positionZTangentX = unpackHalf2x16(delta.PositionZ_TangentX);
        deltaPosition += vec3(positionXY, positionZTangentX.x) * uMorphWeightBuffer[baseWeight + delta.MorphIndex];
    }

    // saturates around a tenth of an engine unit, enough to read a facial morph
    color = heatmap(clamp(length(deltaPosition) * 10.0, 0.0, 1.0));
    return true;
}
#endif

#endif
