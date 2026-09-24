--!strict
-- Balance numbers for Convenience Empire. Tune the game here, not inside the services.
-- Everything here is a first guess. Change it after playtesting.

local Config = {}

Config.MaxPlayers = 6

-- One clock for the whole server. The business window runs at normal speed and the rest of the
-- day fast-forwards, so closed time (restock, order, upgrade) stays short.
Config.Clock = table.freeze({
	BusinessStartHour = 8,
	BusinessEndHour = 18,
	BusinessPhaseSeconds = 480, -- real seconds from 8:00 to 18:00
	NightPhaseSeconds = 120, -- real seconds from 18:00 back to 8:00
})

Config.Store = table.freeze({
	StartingCash = 250,
	-- Starting hours. Longer hours are an upgrade later, up to 24/7.
	OpenHour = 8,
	CloseHour = 18,
})

Config.Delivery = table.freeze({
	DelaySeconds = 20,
})

Config.Customers = table.freeze({
	MaxPerStore = 6,
	MaxPerServer = 30,
	SpawnIntervalMin = 6,
	SpawnIntervalMax = 14,
	RushHours = table.freeze({ 12, 17 }), -- each rush lasts one game hour
	RushSpawnMultiplier = 2,
	PatienceSeconds = 45,
	ItemsPerVisitMin = 1,
	ItemsPerVisitMax = 4,
	-- A customer walks if the price is more than this fraction over the default price.
	PriceTolerance = 0.25,
})

Config.Staff = table.freeze({
	Cashier = table.freeze({
		HireCost = 300,
		DailyWage = 40,
		SecondsPerItem = 1.5, -- at level 1, a bit slower than a player
	}),
	Stocker = table.freeze({
		HireCost = 300,
		DailyWage = 40,
		SecondsPerUnit = 0.5,
	}),
	MaxLevel = 5,
	SpeedBonusPerLevel = 0.1, -- each level above 1 is 10% faster
	UpgradeCostPerLevel = 150, -- going from level n to n + 1 costs n times this
})

return table.freeze(Config)
