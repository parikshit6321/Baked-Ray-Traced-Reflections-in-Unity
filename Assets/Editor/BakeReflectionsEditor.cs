using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BakeReflections))]
class BakeReflectionsEditor : Editor {

	private GameObject reflectionBaker = null;

	public override void OnInspectorGUI() {
	
		DrawDefaultInspector();

		if (GUILayout.Button ("Bake")) {

			reflectionBaker = GameObject.FindGameObjectWithTag ("VoxelReflectionBaker");
			reflectionBaker.GetComponent<BakeReflections> ().Bake ();

		}
			
	}

}