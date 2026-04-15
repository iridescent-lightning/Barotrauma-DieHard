using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Xml.Linq;
using Barotrauma.Networking;

using Barotrauma.Items.Components;

using Barotrauma.Extensions;
using Barotrauma;

namespace VerticalEngine//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    class VerticalEngine : Engine
    {
        //private ItemContainer itemContainer; 
		//private Item motor;


        public VerticalEngine(Item item, ContentXElement element)
            : base(item, element)
        {
            // Additional initialization if necessary
            // I guess this is where to link the xml element to the code
        }

        public override void Update(float deltaTime, Camera cam)
        {
            UpdateOnActiveEffects(deltaTime);//these two codes are causing the server to desync with client

            //;

            controlLockTimer -= deltaTime;

            if (powerConsumption == 0.0f)
            {
                prevVoltage = 1;
                hasPower = true;
            }
            else
            {
                hasPower = Voltage > MinVoltage;
            }

            if (lastReceivedTargetForce.HasValue)
            {
                targetForce = lastReceivedTargetForce.Value;
            }

            // Instead of using Force along the x-axis, create a vertical force
            Force = MathHelper.Lerp(force, (Voltage < MinVoltage) ? 0.0f : targetForce, deltaTime * 10.0f);

            if (Math.Abs(Force) > 1.0f)
            {
                float voltageFactor = MinVoltage <= 0.0f ? 1.0f : Math.Min(Voltage, MaxOverVoltageFactor);
                float currForce = force * MathF.Pow(voltageFactor, PowerToForceExponent);
                float condition = item.MaxCondition <= 0.0f ? 0.0f : item.Condition / item.MaxCondition;

                // Broken engine makes more noise.
                float noise = Math.Abs(currForce) * MathHelper.Lerp(1.5f, 1f, condition);
                UpdateAITargets(noise);

                // Arbitrary multiplier that was added to changes in submarine mass without having to readjust all engines
                float forceMultiplier = 0.1f;

                if (User != null)
                {
                    forceMultiplier *= MathHelper.Lerp(0.5f, 2.0f, (float)Math.Sqrt(User.GetSkillLevel("helm") / 100));
                }

                currForce *= item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.EngineMaxSpeed, MaxForce) * forceMultiplier;

                if (item.GetComponent<Repairable>() is { IsTinkering: true } repairable)
                {
                    currForce *= 1f + repairable.TinkeringStrength * TinkeringForceIncrease;
                }

                currForce = item.StatManager.GetAdjustedValueMultiplicative(ItemTalentStats.EngineSpeed, currForce);

                // Less effective when in a bad condition
                currForce *= MathHelper.Lerp(0.5f, 2.0f, condition);

                // Apply force vertically for a vertical engine
                Vector2 forceVector = new Vector2(0, currForce);

                // If the submarine is flipped vertically, reverse the force direction
                

                item.Submarine.ApplyForce(forceVector * deltaTime * Timing.FixedUpdateRate);

                UpdatePropellerDamage(deltaTime);

        #if CLIENT
                float particleInterval = 1.0f / particlesPerSec;
                particleTimer += deltaTime;
                UpdateAnimation(deltaTime);// this was out of the reprosscor. but it causeed desync in mp. so move it here.
                while (particleTimer > particleInterval)
                {
                    // Adjust the particle velocity for the vertical force
                    Vector2 particleVel = -forceVector.ClampLength(5000.0f) / 5.0f;

                    // Create particle at the adjusted position
                    GameMain.ParticleManager.CreateParticle("bubbles", item.WorldPosition + PropellerPos * item.Scale,
                        particleVel * Rand.Range(0.8f, 1.1f),
                        0.0f, item.CurrentHull);

                    particleTimer -= particleInterval;
                }
        #endif
            }
        }

    }
}