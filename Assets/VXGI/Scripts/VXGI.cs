using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[ExecuteInEditMode]
public class VXGI : MonoBehaviour {

    // Enum representing the computation to be performed
    public enum Computation
    {
        SPECULAR,
        VOXELIZATION
    }
    
	// Structure representing an individual voxel element
	public struct Voxel
	{
		public int data;
	}

    [Header("General")]
    // Computation to be performed
	public Computation computation = Computation.SPECULAR;

    // Shader used to render the final scene using cone tracing
    public Shader lightingShader = null;
    
	// Threshold used in masking reflective surfaces
	public float threshold = 0.1f;
    
    // Strength of the direct lighting
    public float directLightingStrength = 0.5f;

    [Header("Indirect Specular Lighting")]
    
	// Number of cone tracing iterations used in the indirect specular lighting step
    [Range(0.0f, 500.0f)]
    public float maximumIterationsSpecular = 10;

    // Step value for the cone tracing process in the indirect specular lighting step
    public float coneStepSpecular = 0.5f;

    // Offset value for the cone tracing process in the indirect specular lighting step
    public float coneOffsetSpecular = 0.1f;

    // Strength of the computed indirect specular lighting
    public float indirectSpecularLightingStrength = 1.0f;
    
    // Downsampling for the indirect specular lighting
    public int downsampleSpecular = 1;

    // Number of blur iterations for the indirect specular lighting
    public int blurIterationsSpecular = 0;

    // Step value for blurring of indirect specular lighting
    public float blurStepSpecular = 1.0f;

	// Boundary of world volume which will be voxelized in the respective cascades
	public int worldVolumeBoundary = 1;

	// Dimension of the voxel grid for the specular voxel grid
	public int voxelVolumeDimensionSpecular = 1;

	// Gameobject which will be baking the reflections
	public GameObject reflectionBaker = null;

    // Material for the given shader
    private Material material = null;

	// Voxel data representation of specular voxel grid
	private Voxel[] voxelDataSpecular;

	// Structured buffer to hold the data in the specular voxel grid
	private ComputeBuffer voxelVolumeBufferSpecular = null;

    // Use this for initialization
    void Awake () {
        
		ReadBakedReflectionData ();

		GetComponent<Camera> ().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

        // Send all the shader variables through the material
		InitializeMaterial();

    }

	private void ReadBakedReflectionData() {

		// Specular voxel grid
		voxelDataSpecular = new Voxel[voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular];

		TextAsset reflectionData = Resources.Load<TextAsset>(reflectionBaker.gameObject.name);

		StreamReader reader = new StreamReader (new MemoryStream(reflectionData.bytes));

		int temp = 0;

		for (int i = 0; i < voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular * voxelVolumeDimensionSpecular; ++i) {

			temp = int.Parse(reader.ReadLine ());
			voxelDataSpecular [i].data = temp;

		}

		voxelVolumeBufferSpecular = new ComputeBuffer(voxelDataSpecular.Length, 4);
		voxelVolumeBufferSpecular.SetData(voxelDataSpecular);

		reader.Close ();

	}

	// Function to send shader variables with OnAwake() call
	private void InitializeMaterial() {

		// Create a material for the given shader
		if (lightingShader != null)
		{
			material = new Material(lightingShader);
		}

		// Set the general shader properties
		material.SetBuffer("_VoxelVolumeBufferSpecular", voxelVolumeBufferSpecular);
		material.SetInt("_VoxelVolumeDimensionSpecular", voxelVolumeDimensionSpecular);
		material.SetInt("_WorldVolumeBoundary", worldVolumeBoundary);
		material.SetFloat("_DirectStrength", directLightingStrength);
		material.SetFloat("_MaximumIterations", maximumIterationsSpecular);
		material.SetFloat("_ConeStep", coneStepSpecular);
		material.SetFloat("_ConeOffset", coneOffsetSpecular);
		material.SetFloat("_BlurStep", blurStepSpecular);
		material.SetFloat("_IndirectSpecularStrength", indirectSpecularLightingStrength);
		material.SetFloat ("threshold", threshold);

	}

	// Function called after rendering the current image
	void OnRenderImage (RenderTexture source, RenderTexture destination) {
        
		material.SetMatrix( "InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		material.SetMatrix( "InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		material.SetVector ("mainCameraPosition", GetComponent<Camera>().transform.position);

        // Indirect specular lighting
        if(computation == Computation.SPECULAR)
        {
            RenderTexture indirectSpecular = RenderTexture.GetTemporary(source.width / downsampleSpecular, source.height / downsampleSpecular);
            RenderTexture indirectSpecularTemp = RenderTexture.GetTemporary(source.width / downsampleSpecular, source.height / downsampleSpecular);

			Graphics.Blit(source, indirectSpecular, material, 0);
            
            for(int i = 0; i < blurIterationsSpecular; ++i)
            {
                Graphics.Blit(indirectSpecular, indirectSpecularTemp, material, 2);
                Graphics.Blit(indirectSpecularTemp, indirectSpecular, material, 3);
            }
	
			material.SetTexture("_IndirectSpecular", indirectSpecular);

            Graphics.Blit(source, destination, material, 1);

            RenderTexture.ReleaseTemporary(indirectSpecular);
            RenderTexture.ReleaseTemporary(indirectSpecularTemp);
        }
        // Voxelization debug pass
        else if(computation == Computation.VOXELIZATION)
        {
            Graphics.Blit(source, destination, material, 4);
        }
    }
}