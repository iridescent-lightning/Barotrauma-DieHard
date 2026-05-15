--this was to render qte repaire non-effective
--i think it's better to find a way to make it more interesting than simply disable it. since repair already cost condition of the tools
--[[Hook.Patch("Barotrauma.Items.Components.Repairable", "RepairBoost", function(instance, ptable)
ptable.PreventExecution = true

end, Hook.HookMethodType.Before)--]]


--now a cs function. not used anywhere
Hook.Add("NeedWaterTankdisable","NeedWaterTankdisable",function(effect, deltaTime, item, targets, worldPosition)
local oxygenComponent = item:GetComponentString("OxygenGenerator") 
local voltage = oxygenComponent.Voltage
local minvoltage = oxygenComponent.MinVoltage
local waterTankFound = false
if voltage < minvoltage then return end

	
    for container in item.GetComponents(Components.ItemContainer) do
        if container.Inventory.FindItemByIdentifier("watertank") and container.Inventory.GetItemAt(0).Condition > 0 then
			waterTankFound = true
			container.Inventory.GetItemAt(0).Condition = container.Inventory.GetItemAt(0).Condition - 0.15
		else
		end
    end
	
	if waterTankFound then
		oxygenComponent.IsActive = true
	else
		oxygenComponent.IsActive = false
	end
	
end)


--now a cs function. need to be work on
--[[local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
Hook.Add("ReactorCoolant","ReactorCoolant",function(effect, deltaTime, item, targets, worldPosition)
local reactorComponent = item:GetComponentString("Reactor")
local temperature = reactorComponent.Temperature

	for container in  item.GetComponents(Components.ItemContainer) do
		if container.Inventory.FindItemByIdentifier("controlrod") and container.Inventory.GetItemAt(0).Condition > 0 then
				controlrodFound = true
				if temperature > 5 then
					container.Inventory.GetItemAt(0).Condition = container.Inventory.GetItemAt(0).Condition - 0.05
				end
			else
				if temperature > 5 then
				controlrodFound = false
					end
			end

	end
	if not controlrodFound then
	item.Condition = item.Condition -1.5
	end
	updateTimer = getRandomUpdateTime();
	
end)--]]




--[[LuaUserData.RegisterType("Barotrauma.Sprite")
Hook.Add("doSomething", "doSomething", function(effect, deltaTime, item, targets, worldPosition)
print(item.Prefab.ContainedSprites)
item.Prefab.ContainedSprites = "Content/Items/JobGear/TalentGear.png: (X:233 Y:22 Width:123 Height:234)"
end)--]]

Hook.Add("randomwaterdamage","WaterDamage",function(effect, deltaTime, item, targets, worldPosition)
	local randomDamage = math.random(0,10)
	item.Condition = item.Condition - randomDamage
	--print("taking damage")
	
end)





Hook.Add("junctionboxfusedamage","junctionboxfusedamage",function(effect, deltaTime, item, targets, worldPosition)
local randomDamage = math.random(10,35)
local container = item:GetComponentString("ItemContainer")
local fuse = container.Inventory.GetItemAt(0)

	if fuse then
	local fuseCondition = fuse.Condition
		fuse.Condition = fuseCondition - randomDamage
	end
end)

Hook.Add("ContainerBrokenLock","ContainerBrokenLock",function(effect, deltaTime, item, targets, worldPosition)
local container = item:GetComponentString("ItemContainer")
container.Locked = true
end)
Hook.Add("KeyLockMedium","KeyLockMedium",function(effect, deltaTime, item, targets, worldPosition)
	local container = item:GetComponentString("ItemContainer")
	local lock = false
	for container in item.GetComponents(Components.ItemContainer) do
		if container.Inventory.FindItemByIdentifier("key1") or container.Inventory.FindItemByIdentifier("keymaster") or item.Condition <= 15 then
			lock = false
			
		else
			lock = true
			
		end
	end
	if not lock then
		container.Locked = false
	else
		container.Locked = true
	end
end)
Hook.Add("KeyLockLarge","KeyLockLarge",function(effect, deltaTime, item, targets, worldPosition)
	local container = item:GetComponentString("ItemContainer")
	local lock = false
	for container in item.GetComponents(Components.ItemContainer) do
		if container.Inventory.FindItemByIdentifier("key2") or container.Inventory.FindItemByIdentifier("keymaster") or item.Condition <= 15 then
			lock = false
			
		else
			lock = true
			
		end
	end
	if not lock then
		container.Locked = false
	else
		container.Locked = true
	end
end)
Hook.Add("KeyLockDivingLocker","KeyLockDivingLocker",function(effect, deltaTime, item, targets, worldPosition)
	local container = item:GetComponentString("ItemContainer")
	local lock = false
	for container in item.GetComponents(Components.ItemContainer) do
		if container.Inventory.FindItemByIdentifier("key3") or container.Inventory.FindItemByIdentifier("keymaster") or item.Condition <= 15 then
			lock = false
			
		else
			lock = true
			
		end
	end
	if not lock then
		container.Locked = false
	else
		container.Locked = true
	end
end)



Hook.Add("AddTagForOpen","AddTagForOpen",function(effect, deltaTime, item, targets, worldPosition)

	item.AddTag("draw_container_open")
end)

Hook.Add("JunctionboxOpen","JunctionboxOpen",function(effect, deltaTime, item, targets, worldPosition)

	item.AddTag("junctionbox_openlid")
end)

Hook.Add("HasAddextraLoad","HasAddextraLoad",function(effect, deltaTime, item, targets, worldPosition)
	
    for target in targets do
		print(target)
		local powerTransfer = target.GetComponentString("PowerTransfer") 
        -- 统一获取 Item 对象
        
        
        if powerTransfer ~= nil and powerTransfer.item.Prefab.Identifier == "junctionbox" then
            powerTransfer.item.AddTag("HasAddextraLoad")
        end
    end
end)




--LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.AICharacter"], "PlayerInput")
Hook.Add("Flamethrower","Flamethrower",function(effect, deltaTime, item, targets, worldPosition)
local repairTool = item.GetComponentString("RepairTool") 
	for target in targets do
		if target.Prefab.Identifier == "flamethrowerfueltank" then
			if target.Condition > 0 then
				repairTool.UsableIn = repairTool.UseEnvironment.Air
			elseif target.Condition == 0  then
				repairTool.UsableIn = repairTool.UseEnvironment.None
			end
		end
	end
	

end)

Hook.Add("FlamethrowerForceDrop","FlamethrowerForceDrop",function(effect, deltaTime, item, targets, worldPosition)
	for target in targets do

		local rightHand = target.Inventory.GetItemInLimbSlot(InvSlotType.RightHand)
		local leftHand = target.Inventory.GetItemInLimbSlot(InvSlotType.LeftHand)
	

			if rightHand then
			rightHand.Drop(target)
			elseif leftHand then
			leftHand.Drop(target)
			end

		end

end)


local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
--disabled. now a c# feature
--[[Hook.Add("junctionboxactive","junctionboxactive",function(effect, deltaTime, item, targets, worldPosition)
	if escapedTime <= updateTimer then return end
	local container = item:GetComponentString("ItemContainer")
	local powerT = item:GetComponentString("PowerTransfer")
	local fuse = container.Inventory.GetItemAt(0)
	if not fuse or fuse.Condition == 0 then
		item.Condition = item.Condition - 1
	end
	updateTimer = getRandomUpdateTime();
end)--]]


--[[Hook.Add("test","test",function(effect, deltaTime, item, targets, worldPosition)
if effect.user then
print(effect.user.GetStatValue(StatTypes.RangedSpreadReduction))
effect.user.StatTypes.RangedSpreadReduction = 100
end
end)--]]
--[[Hook.Add("DangerousFuelRod","DangerousFuelRod",function(effect, deltaTime, item, targets, worldPosition)
item.Prefab.IsDangerous = true
end)--]]

--[[Hook.Add("junctionboxactive","junctionboxactive",function(effect, deltaTime, item, targets, worldPosition)
local container = item:GetComponentString("ItemContainer")
local fuse = container.Inventory.GetItemAt(0)
local powerTransfer = item:GetComponentString("PowerTransfer")
	if item.Condition == 0 then return end
	if fuse and fuse.Condition > 0 then
	powerTransfer.CanTransfer = true
	elsenoeffectonjunctionbox
	powerTransfer.CanTransfer = false
	end
end)--]]
--[[defualtLampState = {} --must be a gloable table or nil error doesn't work well in mp

Hook.Add("roundStart", "defualtLampflicker", function()
	
	for k,v in pairs(Item.ItemList) do 
		if v.HasTag("lamp") then
			local theComponent = v:GetComponentString('LightComponent')
			if theComponent then
			defualtLampState[v.ID] = {
			dflicker = theComponent.Flicker,
			dflickerspeed = theComponent.FlickerSpeed,
			LightStateSet = false
			}
			end
		end
	end
end)--]]

		--now a c# feature
--[[Hook.Add("LampUpdate","LampUpdate",function(effect, deltaTime, item, targets, worldPosition)

		local LightComponent = item:GetComponentString('LightComponent')
		
		if item.Condition >= 50 and not defualtLampState[item].LightStateSet then
		
			LightComponent.Flicker = defualtLampState[item].dflicker
			LightComponent.FlickerSpeed = defualtLampState[item].dflickerspeed
			defualtLampState[item].LightStateSet = true
			--print("good")
		elseif item.Condition < 50 and defualtLampState[item].LightStateSet then

			LightComponent.Flicker= 0.2
			LightComponent.FlickerSpeed = 0.3
			defualtLampState[item].LightStateSet = false
			--print("bad")
		end
end)--]]


Hook.Add("ExcludeArmor","ExcludeArmor",function(effect, deltaTime, item, targets, worldPosition)

	for target in targets do
		local gearSlot = target.Inventory.GetItemInLimbSlot(InvSlotType.OuterClothes)
		
		if gearSlot and gearSlot.HasTag('bodyarmor') then
			gearSlot.Drop()
		end
	end
end)


Hook.Add("Rebreather","Rebreather",function(effect, deltaTime, item, targets, worldPosition)
	local battery = item.OwnInventory.GetItemAt(0)
	for human in targets do
		local divingSuit = human.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes)
			if divingSuit and divingSuit.HasTag('deepdiving') then
				local oxgenSource = divingSuit.OwnInventory.GetItemAt(0)
				if oxgenSource and oxgenSource.HasTag('oxygensource') and oxgenSource.Condition ~= 0 and battery and battery.HasTag('mobilebattery') and battery.Condition ~= 0 then
					oxgenSource.Condition = oxgenSource.Condition + 1
					battery.Condition = battery.Condition - 0.05
				end
			end
	end

end)







Hook.Add("RebreatherButton","RebreatherButton",function(effect, deltaTime, item, targets, worldPosition)
	rebreatherStart[item].init = rebreatherStart[item].init + 1
	if rebreatherStart[item].init > 1 then
		rebreatherStart[item].init = 0
	end

end)

