/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;
using System.Collections.Generic;

[InitializeOnLoad]
class VirtualProjectorScriptingDefineManager
{

	enum RenderPipeline { BiRP, URP, HDRP }

	const string VIRTUAL_PROJECTOR_HDRP = nameof( VIRTUAL_PROJECTOR_HDRP );


	static VirtualProjectorScriptingDefineManager()
	{
		var renderPipeline = currentRenderPileline;
		var namedBuiltTarget = currentNamedBuildTarget;

		var symbols = new List<string>();
		var rawSymnbols = PlayerSettings.GetScriptingDefineSymbols( namedBuiltTarget );
		if( rawSymnbols.Length > 0 ) symbols.AddRange( rawSymnbols.Split( ';' ) );

		bool change = false;
		if( renderPipeline == RenderPipeline.HDRP ){
			if( !symbols.Contains( VIRTUAL_PROJECTOR_HDRP ) ){
				symbols.Add( VIRTUAL_PROJECTOR_HDRP );
				Debug.Log( $"{VirtualProjector.logPrepend} Adding Scriptin Define Symbol VIRTUAL_PROJECTOR_HDRP.\n" );
				change = true;
			}
		} else { 
			if( symbols.Contains( VIRTUAL_PROJECTOR_HDRP ) ){
				symbols.Remove( VIRTUAL_PROJECTOR_HDRP );
				Debug.Log( $"{VirtualProjector.logPrepend} Removing Scriptin Define Symbol VIRTUAL_PROJECTOR_HDRP.\n" );
				change = true;
			}
		}

		if( change ){
			PlayerSettings.SetScriptingDefineSymbols( namedBuiltTarget, symbols.ToArray() );
			Debug.Log( string.Join( ';', symbols.ToArray() ) );
		}
	}



	static RenderPipeline currentRenderPileline
	{
		get {
			if( !GraphicsSettings.defaultRenderPipeline ) return RenderPipeline.BiRP;
			var pipelineName = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
			if( pipelineName.Contains( "HighDefinition" ) ) return RenderPipeline.HDRP;
			return RenderPipeline.URP;
		}
	}


	// Fron https://discussions.unity.com/t/unity-2021-2-get-current-namedbuildtarget/869508/9
	static NamedBuildTarget currentNamedBuildTarget
	{
		get
		{
#if UNITY_SERVER
			return NamedBuildTarget.Server;
#else
			BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
			BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
			NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
			return namedBuildTarget;
#endif
		}
	}
}
