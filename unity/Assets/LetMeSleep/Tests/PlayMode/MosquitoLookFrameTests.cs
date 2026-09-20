using System;
using LetMeSleep.Gameplay;
using LetMeSleep.Gameplay.Unity;
using NUnit.Framework;
using UnityEngine;

namespace LetMeSleep.Tests.PlayMode
{
    public sealed class MosquitoLookFrameTests
    {
        private static Rotation Frame(Float3 up) => Rotation.Look(Float3.Forward, up);
        private static Float3 ViewUp(MosquitoLookFrame frame) => new Rotation(frame.View.x, frame.View.y, frame.View.z, frame.View.w).Up;
        private static float Angle(Quaternion a, Quaternion b)
        {
            double dot=Math.Abs((double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w);
            return (float)(2*Math.Acos(Math.Min(1,dot))*180/Math.PI);
        }
        private static void Valid(MosquitoLookFrame frame)
        {
            Assert.That(frame.Forward.IsFinite,Is.True);Assert.That(frame.Forward.Length,Is.EqualTo(1).Within(.00001));
            var q=frame.View;Assert.That(MathEx.Finite(q.x)&&MathEx.Finite(q.y)&&MathEx.Finite(q.z)&&MathEx.Finite(q.w),Is.True);
            Assert.That(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w,Is.EqualTo(1).Within(.00001));
            var rotation=new Rotation(q.x,q.y,q.z,q.w);
            Assert.That((rotation.Forward-frame.Forward).Length,Is.LessThan(.00001));
            Assert.That(Math.Abs(Float3.Dot(rotation.Up,frame.Forward)),Is.LessThan(.00001));
        }
        [TestCase(.0101f,.0099f)] [TestCase(-.0101f,-.0099f)]
        public void ResetAcrossOldWallPoleThresholdHasNoHorizonFlip(float before,float after)
        {
            var body=Frame(new Float3(1,0,0));var a=new MosquitoLookFrame();var b=new MosquitoLookFrame();
            a.Reset(body,new Float3(1,0,before));b.Reset(body,new Float3(1,0,after));Valid(a);Valid(b);
            Assert.That(Angle(a.View,b.View),Is.LessThan(.1f));Assert.That(Float3.Dot(ViewUp(a),ViewUp(b)),Is.GreaterThan(.999f));
            Assert.That(Math.Asin(Float3.Dot(a.Forward,body.Up))*180/Math.PI,Is.EqualTo(89).Within(.001));
        }
        [Test] public void ZeroInputAtWallPoleIsFiniteAndDoesNotAccumulateRotation()
        {
            var body=Frame(new Float3(1,0,0));var frame=new MosquitoLookFrame();frame.Reset(body,new Float3(1,0,.0099f));var initial=frame.View;
            for(int i=0;i<1000;i++){frame.Step(body,0,0);Valid(frame);}
            Assert.That(Angle(initial,frame.View),Is.LessThan(.1f));
        }
        [Test] public void StepPitchSweepStopsBeforeSupportPoleAndReversesContinuously()
        {
            var body=Frame(new Float3(1,0,0));var frame=new MosquitoLookFrame();frame.Reset(body,Float3.Forward);var previous=frame.View;
            for(int i=0;i<120;i++)
            {
                frame.Step(body,0,(float)Math.PI/180);Valid(frame);
                Assert.That(Angle(previous,frame.View),Is.LessThan(1.1f));previous=frame.View;
                Assert.That(Float3.Dot(frame.Forward,body.Up),Is.LessThanOrEqualTo((float)Math.Sin(89*Math.PI/180)+.000001f));
            }
            frame.Step(body,0,-(float)Math.PI/180);Assert.That(Math.Asin(Float3.Dot(frame.Forward,body.Up))*180/Math.PI,Is.EqualTo(88).Within(.001));
        }
        [Test] public void FloorWallCeilingTransportPreservesSupportRelativePitch()
        {
            var frame=new MosquitoLookFrame();frame.Reset(Frame(Float3.Up),(Float3.Forward+Float3.Up*.3f).Normalized);
            float pitch=Float3.Dot(frame.Forward,Float3.Up);
            foreach(var up in new[]{new Float3(1,0,0),-Float3.Up,Float3.Up})
            {
                frame.Step(Frame(up),0,0);Valid(frame);
                Assert.That(Float3.Dot(frame.Forward,up),Is.EqualTo(pitch).Within(.00001));
                Assert.That(Float3.Dot(Float3.ProjectPlane(frame.Forward,up).Normalized,Float3.Forward),Is.GreaterThan(.9999f));
            }
        }
        [Test] public void AntipodalNormalUsesPriorTangentAndRoundTripsWithoutYawChange()
        {
            var frame=new MosquitoLookFrame();frame.Reset(Frame(Float3.Up),new Float3(1,.4f,1));var initial=frame.View;
            var tangent=Float3.ProjectPlane(frame.Forward,Float3.Up).Normalized;
            frame.Step(Frame(-Float3.Up),0,0);Valid(frame);
            Assert.That(Float3.Dot(Float3.ProjectPlane(frame.Forward,-Float3.Up).Normalized,tangent),Is.GreaterThan(.9999f));
            frame.Step(Frame(Float3.Up),0,0);Assert.That(Angle(initial,frame.View),Is.LessThan(.1f));
        }
        [Test] public void BodyYawChangesAloneNeverFeedBackIntoCamera()
        {
            var frame=new MosquitoLookFrame();frame.Reset(Rotation.Identity,new Float3(1,.2f,1));var initial=frame.View;
            for(int i=0;i<720;i++)frame.Step(Rotation.Yaw(i*.01f),0,0);
            Valid(frame);Assert.That(Angle(initial,frame.View),Is.LessThan(.1f));
        }
        [Test] public void YawIsAppliedOnceAboutSupportUp()
        {
            var body=Frame(new Float3(1,0,0));var frame=new MosquitoLookFrame();frame.Reset(body,Float3.Forward);
            frame.Step(body,(float)Math.PI/2,0);Assert.That((frame.Forward+Float3.Up).Length,Is.LessThan(.00001));
            var previous=frame.View;frame.Step(Rotation.Look(frame.Forward,body.Up),0,0);Assert.That(Angle(previous,frame.View),Is.LessThan(.1f));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ExactPoleResetUsesBodyTangentAndSignedPitch(int sign)
        {
            var body=Frame(new Float3(1,0,0));var frame=new MosquitoLookFrame();frame.Reset(body,body.Up*sign);Valid(frame);
            Assert.That(Float3.Dot(Float3.ProjectPlane(frame.Forward,body.Up).Normalized,body.Forward),Is.GreaterThan(.9999f));
            Assert.That(Math.Asin(Float3.Dot(frame.Forward,body.Up))*180/Math.PI,Is.EqualTo(sign*89).Within(.001));
        }
        [Test] public void InvalidDeltasAndResetAimDoNotProduceNonfiniteFrame()
        {
            var frame=new MosquitoLookFrame();frame.Reset(Rotation.Identity,new Float3(float.NaN,0,0));frame.Step(Rotation.Identity,float.NaN,float.PositiveInfinity);Valid(frame);
            Assert.That((frame.Forward-Float3.Forward).Length,Is.LessThan(.00001));
        }
    }
}
