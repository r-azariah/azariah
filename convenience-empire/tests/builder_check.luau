-- Runs after roblox_mock.luau and BuildStoreTemplate.luau in the same chunk (see run.sh), so the
-- builder's locals (store, plotW, plotD, F) are in scope. Checks happen in model space, before the
-- PivotTo that places the store on a real plot.

local failures = 0
local function check(ok, message)
	if not ok then
		failures += 1
		print("FAIL: " .. message)
	end
end

local function bounds(part)
	local p, s = part.CFrame.Position, part.Size
	return p.X - s.X / 2, p.X + s.X / 2, p.Y - s.Y / 2, p.Y + s.Y / 2, p.Z - s.Z / 2, p.Z + s.Z / 2
end

local parts, solids = {}, {}
local tagged = {}
for _, instance in MockInstances do
	if instance.ClassName == "Part" and instance.Name ~= "Origin" then
		table.insert(parts, instance)
		if instance.CanCollide then
			table.insert(solids, instance)
		end
		for tag in instance._tags do
			tagged[tag] = tagged[tag] or {}
			table.insert(tagged[tag], instance)
		end
	end
end

check(store.Parent == workspace, "store should be parented to Workspace")
check(store.PrimaryPart ~= nil and store.PrimaryPart.Name == "Origin", "store PrimaryPart should be Origin")

-- Every part has a real size and stays on the plot.
for _, part in parts do
	local s = part.Size
	check(s.X > 0 and s.Y > 0 and s.Z > 0, part.Name .. " has a zero or negative size")
	local x0, x1, _, _, z0, z1 = bounds(part)
	check(x0 >= -plotW / 2 and x1 <= plotW / 2 and z0 >= -plotD / 2 and z1 <= plotD / 2, part.Name .. " sticks out of the plot")
end

-- Solid parts never overlap each other.
local EPS = 0.001
for i = 1, #solids do
	local a = solids[i]
	local ax0, ax1, ay0, ay1, az0, az1 = bounds(a)
	for j = i + 1, #solids do
		local b = solids[j]
		local bx0, bx1, by0, by1, bz0, bz1 = bounds(b)
		local overlaps = ax0 < bx1 - EPS and bx0 < ax1 - EPS and ay0 < by1 - EPS and by0 < ay1 - EPS and az0 < bz1 - EPS and bz0 < az1 - EPS
		check(not overlaps, ("%s.%s overlaps %s.%s"):format(a.Parent.Name, a.Name, b.Parent.Name, b.Name))
	end
end

-- Tag counts match the layout.
local expectedCounts = {
	Shelf = 12,
	Cooler = 4,
	HotFood = 2,
	Register = 1,
	CashierSpot = 1,
	QueueSpot = 5,
	Stockroom = 1,
	DeliveryZone = 1,
	OpenSign = 1,
	CustomerSpawn = 1,
	EntrancePoint = 1,
	StaffSpawn = 1,
}
for tag, expected in expectedCounts do
	local count = if tagged[tag] then #tagged[tag] else 0
	check(count == expected, ("expected %d parts tagged %s, found %d"):format(expected, tag, count))
end

-- Slots carry the attributes the services read.
for _, tag in { "Shelf", "Cooler", "HotFood" } do
	for _, part in tagged[tag] or {} do
		check(part:GetAttribute("ShelfType") == tag, part.Name .. " has the wrong ShelfType")
		check(type(part:GetAttribute("Capacity")) == "number", part.Name .. " is missing Capacity")
		check(type(part:GetAttribute("ProductId")) == "string", part.Name .. " is missing ProductId")
		check(part:FindFirstChild("StandPoint") ~= nil, part.Name .. " is missing a StandPoint")
	end
end

