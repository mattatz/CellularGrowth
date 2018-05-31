#ifndef __CELL_INCLUDED__
#define __CELL_INCLUDED__

struct Cell
{
    float2 position;
    float2 velocity;
    float2 force;
    float radius;
    float threshold;
    float stress;
    int type;
    int links;
    int membrane;
    bool alive;
};

#endif // __CELL_INCLUDED__
