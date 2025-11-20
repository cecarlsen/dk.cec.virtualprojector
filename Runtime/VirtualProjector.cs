/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk

	TODO: note that in URP you have to increate the cookie atlas size. Find a way to do this programmatically.
	https://discussions.unity.com/t/cookie-atlas-resolution-and-cookie-altas-format-documentation/861822
*/

using UnityEngine;

[ExecuteInEditMode]
public class VirtualProjector : MonoBehaviour
{
	[SerializeField] Texture _texture;
	[SerializeField,Range(0.1f,4f)] float _throwRatio = 0.5f;

	[SerializeField] Shader _blitShader;
	[SerializeField,HideInInspector] Light _light;

	RenderTexture _cookieTexture;
	Material _blitMaterial;

	int _WhitePass;
	int _TexturePass;

	bool _dirty;

	static readonly string logPrepend = $"<b>[{nameof( VirtualProjector )}]</b>";


	static class ShaderIDs
	{
		public static readonly int _MainTexTransform = Shader.PropertyToID( nameof( _MainTexTransform ) );
	}


	void OnEnable()
	{
		EnsureResources();
	}


	void OnDisable()
	{
		if( _cookieTexture != null ) _cookieTexture?.Release();
		_cookieTexture = null;
		if( _blitMaterial && !Application.isPlaying ) DestroyImmediate( _blitMaterial );
		_blitMaterial = null;
	}


	void Update()
	{
		if( !EnsureResources() ) return;

		if( _dirty ){
			UpdateProjection();
			_dirty = false;
		}
	}


	void OnValidate()
	{
		_dirty = true;
	}


	void UpdateProjection()
	{
		// To avoid the spolight cropping the input image to a circle, we have to make the cookie texture larger than the
		// input image and pad it with black. This seems to be the dirty hack everyone does.

		float imageWidthPx = _texture ? _texture.width : 1f;
		float imageHeightPx = _texture ? _texture.height : 1f;
		float aspect = imageWidthPx / imageHeightPx;
		float diagonalExtents = Mathf.Sqrt( imageWidthPx * imageWidthPx + imageHeightPx * imageHeightPx ) * 0.5f;
		float imageWidthProportion = 0.5f * imageWidthPx / diagonalExtents;
		float imageHeightProportion = 0.5f * imageHeightPx / diagonalExtents;
		float spotWidthProportion = 1f / imageWidthProportion;
		float spotHeightProportion = 1f / imageHeightProportion;
		float cokieSizePxF = imageWidthPx * spotWidthProportion;
		int cokieSizePx = Mathf.CeilToInt( cokieSizePxF );
		float spotAngle = Mathf.Atan2( 0.5f * spotWidthProportion / _throwRatio, 1f ) * Mathf.Rad2Deg * 2f;
		var imageTransform = new Vector4(
			spotWidthProportion,
			spotHeightProportion,
			-( 1f - imageWidthProportion ) * 0.5f * spotWidthProportion,
			-( 1f - imageHeightProportion ) * 0.5f * spotHeightProportion
		);

		// Update light.
		_light.spotAngle = spotAngle;
		_light.innerSpotAngle = spotAngle;

		// Create or resize cokie.
		if( !_cookieTexture || _cookieTexture.width != cokieSizePx )
		{
			if( _cookieTexture != null ) _cookieTexture?.Release();
			_cookieTexture = new RenderTexture( cokieSizePx, cokieSizePx, 16, RenderTextureFormat.ARGB32 );
			_cookieTexture.wrapMode = TextureWrapMode.Clamp;
			_cookieTexture.name = "VirtualProjection";
		}

		// Set shader constants.
		_blitMaterial.SetVector( ShaderIDs._MainTexTransform, imageTransform );
		
		// Copy texture into cookie.
		if( _texture ){
			Graphics.Blit( _texture, _cookieTexture, _blitMaterial, _TexturePass );
		} else {
			// TODO: Make a cool looking default grid here.
			Graphics.Blit( _cookieTexture, _blitMaterial, _WhitePass );
		}

		// Apply. This seems to be necesarry to update the rendered cookie.
		_light.cookie = _cookieTexture;
	}


	bool EnsureResources()
	{
		if( !_blitShader ){
			Debug.LogWarning( $"{logPrepend} Shader is missing. Please reset your VirtualProjector or recreate it.\n" );
			return false;
		}

		if( !_light ) _light = transform.GetComponentInChildren<Light>();
		if( !_light ){
			_light = new GameObject( "Light" ).AddComponent<Light>();
			_light.transform.SetParent( transform );
			_light.transform.localPosition = Vector3.zero;
			_light.transform.localRotation = Quaternion.identity;
			_light.type = LightType.Spot;
		}
		
		if( !_blitMaterial ){
			_blitMaterial = new Material( _blitShader );
			_WhitePass = _blitMaterial.FindPass( nameof( _WhitePass ) );
			_TexturePass = _blitMaterial.FindPass( nameof( _TexturePass ) );
			_dirty = true;
		}

		return true;
	}
}