Hook.Add("RebreatherWork","RebreatherWork",function(effect, deltaTime, item, targets, worldPosition)
	if rebreatherStart[item].init == 1 then
	local battery = item.OwnInventory.GetItemAt(0)
	local rebreatherTank = item.OwnInventory.GetItemAt(1)
		for human in targets do
		local divingSuit = human.Inventory.GetItemInLimbSlot(InvSlotType.InnerClothes)
			if divingSuit and divingSuit.HasTag('deepdiving') and battery and battery.Condition~= 0then
				local oxgenSource = divingSuit.OwnInventory.GetItemAt(0)
				if oxgenSource and oxgenSource.Prefab.Identifier == 'oxygentank' and battery and battery.HasTag('mobilebattery') and battery.Condition ~= 0 and rebreatherTank and rebreatherTank.Condition ~= 0 then
					oxgenSource.Condition = oxgenSource.Condition + 20
					battery.Condition = battery.Condition - 0.5
					rebreatherTank.Condition = rebreatherTank.Condition - 1
				end
			end
		end
	end
end)
--LuaUserData.MakeMethodAccessible(Descriptors["Barotrauma.Character"],"SpeedMultiplier")
local hasHideTankBar = false
local hasShowTankBar = false
Hook.Add("DivingSuit","DivingSuit",function(effect, deltaTime, item, targets, worldPosition)

	local oxygenTank = item.OwnInventory.GetItemAt(0)
	local battery = item.OwnInventory.GetItemAt(1)
	local divingLight = item.GetComponentString("LightComponent")
	
	for target in targets do
	local torso = target.AnimController.GetLimb(LimbType.Torso)
	local burnPrefab = AfflictionPrefab.Prefabs["burn"]
	if target.IsDead then return end
	
		if oxygenTank and oxygenTank.Prefab.Identifier == "oxygentank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = 1000
			oxygenTank.Condition = oxygenTank.Condition - 0.1 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "oxygenitetank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = 1500
			oxygenTank.Condition = oxygenTank.Condition - 0.1 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "weldingfueltank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = -100
			oxygenTank.Condition = oxygenTank.Condition - 0.5 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "incendiumfueltank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = -100
			target.CharacterHealth.ApplyAffliction(torso, burnPrefab.Instantiate(1))
			target.UseHullOxygen = false
		else 
			target.UseHullOxygen = false
			target.OxygenAvailable = 0
		end

		if battery and battery.HasTag('mobilebattery') and battery.Condition ~= 0 then
			divingLight.IsOn = true
			battery.Condition = battery.Condition - 0.01 * deltaTime
			if (oxygenTank and not hasShowTankBar) then
				hasShowTankBar = true
				hasHideTankBar = false
			end
		else
			divingLight.IsOn = false
			if oxygenTank and not hasHideTankBar then
				hasHideTankBar = true
				hasShowTankBar = false
			end
		end
	end
	--[[if not item.CurrentHull and not hasLockSuitOutSide then
		item.OwnInventory.Locked = true
		if oxygentank then
			oxygentank.Prefab.HideConditionBar = true
		end
		hasLockSuitOutSide = true
		hasLockSuitInside = false
	elseif item.CurrentHull and not hasLockSuitInside then
		item.OwnInventory.Locked = false
		if oxygentank then
			oxygentank.Prefab.HideConditionBar = false
		end
		hasLockSuitInside = true
		hasLockSuitOutSide = false
	end--]]
end)
Hook.Add("DivingMask","DivingMask",function(effect, deltaTime, item, targets, worldPosition)
local oxygenTank = item.OwnInventory.GetItemAt(0)
	
	for target in targets do
	local torso = target.AnimController.GetLimb(LimbType.Torso)
	local burnPrefab = AfflictionPrefab.Prefabs["burn"]
	if target.IsDead then return end
	
		if oxygenTank and oxygenTank.Prefab.Identifier == "oxygentank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = 1000
			oxygenTank.Condition = oxygenTank.Condition - 0.1 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "oxygenitetank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = 1500
			oxygenTank.Condition = oxygenTank.Condition - 0.1 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "weldingfueltank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = -100
			oxygenTank.Condition = oxygenTank.Condition - 0.5 * deltaTime
			target.UseHullOxygen = false
		elseif oxygenTank and oxygenTank.Prefab.Identifier == "incendiumfueltank" and oxygenTank.Condition ~= 0 then
			target.OxygenAvailable = -100
			target.CharacterHealth.ApplyAffliction(torso, burnPrefab.Instantiate(1))
			target.UseHullOxygen = false

		else 
			target.UseHullOxygen = false
			target.OxygenAvailable = 0
		end
	end
end)

Hook.Add("DivingSuitPressure","DivingSuitPressure",function(effect, deltaTime, item, targets, worldPosition)

	for target in targets do
	if target.IsDead then return end
			if item.Condition > 0 then 
			target.PressureProtection = 10000
			target.PropulsionSpeedMultiplier = 0.8
		else
			target.PressureProtection = 0
			target.PropulsionSpeedMultiplier = 0.8
		end
	end
end)




Hook.Add("WaterDoor","WaterDoor",function(effect, deltaTime, item, targets, worldPosition)
	if item.FindHull()and item.FindHull().WaterPercentage > 50 then
		item.Condition = item.Condition - 1
	end
end)


Hook.Add("PullSourceSub", "PullSourceSub", function(effect, deltaTime, item, targets, worldPosition)
	local rope = item.GetComponentString('Rope')
	local projectile = item.GetComponentString('Projectile')
    local position = item.WorldPosition
	local submarine = Submarine.MainSub
	if not rope.Snapped and projectile.IsStuckToTarget then
		
		local direction = Vector2.Normalize(position - submarine.WorldPosition)
		submarine.ApplyForce(direction * 10000)
		--print(direction)
	end
end)
Hook.Add("GrappleGun", "GrappleGun", function(effect, deltaTime, item, targets, worldPosition)
	local rangedWeapon = item.GetComponentString('RangedWeapon')
	local gasTank = item.OwnInventory.GetItemAt(1)
	
	if gasTank and gasTank.Condition > 0 then
		item.IsShootable = true

	else
		item.IsShootable = false

	end
end)
Hook.Add("GrappleGunUseGas", "GrappleGunUseGas", function(effect, deltaTime, item, targets, worldPosition)
	local gasTank = item.OwnInventory.GetItemAt(1)
	
	if gasTank and gasTank.Condition > 0 then
		gasTank.Condition = gasTank.Condition - 10

	end
end)


