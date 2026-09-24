local Config = require("../src/shared/Config")
local ProductCatalog = require("../src/shared/ProductCatalog")

local failures = 0
local function check(ok, message)
	if not ok then
		failures += 1
		print("FAIL: " .. message)
	end
end

local SHELF_TYPES = { Shelf = true, Cooler = true, HotFood = true }
local BANNED = { "beer", "wine", "liquor", "alcohol", "cigarette", "tobacco", "vape", "lottery" }

local products = ProductCatalog.List()
check(#products == 10, "expected 10 starter products, found " .. #products)

for _, product in products do
	local id = product.Id
	check(ProductCatalog.Get(id) == product, id .. " isn't returned by Get")
	check(SHELF_TYPES[product.ShelfType] == true, id .. " has an unknown ShelfType")
	check(product.WholesaleCost > 0, id .. " should cost something")
	check(product.DefaultPrice > product.WholesaleCost, id .. " sells at a loss by default")
	check(product.UnitsPerBox >= 1 and product.UnitsPerBox % 1 == 0, id .. " needs a whole number of units per box")
	check(product.Popularity > 0, id .. " needs a popularity above 0")
	check(product.ShelfLifeHours == nil or product.ShelfLifeHours > 0, id .. " has a bad shelf life")
	for _, word in BANNED do
		check(not string.find(string.lower(id .. " " .. product.Name), word, 1, true), id .. " isn't Roblox-safe")
	end
end

check(ProductCatalog.Get("nope") == nil, "unknown ids should return nil")
check(not pcall(function()
	(products[1] :: any).DefaultPrice = 0
end), "products should be read-only")

local total = 0
for shelfType in SHELF_TYPES do
	local list = ProductCatalog.ForShelfType(shelfType)
	check(#list > 0, "no products for " .. shelfType)
	for _, product in list do
		check(product.ShelfType == shelfType, product.Id .. " came back for the wrong shelf type")
	end
	total += #list
end
check(total == #products, "ForShelfType should cover every product exactly once")

-- The starter store builder hardcodes these hot food products on its stations.
check(ProductCatalog.Get("coffee") ~= nil and ProductCatalog.Get("coffee").ShelfType == "HotFood", "coffee should be HotFood")
check(ProductCatalog.Get("hot_dog") ~= nil and ProductCatalog.Get("hot_dog").ShelfType == "HotFood", "hot_dog should be HotFood")

local clock = Config.Clock
check(clock.BusinessStartHour < clock.BusinessEndHour, "business hours are backwards")
check(Config.Store.OpenHour >= clock.BusinessStartHour and Config.Store.CloseHour <= clock.BusinessEndHour, "starting store hours should fit in the business window")
for _, hour in Config.Customers.RushHours do
	check(hour >= Config.Store.OpenHour and hour < Config.Store.CloseHour, "rush hour " .. hour .. " falls outside store hours")
end
check(Config.Customers.SpawnIntervalMin <= Config.Customers.SpawnIntervalMax, "customer spawn interval is backwards")
check(Config.Customers.ItemsPerVisitMin <= Config.Customers.ItemsPerVisitMax, "items per visit is backwards")
check(Config.Customers.MaxPerStore * Config.MaxPlayers >= Config.Customers.MaxPerServer, "server cap is higher than all stores combined")
check(Config.Store.StartingCash >= 50, "starting cash is too low to order a first round of stock")

if failures > 0 then
	error(("%d check(s) failed"):format(failures))
end
print("PASS: shared modules")
