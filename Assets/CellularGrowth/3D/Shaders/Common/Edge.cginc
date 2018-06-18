#ifndef __EDGE_INCLUDED__
#define __EDGE_INCLUDED__

struct Edge
{
    int a, b;
    float3 fa, fb;
    bool removable;
    bool alive;
};

bool has_cell_in_edge(Edge e, int i)
{
    return (e.a == i || e.b == i);
}

int opposite_cell_in_edge(Edge e, int i)
{
    if (e.b == i)
    {
        return e.a;
    }
    return e.b;
}

#endif // __EDGE_INCLUDED__
