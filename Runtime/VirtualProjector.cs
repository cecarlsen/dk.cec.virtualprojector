/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk

	This hack is limited by the URP cookie atlas max resolution which is 4096x4096.
	In practise you can only have a single 1920x1080 projection image at full resolution.
	https://discussions.unity.com/t/released-projector-simulator/684142/156

	On lens shifting
	- Be conservative, lens shift range varies for each projector and lens.
	- For Panasonic, you can find some of the shift constraints here: https://docs.connect.panasonic.com/projector/calculator/tdc/index.html
*/

using System;
using UnityEngine;

[ExecuteInEditMode]
public class VirtualProjector : MonoBehaviour
{
	[SerializeField] Texture _texture;
	[SerializeField,Range(0.1f,4f)] float _throwRatio = 0.5f;
	[SerializeField,Range(-0.5f,0.5f)] float _horizontalLensShift = 0f;
	[SerializeField,Range(-0.5f,0.5f)] float _verticalLensShift = 0f;
	[SerializeField] float _brightness = 1f;
	[SerializeField] float _range = 50f;
	[SerializeField] Color _tint = Color.white;

	[Header("Gizmos")]
	[SerializeField] bool _drawGizmosAlways = false;
	[SerializeField] Color _gizmoColor = new Color( 1f, 1f, 1f, 0.2f );

	[SerializeField,HideInInspector] Shader _blitShader;
	[SerializeField,HideInInspector] Light _light;

	RenderTexture _cookieTexture;
	Material _blitMaterial;

	int _WhitePass;
	int _TexturePass;

	bool _dirty;

	static readonly string logPrepend = $"<b>[{nameof( VirtualProjector )}]</b>";

	public float brightness
	{
		get { return _brightness; }
		set {
			_brightness = value > 0f ? value : 0f;
			if( _light ) _light.intensity = _brightness;
		}
	}

	public float range
	{
		get { return _range; }
		set {
			_range = value > 0f ? value : 0f;
			if( _light ) _light.range = _range;
		}
	}

	public Color tint
	{
		get { return _tint; }
		set {
			_tint = value;
			if( _light ) _light.color = _tint;
		}
	}


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

		brightness = _brightness;
		range = _range;
		tint = _tint;
	}


	void OnDrawGizmos()
	{
		if( _drawGizmosAlways ) DrawGizmos();
	}


	void OnDrawGizmosSelected()
	{
		if( !_drawGizmosAlways ) DrawGizmos();
	}


	void DrawGizmos()
	{
		float imageWidthPx = _texture ? _texture.width : 1f;
		float imageHeightPx = _texture ? _texture.height : 1f;
		float aspect = imageWidthPx / imageHeightPx;
		Vector3 corner = new Vector3( 0.5f / _throwRatio, 0.5f / aspect / _throwRatio, 1f );
		Vector2 shift = new Vector2( corner.x * _horizontalLensShift, corner.y * _verticalLensShift ) * 2f;
		Vector3 cornerUR = new Vector3( corner.x + shift.x, corner.y + shift.y, corner.z );
		Vector3 cornerLR = new Vector3( corner.x + shift.x, -corner.y + shift.y, corner.z );
		Vector3 cornerLL = new Vector3( -corner.x + shift.x, -corner.y + shift.y, corner.z );
		Vector3 cornerUL = new Vector3( -corner.x + shift.x, corner.y + shift.y, corner.z );
		
		Gizmos.color = _gizmoColor;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawRay( Vector3.zero, cornerUR * _range );
		Gizmos.DrawRay( Vector3.zero, cornerLR * _range );
		Gizmos.DrawRay( Vector3.zero, cornerLL * _range );
		Gizmos.DrawRay( Vector3.zero, cornerUL * _range );
	}


	void UpdateProjection()
	{
		// To avoid the spolight cropping the input image to a circle, we have to make the cookie texture larger than the
		// input image and pad it with black. This seems to be the dirty hack everyone does.

		float imageWidthPx = _texture ? _texture.width : 1f;
		float imageHeightPx = _texture ? _texture.height : 1f;

		float imageDiagonalExtents = Mathf.Sqrt( imageWidthPx * imageWidthPx + imageHeightPx * imageHeightPx ) * 0.5f;
		float imageWidthProportion = 0.5f * imageWidthPx / imageDiagonalExtents;
		float imageHeightProportion = 0.5f * imageHeightPx / imageDiagonalExtents;
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

		// TODO.
		float horizontalShiftProportion = ( 1f + _horizontalLensShift * 2f );
		float verticalShiftProportion = ( 1f + _verticalLensShift * 2f );
		float shiftedImageAreaWidthPx = imageWidthPx * horizontalShiftProportion; // We need to extend in both directions.
		float shiftedImageAreaHeightPx = imageHeightPx * verticalShiftProportion;
		float shiftedImageAreaDiagonalExtents = Mathf.Sqrt( shiftedImageAreaWidthPx * shiftedImageAreaWidthPx + shiftedImageAreaHeightPx * shiftedImageAreaHeightPx ) * 0.5f;
		float shiftedImageAreaWidthProportion = 0.5f * imageWidthPx / shiftedImageAreaDiagonalExtents;
		float shiftedImageAreaHeightProportion = 0.5f * imageHeightPx / shiftedImageAreaDiagonalExtents;
		float shiftedSpotWidthProportion = 1f / shiftedImageAreaWidthProportion;
		float shiftedSpotHeightProportion = 1f / shiftedImageAreaHeightProportion;
		float shiftedCokieSizePxF = imageWidthPx * shiftedSpotWidthProportion;
		int shiftedCokieSizePx = Mathf.CeilToInt( shiftedCokieSizePxF );
		float shiftedSpotAngle = Mathf.Atan2( 0.5f * shiftedSpotWidthProportion / _throwRatio, 1f ) * Mathf.Rad2Deg * 2f;
		var shiftedImageTransform = new Vector4(
			horizontalShiftProportion + spotWidthProportion,
			verticalShiftProportion + spotHeightProportion,
			-( 1f - imageWidthProportion ) * 0.5f * spotWidthProportion,
			-( 1f - imageHeightProportion ) * 0.5f * spotHeightProportion
		);

		//spotAngle = shiftedSpotAngle;
		//imageTransform = shiftedImageTransform;


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

		// Other lights settings.
		brightness = _brightness;
		range = _range;
		tint = _tint;
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
			_light.shadows = LightShadows.Hard;
			//_light.shadowStrength = 1;
			_dirty = true;
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