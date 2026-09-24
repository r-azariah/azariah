-- Convenience Empire: graybox builder for the starter store.
--
-- How to use:
--   1. Open the Convenience Empire copy of the place. Never run this in the original TCG place.
--   2. Select the floor part of the plot you want to build on (or a plot Model with a PrimaryPart).
--      With nothing selected, it builds at the camera focus on an 80x80 plot.
--   3. Paste this whole file into the Command Bar (View > Command Bar) and press Enter.
--
-- It creates one Model called StoreTemplate in Workspace so you can walk around it and move
-- things. When the layout feels right, drag it into ServerStorage. PlotService clones it onto
-- each player's plot with PivotTo. The pivot (the Origin part) is the plot center at ground level.
--
-- The store front faces the plot part's front (-Z). If it faces away from the street, delete the
-- model, change ROTATION_DEGREES and run it again.

local ROTATION_DEGREES = 0 -- 0, 90, 180 or 270
local DEFAULT_PLOT_SIZE = Vector2.new(80, 80)
local ROOF_TRANSPARENCY = 0.6 -- see-through while we graybox, set to 0 later
local MARKER_TRANSPARENCY = 0.5 -- NPC spots and pads, hidden at runtime

local ChangeHistoryService = game:GetService("ChangeHistoryService")
local CollectionService = game:GetService("CollectionService")
local Selection = game:GetService("Selection")
local ServerStorage = game:GetService("ServerStorage")

if workspace:FindFirstChild("StoreTemplate") or ServerStorage:FindFirstChild("StoreTemplate") then
	warn("[StoreTemplate] A StoreTemplate already exists. Rename or delete it, then run this again.")
	return
end

-- Where to build ---------------------------------------------------------------------------------

local plotSize = DEFAULT_PLOT_SIZE
local plotCFrame

local selected = Selection:Get()[1]
if selected and selected:IsA("Model") and selected.PrimaryPart then
	selected = selected.PrimaryPart
end
if selected and selected:IsA("BasePart") then
	plotSize = Vector2.new(selected.Size.X, selected.Size.Z)
	plotCFrame = selected.CFrame * CFrame.new(0, selected.Size.Y / 2, 0)
else
	local focus = workspace.CurrentCamera.Focus.Position
	local hit = workspace:Raycast(focus + Vector3.new(0, 500, 0), Vector3.new(0, -1000, 0))
	plotCFrame = CFrame.new(focus.X, if hit then hit.Position.Y else focus.Y, focus.Z)
	print("[StoreTemplate] Nothing selected, building at the camera focus on an 80x80 plot.")
end
plotCFrame = plotCFrame * CFrame.Angles(0, math.rad(ROTATION_DEGREES), 0)
if ROTATION_DEGREES % 180 ~= 0 then
	plotSize = Vector2.new(plotSize.Y, plotSize.X)
end

-- Layout -----------------------------------------------------------------------------------------
-- Store-local coordinates: x runs across the store's width, z runs from the front wall (0) toward
-- the back, y is measured from the plot surface. All sizes are in studs.
-- docs/store-layout.png shows the result from above.

local STORE_W = 44
local STORE_D = 44
local FLOOR_T = 0.4
local WALL_H = 14
local WALL_T = 1
local DOOR_H = 10
local FRONT_SETBACK = 4 -- sidewalk between the plot's front edge and the store
local EDGE_MARGIN = 2

local F = FLOOR_T
local WALL_TOP = F + WALL_H

local ENTRANCE_X0, ENTRANCE_X1 = 26, 34
local BACK_DOOR_X0, BACK_DOOR_X1 = 20, 26
local STOCKROOM_DOOR_X0, STOCKROOM_DOOR_X1 = 36, 40
local DIVIDER_Z = 30 -- wall between the sales floor and the stockroom
local DELIVERY_Z0, DELIVERY_Z1 = STORE_D + 1, STORE_D + 10

local plotW, plotD = plotSize.X, plotSize.Y
local storeLeft = -plotW / 2 + EDGE_MARGIN
local frontZ = -plotD / 2 + FRONT_SETBACK

