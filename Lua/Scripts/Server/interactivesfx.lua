


--[[Hook.Add("item.created", "playsound_nav", function(item)
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_naviterminal")
	if (item.HasTag('primarynavterminal')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
end)--]]



Hook.Add("item.created", "playsound_largesteelcab", function(item)
	--universial deselect. For those who alreay have deselect action defined, individual tag remove is required.
	item.OnDeselect = function()
		item.RemoveTag("draw_container_open")
		item.RemoveTag("junctionbox_openlid")
	end

	

	if not (item.HasTag('playinteractsound')) then return end
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_largesteelcab")
	if (item.HasTag('steelcabinetsfx')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_largesteelcab_close")
	if (item.HasTag('steelcabinetsfx')) then 
		item.OnDeselect = function()
			item.RemoveTag("draw_container_open")
			Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	
	
	
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_mediumsteelcab")
	if (item.HasTag('mediumsteelcabinetsfx')) or (item.HasTag('mediumwindowedsteelcabinet')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_mediumsteelcab_close")
	if (item.HasTag('mediumsteelcabinetsfx')) or (item.HasTag('mediumwindowedsteelcabinet')) then 
		item.OnDeselect = function()
			item.RemoveTag("draw_container_open")
			Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_fireextinguisher")
	if (item.HasTag('extinguisherholder')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_emgercab")
	if (item.HasTag('suppliescontainer')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_emgercab")
	if (item.HasTag('suppliescontainer')) then 
		item.OnDeselect = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_sec_idcardopen")
	if (item.HasTag('securecontainer')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_sec_idcardclose")
	if (item.HasTag('securecontainer')) then 
		item.OnDeselect = function()
			item.RemoveTag("draw_container_open")
			Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	
	--[[local soundPrefab = ItemPrefab.GetItemPrefab("sfx_medcontainer")
	if (item.HasTag('medcontainer')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_medcontainer_close")
	if (item.HasTag('medcontainer')) then 
		item.OnDeselect = function()
			item.RemoveTag("draw_container_open")
			Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_doorinteract")
	if (item.HasTag('idcarddoor')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	
	
	local soundPrefab = ItemPrefab.GetItemPrefab("sfx_crate")
	if (item.HasTag('crate')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(soundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	
	local terminalsoundPrefab = ItemPrefab.GetItemPrefab("sfx_terminalinteract")
	if item.Prefab.Identifier == 'terminal' then 
		item.OnInteract = function()
		
		Entity.Spawner.AddItemToSpawnQueue(terminalsoundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	
	local divinglockersoundPrefab = ItemPrefab.GetItemPrefab("sfx_divinglockerinteract")
	if item.Prefab.Identifier == "divingsuitlocker" then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(divinglockersoundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end
	--sound is too bad
	--[[local pumpsoundPrefab = ItemPrefab.GetItemPrefab("sfx_pumpinteract")
	if (item.HasTag('pump')) then 
		item.OnInteract = function()
		Entity.Spawner.AddItemToSpawnQueue(pumpsoundPrefab, item.WorldPosition, nil, nil, function(item)
		end)
		end
	end--]]
	
	
	
end)





Hook.Add("PullSourceUser", "PullSourceUser", function(effect, deltaTime, item, targets, worldPosition)
    local rope = item.GetComponentString('Rope')
    local projectile = item.GetComponentString('Projectile')
    local position = item.WorldPosition
    local value = effect.user
    
	--IsKeyDown(8) means shift key. Now used up and down key.
    if value.IsKeyDown(InputType.Down) then
		-- key code for ‘9’ is used to set pull mode to ‘1’ 
        setPullMode(item, 1)
    elseif value.IsKeyDown(InputType.Up) then
		-- key code for ‘8’ is used to set pull mode to ‘0’ 
        setPullMode(item, 0)
    end
    
    local pullModeItem = getPullMode(item) -- get the pull mode for this item

	if not rope.Snapped and projectile.IsStuckToTarget and value.IsKeyDown(InputType.Crouch) then

			print("snapped")
        	rope.Snapped = true
			SoundPlayer.PlaySound("rope_cut", value.WorldPosition)

	end
    
    if not rope.Snapped and projectile.IsStuckToTarget and value.IsRagdolled then
        local direction = Vector2.Normalize(position - value.WorldPosition)
        
        if pullModeItem == 1 then -- if the pull mode for this item is ‘1’
			--value.AnimController.Collider.FarseerBody.IgnoreGravity = true;
            --value.AnimController.MainLimb.body.ApplyTorque(50) This is obsolete because the lastest patch fixed the motion issue.
            value.AnimController.MainLimb.body.ApplyLinearImpulse(direction * 1.5,1)
			SoundPlayer.PlaySound("rope_slide", value.WorldPosition)

        else -- otherwise, the pull mode is ‘0’
            value.AnimController.MainLimb.body.ApplyLinearImpulse(direction * 550,5)
			SoundPlayer.PlaySound("rope_slide", value.WorldPosition)
            
        end
    end
end)


