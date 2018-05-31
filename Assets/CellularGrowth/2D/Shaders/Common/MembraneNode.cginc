#ifndef __MEMBRANE_NODE_INCLUDED__
#define __MEMBRANE_NODE_INCLUDED__

struct MembraneNode
{
    float2 position;
    float2 velocity;
    float2 force;
    float radius;
    bool alive;
};

#endif
