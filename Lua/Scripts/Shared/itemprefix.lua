
LiftStopper = {}
mainsubHulls = {}--in mp this is needed
rebreatherStart = {}
platformState = {}
subDimensionRatio = 0 -- need  to delcear first or some display run ahead of the water 
subWidth = 0
subHeight = 0
shapeFactor = 0

LuaUserData.MakeMethodAccessible(Descriptors["Barotrauma.Items.Components.Door"], "DisableBody")

Hook.Add("roundStart", "startconfig", function()
Timer.Wait(function()
	for hull in Submarine.MainSub.GetHulls(true) do
		local sizeVector = tostring(hull.Size)
		local x, y = sizeVector:match("X:(%d+) Y:(%d+)")
		x = tonumber(x)
		y = tonumber(y)
		local sizeHull = x * y / 10000
		mainsubHulls[hull] = {
			Rsize = sizeHull--R for registered
		}
	end
	 playerSubDimensions = Submarine.MainSub.CalculateDimensions(true)
	 subWidth = tostring(playerSubDimensions):match("Width:(%d+)")
	 subHeight = tostring(playerSubDimensions):match("Height:(%d+)")
	shapeFactor = 0.185
	subDimensionRatio = subHeight/subWidth
	
	
    for _, item in pairs(Item.ItemList) do
        
		if item.HasTag('topstop') or item.HasTag('bottomstop') then
			table.insert(LiftStopper,item)
			--liftstopper[item] = {}
		elseif item.HasTag('mineralscanner_sonar') then
		Timer.Wait(function()
			local sonarComponent = item:GetComponentString('Sonar')
			if sonarComponent then
                --print('Made ',item.ID,' mineralscanner.');
                sonarComponent.HasMineralScanner=true;
			end
			end,100)
		--[[elseif item.HasTag('creature_loot') then
		Timer.Wait(function()
			local randomDecay = math.random(1,20)
				item.Condition = item.Condition - randomDecay
			if item.Condition == 0 then
				Entity.Spawner.AddEntityToRemoveQueue(item)
			end
			end,100)--]]
		elseif item.Prefab.Identifier == 'warrant' then
		
			item.Condition = item.Condition - 50
			if item.Condition == 0 then
				Entity.Spawner.AddEntityToRemoveQueue(item)
			end
		elseif item.Prefab.Identifier == "rebreather" then
		rebreatherStart[item] = 
		{
			init = 0
		}
		elseif item.Prefab.Identifier == "lift" then
		platformState[item] = 
		{
			hasReachBottom = false,
			hasReachTop = false
		}
		
		elseif item.HasTag('windoweddoorwbuttons') or item.HasTag('doorwbuttons') then
			local theComponent = item.GetComponentString('Door')
			if theComponent then
			theComponent.set_OpeningSpeed(1)
			theComponent.set_ClosingSpeed(0.8)
			end

		elseif item.Prefab.Identifier == 'enterable_doora' then
		
			local fakeDoor = item:GetComponentString('Door')
			if fakeDoor then
				fakeDoor.DisableBody()
				--print("body siabled")
			end
        end
		
		
    end
	end,2000)
end);

Hook.Add("roundEnd","ClearCache",function()
LiftStopper = {}
platformState = {}
--ballastPump = {}
mainsubHulls = {}
rebreatherStart = {}


end)
--[[LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.ItemPrefab"], "ReuiredItems")
Hook.Add("think", "ChangeRapair", function()
    for _, item in pairs(Item.ItemList) do
        if item.HasTag('turret') then
			item.Prefab.RequiredItemByIdentifier = "weaponparts"
		end
    end
end);--]]

--[[Hook.Patch("Barotrauma.Structure","SetDamage",function(instance,ptable)
ptable['createExplosionEffect'] = false
end, Hook.HookMethodType.Before)--]]


