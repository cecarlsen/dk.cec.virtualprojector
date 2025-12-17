/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk

	This hack is limited by the cookie atlas max resolution which is 4096x4096 in URP and 16384 in HDRP.
	In practise you can only have a single 1920x1080 projection image at full resolution.
	https://discussions.unity.com/t/released-projector-simulator/684142/156

	On lens shifting
	- Be conservative, lens shift range varies for each projector and lens.
	- For Panasonic, you can find some of the shift constraints here: https://docs.connect.panasonic.com/projector/calculator/tdc/index.html
*/

using System;
using UnityEngine;
#if VIRTUAL_PROJECTOR_HDRP
	using UnityEngine.Rendering.HighDefinition;
#endif

[ExecuteInEditMode]
public class VirtualProjector : MonoBehaviour
{
	[Header("Content")]
	[SerializeField] Texture _texture;
	[SerializeField,Range(0f,1f)] float _blackLevel = 0f;
	
	[Header("Intrinsics")]
	[SerializeField,Range(0.1f,4f)] float _throwRatio = 0.5f;
	[SerializeField,Range(-1f,1f)] float _horizontalLensShift = 0f;
	[SerializeField,Range(-1f,1f)] float _verticalLensShift = 0f;

	[Header("Gizmos")]
	[SerializeField] bool _drawGizmosAlways = false;
	[SerializeField] Color _gizmoColor = new Color( 1f, 1f, 1f, 0.2f );

	[Header("Debug")]
	[SerializeField,Tooltip("Reveal how the texture is fitted into the spot circle.")] bool _showSpotArea = false;

	[SerializeField,HideInInspector] Shader _blitShader;
	[SerializeField,HideInInspector] Light _light;

	RenderTexture _cookieTexture;
	Material _blitMaterial;

	int _WhitePass;
	int _TexturePass;

	bool _dirty;


	public static readonly string logPrepend = $"<b>[{nameof( VirtualProjector )}]</b>";


	public Texture texture
	{
		get { return _texture; }
		set{
			_texture = value;
			_dirty = true;
		}
	}

	public float blackLevel
	{
		get { return _blackLevel; }
		set {
			_blackLevel = Mathf.Clamp01( value );
			_dirty = true;
		}
	}

	public bool showSpotArea
	{
		get { return _showSpotArea; }
		set {
			_showSpotArea = value;
			if( _blitMaterial ) _blitMaterial.SetColor( ShaderIDs._MaskColor, _showSpotArea ? Color.green : Color.black );
		}
	}



	static class ShaderIDs
	{
		public static readonly int _MainTexTransform = Shader.PropertyToID( nameof( _MainTexTransform ) );
		public static readonly int _MaskColor = Shader.PropertyToID( nameof( _MaskColor ) );
		public static readonly int _BlackLevel = Shader.PropertyToID( nameof( _BlackLevel ) );
	}


	void OnEnable()
	{
		EnsureResources();
		OnValidate();
	}


	void OnDisable()
	{
		if( _cookieTexture != null ) _cookieTexture?.Release();
		_cookieTexture = null;
		if( _blitMaterial && !Application.isPlaying ) DestroyImmediate( _blitMaterial );
		_blitMaterial = null;
	}


	void LateUpdate()
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

		blackLevel = _blackLevel;
		showSpotArea = _showSpotArea;
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
		if( !_light ) return;

		float imageWidthPx = _texture ? _texture.width : 1f;
		float imageHeightPx = _texture ? _texture.height : 1f;
		float aspect = imageWidthPx / imageHeightPx;
		Vector3 corner = new Vector3( 0.5f / _throwRatio, 0.5f / aspect / _throwRatio, 1f );
		Vector2 shift = new Vector2( corner.x * _horizontalLensShift, corner.y * _verticalLensShift ) * 2f;
		Vector3 cornerUR = new Vector3( corner.x + shift.x, corner.y + shift.y, corner.z ) * _light.range;
		Vector3 cornerLR = new Vector3( corner.x + shift.x, -corner.y + shift.y, corner.z ) * _light.range;
		Vector3 cornerLL = new Vector3( -corner.x + shift.x, -corner.y + shift.y, corner.z ) * _light.range;
		Vector3 cornerUL = new Vector3( -corner.x + shift.x, corner.y + shift.y, corner.z ) * _light.range;

		RaycastHit hit;
		if( Physics.Raycast( transform.position, transform.rotation * cornerUR, out hit, _light.range ) ) cornerUR = cornerUR.normalized * hit.distance;
		if( Physics.Raycast( transform.position, transform.rotation * cornerLR, out hit, _light.range ) ) cornerLR = cornerLR.normalized * hit.distance;
		if( Physics.Raycast( transform.position, transform.rotation * cornerLL, out hit, _light.range ) ) cornerLL = cornerLL.normalized * hit.distance;
		if( Physics.Raycast( transform.position, transform.rotation * cornerUL, out hit, _light.range ) ) cornerUL = cornerUL.normalized * hit.distance;
		