--[[Hook.Patch("Barotrauma.Items.Components.Projectile", "HandleProjectileCollision", function (instance,ptable)
	print(ptable['target'])
	print(ptable['collisionNormal'])
	print(ptable['velocity'])
	if instance.Item.body.CollisionCategories == Physics.CollisionItem then
	
	end
	
end, Hook.HookMethodType.After)--]]


Hook.Add("TaserStun", "TaserStun", function(effect, deltaTime, item, targets, worldPosition)
	local rope = item.GetComponentString('Rope')
	local projectile = item.GetComponentString('Projectile')
	for target in targets do
	if not rope.Snapped and projectile.IsStuckToTarget then
		target.Stun = math.max(value.Stun, 0.5)
	end
	end
end)


Hook.Patch("Barotrauma.Items.Components.EntitySpawnerComponent","SpawnCharacter",function(instance,ptable)

	math.randomseed(os.time())
	local init = math.random(1,100)
	if instance.Item.HasTag('diehardspawner25%') then
		if init > 25 then
			print('25')
			instance.IsActive = false
			return false
		end
	elseif instance.Item.HasTag('diehardspawner50%') then
		if init > 50 then
		print('50')
			instance.IsActive = false
			return false
		end
	elseif instance.Item.HasTag('diehardspawner25%') then
		if init > 75 then
		print('75')
			instance.IsActive = false
			return false
		end
	end
end, Hook.HookMethodType.Before)

--[[LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.Items.Components.EntitySpawnerComponent"], "spawnTimerGoal")
Hook.Patch("Barotrauma.Items.Components.EntitySpawnerComponent","SpawnCharacter",function(instance,ptable)
print('d')
	if instance.CanSpawn  then
		print('s')
	end
end)--]]
Hook.Add("StoneSpawner","StoneSpawner",function(effect, deltaTime, item, targets, worldPosition)
local chance = math.random(100)
local stone1 = ItemPrefab.GetItemPrefab("rollingstone")
local stone2 = ItemPrefab.GetItemPrefab("rollingstone1")
	if chance > 50 then
		Entity.Spawner.AddItemToSpawnQueue(stone, item.WorldPosition)
	else
		Entity.Spawner.AddItemToSpawnQueue(stone1, item.WorldPosition)
	end
end)

Hook.Add("SpawnEntity", "SpawnEntity", function(effect, deltaTime, item, targets, worldPosition)

	local number = math.random(1,100)
	local init = math.random(1,100)
	print(number)
	if init < 25 then return end
	if number < 20 then
		Entity.Spawner.AddCharacterToSpawnQueue('Humanhusk',item.WorldPosition)
	elseif number < 40 and number >=20 then
		Entity.Spawner.AddCharacterToSpawnQueue('Husk',item.WorldPosition)
	elseif number < 60 and number >= 40 then
		Entity.Spawner.AddCharacterToSpawnQueue('Mudraptor',item.WorldPosition)
	elseif number < 80 and number >= 60 then
		Entity.Spawner.AddCharacterToSpawnQueue('Crawler',item.WorldPosition)
	elseif number < 65 and number >= 60 then
		Entity.Spawner.AddCharacterToSpawnQueue('Crawler_large',item.WorldPosition)
	elseif number < 100 and number >= 80 then
		Entity.Spawner.AddCharacterToSpawnQueue('Mudraptor_veteran',item.WorldPosition)
	end
end)


Hook.Add("SpawnMineral", "SpawnMineral", function(effect, deltaTime, item, targets, worldPosition)

	local itemPrefabs = {
		"titanite","brockite","thorianite","amblygonite","sphalerite","pyromorphite","quartz","diamond","hydroxyapatite",
		"uraniumore","stannite","chalcopyrite","esperite","galena","triphylite","chamosite","langbeinite","chamosite",
		"ironore","polyhalite","graphite","sylvite","lazulite","bornite","cassiterite","cryolite","aragonite","chrysoprase"}
	local badNum = math.random(1,300)
	local number = math.random(1,#itemPrefabs + 1)
	local position = item.worldPosition - Level.Loaded.StartOutpost.Position
	local piezocrystalPrefab = ItemPrefab.GetItemPrefab("piezocrystal")
	if badNum == 1 then
		Entity.Spawner.AddItemToSpawnQueue(piezocrystalPrefab, position, Level.Loaded.StartOutpost, nil, nil, function(mineral)end)
	else
		if number <= #itemPrefabs then
			local itemPrefab = ItemPrefab.GetItemPrefab(itemPrefabs[number])
			Entity.Spawner.AddItemToSpawnQueue(itemPrefab, position, Level.Loaded.StartOutpost, nil, nil, function(mineral)
				
			end)
		end
	end	
	
end)
Hook.Add("SpawnMineralSubTest", "SpawnMineralSubTest", function(effect, deltaTime, item, targets, worldPosition)

	local itemPrefabs = {
		"titanite","brockite","thorianite","amblygonite","sphalerite","pyromorphite","quartz","diamond","hydroxyapatite",
		"uraniumore","stannite","chalcopyrite","esperite","galena","triphylite","chamosite","langbeinite","chamosite",
		"ironore","polyhalite","graphite","sylvite","lazulite","bornite","cassiterite","cryolite","aragonite","chrysoprase"}
	local badNum = math.random(1,100)
	local number = math.random(1,#itemPrefabs + 1)
	local position = item.worldPosition - Submarine.MainSub.Position
	local piezocrystalPrefab = ItemPrefab.GetItemPrefab("piezocrystal")
	local test = ItemPrefab.GetItemPrefab("diamond")
	if badNum == 1 then
		Entity.Spawner.AddItemToSpawnQueue(piezocrystalPrefab, position, Submarine.MainSub, nil, nil, function(mineral)end)
	else
		if number <= #itemPrefabs then
			local itemPrefab = ItemPrefab.GetItemPrefab(itemPrefabs[number])
			Entity.Spawner.AddItemToSpawnQueue(test, position, Submarine.MainSub, nil, nil, function(mineral)
				--mineral.GetComponentString('Holdable').AttachToWall()
				--mineral.GetComponentString('Holdable').Attached = true
			end)
		end
	end	
	
end)


--[[Hook.Patch("Barotrauma.Items.Components.Turret","GetCurrentPowerConsumption",function(instance,ptable)
	if instance.Item.HasTag('chaingunturret') or instance.Item.HasTag('flakcannon_turret') then
		return 1000
	end
end, Hook.HookMethodType.After)
Hook.Patch("Barotrauma.Items.Components.Turret","HasPowerToShoot",function(instance,ptable)
	if instance.Item.HasTag('chaingunturret') or instance.Item.HasTag('flakcannon_turret') then
		if instance.Voltage > instance.MinVoltage then
			print('s')
			return true
		else
			print('d')
			return false
		end
	end
end, Hook.HookMethodType.After)--]]
--[[LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.Items.Components.Door"], "isStuck")
--LuaUserData.MakeMethodAccessible(Descriptors["Barotrauma.Items.Components.Door"], "set_IsStuck")
Hook.Add("DoorStuck","DoorStuck",function(effect, deltaTime, item, targets, worldPosition)
	local door = item:GetComponentString('Door')
	door.isJammed = true
end)--]]
--[[Hook.Patch("Barotrauma.Items.Components.Rope","Snap",function(instance,ptable)
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_cablesnap")
	if instance.Item.Prefab.Identifier == "grapple" then
		print('s')
		local item = instance.Item
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)

        end)
	end
end, Hook.HookMethodType.After)--]]