--[[LuaUserData.RegisterType("Barotrauma.WallSection")
Hook.Patch("Barotrauma.Structure","SetDamage",function(instance,ptable)
if not Level.Loaded then return end
if Submarine.MainSub.AtDamageDepth then return end --when entering the crush depth this must be removed or huge lag
local sections = instance.Sections
local maxHealth = instance.Health
local explosionPrefab = ItemPrefab.GetItemPrefab("wallshrapnel")
--local explosionPrefabLow = ItemPrefab.GetItemPrefab("wallshrapnel04")
--local tool = ptable['attacker'].Inventory.GetItemInLimbSlot(InvSlotType.RightHand)
--local preopen = sections.prevGapOpenState

	for section in (sections) do	
	--print(ptable['attacker'].Inventory.GetItemInLimbSlot(InvSlotType.RightHand))
	
		local gap = section.gap
		local damage = section.damage
		--print(damage)
		if gap then
			--gap.CreateWallDamageExplosion()
			--gap.Open = (damage / maxHealth - 0.7)*(1 / (1 - 0.1))
			if damage/maxHealth < 0.4 then
			gap.Open = 0
			--elseif damage/maxHealth < 0.7 then
			--gap.Open = 0.2
			--elseif damage/maxHealth == 0.6  then
			--
			elseif damage/maxHealth < 0.9 then
			gap.Open = 0.25
			elseif damage/maxHealth >= 0.9 then
			gap.Open = 1
			end
		end
		--print((damage / maxHealth*100).."%")
		
	end
	
end, Hook.HookMethodType.After)--]]

--[[Hook.Patch("Barotrauma.Structure","AddDamage",{
"System.Int32",
"System.Single",
"Barotrauma.Character",
"System.Boolean"
},
function(instance,ptable)
	print(ptable['attacker'])
	local sections = instance.Sections
	--sections.emitParticles = false This is the small particles from the cutter. Not the wall bug particle
end, Hook.HookMethodType.Before)--]]
--[[
Hook.Patch("Barotrauma.Structure","AddDamage",{
"Barotrauma.Character",
"Microsoft.Xna.Framework.Vector2",
"Barotrauma.Attack",
"System.Single",
"System.Boolean"
},
function(instance,ptable)
print(instance)
local sections = instance.Sections
	--This is respons for when the wall gets damaged by a creature or something
--ptable.PreventExecution = true
end, Hook.HookMethodType.After)--]]
--[[
Hook.Patch("Barotrauma.Structure","CreateWallDamageExplosion",function(instance,ptable)


 
	

		ptable.PreventExecution = true
end, Hook.HookMethodType.Before)--]]

