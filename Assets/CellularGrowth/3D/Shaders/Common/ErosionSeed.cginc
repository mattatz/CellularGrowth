#ifndef __EROSION_SEED_INCLUDED__
#define __EROSION_SEED_INCLUDED__

struct SeedVertex
{
    float3 position;
    float radius;
    int index;
};

struct SeedEdge
{
    int a, b;
    int index;
};

struct SeedFace
{
    int c0, c1, c2;
    int e0, e1, e2;
};

#endif // 
