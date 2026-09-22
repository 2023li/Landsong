#ifndef PROCESS_ANIMATIONS_HLSL_
#define PROCESS_ANIMATIONS_HLSL_

#include "Packages/com.rukhanka.animation/Rukhanka.Runtime/GPUAnimationEngine/Resources/TrackGroupSampler.hlsl"

/////////////////////////////////////////////////////////////////////////////////

#define NUM_MAXIMUM_LAYER_WEIGHTS 16
RWStructuredBuffer<BoneTransform> outAnimatedBones;
    
/////////////////////////////////////////////////////////////////////////////////

struct LayerInfo
{
    int index;
    float weight;
    int blendMode;
};

/////////////////////////////////////////////////////////////////////////////////

float2 NormalizeAnimationTime(float at, AnimationClip ac)
{
    at += ac.cycleOffset;
    if (at < 0) at = 1 + at;
    float normalizedTime = ac.IsLooped() ? frac(at) : saturate(at);
    float timeInSeconds = normalizedTime * ac.length;
    float2 rv = float2(timeInSeconds, normalizedTime);
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

BoneTransform MakeAdditiveAnimation(BoneTransform bonePose, BoneTransform zeroFramePose)
{
    BoneTransform rv;
    rv.pos = bonePose.pos - zeroFramePose.pos;
    Quaternion conjugateZFRot = Quaternion::NormalizeSafe(Quaternion::Conjugate(zeroFramePose.rot));
    conjugateZFRot = Quaternion::ShortestRotation(bonePose.rot, conjugateZFRot);
    rv.rot = Quaternion::Multiply(conjugateZFRot, Quaternion::Normalize(bonePose.rot));
    rv.scale = bonePose.scale / zeroFramePose.scale;
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

BoneTransform CalculateLoopPose(BoneTransform bonePose, TrackSet ts, HumanRotationData hrd, float normalizedTime)
{
    float lerpFactor = normalizedTime;

    TrackSampler ffSampler = CreateFirstFrameTrackSampler();
    BoneTransformAndFlags rootPoseStart = SampleTrackGroup(ts, ffSampler, hrd);
    TrackSampler lfSampler = CreateLastFrameTrackSampler();
    BoneTransformAndFlags rootPoseEnd = SampleTrackGroup(ts, lfSampler, hrd);

    float3 dPos = rootPoseEnd.bt.pos - rootPoseStart.bt.pos;
    Quaternion dRot = Quaternion::Multiply(Quaternion::Conjugate(rootPoseEnd.bt.rot), rootPoseStart.bt.rot);

    BoneTransform rv;
    rv.pos = bonePose.pos - dPos * lerpFactor;
    rv.rot = Quaternion::Multiply(bonePose.rot, Quaternion::Slerp(Quaternion::Identity(), dRot, lerpFactor));
    rv.scale = bonePose.scale;
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

bool SampleAnimation(AnimationClip ac, uint baseAddress, float2 animTime, int rigBoneIndex, uint rigBoneHash, int blendMode, HumanRotationData hrd, out BoneTransformAndFlags btf)
{
    btf = BoneTransformAndFlags::Identity();

    TrackSet tsClip = ac.clipTracks;
    tsClip.OffsetByAddress(baseAddress);
    
    int trackGroupIndex = tsClip.GetTrackGroupIndex(rigBoneHash);
    if (trackGroupIndex < 0)
        return false;

    tsClip.trackGroupsOffset += trackGroupIndex * 4;

    float timeInSeconds = animTime.x;
    TrackSampler tSampler = CreateDefaultTrackSampler(timeInSeconds);
    btf = SampleTrackGroup(tsClip, tSampler, hrd);

    if (blendMode == BLEND_MODE_ADDITIVE)
    {   
        TrackSet additiveTrackSet = ac.clipTracks;
        if (ac.additiveReferencePoseTracks.keyFramesOffset >= 0)
            additiveTrackSet = ac.additiveReferencePoseTracks;

        additiveTrackSet.OffsetByAddress(baseAddress);

        int additiveTrackGroupIndex = QueryPerfectHashTable(rigBoneHash, additiveTrackSet.trackGroupPHTSeed, additiveTrackSet.trackGroupPHTOffset, additiveTrackSet.trackGroupPHTSizeMask);
        if (additiveTrackGroupIndex >= 0)
        {
            TrackSampler ffSampler = CreateFirstFrameTrackSampler();
            additiveTrackSet.trackGroupsOffset += additiveTrackGroupIndex * 4;
            BoneTransformAndFlags additiveFramePose = SampleTrackGroup(additiveTrackSet, ffSampler, hrd);
            btf.bt = MakeAdditiveAnimation(btf.bt, additiveFramePose.bt);
        }
    }

    bool calculateLoopPose = ac.LoopPoseBlend() & rigBoneIndex != 0;
    if (calculateLoopPose)
    {
        btf.bt = CalculateLoopPose(btf.bt, tsClip, hrd, animTime.y);
    }

    return true;
}

/////////////////////////////////////////////////////////////////////////////////

BoneTransform AppendScaledPose(BoneTransform curPose, BoneTransform addedPose, float weight)
{
    BoneTransform rv;
    rv.pos = curPose.pos + addedPose.pos * weight;
    rv.rot = Quaternion::Nlerp(curPose.rot, addedPose.rot, weight);
    rv.scale = curPose.scale + addedPose.scale * weight;
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

BoneTransform BlendLayerPose(BoneTransform curPose, BoneTransform layerPose, BoneTransform refPose, LayerInfo layerInfo, float weightSum, float3 layerFlags)
{
    BoneTransform rv = curPose;

    //  Override
    if (layerInfo.blendMode == BLEND_MODE_OVERRIDE)
    {
        if (weightSum < 1)
            layerPose = AppendScaledPose(layerPose, refPose, max(0, 1 - weightSum));

        if (layerFlags.x > 0)
            rv.pos = lerp(curPose.pos, layerPose.pos, layerInfo.weight);

        if (layerFlags.y > 0)
        {
            layerPose.rot = Quaternion::ShortestRotation(curPose.rot, layerPose.rot);
            rv.rot = Quaternion::Nlerp(curPose.rot, layerPose.rot, layerInfo.weight);
        }

        if (layerFlags.z > 0)
            rv.scale = lerp(curPose.scale, layerPose.scale, layerInfo.weight);
    }
    //  Additive
    else
    {
        if (layerFlags.x > 0)
            rv.pos = curPose.pos + layerPose.pos * layerInfo.weight;

        if (layerFlags.y > 0)
        {
            Quaternion layerRot;
            layerRot.value = float4(layerPose.rot.value.xyz * layerInfo.weight, layerPose.rot.value.w);
            layerRot = Quaternion::NormalizeSafe(layerRot);
            layerRot = Quaternion::ShortestRotation(curPose.rot, layerRot);
            rv.rot = Quaternion::Multiply(curPose.rot, layerRot);
        }

        if (layerFlags.z > 0)
            rv.scale = curPose.scale * lerp(1, layerPose.scale, layerInfo.weight);
    }
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

LayerInfo GetLayerInfoFromAnimation(AnimationToProcess atp)
{
    LayerInfo rv;
    rv.weight = atp.layerWeight;
    rv.blendMode = atp.blendMode;
    rv.index = atp.layerIndex;
    return rv;
}

/////////////////////////////////////////////////////////////////////////////////

[numthreads(128, 1, 1)]
void ProcessAnimations(uint tid: SV_DispatchThreadID)
{
    if (tid >= (uint)animatedBonesCount)
        return;

    CHECK_STRUCTURED_BUFFER_OUT_OF_BOUNDS(RUKHANKADEBUGMARKERS_GPUANIMATOR_PROCESS_ANIMATIONS_ANIMATED_BONE_WORKLOAD_READ, tid, animatedBoneWorkload);
    AnimatedBoneWorkload boneWorkload = animatedBoneWorkload[tid];

    CHECK_STRUCTURED_BUFFER_OUT_OF_BOUNDS(RUKHANKADEBUGMARKERS_GPUANIMATOR_PROCESS_ANIMATIONS_ANIMATION_JOBS_READ, boneWorkload.animationJobIndex, animationJobs);
    AnimationJob animationJob = animationJobs[boneWorkload.animationJobIndex];

    RigDefinition rigDef = RigDefinition::ReadFromRawBuffer(rigDefinitions, animationJob.rigDefinitionIndex);
    RigBone rigBone = RigBone::ReadFromRawBuffer(rigBones, rigDef.rigBonesRange.x + boneWorkload.boneIndexInRig);

    HumanRotationData hrd = (HumanRotationData)0;
    if (rigDef.humanRotationDataRange.x >= 0)
        hrd = HumanRotationData::ReadFromRawBuffer(humanRotationDataBuffer, rigDef.humanRotationDataRange.x + boneWorkload.boneIndexInRig);

	BoneTransform blendedBonePose = rigBone.refPose;
    BoneTransform layerPose = BoneTransform::Zero();
    float weightSum = 0;
    float3 layerFlags = 0;
    LayerInfo layerInfo = (LayerInfo)0;

    int atpIndexStart = animationJob.animationsToProcessRange.x;
    int atpIndexEnd = animationJob.animationsToProcessRange.x + animationJob.animationsToProcessRange.y;
    for (int i = atpIndexStart; i < atpIndexEnd; ++i)
    {
        AnimationToProcess atp = animationsToProcess[i];
        bool inAvatarMask = IsBoneInAvatarMask(atp.avatarMaskDataOffset, rigBone.humanBodyPart, boneWorkload.boneIndexInRig);

        if (atp.animationClipAddress < 0 || atp.weight == 0 || atp.layerWeight == 0 || !inAvatarMask)
            continue;

        LayerInfo curLayerInfo = GetLayerInfoFromAnimation(atp);
        if (layerInfo.index != curLayerInfo.index)
        {
            blendedBonePose = BlendLayerPose(blendedBonePose, layerPose, rigBone.refPose, layerInfo, weightSum, layerFlags);
            weightSum = 0;
            layerFlags = 0;
            layerPose = BoneTransform::Zero();
        }
        layerInfo = curLayerInfo;

        int baseAddress = atp.animationClipAddress;
        AnimationClip ac = AnimationClip::ReadFromRawBuffer(animationClips, baseAddress);
        float2 animTime = NormalizeAnimationTime(atp.time, ac);

        BoneTransformAndFlags btf;
        if (SampleAnimation(ac, baseAddress, animTime, boneWorkload.boneIndexInRig, rigBone.hash, atp.blendMode, hrd, btf))
        {
            weightSum += atp.weight;
            layerFlags += btf.flags;
            layerPose = AppendScaledPose(layerPose, btf.bt, atp.weight);
        }
    }

    blendedBonePose = BlendLayerPose(blendedBonePose, layerPose, rigBone.refPose, layerInfo, weightSum, layerFlags);

    int outIndex = animationJob.animatedBoneIndexOffset + boneWorkload.boneIndexInRig;

    CHECK_STRUCTURED_BUFFER_OUT_OF_BOUNDS(RUKHANKADEBUGMARKERS_GPUANIMATOR_PROCESS_ANIMATIONS_OUT_ANIMATED_BONES_WRITE, outIndex, outAnimatedBones);
    outAnimatedBones[outIndex] = blendedBonePose;
}

#endif // PROCESS_ANIMATIONS_HLSL_
