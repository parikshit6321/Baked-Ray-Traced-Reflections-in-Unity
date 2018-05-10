using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaskScript : MonoBehaviour {

	public RenderTexture maskTexture = null;
	public Shader positionShader = null;
	private int worldVolumeBoundary = 1;

	public void Initialize() {

		worldVolumeBoundary = GameObject.Find ("Main Camera").GetComponent<VXGI>().worldVolumeBoundary;
		maskTexture = RenderTexture.GetTemporary (Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32);
		GetComponent<Camera> ().targetTexture = maskTexture;
		Shader.SetGlobalInt("_WorldVolumeBoundary", worldVolumeBoundary);

	}

	public void RenderMask() {

		GetComponent<Camera> ().RenderWithShader (positionShader, null);

	}

	void OnDestroy() {

		RenderTexture.ReleaseTemporary (maskTexture);

	}

}
