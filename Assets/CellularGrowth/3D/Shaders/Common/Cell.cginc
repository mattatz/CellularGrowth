#ifndef __CELL_INCLUDED__
#define __CELL_INCLUDED__

struct Cell
{
    float3 position;
    float3 velocity;
    float3 force;
    float radius;
    float threshold;
    float stress;
    int type;
    int links;
    int faces;
    bool dividable;
    bool alive;
};

#endif // __CELL_INCLUDED__
