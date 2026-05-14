--defualtLampState = {} --must be a gloable table or nil error 
--Stop thinking before I play will ya? You are messing up my table!


local lampUpdateCooldown = 0
local lampUpdateInterval = 3000
local lampUpdateDelta = lampUpdateInterval / 60 

Hook.Add("Think","lampUpdate",function()

	if DH.HF.GameIsPaused() then 
	return 
	end

	lampUpdateCooldown = lampUpdateCooldown - 1
	if (lampUpdateCooldown <= 0) then 
		lampUpdateCooldown = lampUpdateInterval
		updateLamps()
	end
end)

function updateLamps()
	local lampsToUpdate = {}
	for key, v in pairs(Item.ItemList) do
		if v.HasTag("lamp") then
			table.insert(lampsToUpdate, v)
		end
	end


	local amountLamps = #lampsToUpdate
	for i,lamp in pairs(lampsToUpdate) do 
		Timer.Wait(function()
			if (lamp ~= nil and not lamp.Removed) then
			updateLamp(lamp) end
			end,((i + 1) / amountLamps) * lampUpdateDelta * 1000)
	end
end

--now a c# feature
--[[Hook.Add("roundStart", "defualtLampflicker", function()
	Timer.Wait(function()
	for k,v in pairs(Item.ItemList) do 
		if v.HasTag("lamp") then
			local theComponent = v:GetComponentString('LightComponent')

			defualtLampState[v] = {
			dflicker = theComponent.Flicker,
			dflickerspeed = theComponent.FlickerSpeed,
			LightStateSet = false
			}

		end
	end
	end,4000)
end)--]]

		
function updateLamp(instance)
		if instance.Removed then return end
		local State = defualtLampState
		local LightComponent = instance:GetComponentString('LightComponent')
		
		if instance.Condition >= 50 and not State[instance].LightStateSet then
		
			LightComponent.Flicker = State[instance].dflicker
			LightComponent.FlickerSpeed = State[instance].dflickerspeed
			State[instance].LightStateSet = true
			print("good")
		elseif instance.Condition < 50 and State[instance].LightStateSet then

			LightComponent.Flicker= 0.2
			LightComponent.FlickerSpeed = 0.3
			State[instance].LightStateSet = false
			print("bad")
		end
end




















--[[Hook.Patch("Barotrauma.Items.Components.LightComponent", "Update", function(instance, ptable)
	local State = defualtLampState[instance.Item.ID]
	if instance.Item.Condition >= 40 and not State.LightStateSet then --Add a 'LightStateSet' field to make sure that game won't keep setting 
		instance.Flicker = defualtLampState[instance.Item.ID].dflicker
		instance.FlickerSpeed = defualtLampState[instance.Item.ID].dflickerspeed
		State.LightStateSet = true
		print("good")
	elseif instance.Item.Condition < 40 and State.LightStateSet then
		instance.Flicker= 0.2
		instance.FlickerSpeed = 0.3
		State.LightStateSet = false
		print("bad")
	end
end)--]]

--[[local lampState = {}
Hook.Add("defualtLamp","defualtLamp",function(effect, deltaTime, item, targets, worldPosition)
	if not lampState[item.ID] then
	local LightComponent = item:GetComponentString('LightComponent')

	LightComponent.Flicker = defualtLampState[item.ID].dflicker
	LightComponent.FlickerSpeed = defualtLampState[item.ID].dflickerspeed
	lampState[item.ID] = true
	print("good")
	end
end)

Hook.Add("flickerLamp","flickerLamp",function(effect, deltaTime, item, targets, worldPosition)

	if lampState[item.ID] then
	local LightComponent = item:GetComponentString('LightComponent')
	LightComponent.Flicker = 0.2
	LightComponent.FlickerSpeed = 0.3
	lampState[item.ID] = false
	print("bad")
	end
end)--]]


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
	