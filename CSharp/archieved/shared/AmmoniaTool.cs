using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;
using Barotrauma.MapCreatures.Behavior;
using Barotrauma;
using Barotrauma.Items.Components;




namespace BarotraumaDieHard
{
    partial class AmmoniaTool : RepairTool
    {

        private float ammoniaAmount;

        [Editable, Serialize(100f, IsPropertySaveable.Yes, description: "The Amount of lubercation deal per delta time.", alwaysUseInstanceValues: true)]
        public float AmmoniaAmount
        {
            get { return ammoniaAmount; }
            set { ammoniaAmount = value; }
        }

        public AmmoniaTool(Item item, ContentXElement element)
            : base(item, element)
        {
            
            
        }
		public override bool Use(float deltaTime, Character character = null)
        {
            if (character != null)
            {
                if (item.RequireAimToUse && !character.IsKeyDown(InputType.Aim)) { return false; }
            }
            
            float degreeOfSuccess = character == null ? 0.5f : DegreeOfSuccess(character);

            bool failed = false;
            if (Rand.Range(0.0f, 0.5f) > degreeOfSuccess)
            {
                ApplyStatusEffects(ActionType.OnFailure, deltaTime, character);
                failed = true;
            }
            if (UsableIn == UseEnvironment.None)
            {
                ApplyStatusEffects(ActionType.OnFailure, deltaTime, character);
                failed = true;
            }
            if (item.InWater)
            {
                if (UsableIn == UseEnvironment.Air)
                {
                    ApplyStatusEffects(ActionType.OnFailure, deltaTime, character);
                    failed = true;
                }
            }
            else
            {
                if (UsableIn == UseEnvironment.Water)
                {
                    ApplyStatusEffects(ActionType.OnFailure, deltaTime, character);
                    failed = true;
                }
            }
            if (failed)
            {
                // Always apply ActionType.OnUse. If doesn't fail, the effect is called later.
                ApplyStatusEffects(ActionType.OnUse, deltaTime, character);
                return false;
            }

            Vector2 rayStart;
            Vector2 rayStartWorld;
            Vector2 sourcePos = character?.AnimController == null ? item.SimPosition : character.AnimController.AimSourceSimPos;
            Vector2 barrelPos = item.SimPosition + ConvertUnits.ToSimUnits(TransformedBarrelPos);
            //make sure there's no obstacles between the base of the item (or the shoulder of the character) and the end of the barrel
            if (Submarine.PickBody(sourcePos, barrelPos, collisionCategory: Physics.CollisionWall | Physics.CollisionLevel | Physics.CollisionItemBlocking) == null)
            {
                //no obstacles -> we start the raycast at the end of the barrel
                rayStart = ConvertUnits.ToSimUnits(item.Position + TransformedBarrelPos);
                rayStartWorld = ConvertUnits.ToSimUnits(item.WorldPosition + TransformedBarrelPos);
            }
            else
            {
                rayStart = rayStartWorld = Submarine.LastPickedPosition + Submarine.LastPickedNormal * 0.1f;
                if (item.Submarine != null) { rayStartWorld += item.Submarine.SimPosition; }
            }

            //if the calculated barrel pos is in another hull, use the origin of the item to make sure the particles don't end up in an incorrect hull
            if (item.CurrentHull != null)
            {
                var barrelHull = Hull.FindHull(ConvertUnits.ToDisplayUnits(rayStartWorld), item.CurrentHull, useWorldCoordinates: true);
                if (barrelHull != null && barrelHull != item.CurrentHull)
                {
                    if (MathUtils.GetLineRectangleIntersection(ConvertUnits.ToDisplayUnits(sourcePos), ConvertUnits.ToDisplayUnits(rayStart), item.CurrentHull.Rect, out Vector2 hullIntersection))
                    {
                        if (!item.CurrentHull.ConnectedGaps.Any(g => g.Open > 0.0f && Submarine.RectContains(g.Rect, hullIntersection))) 
                        { 
                            Vector2 rayDir = rayStart.NearlyEquals(sourcePos) ? Vector2.Zero : Vector2.Normalize(rayStart - sourcePos);
                            rayStartWorld = ConvertUnits.ToSimUnits(hullIntersection - rayDir * 5.0f);
                            if (item.Submarine != null) { rayStartWorld += item.Submarine.SimPosition; }
                        }
                    }
                }
            }

            float spread = MathHelper.ToRadians(MathHelper.Lerp(UnskilledSpread, Spread, degreeOfSuccess));

            float angle = MathHelper.ToRadians(BarrelRotation) + spread * Rand.Range(-0.5f, 0.5f);
            float dir = 1;
            if (item.body != null)
            {
                angle += item.body.Rotation;
                dir = item.body.Dir;
            }
            Vector2 rayEnd = rayStartWorld + ConvertUnits.ToSimUnits(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Range * dir);

            ignoredBodies.Clear();
            if (character != null)
            {
                foreach (Limb limb in character.AnimController.Limbs)
                {
                    if (Rand.Range(0.0f, 0.5f) > degreeOfSuccess) continue;
                    ignoredBodies.Add(limb.body.FarseerBody);
                }
                ignoredBodies.Add(character.AnimController.Collider.FarseerBody);
            }

            IsActive = true;
            activeTimer = 0.1f;
            
            debugRayStartPos = ConvertUnits.ToDisplayUnits(rayStartWorld);
            debugRayEndPos = ConvertUnits.ToDisplayUnits(rayEnd);

            Submarine parentSub = character?.Submarine ?? item.Submarine;
            if (parentSub == null)
            {
                foreach (Submarine sub in Submarine.Loaded)
                {
                    Rectangle subBorders = sub.Borders;
                    subBorders.Location += new Point((int)sub.WorldPosition.X, (int)sub.WorldPosition.Y - sub.Borders.Height);
                    if (!MathUtils.CircleIntersectsRectangle(item.WorldPosition, Range * 5.0f, subBorders))
                    {
                        continue;
                    }
                    Repair(rayStartWorld - sub.SimPosition, rayEnd - sub.SimPosition, deltaTime, character, degreeOfSuccess, ignoredBodies);
                }
                Repair(rayStartWorld, rayEnd, deltaTime, character, degreeOfSuccess, ignoredBodies);
            }
            else
            {
                Repair(rayStartWorld - parentSub.SimPosition, rayEnd - parentSub.SimPosition, deltaTime, character, degreeOfSuccess, ignoredBodies);
            }
            
            UseProjSpecific(deltaTime, rayStartWorld); // change the class to partial to use this

            return true;
        }
        partial void UseProjSpecific(float deltaTime, Vector2 raystart);
        private void Repair(Vector2 rayStart, Vector2 rayEnd, float deltaTime, Character user, float degreeOfSuccess, List<Body> ignoredBodies)
        {
            
            var collisionCategories = Physics.CollisionWall | Physics.CollisionItem | Physics.CollisionLevel | Physics.CollisionRepairableWall;
            if (!IgnoreCharacters)
            {
                collisionCategories |= Physics.CollisionCharacter;
            }

            if (ammoniaAmount > 0.0f && item.Submarine != null)
            {
                
                Vector2 pos = ConvertUnits.ToDisplayUnits(rayStart + item.Submarine.SimPosition);

                // Could probably be done much efficiently here
                foreach (Item it in Item.ItemList)
                {
                    if (it.Submarine == item.Submarine && it.GetComponent<InternalCombustionEngine>() is { } engine)
                    {
                        //if (it.GetComponent<Holdable>() is { } holdable && holdable.Attachable && !holdable.Attached) { continue; }
                        
                        Rectangle collisionRect = it.WorldRect;
                        collisionRect.Y -= collisionRect.Height;
                        
                        
                        if (collisionRect.Left < pos.X && collisionRect.Right > pos.X && collisionRect.Bottom > pos.Y &&  collisionRect.Top < pos.Y) // It seems that the top and bottom are reversed
                        {
                           
                            Body collision = Submarine.PickBody(rayStart, it.SimPosition, ignoredBodies, collisionCategories);
                            if (collision == null)
                            {
                                //DebugConsole.NewMessage("Lubricating", Color.White);
                                if (engine.Ammonia < 1000f)
                                {
                                    engine.Ammonia += ammoniaAmount * deltaTime;
                                }
                                
                                this.item.Condition -= 1f * deltaTime;

#if CLIENT
                                //SoundPlayer.PlaySound("use_lubricate", this.item.WorldPosition, hullGuess: this.item.CurrentHull);
                                float barOffset = 10f * GUI.Scale;
                                //Vector2 offset = planter.PlantSlots.ContainsKey(i) ? planter.PlantSlots[i].Offset : Vector2.Zero;
                                user?.UpdateHUDProgressBar(engine, engine.Item.DrawPosition + new Vector2(barOffset, 0), engine.ammonia / 1000f, GUIStyle.Blue, GUIStyle.Blue, "progressbar.Lubricating");
#endif
                            }
                        }
                    }
                }
            }

        }
           
    }
        
}

