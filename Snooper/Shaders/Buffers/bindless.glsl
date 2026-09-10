// Bindless texture handle storage inside SSBO structs.
//
// TEXTURE_HANDLE is the member type, TO_SAMPLER(h) turns it into a sampler2D at the sampling site.
// Declaring the member as sampler2D lets the compiler treat the handle as dynamically uniform and keep
// it in a scalar register. Declaring it as uvec2 (BINDLESS_RAW_HANDLES, injected by DeviceInfo for
// drivers that reject opaque types in buffer blocks) forces the compiler to assume divergence and
// waterfall every texture fetch, so only enable it where the sampler2D form does not compile.
#ifdef BINDLESS_RAW_HANDLES
#define TEXTURE_HANDLE uvec2
#define TO_SAMPLER(h) sampler2D(h)
#else
#define TEXTURE_HANDLE sampler2D
#define TO_SAMPLER(h) (h)
#endif