-- The plot's usable edges, in store-local coordinates.
local plotRight = plotW - 2 * EDGE_MARGIN
local plotFront = EDGE_MARGIN - FRONT_SETBACK
local plotBack = plotD - FRONT_SETBACK - EDGE_MARGIN

if STORE_W > plotRight or DELIVERY_Z1 > plotBack then
	warn(("[StoreTemplate] This plot (%g x %g) is smaller than the store needs. Building anyway."):format(plotW, plotD))
end

local COLORS = {
	Floor = Color3.fromRGB(205, 205, 200),
	Wall = Color3.fromRGB(165, 160, 150),
	Roof = Color3.fromRGB(95, 95, 100),
	Glass = Color3.fromRGB(165, 205, 230),
	Gondola = Color3.fromRGB(115, 115, 125),
	ShelfSlot = Color3.fromRGB(95, 165, 95),
	Cooler = Color3.fromRGB(80, 130, 195),
	HotFood = Color3.fromRGB(215, 125, 60),
	Counter = Color3.fromRGB(125, 95, 65),
	Register = Color3.fromRGB(40, 40, 45),
	Rack = Color3.fromRGB(140, 140, 150),
	StockPad = Color3.fromRGB(185, 165, 120),
	Delivery = Color3.fromRGB(240, 185, 40),
	Marker = Color3.fromRGB(255, 215, 0),
	Expansion = Color3.fromRGB(120, 200, 255),
	SignBoard = Color3.fromRGB(245, 240, 225),
	SignText = Color3.fromRGB(200, 40, 40),
}

-- Helpers ----------------------------------------------------------------------------------------

local store = Instance.new("Model")
store.Name = "StoreTemplate"
store:SetAttribute("TemplateVersion", 1)
store:SetAttribute("StoreWidth", STORE_W)
store:SetAttribute("StoreDepth", STORE_D)

local function toModelSpace(x, y, z)
	return Vector3.new(storeLeft + x, y, frontZ + z)
end

local function folder(name, parent)
	local newFolder = Instance.new("Folder")
	newFolder.Name = name
	newFolder.Parent = parent or store
	return newFolder
end

local function model(name, parent)
	local newModel = Instance.new("Model")
	newModel.Name = name
	newModel.Parent = parent
	return newModel
end

