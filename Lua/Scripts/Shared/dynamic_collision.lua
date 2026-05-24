LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.FishAnimController"], "prevCollisionCategory")
LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.HumanoidAnimController"], "prevCollisionCategory")
LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.Explosion"], "force")
LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.Items.Components.Throwable"], "midAir")
LuaUserData.MakeMethodAccessible(Descriptors["Barotrauma.ItemPrefab"], "set_DamagedByProjectiles")

--[[for prefab in ItemPrefab.Prefabs do
    local element = prefab.ConfigElement.Element
    if element.Element("Projectile") == nil then
        prefab.set_DamagedByProjectiles(true)
    end
end--]]

local function MakeItemCollide(item, notCollideCharacter)--The standard item collide for tag collidable
    if (item.HasTag('collidable')) then
        local collision = bit32.bor(Physics.CollisionWall, Physics.CollisionLevel)
        --collision = bit32.bor(collision, Physics.CollisionPlatform)
        if not notCollideCharacter  then
            collision = bit32.bor(collision, Physics.CollisionCharacter)
        end
        collision = bit32.bor(collision, Physics.CollisionItem)
        collision = bit32.bor(collision, Physics.CollisionProjectile)
		collision = bit32.bor(collision, Physics.CollisionPlatform)
        item.body.CollidesWith = collision
        item.body.CollisionCategories = Physics.CollisionItem
    end

    local door = item.GetComponentString("Door")

    if door and door.Body then
        door.Body.CollidesWith = bit32.bor(door.Body.CollidesWith, Physics.CollisionWall)
    end
end

local function MakeItemCollideMidAir(item, notCollideCharacter)--midair item collides with character
    if (item.HasTag('midair_collide')) then
		
        local collision = bit32.bor(Physics.CollisionWall, Physics.CollisionLevel)
		
        if not notCollideCharacter  then
            collision = bit32.bor(collision, Physics.CollisionCharacter)
        end
        collision = bit32.bor(collision, Physics.CollisionItem)
        collision = bit32.bor(collision, Physics.CollisionProjectile)
		collision = bit32.bor(collision, Physics.CollisionPlatform)
        item.body.CollidesWith = collision
        item.body.CollisionCategories = Physics.CollisionWall
    end
end
local function MakeItemCollideAlways(item, notCollideCharacter)--always collide with everything
    if (item.HasTag('collidable_always')) then
        local collision = bit32.bor(Physics.CollisionWall, Physics.CollisionLevel)

        if not notCollideCharacter  then
            collision = bit32.bor(collision, Physics.CollisionCharacter)
        end
        collision = bit32.bor(collision, Physics.CollisionItem)
        collision = bit32.bor(collision, Physics.CollisionProjectile)
		collision = bit32.bor(collision, Physics.CollisionPlatform)
        item.body.CollidesWith = collision
        item.body.CollisionCategories = Physics.CollisionWall
    end
end

local function MakeItemCollideGrenadeMidAir(item, notCollideCharacter)--sepcial grenade midair collide 

        local collision = bit32.bor(Physics.CollisionWall, Physics.CollisionLevel)

        if not notCollideCharacter  then
            collision = bit32.bor(collision, Physics.CollisionCharacter)
        end
        collision = bit32.bor(collision, Physics.CollisionItem)
        collision = bit32.bor(collision, Physics.CollisionProjectile)
		collision = bit32.bor(collision, Physics.CollisionPlatform)
        item.body.CollidesWith = collision
        item.body.CollisionCategories = Physics.CollisionRepairableWall
		
end

Hook.Patch("Barotrauma.Items.Components.Door", "OnItemLoaded", function (instance, ptable)
    MakeItemCollide(instance.Item)
end, Hook.HookMethodType.After)

Hook.Add("item.drop", "ItemCollision.TemporaryCollision", function (item, dropper)
    if not dropper then return end

    MakeItemCollideAlways(item, true)
    Timer.Wait(function ()
        MakeItemCollideAlways(item)
    end, 500)
end)


Hook.Add("item.created", "ItemCollision.MakeItemCollider", function (item)
	if (item.HasTag('collidable')) then
    MakeItemCollide(item)
	elseif (item.HasTag('collidable_always'))then
	MakeItemCollideAlways(item)
	end
end)

for key, value in pairs(Item.ItemList) do
    if (value.HasTag('collidable')) then
    MakeItemCollide(value)
    --original was MakeItemCollide(value). But it looked like an error. Now changed to 'item'
	end
end

