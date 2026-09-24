--!strict
-- Every product the store can sell. Costs and prices are per unit.
-- Keep it Roblox-safe: no alcohol, tobacco, vapes or lottery tickets.

export type ShelfType = "Shelf" | "Cooler" | "HotFood"

export type Product = {
	Id: string,
	Name: string,
	ShelfType: ShelfType,
	WholesaleCost: number,
	DefaultPrice: number,
	UnitsPerBox: number, -- for hot food this is one batch: a pot of coffee, a grill load
	ShelfLifeHours: number?, -- game hours before it spoils, nil if it doesn't
	Popularity: number, -- how often customers want it, relative to the others
}

local PRODUCTS: { Product } = {
	{ Id = "chips", Name = "Chips", ShelfType = "Shelf", WholesaleCost = 1.00, DefaultPrice = 2.00, UnitsPerBox = 12, Popularity = 10 },
	{ Id = "candy_bar", Name = "Candy Bar", ShelfType = "Shelf", WholesaleCost = 0.60, DefaultPrice = 1.50, UnitsPerBox = 24, Popularity = 9 },
	{ Id = "gum", Name = "Gum", ShelfType = "Shelf", WholesaleCost = 0.40, DefaultPrice = 1.00, UnitsPerBox = 24, Popularity = 5 },
	{ Id = "bread", Name = "Bread", ShelfType = "Shelf", WholesaleCost = 1.50, DefaultPrice = 3.00, UnitsPerBox = 8, ShelfLifeHours = 48, Popularity = 4 },
	{ Id = "soda", Name = "Soda", ShelfType = "Cooler", WholesaleCost = 0.80, DefaultPrice = 2.00, UnitsPerBox = 12, Popularity = 10 },
	{ Id = "water", Name = "Water", ShelfType = "Cooler", WholesaleCost = 0.40, DefaultPrice = 1.50, UnitsPerBox = 12, Popularity = 7 },
	{ Id = "energy_drink", Name = "Energy Drink", ShelfType = "Cooler", WholesaleCost = 1.50, DefaultPrice = 3.50, UnitsPerBox = 12, Popularity = 6 },
	{ Id = "milk", Name = "Milk", ShelfType = "Cooler", WholesaleCost = 1.80, DefaultPrice = 3.50, UnitsPerBox = 8, ShelfLifeHours = 72, Popularity = 4 },
	{ Id = "hot_dog", Name = "Hot Dog", ShelfType = "HotFood", WholesaleCost = 0.80, DefaultPrice = 2.50, UnitsPerBox = 10, ShelfLifeHours = 4, Popularity = 6 },
	{ Id = "coffee", Name = "Coffee", ShelfType = "HotFood", WholesaleCost = 0.30, DefaultPrice = 1.75, UnitsPerBox = 20, ShelfLifeHours = 2, Popularity = 7 },
}

local byId: { [string]: Product } = {}
for _, product in PRODUCTS do
	local id = product.Id
	assert(byId[id] == nil, "duplicate product id " .. id)
	byId[id] = product
end
for _, product in PRODUCTS do
	table.freeze(product)
end
table.freeze(PRODUCTS)

local ProductCatalog = {}

function ProductCatalog.Get(id: string): Product?
	return byId[id]
end

function ProductCatalog.List(): { Product }
	return table.clone(PRODUCTS)
end

function ProductCatalog.ForShelfType(shelfType: ShelfType): { Product }
	local result = {}
	for _, product in PRODUCTS do
		if product.ShelfType == shelfType then
			table.insert(result, product)
		end
	end
	return result
end

return table.freeze(ProductCatalog)