-- A box from (x0, y0, z0) to (x1, y1, z1) in store-local coordinates.
local function box(parent, name, x0, x1, y0, y1, z0, z1, color, solid)
	local part = Instance.new("Part")
	part.Name = name
	part.Anchored = true
	part.Size = Vector3.new(x1 - x0, y1 - y0, z1 - z0)
	part.CFrame = CFrame.new(toModelSpace((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
	part.Color = color
	part.Material = Enum.Material.SmoothPlastic
	part.TopSurface = Enum.SurfaceType.Smooth
	part.BottomSurface = Enum.SurfaceType.Smooth
	if solid == false then
		part.CanCollide = false
		part.CanQuery = false
		part.CanTouch = false
	end
	part.Parent = parent
	return part
end

local function glass(part)
	part.Material = Enum.Material.Glass
	part.Transparency = 0.6
	return part
end

-- Where an NPC stands to use a slot, stored on the slot so behaviors don't have to guess.
local function standPoint(part, x, z)
	local attachment = Instance.new("Attachment")
	attachment.Name = "StandPoint"
	attachment.Position = part.CFrame:PointToObjectSpace(toModelSpace(x, F + 0.5, z))
	attachment.Parent = part
	return attachment
end

-- Shelf, Cooler and HotFood slots. ProductId starts empty: the first product the player stocks
-- claims the slot, so their layout becomes the blueprint.
local function slot(part, tag, capacity, productId)
	CollectionService:AddTag(part, tag)
	part:SetAttribute("ShelfType", tag)
	part:SetAttribute("Capacity", capacity)
	part:SetAttribute("ProductId", productId or "")
	return part
end

local function marker(parent, name, x, z, onFloor, tag)
	local base = if onFloor then F else 0
	local part = box(parent, name, x - 1, x + 1, base, base + 0.1, z - 1, z + 1, COLORS.Marker, false)
	part.Transparency = MARKER_TRANSPARENCY
	CollectionService:AddTag(part, "Marker")
	if tag then
		CollectionService:AddTag(part, tag)
	end
	return part
end

local function label(part, face, text, font, color)
	local gui = Instance.new("SurfaceGui")
	gui.Face = face
	gui.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	gui.PixelsPerStud = 40
	local textLabel = Instance.new("TextLabel")
	textLabel.Size = UDim2.fromScale(1, 1)
	textLabel.BackgroundTransparency = 1
	textLabel.TextScaled = true
	textLabel.Font = font
	textLabel.TextColor3 = color
	textLabel.Text = text
	textLabel.Parent = gui
	gui.Parent = part
	return textLabel
end

local recording
pcall(function()
	recording = ChangeHistoryService:TryBeginRecording("Build StoreTemplate")
end)

local origin = Instance.new("Part")
origin.Name = "Origin"
origin.Anchored = true
origin.CanCollide = false
origin.CanQuery = false
origin.CanTouch = false
origin.Transparency = 1
origin.Size = Vector3.new(1, 1, 1)
origin.CFrame = CFrame.new(0, 0, 0)
origin.Parent = store
store.PrimaryPart = origin

-- Building ---------------------------------------------------------------------------------------

local building = folder("Building")
box(building, "Floor", 0, STORE_W, 0, F, 0, STORE_D, COLORS.Floor)

-- Front wall: big window, then the entrance, then a solid piece next to the register.
box(building, "FrontWallLeft", 0, 3, F, WALL_TOP, 0, WALL_T, COLORS.Wall)
box(building, "FrontWindowSill", 3, 24, F, F + 3, 0, WALL_T, COLORS.Wall)
box(building, "FrontWindowHeader", 3, 24, F + 10, WALL_TOP, 0, WALL_T, COLORS.Wall)
glass(box(building, "FrontWindow", 3, 24, F + 3, F + 10, 0.4, 0.6, COLORS.Glass))
box(building, "FrontWallMiddle", 24, ENTRANCE_X0, F, WALL_TOP, 0, WALL_T, COLORS.Wall)
box(building, "EntranceHeader", ENTRANCE_X0, ENTRANCE_X1, F + DOOR_H, WALL_TOP, 0, WALL_T, COLORS.Wall)
box(building, "FrontWallRight", ENTRANCE_X1, STORE_W, F, WALL_TOP, 0, WALL_T, COLORS.Wall)

box(building, "LeftWall", 0, WALL_T, F, WALL_TOP, WALL_T, STORE_D - WALL_T, COLORS.Wall)
box(building, "RightWall", STORE_W - WALL_T, STORE_W, F, WALL_TOP, WALL_T, STORE_D - WALL_T, COLORS.Wall)

box(building, "BackWallLeft", 0, BACK_DOOR_X0, F, WALL_TOP, STORE_D - WALL_T, STORE_D, COLORS.Wall)
box(building, "BackDoorHeader", BACK_DOOR_X0, BACK_DOOR_X1, F + DOOR_H, WALL_TOP, STORE_D - WALL_T, STORE_D, COLORS.Wall)
box(building, "BackWallRight", BACK_DOOR_X1, STORE_W, F, WALL_TOP, STORE_D - WALL_T, STORE_D, COLORS.Wall)

box(building, "DividerLeft", WALL_T, STOCKROOM_DOOR_X0, F, WALL_TOP, DIVIDER_Z, DIVIDER_Z + WALL_T, COLORS.Wall)
box(building, "StockroomDoorHeader", STOCKROOM_DOOR_X0, STOCKROOM_DOOR_X1, F + DOOR_H, WALL_TOP, DIVIDER_Z, DIVIDER_Z + WALL_T, COLORS.Wall)
box(building, "DividerRight", STOCKROOM_DOOR_X1, STORE_W - WALL_T, F, WALL_TOP, DIVIDER_Z, DIVIDER_Z + WALL_T, COLORS.Wall)

box(building, "Roof", 0, STORE_W, WALL_TOP, WALL_TOP + 1, 0, STORE_D, COLORS.Roof).Transparency = ROOF_TRANSPARENCY

-- Shelves: three double-sided aisles, two slots per side --------------------------------------

local shelves = folder("Shelves")
local AISLE_CENTERS = { 10.5, 18.5, 26.5 }
local AISLE_Z0, AISLE_Z1 = 9, 22
local AISLE_HALF_W = 1.5
local SLOT_T = 0.2
local SLOT_SPLIT_Z = (AISLE_Z0 + AISLE_Z1) / 2

for index, centerX in AISLE_CENTERS do
	local aisle = model("Aisle" .. index, shelves)
	local left, right = centerX - AISLE_HALF_W, centerX + AISLE_HALF_W
	box(aisle, "Body", left, right, F, F + 6, AISLE_Z0, AISLE_Z1, COLORS.Gondola)

	local sections = { { AISLE_Z0, SLOT_SPLIT_Z }, { SLOT_SPLIT_Z, AISLE_Z1 } }
	for sectionIndex, section in sections do
		local z0, z1 = section[1], section[2]
		local midZ = (z0 + z1) / 2

		local leftSlot = box(aisle, "SlotL" .. sectionIndex, left - SLOT_T, left, F + 0.5, F + 5.5, z0, z1, COLORS.ShelfSlot)
		slot(leftSlot, "Shelf", 24)
		standPoint(leftSlot, left - 2.5, midZ)

		local rightSlot = box(aisle, "SlotR" .. sectionIndex, right, right + SLOT_T, F + 0.5, F + 5.5, z0, z1, COLORS.ShelfSlot)
		slot(rightSlot, "Shelf", 24)
		standPoint(rightSlot, right + 2.5, midZ)
	end
end

-- Coolers: a wall of drink coolers along the stockroom divider ---------------------------------

local coolers = folder("Coolers")
local COOLER_W = 6
local COOLER_Z0, COOLER_Z1 = 27, 29.5

for index = 1, 4 do
	local x0 = 4 + (index - 1) * COOLER_W
	local unit = model("Cooler" .. index, coolers)
	local body = box(unit, "Body", x0, x0 + COOLER_W, F, F + 8, COOLER_Z0, COOLER_Z1, COLORS.Cooler)
	slot(body, "Cooler", 24)
	standPoint(body, x0 + COOLER_W / 2, COOLER_Z0 - 2.5)
	glass(box(unit, "Door", x0 + 0.3, x0 + COOLER_W - 0.3, F + 0.5, F + 7.5, COOLER_Z0 - 0.2, COOLER_Z0, COLORS.Glass))
end

-- Hot food: coffee station and roller grill on a counter by the front window -------------------

local hotFood = folder("HotFood")
box(hotFood, "Counter", WALL_T, 4, F, F + 3.5, 4, 12, COLORS.Counter)

local coffee = box(hotFood, "CoffeeStation", 1.5, 3.5, F + 3.5, F + 5.5, 4.5, 7.5, COLORS.HotFood)
slot(coffee, "HotFood", 20, "coffee")
standPoint(coffee, 6.5, 6)

local grill = box(hotFood, "RollerGrill", 1.5, 3.5, F + 3.5, F + 4.5, 8.5, 11.5, COLORS.HotFood)
slot(grill, "HotFood", 10, "hot_dog")
standPoint(grill, 6.5, 10)

-- Register: counter by the entrance, cashier behind it, line forming along the aisle -----------

local registerFolder = folder("Register")
box(registerFolder, "Counter", 33, 36, F, F + 3.5, 5, 15, COLORS.Counter)
local register = box(registerFolder, "Register", 33.5, 35.5, F + 3.5, F + 4.7, 8, 10, COLORS.Register)
CollectionService:AddTag(register, "Register")
marker(registerFolder, "CashierSpot", 38.5, 9, true, "CashierSpot")

local queueSpots = folder("QueueSpots", registerFolder)
local QUEUE_LENGTH = 5
register:SetAttribute("QueueLength", QUEUE_LENGTH)
for index = 1, QUEUE_LENGTH do
	local spot = marker(queueSpots, "Queue" .. index, 30.5, 9 + (index - 1) * 3, true, "QueueSpot")
	spot:SetAttribute("Index", index)
end

-- Stockroom --------------------------------------------------------------------------------------

local stockroom = folder("Stockroom")
local boxArea = box(stockroom, "BoxArea", 4, 18, F, F + 0.05, 33, 41, COLORS.StockPad, false)
boxArea.Transparency = 0.3
CollectionService:AddTag(boxArea, "Stockroom")
box(stockroom, "Rack", 30, 42, F, F + 8, 40.5, 42.9, COLORS.Rack)

-- Delivery zone: outside the back door ----------------------------------------------------------

local deliveryZone = folder("DeliveryZone")
local dropPad = box(deliveryZone, "DropPad", 17, 29, 0, 0.1, DELIVERY_Z0, DELIVERY_Z1, COLORS.Delivery, false)
dropPad.Transparency = 0.3
CollectionService:AddTag(dropPad, "DeliveryZone")

-- Signs ------------------------------------------------------------------------------------------

local signs = folder("Sign")
local storeSign = box(signs, "StoreSign", 6, 38, WALL_TOP - 1, WALL_TOP + 4, -0.5, 0, COLORS.SignBoard)
storeSign.CFrame = storeSign.CFrame * CFrame.Angles(0, 0, math.rad(-2)) -- a little crooked, it's homemade
label(storeSign, Enum.NormalId.Front, "CORNER STORE", Enum.Font.PermanentMarker, COLORS.SignText)

local openSign = box(signs, "OpenSign", 35, 38, F + 5, F + 7.5, -0.3, 0, COLORS.SignBoard)
CollectionService:AddTag(openSign, "OpenSign")
label(openSign, Enum.NormalId.Front, "CLOSED", Enum.Font.GothamBold, COLORS.SignText)

-- NPC markers ------------------------------------------------------------------------------------

local markers = folder("Markers")
marker(markers, "CustomerSpawn", 30, plotFront - 1, false, "CustomerSpawn") -- placeholder, match the TCG spawn points
marker(markers, "EntrancePoint", 30, 3, true, "EntrancePoint")
marker(markers, "StaffSpawn", 38, 35, true, "StaffSpawn")

-- Expansion zones: space saved for later, don't build here -------------------------------------

local expansion = folder("ExpansionZones")

local function zone(name, x0, x1, z0, z1, purpose)
	if x1 - x0 < 4 or z1 - z0 < 4 then
		warn(("[StoreTemplate] Not enough room on this plot for %s, skipped it."):format(name))
		return
	end
	local part = box(expansion, name, x0, x1, 0, 0.2, z0, z1, COLORS.Expansion, false)
	part.Transparency = 0.7
	part:SetAttribute("Purpose", purpose)
	label(part, Enum.NormalId.Top, name .. "\n(" .. purpose .. ")", Enum.Font.GothamBold, Color3.new(1, 1, 1))
end

zone("SideExpansion", STORE_W + 2, plotRight, plotFront, STORE_D, "bigger floor, parking, gas pumps")
zone("RearExpansion", 0, plotRight, DELIVERY_Z1 + 2, plotBack, "parking, loading dock, warehouse")

-- Place it ---------------------------------------------------------------------------------------

store:PivotTo(plotCFrame)
store.Parent = workspace
Selection:Set({ store })

if recording then
	ChangeHistoryService:FinishRecording(recording, Enum.FinishRecordingOperation.Commit)
end

print(("[StoreTemplate] Built on a %g x %g plot. Look it over, then drag it into ServerStorage."):format(plotW, plotD))
