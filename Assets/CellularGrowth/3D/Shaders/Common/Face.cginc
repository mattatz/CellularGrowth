#ifndef __FACE_INCLUDED__
#define __FACE_INCLUDED__

struct Face
{
    int c0, c1, c2;
    int e0, e1, e2;
    bool removable;
    bool alive;
};

bool has_cell_in_face(Face f, int ic)
{
    return (f.c0 == ic || f.c1 == ic || f.c2 == ic);
}

bool has_edge_in_face(Face f, int ie)
{
    return (f.e0 == ie || f.e1 == ie || f.e2 == ie);
}

#endif // __FACE_INCLUDED__