--[[Hook.Add("item.SecondaryUse", "sadf", function(item, itemUser)
	if item.Prefab.Identifier.Value == "revolver" then 
	print("d")
	end
end)

Hook.Add("item.Use", "sadf", function(item, itemUser)
	if item.Prefab.Identifier.Value == "revolver" then 
	print("e")
	end
end)--]]





--[[Hook.Patch("Barotrauma.Items.Components.Door", "SetState", function(instance, ptable)
if instance then
	
	if instance.Item.Prefab.Identifier.Value == "doorwbuttons" or instance.Item.Prefab.Identifier.Value == "windoweddoorwbuttons" then
	
		if instance.Item.Condition ~= 0 then
			for _,component in pairs (instance.Item.Components) do
			
				if component.Name == "LightComponent" then
						component.IsOn = true
				end
			end
		end
	end
end
end)

Hook.Add("signalReceived.doorwbuttons", "keepDoorLightsOn", function(signal, connection)

				for component in connection.Item.Components do
					if component.Name == "LightComponent" then
						component.IsOn = true
						print("sd")
					end
				end
	
end)--]]

--[[Hook.Add("chatMessage", "readandwrite", function (message, client)    
    local target = client and client.Character or Character.Controlled

    if message == "!write" then
        File.Write(DH.Path.."/Maps/DryCaves/0Disclaimer.txt", "234")
    end
	if message == "!read" then
        print(File.Read(DH.Path.."/Maps/DryCaves/0Disclaimer.txt"))
    end
end)--]]

--[[Hook.Patch("Barotrauma.Items.Components.Holdable","Update",function(instance,ptable)
--print('s')
	instance.Item.body.FarseerBody.IgnoreGravity = true
end)
Hook.Patch("Barotrauma.Character","Update",function(instance,ptable)
	instance.AnimController.Collider.FarseerBody.IgnoreGravity = true
end)
Hook.Patch("Barotrauma.Ragdoll", "Update", function (instance, ptable)
	instance.Collider.FarseerBody.IgnoreGravity = true
end)


Hook.HookMethod("Barotrauma.Submarine", "Update", function(submarine)
   --submarine.PhysicsBody.FarseerBody.IgnoreGravity = false
end)--]]

--[[Hook.HookMethod("Barotrauma.SubmarineBody", "CalculateBuoyancy", function(instance,ptable)
	
	local itemsInSub = Submarine.MainSub.GetItems(true)
	local number = #itemsInSub
	print(number)
	local forceVectorI = Vector2(0, -10 * number)
	Submarine.MainSub.ApplyForce(forceVectorI)
	--return Vector2(100000, 10000)
	
end,Hook.HookMethodType.After)--]]




Hook.Add("MoveHatchUp", "MoveHatchUp", function(effect, deltaTime, item, targets, worldPosition)
	
	local fakeLight = item.GetComponentString('LightComponent')
	
	for v in LiftStopper do
		if v.Prefab.Identifier == "lifttopstop" then
			if DH.distanceBetween(v.worldPosition, item.worldPosition) < 10 then 
			platformState[item] = {
			hasReachTop = true,
			hasReachBottom = false
			}
			item.SendSignal("1", "Dock_State")
			else
			item.SendSignal("0", "Dock_State")
			end
		end
	end
	if not platformState[item].hasReachTop then
		item.Move(Vector2(0,2),false)
		fakeLight.IsOn = true
	else
		fakeLight.IsOn = false
	end
end)


