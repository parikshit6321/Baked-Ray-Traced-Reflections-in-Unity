using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class BakeReflections : MonoBehaviour {

	// Structure representing an individual voxel element
	public struct Voxel
	{
		public int data;
	}

	// Compute shader used to filter the unoccupied voxels
	public ComputeShader filteredVoxelizationShader = null;

	// Boundary of world volume which will be voxelized in the respective cascades
	public int worldVolumeBoundary = 1;

	// Dimension of the voxel grid for the specular voxel grid
	public int voxelVolumeDimensionSpecular = 1;

	// Array of gameobjects used for per object voxelization
	public Object[] objectsWithRenderer = null;

	// List of all the static objects in the scene
	public List<GameObject> objectsToBeVoxelized = null;

	// List of all the active objects in the scene
	public List<GameObject> activeObjects = null;

	// Voxel data representation of specular voxel grid
	private Voxel[] voxelDataSpecular;

	// Structured buffer to hold the data in the specular voxel grid
	private ComputeBuffer voxelVolumeBufferSpecular = null;

	// Camera references
	private Camera[] cameras = null;
	private Camera frontCamera = null;
	private Camera backCamera = null;
	private Camera leftCamera = null;
	private Camera rightCamera = null;
	private Camera topCamera = null;
	private Camera bottomCamera = null;

	// Function to bake all the scene geometry
	public void Bake () {

		objectsToBeVoxelized.Clear ();
		activeObjects.Clear ();

		// Initialize the voxel grid data
		InitializeVoxelGrid();

		// Initialize the camera references
		InitializeCameras();

		// Initialize the game objects array
		InitializeGameObjectsArray();

		// Filtered per object voxelization
		VoxelizePerObject();

		// Store the voxel data in an external file
		BakeVoxelGrid();
	}

	// Function to initialize the voxel grid data
	private void InitializeVoxelGrid() {

		// Specular voxel grid
		voxelDataSpecular = new Voxel[voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular];

		for (int i = 0; i < voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular; ++i)
		{
			voxelDataSpecular[i].data = 0;
		}

		voxelVolumeBufferSpecular = new ComputeBuffer(voxelDataSpecular.Length, 4);
		voxelVolumeBufferSpecular.SetData(voxelDataSpecular);

	}

	// Function to initialize the camera references
	private void InitializeCameras() {

		cameras = Resources.FindObjectsOfTypeAll<Camera>();

		for (int i = 0; i < cameras.Length; ++i)
		{
			if (cameras [i].name.Equals ("Front Camera")) {
				frontCamera = cameras [i];
				frontCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			} else if (cameras [i].name.Equals ("Back Camera")) {
				backCamera = cameras [i];
				backCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			} else if (cameras [i].name.Equals ("Left Camera")) {
				leftCamera = cameras [i];
				leftCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			} else if (cameras [i].name.Equals ("Right Camera")) {
				rightCamera = cameras [i];
				rightCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			} else if (cameras [i].name.Equals ("Top Camera")) {
				topCamera = cameras [i];
				topCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			} else if (cameras [i].name.Equals ("Bottom Camera")) {
				bottomCamera = cameras [i];
				bottomCamera.GetComponent<WorldPositionSecondary> ().Initialize ();
			}
		}
	}

	// Function to initialize the game objects array
	private void InitializeGameObjectsArray() {

		objectsWithRenderer = FindObjectsOfType(typeof(Renderer));

		for (int i = 0; i < objectsWithRenderer.Length; ++i) {

			GameObject tempObj = ((Renderer)objectsWithRenderer [i]).gameObject;

			activeObjects.Add (tempObj);

			if (tempObj.isStatic) {

				objectsToBeVoxelized.Add (tempObj);

			}

		}

	}

	// Function which voxelizes the scene and stores the grid in the voxel buffer
	private void Voxelize() {

		// Kernel index for the entry point in compute shader
		int kernelHandle = filteredVoxelizationShader.FindKernel("FilteredVoxelizationMain");

		int currentVoxelVolumeDimension = 1;

		filteredVoxelizationShader.SetBuffer(kernelHandle, "_VoxelVolumeBuffer", voxelVolumeBufferSpecular);
		filteredVoxelizationShader.SetInt("_VoxelVolumeDimension", voxelVolumeDimensionSpecular);
		currentVoxelVolumeDimension = voxelVolumeDimensionSpecular;

		// 1st pass
		// Render the color and position textures for the camera
		frontCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", frontCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", frontCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

		// 2nd pass
		// Render the color and position textures for the camera
		backCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", backCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", backCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

		// 3rd pass
		// Render the color and position textures for the camera
		leftCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", leftCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", leftCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

		// 4th pass
		// Render the color and position textures for the camera
		rightCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", rightCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", rightCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

		// 5th pass
		// Render the color and position textures for the camera
		topCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", topCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", topCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

		// 6th pass
		// Render the color and position textures for the camera
		bottomCamera.GetComponent<WorldPositionSecondary>().RenderTextures();

		filteredVoxelizationShader.SetTexture(kernelHandle, "_DirectLightingColorTexture", bottomCamera.GetComponent<WorldPositionSecondary>().GetDirectTexture());
		filteredVoxelizationShader.SetTexture(kernelHandle, "_PositionTexture", bottomCamera.GetComponent<WorldPositionSecondary>().GetPositionTexture());

		filteredVoxelizationShader.Dispatch(kernelHandle, currentVoxelVolumeDimension, currentVoxelVolumeDimension, 1);

	}

	// Function used for per object voxelization
	private void VoxelizePerObject() {

		// Deactivate all the gameobjects
		for (int i = 0; i < activeObjects.Count; ++i)
		{
			activeObjects[i].SetActive(false);
		}

		// Activate the gameobjects one by one and voxelize them
		for (int i = 0; i < objectsToBeVoxelized.Count; ++i)
		{
			objectsToBeVoxelized[i].SetActive(true);
			Voxelize();
			objectsToBeVoxelized[i].SetActive(false);

		}

		// Activate all the gameobjects
		for (int i = 0; i < activeObjects.Count; ++i)
		{
			activeObjects[i].SetActive(true);
		}
	}

	// Function to store the voxel data to an external file
	private void BakeVoxelGrid () {

		voxelVolumeBufferSpecular.GetData (voxelDataSpecular);

		StreamWriter writer = new StreamWriter(("Assets/Resources/" + this.gameObject.name + ".txt"), false);

		for (int i = 0; i < voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular; ++i) {

			writer.WriteLine (voxelDataSpecular [i].data.ToString ());

		}

		writer.Close ();

	}
}
