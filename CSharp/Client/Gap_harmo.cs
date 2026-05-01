using Barotrauma;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

using HarmonyLib;


using System.Globalization;
using System.Reflection;// for bindingflags

namespace BarotraumaDieHard
{

    partial class GapPatch
    {
        [HarmonyPatch("EmitParticles")]
        [HarmonyPrefix]
        public static bool EmitParticlesPrefix(float deltaTime, Gap __instance)
        {
            Gap _ = __instance;

            if (_.flowTargetHull == null) { return false; }

            if (_.linkedTo.Count == 2 && _.linkedTo[0] is Hull hull1 && _.linkedTo[1] is Hull hull2)
            {
                //no flow particles between linked hulls (= rooms consisting of multiple hulls)
                if (hull1.linkedTo.Contains(hull2)) { return false; }
                foreach (var linkedEntity in hull1.linkedTo)
                {
                    if (linkedEntity is Hull h && h.linkedTo.Contains(hull1) && h.linkedTo.Contains(hull2)) { return false; }
                }
                foreach (var linkedEntity in hull2.linkedTo)
                {
                    if (linkedEntity is Hull h && h.linkedTo.Contains(hull1) && h.linkedTo.Contains(hull2)) { return false; }
                }
            }

            Vector2 pos = _.Position;
            if (_.IsHorizontal)
            {
                pos.X += Math.Sign(_.flowForce.X);
                pos.Y = MathHelper.Clamp(Rand.Range(_.higherSurface, _.lowerSurface), _.rect.Y - _.rect.Height, _.rect.Y);
            }
            if (_.flowTargetHull != null)
            {
                pos.X = MathHelper.Clamp(pos.X, _.flowTargetHull.Rect.X + 1, _.flowTargetHull.Rect.Right - 1);
                pos.Y = MathHelper.Clamp(pos.Y, _.flowTargetHull.Rect.Y - _.flowTargetHull.Rect.Height + 1, _.flowTargetHull.Rect.Y - 1);
            }

            //spawn less particles when there's already a large number of them
            float particleAmountMultiplier = 1.0f - GameMain.ParticleManager.ParticleCount / (float)GameMain.ParticleManager.MaxParticles;
            particleAmountMultiplier *= particleAmountMultiplier;


            //heavy flow -> strong waterfall type of particles
            if (_.LerpedFlowForce.LengthSquared() > 20000.0f)
            {
                _.particleTimer += deltaTime;
                if (_.IsHorizontal)
                {
                    float particlesPerSec = _.open * _.rect.Height * 0.1f * particleAmountMultiplier;
                    if (_.openedTimer > 0.0f) { particlesPerSec *= 1.0f + _.openedTimer * 10.0f; }
                    float emitInterval = 1.0f / particlesPerSec;
                    while (_.particleTimer > emitInterval)
                    {
                        Vector2 velocity = new Vector2(
                            MathHelper.Clamp(_.flowForce.X, -5000.0f, 5000.0f) * Rand.Range(0.5f, 0.7f),
                        _.flowForce.Y * Rand.Range(0.5f, 0.7f));

                        if (_.flowTargetHull.WaterVolume < _.flowTargetHull.Volume * 0.95f)
                        {
                            var particle = GameMain.ParticleManager.CreateParticle(
                                "watersplash",
                                (_.Submarine == null ? pos : pos + _.Submarine.Position) - Vector2.UnitY * Rand.Range(0.0f, 10.0f),
                                velocity, 0, _.flowTargetHull);
                            if (particle != null)
                            {
                                if (particle.CurrentHull == null) { GameMain.ParticleManager.RemoveParticle(particle); }
                                particle.Size *= Math.Min(Math.Abs(_.flowForce.X / 500.0f), 5.0f);
                            }
                            if (GapSize() <= Structure.WallSectionSize || !_.IsRoomToRoom)
                            {
                                CreateWaterSpatter();
                            }
                        }

                        if (Math.Abs(_.flowForce.X) > 300.0f && _.flowTargetHull.WaterVolume > _.flowTargetHull.Volume * 0.1f)
                        {
                            pos.X += Math.Sign(_.flowForce.X) * 10.0f;
                            if (_.rect.Height < 32)
                            {
                                pos.Y = _.rect.Y - _.rect.Height / 2;
                            }
                            else
                            {
                                float bottomY = _.rect.Y - _.rect.Height + 16;
                                float topY = MathHelper.Clamp(_.lowerSurface, bottomY, _.rect.Y - 16);
                                pos.Y = Rand.Range(bottomY, topY);
                            }
                            GameMain.ParticleManager.CreateParticle(
                                "bubbles",
                                _.Submarine == null ? pos : pos + _.Submarine.Position,
                                velocity, 0, _.flowTargetHull);
                        }
                        _.particleTimer -= emitInterval;
                    }
                }
                else
                {
                    //do not emit particles unless water is flowing towards the target hull
                    //(using lerpedFlowForce smooths out "flickers" when the direction of flow is rapidly changing)
                    if (Math.Sign(_.flowTargetHull.WorldPosition.Y - _.WorldPosition.Y) != Math.Sign(_.LerpedFlowForce.Y)) { return false; }

                    float particlesPerSec = Math.Max(_.open * _.rect.Width * particleAmountMultiplier, 10.0f);
                    float emitInterval = 1.0f / particlesPerSec;
                    while (_.particleTimer > emitInterval)
                    {
                        pos.X = Rand.Range(_.rect.X, _.rect.X + _.rect.Width + 1);
                        Vector2 velocity = new Vector2(
                            _.LerpedFlowForce.X * Rand.Range(0.5f, 0.7f),
                            MathHelper.Clamp(_.LerpedFlowForce.Y, -500.0f, 1000.0f) * Rand.Range(0.5f, 0.7f));

                        if (_.flowTargetHull.WaterVolume < _.flowTargetHull.Volume * 0.95f)
                        {
                            var splash = GameMain.ParticleManager.CreateParticle(
                                "watersplash",
                                _.Submarine == null ? pos : pos + _.Submarine.Position,
                                velocity, 0, _.flowTargetHull);
                            if (splash != null) 
                            {
                                if (splash.CurrentHull == null) { GameMain.ParticleManager.RemoveParticle(splash); }
                                splash.Size *= MathHelper.Clamp(_.rect.Width / 50.0f, 1.5f, 4.0f);
                            }
                            if (GapSize() <= Structure.WallSectionSize || !_.IsRoomToRoom)
                            {
                                CreateWaterSpatter();
                            }
                        }
                        if (Math.Abs(_.flowForce.Y) > 190.0f && Rand.Range(0.0f, 1.0f) < 0.3f && _.flowTargetHull.WaterVolume > _.flowTargetHull.Volume * 0.1f)
                        {
                            GameMain.ParticleManager.CreateParticle(
                                "bubbles",
                                _.Submarine == null ? pos : pos + _.Submarine.Position,
                                _.flowForce / 2.0f, 0, _.flowTargetHull);
                        }
                        _.particleTimer -= emitInterval;
                    }
                }
            }
            //light dripping
            else if (_.LerpedFlowForce.LengthSquared() > 100.0f && 
                /*no dripping from large gaps between rooms (looks bad)*/
                ((GapSize() <= Structure.WallSectionSize) || !_.IsRoomToRoom))
            {
                _.particleTimer += deltaTime; 
                float particlesPerSec = _.open * 10.0f * particleAmountMultiplier;
                float emitInterval = 1.0f / particlesPerSec;
                while (_.particleTimer > emitInterval)
                {
                    Vector2 velocity = _.flowForce;
                    if (!_.IsHorizontal)
                    {
                        velocity.X *= Rand.Range(1.0f, 3.0f);
                    }

                    if (_.flowTargetHull.WaterVolume < _.flowTargetHull.Volume)
                    {
                        GameMain.ParticleManager.CreateParticle(
                            Rand.Range(0.0f, _.open) < 0.05f ? "waterdrop" : "watersplash",
                            _.Submarine == null ? pos : pos + _.Submarine.Position,
                            velocity, 0, _.flowTargetHull);
                        CreateWaterSpatter();
                    }

                    GameMain.ParticleManager.CreateParticle(
                        "bubbles",
                        (_.Submarine == null ? pos : pos + _.Submarine.Position),
                        velocity, 0, _.flowTargetHull);

                    _.particleTimer -= emitInterval;
                }
            }
            else
            {
                _.particleTimer = 0.0f;
            }

            void CreateWaterSpatter()
            {
                Vector2 spatterPos = pos;
                float rotation;
                if (_.IsHorizontal)
                {
                    rotation = _.LerpedFlowForce.X > 0 ? 0 : MathHelper.Pi;
                    spatterPos.Y = _.rect.Y - _.rect.Height / 2;
                }
                else
                {
                    rotation = _.LerpedFlowForce.Y > 0 ? -MathHelper.PiOver2 : MathHelper.PiOver2;
                    spatterPos.X = _.rect.Center.X;
                }

                for (int i = 0; i < 10; i++) // Create multiple particles for more intensity
                {
                    Vector2 velocity = new Vector2(Rand.Range(-100, 100), Rand.Range(-50, 0)); // Adjust the velocity for effect
                    var spatter = GameMain.ParticleManager.CreateParticle(
                        "waterspatter",
                        _.Submarine == null ? spatterPos : spatterPos + _.Submarine.Position,
                        velocity, rotation, _.flowTargetHull);
                    
                    if (spatter != null)
                    {
                        if (spatter.CurrentHull == null) { GameMain.ParticleManager.RemoveParticle(spatter); }
                        spatter.Size *= MathHelper.Clamp(_.LerpedFlowForce.Length() / 100.0f, 1.0f, 2.0f); // Increase size for intensity
                        
                    }
                }
            }


            float GapSize()
            {
                return _.IsHorizontal ? _.rect.Height : _.rect.Width;
            }
            
            return false;
        }

    }

}