Hook.Add("MoveHatchDown", "MoveHatchDown", function(effect, deltaTime, item, targets, worldPosition)
	
	local fakeLight = item.GetComponentString('LightComponent')
	
	for v in LiftStopper do
		if v.Prefab.Identifier == "liftbottomstop" then
			if DH.distanceBetween(v.worldPosition, item.worldPosition) < 10 then 
			platformState[item] = {
			hasReachBottom = true,
			hasReachTop = false
			}
			item.SendSignal("1", "Dock_State")
			else
			item.SendSignal("0", "Dock_State")
			end
		end
	end
	if not platformState[item].hasReachBottom then
		item.Move(Vector2(0,-2),false)
		fakeLight.IsOn = true
	else
		fakeLight.IsOn = false
	end
end)

--[[local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
Hook.Add("heaterWorking","heaterWorking",function(effect, deltaTime, item, targets, worldPosition)
	escapedTime = escapedTime + deltaTime;
	if escapedTime <= updateTimer then return end
local hullInfo = mainsubHulls[item.CurrentHull]
if hullInfo then --InPlayerSubmarine nolonger needed since mainsubHulls had checked it
    local sizeHull = hullInfo.Rsize
	if gas.GetTemperature(item.CurrentHull) < 300 then
		gas.AddTemperature(item.CurrentHull, 0.36/sizeHull)
	end
end
updateTimer = getRandomUpdateTime();
end)

local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
Hook.Add("smheaterWorking","smheaterWorking",function(effect, deltaTime, item, targets, worldPosition)
	escapedTime = escapedTime + deltaTime;
	local hullInfo = mainsubHulls[item.CurrentHull]
if hullInfo then --InPlayerSubmarine nolonger needed since mainsubHulls had checked it
    local sizeHull = hullInfo.Rsize
	if gas.GetTemperature(item.CurrentHull) < 288 then
		gas.AddTemperature(item.CurrentHull, 0.18/sizeHull)
	end
end
updateTimer = getRandomUpdateTime();
end)

local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
Hook.Add("radiatorWorking","radiatorWorking",function(effect, deltaTime, item, targets, worldPosition)
	local hullInfo = mainsubHulls[item.CurrentHull]
	if hullInfo then --InPlayerSubmarine nolonger needed since mainsubHulls had checked it
		local sizeHull = hullInfo.Rsize
		if gas.GetTemperature(item.CurrentHull) < 288 then
			gas.AddTemperature(item.CurrentHull, 0.36/sizeHull)
		end
	end
	updateTimer = getRandomUpdateTime();
	end)--]]

