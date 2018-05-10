using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class WorldPositionSecondary : MonoBehaviour
{
    // Shader for writing the world position
    public Shader positionShader = null;
    
    // Render textures for storing the color with direct lighting
	public RenderTexture directTextureSpecular = null;

    // Render textures for storing the world position
    public RenderTexture positionTextureSpecular = null;

    // Boundaries for the world-space cascade volumes which will be voxelized
    private int worldVolumeBoundary = 1;

    // Dimensions of voxel grids of respective cascades
    private int voxelVolumeDimensionSpecular = 0;

    // Use this for initialization
    public void Initialize()
    {
        worldVolumeBoundary = GameObject.Find("Main Camera").GetComponent<VXGI>().worldVolumeBoundary;
		voxelVolumeDimensionSpecular = GameObject.Find("Main Camera").GetComponent<VXGI>().voxelVolumeDimensionSpecular;

		Shader.SetGlobalInt("_WorldVolumeBoundary", worldVolumeBoundary);

        directTextureSpecular = new RenderTexture(voxelVolumeDimensionSpecular, voxelVolumeDimensionSpecular, 32, RenderTextureFormat.ARGBFloat);
		positionTextureSpecular = new RenderTexture(voxelVolumeDimensionSpecular, voxelVolumeDimensionSpecular, 32, RenderTextureFormat.ARGBFloat);

		directTextureSpecular.filterMode = FilterMode.Trilinear;
		positionTextureSpecular.filterMode = FilterMode.Trilinear;

    }
		
    // Function to release the dynamically allocated memory
    public void ReleaseMemory()
    {
        directTextureSpecular.Release();
		positionTextureSpecular.Release();
    }

    // Function to get the appropriate direct texture
    public RenderTexture GetDirectTexture()
    {
		return directTextureSpecular;
    }

    // Function to get the appropriate position texture
    public RenderTexture GetPositionTexture()
    {
        return positionTextureSpecular;
    }

    // Function used to render the color, position and normal textures
    public void RenderTextures()
    {
        GetComponent<Camera>().orthographicSize = 10;

        // Render the color texture with direct lighting
		GetComponent<Camera>().targetTexture = directTextureSpecular;
        GetComponent<Camera>().Render();
        
        // Render the world position texture
		GetComponent<Camera>().targetTexture = positionTextureSpecular;
        GetComponent<Camera>().RenderWithShader(positionShader, null);

    }
}