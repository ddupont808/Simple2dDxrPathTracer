using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SocialPlatforms;

public class DXRCamera : MonoBehaviour
{	
	public Color SkyColor = Color.blue;
	public Color GroundColor = Color.gray;
	
	private Camera _camera;
	// target texture for raytracing
	private RenderTexture _dxrTarget;
	// textures for accumulation
	private RenderTexture _accumulationTarget1;
	private RenderTexture _accumulationTarget2;

	// scene structure for raytracing
	public RayTracingAccelerationStructure _rtas;

	// raytracing shader
	private RayTracingShader _rayTracingShader;

	// helper materials to accumulate raytracing results
	private Material _linearAccumulationMaterial;
	private Material _rollingAccumulationMaterial;
	private Material _denoiseMaterial;

	public enum Denoiser
	{
		None, LinearMean, MovingMean, TrousWaveletTransform
	}

	public Denoiser denoiser = Denoiser.None;

	private Matrix4x4 _cameraWorldMatrix;

	private int _frameIndex;

	public int rpp = 15;
	
	[Header("Moving Mean Settings")]
    public float _Smoothing = 1f;
	public float _Samples = 1f;

	[Header("À-TrousWavelet Transform Settings")]
	[Range(0f, 5f)]
	public float _DenoiseMin = 1f;
	[Range(0f, 5f)]
	public float _DenoiseMax = 4f;

	// TODO: apply motion vectors to accumulation texture

	private void Start()
	{
		Debug.Log("Raytracing support: " + SystemInfo.supportsRayTracing);

		_camera = GetComponent<Camera>();
		_camera.depthTextureMode = DepthTextureMode.Depth;

		_dxrTarget = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default);
		_dxrTarget.enableRandomWrite = true;
		_dxrTarget.Create();

		_accumulationTarget1 = new RenderTexture(_dxrTarget);
		_accumulationTarget2 = new RenderTexture(_dxrTarget);		

		
		_rayTracingShader = Resources.Load<RayTracingShader>("Pathtracer2D");

		// build scene for raytracing
		InitRaytracingAccelerationStructure();

		_rayTracingShader.SetTexture("_DxrTarget", _dxrTarget);
		// set shader pass name that will be used during raytracing
		_rayTracingShader.SetShaderPass("DxrPass");
			   
		// update raytracing parameters
		UpdateParameters();

