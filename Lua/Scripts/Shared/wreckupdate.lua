local function MakeWrecksMovable()
    if Level.Loaded == nil then return end
  
    for key, value in pairs(Level.Loaded.Wrecks) do
        value.PhysicsBody.FarseerBody.BodyType = 2
        value.SetCrushDepth(math.max(Submarine.MainSub.RealWorldCrushDepth - 150, Level.DefaultRealWorldCrushDepth))
    end
  end
  
  Hook.Add("roundStart", "makeWrecksMovable", MakeWrecksMovable)
  
  Hook.HookMethod("Barotrauma.Submarine", "MakeWreck", function(submarine)
     submarine.PhysicsBody.FarseerBody.BodyType = 2
  end, Hook.HookMethodType.After)
  

