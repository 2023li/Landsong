
using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

//=================================================================================================================//

namespace Rukhanka
{
partial struct AnimationProcessSystem
{
	struct LayerInfo
	{
		public int index;
		public float weight;
		public AnimationBlendingMode blendMode;
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    static float ComputeAnimatedProperty(float animatedValue, in NativeArray<AnimationToProcessComponent> animations, uint trackHash, uint propHash)
    {
		var layerValue = 0.0f;
		var rv = animatedValue;
		var layerHasAnimation = false;
		LayerInfo layerInfo = default;
		
		for (int l = 0; l < animations.Length; ++l)
		{
			var atp = animations[l];
			if (Hint.Unlikely(atp.animation == BlobAssetReference<AnimationClipBlob>.Null || atp.weight == 0 || atp.layerWeight == 0))
				continue;
			
			var curLayerInfo = GetLayerInfoFromAnimation(atp);
			
			//	Apply layer value
			if (layerInfo.index != curLayerInfo.index)
			{
				if (layerHasAnimation)
					rv = ApplyLayerValue(rv, layerValue, layerInfo);
				layerValue = 0;
				layerHasAnimation = false;
			}
			layerInfo = curLayerInfo;
			
			ref var trackSet = ref atp.animation.Value.clipTracks;
			var trackGroupIndex = trackSet.trackGroupPHT.Query(trackHash);
			if (trackGroupIndex < 0)
				continue;
			
			var animTime = ComputeBoneAnimationJob.NormalizeAnimationTime(atp.time, ref atp.animation.Value);
			
			var trackRange = new int2(trackSet.trackGroups[trackGroupIndex], trackSet.trackGroups[trackGroupIndex + 1]);
			for (var k = trackRange.x; k < trackRange.y; ++k)
			{
				var track = trackSet.tracks[k];
				if (track.props == propHash)
				{
					var curveValue = SampleTrack(ref trackSet, k, atp, animTime.x);
					if (atp.animation.Value.loopPoseBlend)
						curveValue -= CalculateTrackLoopValue(ref trackSet, k, atp, animTime.y);
					layerValue += curveValue * atp.weight;	
					layerHasAnimation = true;
					break;
				}
			}
		}
		
		if (layerHasAnimation)
			rv = ApplyLayerValue(rv, layerValue, layerInfo);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static float ApplyLayerValue(float currentValue, float layerValue, in LayerInfo layerInfo)
	{
		var rv = currentValue;
		if (Hint.Likely(layerInfo.blendMode == AnimationBlendingMode.Override))
		{
			rv = math.lerp(rv, layerValue, layerInfo.weight);
		}
		else
		{
			rv += layerValue * layerInfo.weight;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static float SampleTrack(ref TrackSet trackSet, int trackIndex, in AnimationToProcessComponent atp, float animTime)
	{ 
		var curveValue = BlobCurve.SampleAnimationCurve(ref trackSet, trackIndex, animTime);
		//	Make additive animation if requested
		if (atp.blendMode == AnimationBlendingMode.Additive)
		{
			var additiveValue = BlobCurve.SampleAnimationCurve(ref trackSet, trackIndex, 0);
			curveValue -= additiveValue;
		}
		return curveValue;
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static float CalculateTrackLoopValue(ref TrackSet trackSet, int trackIndex, in AnimationToProcessComponent atp, float normalizedTime)
	{
		var startV = SampleTrack(ref trackSet, trackIndex, atp, 0);
		var endV = SampleTrack(ref trackSet, trackIndex, atp, atp.animation.Value.length);

		var rv = (endV - startV) * normalizedTime;
		return rv;
	}
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static LayerInfo GetLayerInfoFromAnimation(in AnimationToProcessComponent atp)
	{
		var rv = new LayerInfo()
		{
			weight = atp.layerWeight,
			blendMode = atp.blendMode,
			index = atp.layerIndex
		};
		return rv;
	}
}
}
