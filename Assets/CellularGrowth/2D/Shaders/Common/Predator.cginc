#ifndef __PREDATOR_INCLUDED__
#define __PREDATOR_INCLUDED__

struct Predator
{
    float2 position;
    float2 velocity;
    float2 force;
    float radius;
    float stress;
    bool alive;
};

#endif // __PREDATOR_INCLUDED__