-- Walkability: flood fill from the entrance on a grid, treating solids at body height as walls
-- (grown by the NPC's half width), then make sure every spot an NPC needs is reachable.
local CELL = 0.5
local HALF_WIDTH = 1
local BODY_Y0, BODY_Y1 = F + 0.5, F + 5

local obstacles = {}
for _, part in solids do
	local x0, x1, y0, y1, z0, z1 = bounds(part)
	if y0 < BODY_Y1 and y1 > BODY_Y0 then
		table.insert(obstacles, { x0 - HALF_WIDTH, x1 + HALF_WIDTH, z0 - HALF_WIDTH, z1 + HALF_WIDTH })
	end
end

local cols = math.floor(plotW / CELL)
local rows = math.floor(plotD / CELL)
local function cellOf(x, z)
	return math.clamp(math.floor((x + plotW / 2) / CELL), 0, cols - 1), math.clamp(math.floor((z + plotD / 2) / CELL), 0, rows - 1)
end
local function isBlocked(cx, cz)
	local x = -plotW / 2 + (cx + 0.5) * CELL
	local z = -plotD / 2 + (cz + 0.5) * CELL
	for _, o in obstacles do
		if x > o[1] and x < o[2] and z > o[3] and z < o[4] then
			return true
		end
	end
	return false
end

local blocked = {}
for cx = 0, cols - 1 do
	for cz = 0, rows - 1 do
		blocked[cx * rows + cz] = isBlocked(cx, cz)
	end
end

-- Flood fill from a point, only through cells whose store-local position passes inRegion.
-- Regions keep each route honest: without them a customer could walk out the back door and
-- around the building, and a bricked-up entrance would still pass.
local function flood(from, inRegion)
	local function allowed(cx, cz)
		local x = -plotW / 2 + (cx + 0.5) * CELL
		local z = -plotD / 2 + (cz + 0.5) * CELL
		return inRegion(x - storeLeft, z - frontZ)
	end
	local sx, sz = cellOf(from.X, from.Z)
	local reached = { [sx * rows + sz] = true }
	local queue, head = { { sx, sz } }, 1
	while head <= #queue do
		local cx, cz = queue[head][1], queue[head][2]
		head += 1
		for _, step in { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } } do
			local nx, nz = cx + step[1], cz + step[2]
			local key = nx * rows + nz
			if nx >= 0 and nx < cols and nz >= 0 and nz < rows and not reached[key] and not blocked[key] and allowed(nx, nz) then
				reached[key] = true
				table.insert(queue, { nx, nz })
			end
		end
	end
	return reached
end

local function checkReachable(route, reached, name, position)
	local cx, cz = cellOf(position.X, position.Z)
	local key = cx * rows + cz
	if blocked[key] then
		check(false, name .. " is inside a wall or too close to one for an NPC")
	else
		check(reached[key] == true, ("%s: can't reach %s"):format(route, name))
	end
end

local function standPoints(tags)
	local points = {}
	for _, tag in tags do
		for _, part in tagged[tag] or {} do
			local standPoint = part:FindFirstChild("StandPoint")
			if standPoint then
				points[part.Parent.Name .. "." .. part.Name .. " StandPoint"] = part.CFrame.Position + standPoint.Position
			end
		end
	end
	return points
end

local function position(tag)
	return tagged[tag][1].CFrame.Position
end

-- Customers: in through the front entrance, sales floor only (never the stockroom).
local customerRoute = flood(position("CustomerSpawn"), function(sx, sz)
	return sx >= 0 and sx <= STORE_W and sz <= DIVIDER_Z
end)
checkReachable("customer route", customerRoute, "EntrancePoint", position("EntrancePoint"))
for name, point in standPoints({ "Shelf", "Cooler", "HotFood" }) do
	checkReachable("customer route", customerRoute, name, point)
end
for _, part in tagged.QueueSpot or {} do
	checkReachable("customer route", customerRoute, part.Name, part.CFrame.Position)
end

-- Staff: from the stockroom to the register and every slot without leaving the building.
local staffRoute = flood(position("StaffSpawn"), function(sx, sz)
	return sx >= 0 and sx <= STORE_W and sz >= 0 and sz <= STORE_D
end)
checkReachable("staff route", staffRoute, "CashierSpot", position("CashierSpot"))
checkReachable("staff route", staffRoute, "Stockroom BoxArea", position("Stockroom"))
for name, point in standPoints({ "Shelf", "Cooler", "HotFood" }) do
	checkReachable("staff route", staffRoute, name, point)
end

-- Deliveries: from the stockroom out the back door to the drop pad.
local deliveryRoute = flood(position("StaffSpawn"), function(sx, sz)
	return sx >= 0 and sx <= STORE_W and sz >= DIVIDER_Z and sz <= DELIVERY_Z1 + 1
end)
checkReachable("delivery route", deliveryRoute, "DropPad", position("DeliveryZone"))

print(("Checked %d parts (%d solid). Plot %g x %g."):format(#parts, #solids, plotW, plotD))
if failures > 0 then
	error(("%d check(s) failed"):format(failures))
end
print("PASS: builder layout")
