using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Animation.Client;
using Keen.VRage.Animation.Client.Animator;
using Keen.VRage.Animation.Client.Data;
using Keen.VRage.Animation.Client.GameObjects;
using Keen.VRage.Animation.Client.Resources;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using SE2VR.Client.Configurations;
using SE2VR.Client.Input;
using static SE2VR.Client.Configurations.VRArmsConfiguration;

namespace SE2VR.Client.Components;

/// <summary>
/// Session component that poses the arms, fingers and head of the controlled character
/// TODO Rewrite and move to component on character input handler component ([Tick] for every session component sucks and will lead to crashes)
/// </summary>
public partial class VRArmsSessionComponent : SessionComponent
{
    [Configuration]
    private readonly VRArmsConfiguration _config;

    [Service]
    private readonly OpenVRSessionComponent _vrSession;

    private OpenVREngineComponent _vr;
    private OpenVRInputEngineComponent _input;

    //third person skeleton
    private Skeleton? _skeleton; 
    private int _head;

    private int _leftWrist;
    private int _leftElbowPart1;
    private int _leftElbow;
    private (int Bone, Finger Finger, RelativeTransform Reference, Vector3 Axis, float CurlAngle)[] _leftFingerJoints = [];

    private int _rightWrist;
    private int _rightElbowPart1;
    private int _rightElbow;
    private (int Bone, Finger Finger, RelativeTransform Reference, Vector3 Axis, float CurlAngle)[] _rightFingerJoints = [];

    [Init]
    protected new void Init()
    {
        base.Init();
        _vr = OpenVREngineComponent.Instance;
        _input = OpenVRInputEngineComponent.Instance;
    }

    [DoNotPause]
    [RigUpdateSystem.InverseKinematicsPostRagdollWindow.Begin]
    private void Tick()
    {
        if (_vrSession.ControlledEntity.Entity is not { } controlled || controlled.TryGet<CharacterComponent>() is null)
            return;

        if (controlled.TryGet<CharacterThirdPersonAnimatorStrategyComponent>() is not IRiggedAnimator rig)
            return;

        var skeleton = rig.Skeleton;
        var pose = rig.GetPose();

        if (_skeleton != skeleton)
        {
            _skeleton = skeleton;
            _head = skeleton.GetBone(_config.HeadBone).Index;
            ResolveArmBones(skeleton, _config.LeftHand, sign: 1, out _leftWrist, out _leftElbowPart1, out _leftElbow, out _leftFingerJoints);
            ResolveArmBones(skeleton, _config.RightHand, sign: -1, out _rightWrist, out _rightElbowPart1, out _rightElbow, out _rightFingerJoints);
        }

        if ((_vrSession.LeftHandOverride ?? _vr.LeftHand) is { } left)
        {
            PoseArm(ref pose, _leftWrist, _leftElbowPart1, _leftElbow, left * _config.LeftHand.WristOffset);
            PoseFingers(ref pose, _leftFingerJoints, _input.LeftFingers);
        }

        if (_vr.RightHand is { } right)
        {
            PoseArm(ref pose, _rightWrist, _rightElbowPart1, _rightElbow, right * _config.RightHand.WristOffset);
            PoseFingers(ref pose, _rightFingerJoints, _input.RightFingers);
        }

        if (_vr.Head is { } head)
        {
            pose.SetAbsolute(_head, head);
        }

        static void PoseArm(ref PoseContainer.PoseReference pose, int wrist, int elbowPart1, int elbow, RelativeTransform transform)
        {
            RelativeTransform absoluteWrist = pose.GetAbsolute(wrist);
            RelativeTransform absoluteElbowPart1 = pose.GetAbsolute(elbowPart1);
            RelativeTransform absoluteElbow = pose.GetAbsolute(elbow);

            var relativeElbowToWrist = RelativeTransform.GetRelativeTransform(absoluteElbow, absoluteWrist);
            var relativeElbowPart1ToWrist = RelativeTransform.GetRelativeTransform(absoluteElbowPart1, absoluteWrist);
            pose.SetAbsolute(elbow, transform * relativeElbowToWrist);
            pose.SetAbsolute(elbowPart1, transform * relativeElbowPart1ToWrist);
            pose.SetRelative(wrist, RelativeTransform.GetRelativeTransform(transform, transform * relativeElbowToWrist));
        }

        static void PoseFingers
        (
            ref PoseContainer.PoseReference pose, 
            (int Bone, Finger Finger, RelativeTransform Reference, Vector3 Axis, float CurlAngle)[] joints, 
            Dictionary<Finger, float> curls
        )
        {
            foreach (var joint in joints)
            {
                if (!curls.TryGetValue(joint.Finger, out float fingerCurl))
                    continue;

                var curl = Quaternion.CreateFromAxisAngle(joint.Axis, Math.Clamp(fingerCurl, 0, 1) * joint.CurlAngle);
                pose.SetRelative(joint.Bone, new RelativeTransform(joint.Reference.Position, curl * joint.Reference.Orientation));
            }
        }
    }

