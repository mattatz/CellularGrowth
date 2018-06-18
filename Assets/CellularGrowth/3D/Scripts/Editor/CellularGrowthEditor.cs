using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

namespace CellularGrowth.Dim3
{

    [CustomEditor (typeof(CellularGrowth))]
    public class CellularGrowthEditor : Editor {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if(GUILayout.Button("Divide"))
            {
                var cell = target as CellularGrowth;
                cell.Divide();
            }
        }

    }

}


