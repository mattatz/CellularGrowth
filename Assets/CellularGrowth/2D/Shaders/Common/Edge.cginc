#ifndef __EDGE_INCLUDED__
#define __EDGE_INCLUDED__

struct Edge
{
    int a, b;
    float2 fa, fb;
    bool alive;
};

bool has(Edge e, int i)
{
    return (e.a == i || e.b == i);
}

int opposite(Edge e, int i)
{
    if (e.b == i)
    {
        return e.a;
    }
    return e.b;
}

#endif // __EDGE_INCLUDED__
