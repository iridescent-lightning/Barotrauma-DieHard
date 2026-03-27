--[[local gasses = {}

local mathUtils = dofile(DH.Path.."/Lua/Scripts/Shared/math_utils.lua")

gasses.normalTemperature = 300
gasses.maxNormalTemperature = 323
gasses.minNormalTemperature = 270

gasses.listGasses = {}
gasses.gasHullStorage = {}
gasses.temperatureHullStorage = {}

gasses.GetTemperature = function (hull)
    return gasses.temperatureHullStorage[hull] or gasses.normalTemperature
end

gasses.AddTemperature = function (hull, amount)
    gasses.temperatureHullStorage[hull] = gasses.GetTemperature(hull) + amount
end

gasses.SetTemperature = function (hull, amount)
    gasses.temperatureHullStorage[hull] = mathUtils.clamp(amount, 0, 99999999)
end

gasses.DefineGas = function (gasname)
    table.insert(gasses.listGasses, gasname)
end

gasses.AddGas = function (hull, gasname, amount)
    if gasses.gasHullStorage[hull] == nil then
        gasses.gasHullStorage[hull] = {}
    end

    gasses.gasHullStorage[hull][gasname] = (gasses.gasHullStorage[hull][gasname] or 0) + amount
end

gasses.SetGas = function (hull, gasname, amount)
    if gasses.gasHullStorage[hull] == nil then
        gasses.gasHullStorage[hull] = {}
    end

    gasses.gasHullStorage[hull][gasname] = amount
end

gasses.GetGas = function (hull, gasname)
    if gasses.gasHullStorage[hull] == nil then
        gasses.gasHullStorage[hull] = {}
    end

    return gasses.gasHullStorage[hull][gasname] or 0
end

gasses.GetTotalMoles = function (hull)
    local sum = 0
    for _, gasName in pairs(gasses.listGasses) do
        sum = sum + gasses.GetGas(hull, gasName)
    end

    return sum
end

return gasses--]]