		_linearAccumulationMaterial = new Material(Shader.Find("Hidden/LinearAccumulation"));
		_rollingAccumulationMaterial = new Material(Shader.Find("Hidden/RollingAccumulation"));
		_denoiseMaterial = new Material(Shader.Find("Hidden/Denoise"));
	}

	private void Update()
	{
		_rtas.Update();

		// update parameters if camera moved
		if (_cameraWorldMatrix != _camera.transform.localToWorldMatrix)
		{
			UpdateParameters();
		}

		// update parameters manually. after material or scene change
		if (Input.GetKeyDown(KeyCode.R))
		{
			UpdateParameters();
			_frameIndex = 0;
		}

		if(Input.GetKeyDown(KeyCode.P))
		{
			Physics2D.autoSimulation = !Physics2D.autoSimulation;
		}

		if(Input.GetKeyDown(KeyCode.Space))
		{
			var vals = System.Enum.GetValues(typeof(Denoiser)) as Denoiser[];
			denoiser = vals[((int)denoiser + 1) % vals.Length];
			_frameIndex = 0;
		}
	}

	private void UpdateParameters()
	{
		// update raytracing scene, in case something moved

		// frustum corners for current camera transform
		Vector3 bottomLeft = _camera.ViewportToWorldPoint(new Vector3(0, 0, -transform.transform.position.z));
		Vector3 topLeft = _camera.ViewportToWorldPoint(new Vector3(0, 1, -transform.transform.position.z));
		Vector3 bottomRight = _camera.ViewportToWorldPoint(new Vector3(1, 0, -transform.transform.position.z));
		Vector3 topRight = _camera.ViewportToWorldPoint(new Vector3(1, 1, -transform.transform.position.z));

		// update camera, environment parameters
		_rayTracingShader.SetVector("_SkyColor", SkyColor.gamma);
		_rayTracingShader.SetVector("_GroundColor", GroundColor.gamma);

		_rayTracingShader.SetVector("_TopLeftFrustumDir", topLeft);
		_rayTracingShader.SetVector("_TopRightFrustumDir", topRight);
		_rayTracingShader.SetVector("_BottomLeftFrustumDir", bottomLeft);
		_rayTracingShader.SetVector("_BottomRightFrustumDir", bottomRight);

		_rayTracingShader.SetVector("_CameraPos", _camera.transform.position);

		_cameraWorldMatrix = _camera.transform.localToWorldMatrix;
	}

	public void InitRaytracingAccelerationStructure()
	{
		if (_rtas != null)
		{
			_rtas.Dispose();
		}

		RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
		// include all layers
		settings.layerMask = ~0;
		// enable automatic updates
		settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
		// include all renderer types
		settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;

		_rtas = new RayTracingAccelerationStructure(settings);
		
		// collect all objects in scene and add them to raytracing scene
		Renderer[] renderers = FindObjectsOfType<Renderer>();
		foreach(Renderer r in renderers)
			_rtas.AddInstance(r);

		// build raytrasing scene
		_rtas.Build();

		_rayTracingShader.SetAccelerationStructure("_RaytracingAccelerationStructure", _rtas);
	}

	[ImageEffectOpaque]
	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		// update frame index and start path tracer
		_rayTracingShader.SetInt("_FrameIndex", _frameIndex);
		_rayTracingShader.SetInt("_RaysPerPixel", rpp);
		// start one thread for each pixel on screen
		_rayTracingShader.Dispatch("MyRaygenShader", _camera.pixelWidth, _camera.pixelHeight, 1, _camera);

		if (denoiser == Denoiser.LinearMean)
		{
			// update accumulation material
			_linearAccumulationMaterial.SetTexture("_CurrentFrame", _dxrTarget);
			_linearAccumulationMaterial.SetTexture("_Accumulation", _accumulationTarget1);
			_linearAccumulationMaterial.SetInt("_FrameIndex", _frameIndex);

			// accumulate current raytracing result
			Graphics.Blit(_dxrTarget, _accumulationTarget2, _linearAccumulationMaterial);
		} else if(denoiser == Denoiser.MovingMean)
		{
			// update accumulation material
			_rollingAccumulationMaterial.SetTexture("_CurrentFrame", _dxrTarget);
			_rollingAccumulationMaterial.SetTexture("_Accumulation", _accumulationTarget1);

			_rollingAccumulationMaterial.SetFloat("_Smoothing", _Smoothing);
			_rollingAccumulationMaterial.SetFloat("_Samples", _Samples);

			// accumulate current raytracing result
			Graphics.Blit(_dxrTarget, _accumulationTarget2, _rollingAccumulationMaterial);
		} else if(denoiser == Denoiser.TrousWaveletTransform)
		{
			_denoiseMaterial.SetTexture("_CurrentFrame", _dxrTarget);
			_denoiseMaterial.SetTexture("_LastDenoisedFrame", _accumulationTarget1);

			_denoiseMaterial.SetVector("_DenoiseRange", new Vector2(_DenoiseMin, _DenoiseMax));

			// accumulate current raytracing result
			Graphics.Blit(_dxrTarget, _accumulationTarget2, _denoiseMaterial);
		} else
		{
			Graphics.Blit(_dxrTarget, _accumulationTarget2);
		}

		_frameIndex++;

		// display result on screen
		Graphics.Blit(_accumulationTarget2, destination);
			
		// switch accumulate textures
		var temp = _accumulationTarget1;
		_accumulationTarget1 = _accumulationTarget2;
		_accumulationTarget2 = temp;
	}

	private void OnGUI()
	{
		// display samples per pixel
		//GUILayout.Label("SPP: " + _frameIndex);
	}

	private void OnDestroy()
	{		
		// cleanup
		_rtas.Release();
		_dxrTarget.Release();
		_accumulationTarget1.Release();
		_accumulationTarget2.Release();
	}
}