--[[local xmlExpSmall = XDocument.Parse([[
		<Explosion range="400.0" stun="0" force="75.0" flames="false" shockwave="true" sparks="true" underwaterbubble="false" camerashake="100" camerashakerange="400" itemdamage="1" onlyhumans="true" ignorecover="true">
			<Affliction identifier="bleeding" strength="80" />
			<Affliction identifier="lacerations" strength="15" />
			<Affliction identifier="explosiondamage" strength="15" />
		</Explosion>			
	--]]
	

	
	--local xElementSamll = ContentXElement(nil, xmlExpSmall.Element("Explosion"))

	
	--local explosionSmall = Explosion(xElementSamll, "")--]]
	--[[local gap = ptable['gap']
	--local attacker = ptable['attacker']--the attacker seems doesn't work well in mp
	local explosionPrefab = ItemPrefab.GetItemPrefab("wallshrapnel")
		
		if not hasExplode then
			--explosionSmall.Explode(ptable["gap"].WorldPosition, nil)--if put gap exp only occur when gap is there?
			Entity.Spawner.AddItemToSpawnQueue(explosionPrefab, gap.WorldPosition, nil, nil, function(item)
		end)
			hasExplode = true
			Timer.Wait(function()
				hasExplode = false
			end, 2000)
		end--]]
		
		
--[[LuaUserData.RegisterType("Barotrauma.Identifier")
LuaUserData.RegisterGenericType("System.Collections.Immutable.ImmutableHashSet`1", "Barotrauma.Identifier")
LuaUserData.MakeFieldAccessible(Descriptors["Barotrauma.ItemPrefab"], "tags")

local tag_entity = Identifier.__new ("some_tag")

for prefab in ItemPrefab.Prefabs do
    prefab.tags = prefab.tags.Add (tag_entity)
end--]]

--[[LuaUserData.RegisterType("Barotrauma.Identifier")
Hook.Patch(
  "Barotrauma.FabricationRecipe", ".ctor",
  {
    "System.Xml.Linq.XElement",
    "Barotrauma.Identifier"
  },
  function(instance, p)
    instance.RequiredItems = instance.RequiredItems.Add (RequiredItem._new("revolver"))
  end,
  Hook.HookMethodType.After
)--]]


--[[Hook.Add("roundStart", "makeOutpostsMovable", function()
if Level.Loaded == nil then return end

      Level.Loaded.StartOutpost.PhysicsBody.FarseerBody.BodyType = 2
      --value.RealWorldCrushDepth = math.max(Submarine.MainSub.RealWorldCrushDepth - 150, Level.DefaultRealWorldCrushDepth)

--end)--]]

Hook.Add("signalReceived.attachablepumpnozzle", "attachablepumpnozzle", function(signal, connection)
--print(connection.Item)--itself
	
    if signal.source == nil then return end

	
    if signal.source.HasTag("attachablepumpa") then
        local voltage = signal.source.GetComponentString('LightComponent').Voltage
		local minvoltage = signal.source.GetComponentString('LightComponent').MinVoltage
		local value = signal.value
		if  voltage > minvoltage and connection.item.InWater and value == "1" then
		 
			signal.source.FindHull().WaterVolume = signal.source.FindHull().WaterVolume + 350
			connection.item.FindHull().WaterVolume = connection.item.FindHull().WaterVolume - 350
		end
    end
end)

--[[

Hook.Patch("Barotrauma.Items.Components.Pump", "Update",function(instance,ptable)
local pump = instance.Item
--print(string.lower(tostring(pump.FindHull())))
	
end)--]]
Hook.Add("item.equip","helmetequip",function(item, character)
	local helmetEquip = ItemPrefab.GetItemPrefab('sfx_helmet_equipped')
	local drawKnife = ItemPrefab.GetItemPrefab('sfx_draw_knife')
	--local clothEquip = ItemPrefab.GetItemPrefab('sfx_cloth_equip')
	--local gunEquip = ItemPrefab.GetItemPrefab('sfx_gun_equip')
	if item.Prefab.Identifier.Value == 'ballistichelmet3' or item.Prefab.Identifier.Value == 'ballistichelmet1' then 
		Entity.Spawner.AddItemToSpawnQueue(helmetEquip, item.WorldPosition, nil, nil, function(item)end)
	
	elseif item.Prefab.Identifier.Value == 'divingknife' then
		Entity.Spawner.AddItemToSpawnQueue(drawKnife, item.WorldPosition, nil, nil, function(item)end)
	end
	
end)


Hook.Add("inventoryPutItem", "reloadsfx", function(inventory, item, characterUser, index, removeItemBool)
	local rifleReload = ItemPrefab.GetItemPrefab('sfx_rifle_loaded')

	if item.HasTag('refilereloadsfx') and tostring(item.GetRootInventoryOwner()) == 'Human' and index == 0 then --index to make sure it wont clip in player pokect

		Entity.Spawner.AddItemToSpawnQueue(rifleReload, item.WorldPosition, nil, nil, function(item)end)
	end
end)

local densityUpdateTimer = 0
totalDensity = 0 --global so monitor can display on client

Hook.Patch("Barotrauma.SubmarineBody", "Update", function(instance,patable)

	
    if Timer.GetTime() > densityUpdateTimer then
        local itemsInSub = Submarine.MainSub.GetItems(true)

        -- Reset totalDensity before recalculating
        totalDensity = 0

        for item in itemsInSub do
            if item.body ~= nil then
                local density = item.body.Density
				totalDensity = totalDensity + density
            end
        end

        --print("Total Density:", totalDensity)

        densityUpdateTimer = Timer.GetTime() + 2 -- updates run at 1 time per second
    end
end,Hook.HookMethodType.After)