		Gizmos.color = _gizmoColor;
		Gizmos.matrix = Matrix4x4.TRS( transform.position, transform.rotation, Vector3.one );
		Gizmos.DrawRay( Vector3.zero, cornerUR );
		Gizmos.DrawRay( Vector3.zero, cornerLR );
		Gizmos.DrawRay( Vector3.zero, cornerLL );
		Gizmos.DrawRay( Vector3.zero, cornerUL );
	}


	void UpdateProjection()
	{
		// Unity spot lights use a build in mask shader that crops the cookie texture to a circle. If we want to render a
		// rectangle we have to fit it inside that circle. The spot cone is always on axis, so to simulate a lens shift (off axis projection), 
		// we have to make padding in both directions no matter what side we shift to. This seems to be the dirty hack everyone does.

		// So ... compute the texture transformation to handle this.
		int imageWidthPx = _texture ? _texture.width : 1;
		int imageHeightPx = _texture ? _texture.height : 1;
		float imageToSlideWidth = 1f + Mathf.Abs( _horizontalLensShift ) * 2f;
		float imageToSlideHeight = 1f + Mathf.Abs( _verticalLensShift ) * 2f;
		float slideWidthPxF = imageWidthPx * imageToSlideWidth;
		float slideHeightPxF = imageHeightPx * imageToSlideHeight;
		float cookieSizePxF = Mathf.Sqrt( slideWidthPxF * slideWidthPxF + slideHeightPxF * slideHeightPxF ); // The diagonal of the slide area is the cookie size.
		float cookieToImageWidth = imageWidthPx / cookieSizePxF;
		float cookieToImageHeight = imageHeightPx / cookieSizePxF;
		float cookieToSlideWidth = slideWidthPxF / cookieSizePxF;
		float cookieToSlideHeight = slideHeightPxF / cookieSizePxF;
		int cokieSizePx = Mathf.CeilToInt( cookieSizePxF );
		float spotAngle = Mathf.Atan2( 0.5f / cookieToImageWidth / _throwRatio, 1f ) * Mathf.Rad2Deg * 2f;
		float shiftX = 0.5f * ( 1f - cookieToSlideWidth ) / cookieToImageWidth;
		float shiftY = 0.5f * ( 1f - cookieToSlideHeight ) / cookieToImageHeight;
		var imageTransform = new Vector4(
			1f / cookieToImageWidth,
			1f / cookieToImageHeight,
			_horizontalLensShift > 0f ? - ( 1f-cookieToImageWidth ) / cookieToImageWidth + shiftX : -shiftX,
			_verticalLensShift > 0f ? - ( 1f-cookieToImageHeight ) / cookieToImageHeight + shiftY : -shiftY
		);

		// Create or resize cokie.
		if( !_cookieTexture || _cookieTexture.width != cokieSizePx )
		{
			if( _cookieTexture != null ){
				_cookieTexture?.Release();
				if( Application.isPlaying ) Destroy( _cookieTexture );
				else DestroyImmediate( _cookieTexture );
			}
			_cookieTexture = new RenderTexture( cokieSizePx, cokieSizePx, 0, RenderTextureFormat.ARGB32 );
			_cookieTexture.name = "VirtualProjection";
		}

		// Set shader constants.
		_blitMaterial.SetVector( ShaderIDs._MainTexTransform, imageTransform );
		_blitMaterial.SetFloat( ShaderIDs._BlackLevel, _blackLevel );
		
		// Copy and transform texture into cookie.
		if( _texture ){
			Graphics.Blit( _texture, _cookieTexture, _blitMaterial, _TexturePass );
			_cookieTexture.IncrementUpdateCount();
		} else {
			// TODO: Make a cool looking default grid here.
			Graphics.Blit( _cookieTexture, _blitMaterial, _WhitePass );
		}

		// Update light.
		_light.cookie = _cookieTexture; // Trigger re-compositing of the light cookie atlas.
		_light.spotAngle = spotAngle;
		_light.innerSpotAngle = spotAngle;

		// Some retard decided HDRP should ignore innerSpotAngle and instead have it's own innerSpotPercent.
		// https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@11.0/api/UnityEngine.Rendering.HighDefinition.HDAdditionalLightData.html
#if VIRTUAL_PROJECTOR_HDRP
		var hdLight = _light.GetComponent<HDAdditionalLightData>();
		if( hdLight ) hdLight.innerSpotPercent = 100f;
#endif
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