    /// <summary>
    /// Resolve the bone indices of an arm and how the fingers should be animated
    /// </summary>
    private static void ResolveArmBones
    (
        Skeleton skeleton,
        Hand hand, 
        int sign, 
        out int wrist, 
        out int elbowPart1, 
        out int elbow, 
        out (int Bone, Finger Finger, RelativeTransform Reference, Vector3 Axis, float CurlAngle)[] fingerJoints
    )
    {
        var wristBone = skeleton.GetBone(hand.WristBone);

        wrist = wristBone.Index;
        elbowPart1 = skeleton.GetBone(hand.ElbowPart1Bone).Index;
        elbow = wristBone.Parent;
        fingerJoints = CreateFingerJoints(skeleton, hand, sign, wristBone);
    }

    /// <summary>
    /// Derives the curl axis of every finger joint from the reference pose, so it does not depend on how the bones are oriented
    /// </summary>
    private static (int Bone, Finger Finger, RelativeTransform Reference, Vector3 CurlAxis, float CurlAngle)[] CreateFingerJoints
    (
        Skeleton skeleton,
        Hand hand, 
        int sign,
        SkeletonBone wrist
    )
    {
        var reference = new PoseContainer(skeleton);
        
        var index = hand.GetKnuckle(skeleton, Finger.Index);
        var middle = hand.GetKnuckle(skeleton, Finger.Middle);
        var pinky = hand.GetKnuckle(skeleton, Finger.Pinky);
        
        var forward = Vector3.Normalize(reference.GetAbsolute(middle.Index).Translation - reference.GetAbsolute(wrist.Index).Translation);
        var across = Vector3.Normalize(reference.GetAbsolute(index.Index).Translation - reference.GetAbsolute(pinky.Index).Translation);
        var palm = Vector3.Normalize(Vector3.Cross(forward, across)) * sign;

        var joints = new List<(int Bone, Finger Finger, RelativeTransform Reference, Vector3 Axis, float CurlAngle)>();
        foreach (var finger in hand.Fingers)
        {
            var bend = finger.Finger == Finger.Thumb ? Vector3.Normalize(palm - across) : palm;

            for (int joint = 0; joint < finger.Joints.Length; joint++)
            {
                var bone = skeleton.GetBone(finger.Joints[joint].Bone);
                var next = skeleton.GetBone(joint + 1 < finger.Joints.Length ? finger.Joints[joint + 1].Bone : finger.TipBone);
                var direction = Vector3.Normalize(reference.GetAbsolute(next.Index).Translation - reference.GetAbsolute(bone.Index).Translation);

                var axis = Vector3.Normalize(Vector3.Cross(direction, bend));
                var parentOrientation = Quaternion.CreateFromRotationMatrix(reference.GetAbsolute(bone.Parent));
                joints.Add((bone.Index, finger.Finger, reference.GetRelative(bone.Index), Vector3.Transform(axis, Quaternion.Conjugate(parentOrientation)), finger.Joints[joint].CurlAngle));
            }
        }

        return joints.ToArray();
    }
}
