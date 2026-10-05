// what every lit or fogged shader reads about the frame. SceneLighting fills it and binds it once per frame,
// so a shader that includes this file has it without anyone setting a uniform on it.
// std140: a vec3 is always followed by the float that fills its fourth slot, keep it that way

#ifndef SCENE_DATA_GLSL
#define SCENE_DATA_GLSL

layout(std140, binding = BINDING_SCENE_DATA) uniform SceneData
{
    mat4 uInverseViewMatrix;

    vec3 uSunDirection; // world space, towards the sun
    float uSunIntensity;
    vec3 uSunColor;
    float uZNear;
    vec3 uSkyLightColor; // from above, its intensity in
    float uZFar;
    vec3 uSkyLightLowerColor;
    float uShadowSoftness;
    vec3 uCameraPosition;
    float uShadowNormalOffset;

    vec3 uFogColor;
    float uFogDensity;
    vec3 uFogDirectionalColor;
    float uFogDirectionalExponent;
    float uFogHeightFalloff;
    float uFogHeight;
    float uFogMaxOpacity;
    float uFogStartDistance;
    float uFogDirectionalStart;

    float uShadowBlend;
    int uShadowCascadeCount;
    int uGridDimX;
    int uGridDimY;
    int uGridDimZ;

    bool useSunLight;
    bool useShadows;
    bool useLighting;
    bool useFog;
};

#endif