local bluntTrauma = AfflictionPrefab.Prefabs["blunttrauma"]
Hook.Patch("Barotrauma.Item", "OnCollision", function (self, ptable)
    local userData = ptable["f2"].Body.UserData
    if tostring(userData) == "Barotrauma.Limb" then
        local velocity = self.body.LinearVelocity.Length()
		local character = userData.character
        if velocity > 0 and (self.HasTag('hardsurface')) then
            
            character.SetStun(0.1 * velocity * self.body.Mass)
            character.CharacterHealth.ApplyAffliction(userData, bluntTrauma.Instantiate(0.05 * velocity * self.body.Mass))
		elseif velocity > 4 and (self.HasTag('rollingstone')) then
			character.SetStun(0.1 * velocity * self.body.Mass)
            character.CharacterHealth.ApplyAffliction(userData, bluntTrauma.Instantiate(0.1 * velocity * self.body.Mass))
        end
    end
end)

Hook.Patch("Barotrauma.Ragdoll", "UpdateCollisionCategories", function (self, ptable)
    ptable.PreventExecution = true

    local wall

    if self.CurrentHull == nil or self.CurrentHull.Submarine == nil then
        wall = bit32.bor(Physics.CollisionWall, Physics.CollisionLevel)
    else
        wall = Physics.CollisionWall
    end

    local collision

    if self.Character.IsDead or self.Character.IsUnconscious or self.Character.IsRagdolled then
        collision = bit32.bor(wall, Physics.CollisionProjectile)
        collision = bit32.bor(collision, Physics.CollisionStairs)
		collision = bit32.bor(collision, Physics.CollisionItem)
		collision = bit32.bor(collision, Physics.CollisionPlatform)
    elseif self.IgnorePlatforms then
        collision = bit32.bor(wall, Physics.CollisionProjectile)
        collision = bit32.bor(collision, Physics.CollisionStairs)   
	else
        collision = bit32.bor(wall, Physics.CollisionProjectile)
        collision = bit32.bor(collision, Physics.CollisionStairs)
        collision = bit32.bor(collision, Physics.CollisionPlatform)
    end

    if self.prevCollisionCategory == collision then return end
    self.prevCollisionCategory = collision

    self.Collider.CollidesWith = collision

    for key, limb in pairs(self.Limbs) do
        if not limb.IgnoreCollisions and not limb.IsSevered then
            limb.body.CollidesWith = collision
        end
    end
end, Hook.HookMethodType.Before)

local odds = 0

Hook.Patch("Barotrauma.Items.Components.Throwable", "Update", function (self)
    if self.midAir and not self.Item.HasTag("grenade_midaircollide") then
        
            MakeItemCollideMidAir(self.Item)
	elseif self.midAir and self.Item.HasTag("grenade_midaircollide") then
		MakeItemCollideGrenadeMidAir(self.Item)

    end
end, Hook.HookMethodType.After)

Hook.Patch("Barotrauma.Items.Components.Projectile", "DisableProjectileCollisions", function (self)
    MakeItemCollide(self.Item)
end, Hook.HookMethodType.After)

Hook.Patch("Barotrauma.Items.Components.Projectile", "DoLaunch", function (self)
    MakeItemCollide(self.Item)
end, Hook.HookMethodType.After)

Hook.Patch("Barotrauma.Explosion", "Explode", function (self, ptable)
    local pos = ptable["worldPosition"]
    local force = self.force
    local range = self.Attack.Range

    for key, item in pairs(Item.ItemList) do
        if item.body and (item.HasTag('collidable')) or (item.body and item.HasTag('collidable_always')) then
            local distance = Vector2.Distance(item.WorldPosition, pos)

            if distance < range then
                local distFactor = 1 - distance / range
                local diff = Vector2.Normalize(item.WorldPosition - pos)
                local impulse = diff * distFactor * force
                local impulsePoint = item.SimPosition - diff * item.body.GetMaxExtent()

                local proj = item.GetComponentString("Projectile")
                if not proj or not proj.Launcher then
                    item.body.ApplyLinearImpulse(impulse, impulsePoint, 64 * 0.2)
                end
            end
        end
    end
end)

Hook.Add("ItemCollision.ItemLauncher", "ItemCollision.ItemLauncher", function(effect, deltaTime, item, targets, worldPosition)
    local weapon = item.GetComponentString("RangedWeapon")
    local items = item.OwnInventory.GetItemsAt(0)

    if #items == 0 then return true end

    local launched = items[1]
    
    launched.Drop(nil, true)
    MakeItemCollide(launched)
    launched.body.SetTransform(weapon.TransformedBarrelPos + item.SimPosition, 0)
    launched.body.FarseerBody.IsBullet = true

    local force = Vector2.Normalize((weapon.TransformedBarrelPos * 1.1) - weapon.TransformedBarrelPos)
    launched.body.ApplyForce(force * 1500 * launched.body.Mass)
end)