--a Lua table to put the connection.Item object into.
--[[local buffer = {}

Hook.Add("signalReceived.trapdoor", "test", function(signal, connection)
    -- If the buffer is empty, populate it with connection.item
    if buffer[connection.Item] == nil then buffer[connection.Item] = {} end
    
    local itemBuffer = buffer[connection.Item]
    
    
    if connection.Name == "moveup" then
        itemBuffer[1] = signal.value
		
    end
	if itemBuffer[1] == "1" then
	
	connection.Item.Move(Vector2(0,2),false)
	end
    --[[if itemBuffer[1] ~= nil then
        
		
        itemBuffer[1] = nil
    end
    print(itemBuffer[1])
end)--]]

local toolgunModes = {}

Hook.Add("luaToolGun.onImpact", "examples.toolgun", function(effect, deltaTime, item, targets, worldPosition)
    --if CLIENT and Game.IsMultiplayer then return end

    if effect.user == nil then return end

    local projectile = item.GetComponentString("Projectile")
    local launcher = projectile.Launcher

    if toolgunModes[launcher] == nil then toolgunModes[launcher] = "teleport" end

    if toolgunModes[launcher] == "teleport" then
        local position = item.WorldPosition
        --effect.user.TeleportTo(position)
    
	--Entity.Spawner.AddEntityToRemoveQueue(targets[1])
	--targets[1].Move(Vector2(0,10))
	--targets[1].FlipX(false)
	--Level.Loaded.StartOutpost.FlipX()
	--Submarine.MainSub.AttemptBallastFloraInfection("ballastfloratest",1000,1)
	-- server-side

end
end)



Hook.Add("floodhull","floodhull",function(effect, deltaTime, item, targets, worldPosition)
--print(item.FindHull().WaterVolume)
--item.FindHull().WaterVolume = item.FindHull().WaterVolume + 100000


	--item.CurrentHull.Move(Vector2(0,10),false)
	--[[for target in targets do
		target.TrySeverLimbJoints(target.AnimController.GetLimb(LimbType.LeftArm), 1000, 10, true, target)
	end--]]
	--print(effect.user)
	--effect.user.TrySeverLimbJoints(effect.user.AnimController.GetLimb(LimbType.LeftArm), 1000, 10, true, target)
	--effect.user.BreakJoints();
	--RespawnCharacter(effect.user)
end)
Hook.Add("killallnpc","rands",function(effect, deltaTime, item, targets, worldPosition)
		for _,v in pairs(Character.CharacterList) do
			if not v.IsOnPlayerTeam then
				v.Kill(CauseOfDeathType.Unknown)
			end
		end	
	end)


	Hook.Add("killallnpc1", "rands", function(effect, deltaTime, item, targets, worldPosition)
		-- Iterate over each character in the game
		for _, v in pairs(Character.CharacterList) do
			if not v.IsOnPlayerTeam then
				-- Kill the existing NPC
				v.Kill(CauseOfDeathType.Unknown)
				
				-- Spawn a corpse at the given world position
				local characterInfo = CharacterInfo(CharacterPrefab.HumanSpeciesName)
				local corpse = Character.Create(CharacterPrefab.HumanSpeciesName, worldPosition, ToolBox.RandomSeed(8), characterInfo)
				
				-- Set the corpse's properties
				corpse.AnimController.FindHull(worldPosition, true)
				corpse.TeamID = CharacterTeamType.None
				corpse.EnableDespawn = false
				
				-- Simulate initial damage and afflictions to make it appear like a corpse
				corpse.Kill(CauseOfDeathType.Unknown, nil, false)
				corpse.CharacterHealth.ApplyAffliction(corpse.AnimController.MainLimb, AfflictionPrefab.OxygenLow.Instantiate(AfflictionPrefab.OxygenLow.MaxStrength))
				
				-- Optional: Apply other effects such as damage or burns
				local applyBurns = Rand.Value() < 0.1
				local applyDamage = Rand.Value() < 0.3
				for _, limb in pairs(corpse.AnimController.Limbs) do
					if applyDamage and (limb.type == LimbType.Head or Rand.Value() < 0.5) then
						local biteWounds = AfflictionPrefab.BiteWounds
						local maxStrength = biteWounds.MaxStrength / biteWounds.DamageOverlayAlpha
						corpse.CharacterHealth.ApplyAffliction(limb, biteWounds.Instantiate(Rand.Range(0, maxStrength)))
					end
					if applyBurns then
						local burns = AfflictionPrefab.Burn
						local maxStrength = burns.MaxStrength / burns.BurnOverlayAlpha
						corpse.CharacterHealth.ApplyAffliction(limb, burns.Instantiate(Rand.Range(0, maxStrength)))
					end
				end
				corpse.CharacterHealth.ForceUpdateVisuals()
			end
		end
	end)
	

Hook.Add("ChangeDirection", "ChangeDirection", function(effect, deltaTime, item, targets, worldPosition)
	if CLIENT and not Game.IsMultiplayer then
	Submarine.MainSub.FlipX()
	end
	
	if SERVER then
	Submarine.MainSub.FlipX()
	local submarine = Submarine.MainSub
-- make sure to also flip the submarine here!!! or else the server will be desync from all clients

	local message = Networking.Start("flip")
	message.WriteUInt16(submarine.ID)
	message.WriteBoolean(submarine.FlippedX)
	Networking.Send(message) -- not specifying a client automatically sends the message to everyone
	end
end)

--[[Hook.Add("noexplosionwhenrepair","noexplosionwhenrepair",function(effect, deltaTime, item, targets, worldPosition)
print(item)
Entity.Spawner.AddEntityToRemoveQueue(item)

end)--]]




Hook.Add("helpermessage","helpermessage",function(effect, deltaTime, item, targets, worldPosition)

local display = item.GetComponentString('Terminal')
local message = display.ShowMessage


if message == '1' then
display.ShowMessage = "Very important----Reactor Operation: The reactor needs controlrod to prevent from being damaged by overheat. If the reactor is damaged to critical state. Turn off the reactor to prevent further catastrophe. \nDuring low damage mode, the FissionRate should below 5 to ensure safetyness for the whole sub. If reactor is down, turn on the backup power grid on the sub!\n Do not remove an undepleted fuel rod from the reactor. The radiation can be fatal. If you insist on removing it, find a suitable container! \nCoalition Engineering Guideline 101"
elseif message == '2' then
display.ShowMessage = "The pumps need motors to work. Remember to check and repair them regularly."
elseif message == '3' then
display.ShowMessage = "Turrets will be damaged if are under attack. EMP attack can instantly break down turrets within range. Some weapons, such as coilgun, can penetrate through hulls of the enemy submarines. Railgun is efficient at breaching hulls. Some special railgun shells also allow you to deal special effect towards your enemy."
elseif message == '4' then
display.ShowMessage = "Small arms are leathal in close quart combat. One shot is enough to kill a strong man if hit to the head or torso. However, bullet vests have chance to full stop a bullet, if it doesn't penetrate the armor. Armor condition will decrease if is hit by bullets."
elseif message == 'home' then
display.ShowMessage ='Welcome to the helper. Select corresponding number to view contents.\n1 Reactor\n2Vessel Payload Condition\n3Ship design and efficiency\nhome'
end

end)


function generateRandomRegisterNumber()
    local letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    local numbers = "0123456789"

    local registerNumber = ""

    -- Add four random letters
    for i = 1, 4 do
        local randomLetterIndex = math.random(1, #letters)
        registerNumber = registerNumber .. letters:sub(randomLetterIndex, randomLetterIndex)
    end

    -- Add two random numbers
    for i = 1, 2 do
        local randomNumberIndex = math.random(1, #numbers)
        registerNumber = registerNumber .. numbers:sub(randomNumberIndex, randomNumberIndex)
    end

    return registerNumber
end

Hook.Add("subdocmessage","subdocmessage",function(effect, deltaTime, item, targets, worldPosition)

local display = item.GetComponentString('Terminal')
local message = display.ShowMessage



if message == '1' then
display.ShowMessage = " Total item load mass in submarine: "..string.format("%.2f",totalDensity)
elseif message == '2' then
	local width = string.format("%.2f",subWidth/100)
	local height = string.format("%.2f",subHeight/100)
	local ratio = string.format("%.2f",subDimensionRatio)
	local op = string.format("%.2f",shapeFactor/subDimensionRatio*100).."%"
display.ShowMessage = "Submarine Dimension\n".."Width: "..width.." Height: "..height.." Ratio: "..ratio.." Shape Optimization: "..op
elseif message == '3' then
display.ShowMessage = generateRandomRegisterNumber()
elseif message == '4' then
local fuelRodCount = 0
local Coolant = 0
local waterTank = 0
local motorBig = 0
local motorSmall = 0
local weldingTank = 0
local battery = 0
local scooter = 0
	for k,v in pairs (Submarine.MainSub.GetItems(false)) do
		if v.HasTag('reactorfuel') and v.Condition ~= 0 then
			fuelRodCount = fuelRodCount + 1
		elseif v.Prefab.Identifier == 'controlrod' and v.Condition ~= 0 then
			Coolant = Coolant +1
		elseif v.Prefab.Identifier == 'watertank' and v.Condition ~= 0 then
			waterTank = waterTank +1 
		elseif v.Prefab.Identifier == 'weldingfueltank' and v.Condition ~= 0 then
			weldingTank = weldingTank +1 
		elseif v.Prefab.Identifier == 'pumppart' and v.Condition ~= 0 then
			motorSmall = motorSmall +1 
		end
	end
display.ShowMessage = "Basic Assets Information:\n".."Fuel Rod: "..fuelRodCount.."\nCoolant: "..Coolant.."\nWater Tank: "..weldingTank.."\nSmall Pump Motor: "..motorSmall.."\nBig Pump Motor: "..motorBig.."\nWelding Fuel Tank: "..weldingTank
elseif message == 'home' then
display.ShowMessage ='Submarine Info\n Select corresponding number to view information.\n1 Vessel Payload Condition\n2 Ship design and efficiency\n3 Register Number\nhome'
end

end)


--[[local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end

Hook.Add("toxicBattery","toxicBattery",function(effect, deltaTime, item, targets, worldPosition)
	escapedTime = escapedTime + deltaTime;
	
	if escapedTime > updateTimer then
	
		if item.Condition > 10 then return end
		local hullInfo = mainsubHulls[item.CurrentHull]
		if hullInfo then --InPlayerSubmarine nolonger needed since mainsubHulls had checked it
			local sizeHull = hullInfo.Rsize

			gas.AddGas(item.CurrentHull, "Chlorine", 0.9/sizeHull)
			updateTimer = getRandomUpdateTime();
		end	
	end
end)

local updateTimer = 3;
local escapedTime = 0;
local function getRandomUpdateTime()
    return updateTimer + math.random() * 0.5 -- Adjust the range as needed
end
Hook.Add("ventilationWork","ventilationWork",function(effect, deltaTime, item, targets, worldPosition)  
	escapedTime = escapedTime + deltaTime;
	if escapedTime <= updateTimer then return end
	local fakeLightV = item.GetComponentString('LightComponent')
	local voltage = fakeLightV.Voltage
	local minvoltage = fakeLightV.MinVoltage
	if voltage < minvoltage then
	return;
	end
	for vental in item.linkedTo do
		
		local hullInfo = mainsubHulls[vental.CurrentHull]
		if hullInfo then
			local sizeHull = hullInfo.Rsize
				if gas.GetGas(vental.CurrentHull, 'Chlorine') > 0 then
					gas.AddGas(vental.CurrentHull, "Chlorine", -0.5/sizeHull)
				elseif gas.GetGas(vental.CurrentHull, 'co2') > 1 then
					gas.AddGas(vental.CurrentHull, "co2", -0.5/sizeHull)
				end
		end
	end
	updateTimer = getRandomUpdateTime();
end)--]]


Hook.Add("layerII","layerII",function(effect, deltaTime, item, targets, worldPosition)
	local projectileProperty = item.GetComponentString('Projectile')
	projectileProperty.MaxTargetsToHit = 3
end)
Hook.Add("layerIII","layerIII",function(effect, deltaTime, item, targets, worldPosition)
	local projectileProperty = item.GetComponentString('Projectile')
	projectileProperty.MaxTargetsToHit = 4
end)
Hook.Add("layerVI","layerVI",function(effect, deltaTime, item, targets, worldPosition)
	local projectileProperty = item.GetComponentString('Projectile')
	projectileProperty.MaxTargetsToHit = 5
end)
Hook.Add("layerV","layerV",function(effect, deltaTime, item, targets, worldPosition)
	local projectileProperty = item.GetComponentString('Projectile')
	projectileProperty.MaxTargetsToHit = 6
end)


--[[Hook.Patch("Barotrauma.Items.Components.Pump", "Update", function(instance, ptable)
--print(instance.currFlow)
print(instance.flowPercentage)
	for hull in Submarine.MainSub.GetHulls() do
		print(hull)
	end
end)--]]

Hook.Add("BrainStorm","BrainStorm",function(effect, deltaTime, item, targets, worldPosition)
	
	for human in targets do
	local headSet = human.Inventory.GetItemInLimbSlot(InvSlotType.Headset)
	local limb = human.AnimController.GetLimb(LimbType.Torso)
		if headSet then
			headSet.Condition = headSet.Condition - 0.5
			if headSet.Condition == 0 and not human.IsDead and human.IsPlayer then
				human.Kill(CauseOfDeathType.Unknown)
				human.BreakJoints()
			end
		end
	end
end)



--[[local guid;
LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.Items.Components.Sonar"], "pingDragDirection")
Hook.Patch("Barotrauma.Items.Components.Sonar", "UpdateHUDComponentSpecific", function(instance, ptable)
	--print(instance.pingDragDirection)
	guid = instance.pingDragDirection;
	print(guid)
end)


Hook.Add("think", "tseet", function()
	for k,item in pairs(Item.ItemList) do
		if item.Prefab.Identifier == "sonarbeacon" then
			if guid then
				item.body.ApplyForce(guid*100, 35)
			end
		end
	end

end)--]]


--This is a nice way adding items to dangerousItem list but we don't need it anymore. Radiation danger needs to be handled separatly.
--[[
LuaUserData.RegisterType('System.Collections.Generic.HashSet`1')
LuaUserData.MakeFieldAccessible(Descriptors['Barotrauma.Item'], 'dangerousItems')

Hook.Add("fuelrod","fuelrod",function(effect, deltaTime, item, targets, worldPosition)

	if (item.Condition < item.MaxCondition and (item.RootContainer == nil or (item ~= nil and not item.RootContainer.HasTag('radiationshield')))) then
		if not(item.dangerousItems.Contains(item)) then
			
			item.dangerousItems.Add(item);
		end
	else
		if (item.dangerousItems.Contains(item)) then
			item.dangerousItems.Remove(item);
		end
	end
end)--]]


