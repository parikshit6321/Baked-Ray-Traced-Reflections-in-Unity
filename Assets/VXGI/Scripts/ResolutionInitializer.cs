using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResolutionInitializer : MonoBehaviour {

	public Vector2Int screenResolution = Vector2Int.zero;

	// Use this for initialization
	void Start () {

		Screen.SetResolution (screenResolution.x, screenResolution.y, true);

	}
}
