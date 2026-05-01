--for sub editor


if not Game.IsSubEditor then return end 

Hook.Add("chatMessage", "forcelightson", function (message, client)
local lightfusePrefab = ItemPrefab.GetItemPrefab("lightfuse")
local coolant = ItemPrefab.GetItemPrefab("reactorcooline")
local jbfuse = ItemPrefab.GetItemPrefab("junctionboxfuse")
	if message == "!lightson" then 
		for _, item in pairs(Item.ItemList) do

			if (item.HasTag('lamp')) then
				Entity.Spawner.AddItemToSpawnQueue(lightfusePrefab, item.OwnInventory, nil, nil, function(item)end)
			end
			if (item.Prefab.Identifier == "outpostreactor") then
				Entity.Spawner.AddItemToSpawnQueue(coolant, item.OwnInventory, nil, nil, function(item)end)
			end

			if (item.Prefab.Identifier == "junctionbox") then
				Entity.Spawner.AddItemToSpawnQueue(jbfuse, item.OwnInventory, nil, nil, function(item)end)
			end

			local theComponent = item:GetComponentString('LightComponent')
			if theComponent then
                theComponent.PowerConsumption = 0;
			end
			
		end
	end
end)


Hook.Add("chatMessage", "dimensions", function (message, client)
if message == "!dimensions" then 
print(Submarine.MainSub.CalculateDimensions(true))--false will consider structures.
end
end)
--[[Hook.Add("chatMessage", "changehullcolor", function (message, client)

	if message =='!changecolor' then
		Character.Controlled.CurrentHull.AmbientLight = Color(255,0,0,255)
	elseif message == 'checkp' then
		print(Character.Controlled.CurrentHull.LethalPressure)
		print(Character.Controlled.CurrentHull.Pressure)
		
	end
	
end)--]]

--[[Hook.Add("Think", "checkpressure", function()
PrintChat(tostring(Character.Controlled.CurrentHull.LethalPressure))
PrintChat("p"..tostring(Character.Controlled.CurrentHull.Pressure))
end)--]]
--[[Hook.Add("chatMessage", "examples.humanSpawning", function (message, client)
    if message ~= "!go" then return end

    local spawnPoint = WayPoint.SelectCrewSpawnPoints({info}, submarine)[1]

 Character.Create("Husk", spawnPoint, "")


    return true -- returning true allows us to hide the message
end)--]]