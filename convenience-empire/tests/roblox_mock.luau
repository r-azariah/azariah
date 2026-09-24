-- Just enough of the Roblox API for BuildStoreTemplate.luau to run outside Studio, so its layout
-- can be checked. Rotation is ignored: everything the builder makes is axis-aligned except the
-- slightly tilted store sign.

local Vector3Meta = {}
Vector3Meta.__index = Vector3Meta
local function v3(x, y, z)
	return setmetatable({ X = x, Y = y, Z = z }, Vector3Meta)
end
Vector3Meta.__add = function(a, b)
	return v3(a.X + b.X, a.Y + b.Y, a.Z + b.Z)
end
Vector3Meta.__sub = function(a, b)
	return v3(a.X - b.X, a.Y - b.Y, a.Z - b.Z)
end
Vector3 = {
	new = function(x, y, z)
		return v3(x or 0, y or 0, z or 0)
	end,
}

Vector2 = {
	new = function(x, y)
		return { X = x or 0, Y = y or 0 }
	end,
}

local CFrameMeta = {}
CFrameMeta.__index = CFrameMeta
local function cf(position)
	return setmetatable({ Position = position, X = position.X, Y = position.Y, Z = position.Z }, CFrameMeta)
end
CFrameMeta.__mul = function(a, b)
	if getmetatable(b) == CFrameMeta then
		return cf(a.Position + b.Position)
	end
	return a.Position + b
end
function CFrameMeta:PointToObjectSpace(point)
	return point - self.Position
end
CFrame = {
	new = function(x, y, z)
		if type(x) == "table" then
			return cf(x)
		end
		return cf(v3(x or 0, y or 0, z or 0))
	end,
	Angles = function()
		return cf(v3(0, 0, 0))
	end,
}

Color3 = {
	fromRGB = function(r, g, b)
		return { r, g, b }
	end,
	new = function(r, g, b)
		return { r, g, b }
	end,
}

UDim2 = {
	fromScale = function(x, y)
		return { x, y }
	end,
}

local function enumProxy(path)
	return setmetatable({}, {
		__index = function(_, key)
			return enumProxy(path .. "." .. key)
		end,
		__tostring = function()
			return path
		end,
	})
end
Enum = enumProxy("Enum")

MockInstances = {}

local InstanceMethods = {}
function InstanceMethods:SetAttribute(key, value)
	self._attributes[key] = value
end
function InstanceMethods:GetAttribute(key)
	return self._attributes[key]
end
function InstanceMethods:IsA(className)
	return self.ClassName == className or (className == "BasePart" and self.ClassName == "Part")
end
function InstanceMethods:FindFirstChild(name)
	for _, child in self._children do
		if child.Name == name then
			return child
		end
	end
	return nil
end
function InstanceMethods:GetChildren()
	return table.clone(self._children)
end
function InstanceMethods:PivotTo(target)
	self._pivotedTo = target
end
function InstanceMethods:Raycast()
	return nil
end

local function newInstance(className)
	local data = { ClassName = className, Name = className, _children = {}, _attributes = {}, _tags = {} }
	if className == "Part" then
		data.CanCollide = true
		data.Transparency = 0
	end
	local proxy
	proxy = setmetatable({}, {
		__index = function(_, key)
			if InstanceMethods[key] then
				return InstanceMethods[key]
			end
			return data[key]
		end,
		__newindex = function(_, key, value)
			if key == "Parent" then
				local old = data.Parent
				if old then
					local siblings = old._children
					table.remove(siblings, table.find(siblings, proxy))
				end
				if value then
					table.insert(value._children, proxy)
				end
			end
			data[key] = value
		end,
	})
	table.insert(MockInstances, proxy)
	return proxy
end
Instance = { new = newInstance }

workspace = newInstance("Workspace")
workspace.CurrentCamera = { Focus = CFrame.new(0, 0, 0) }

local selection = {}
local services = {
	CollectionService = {
		AddTag = function(_, instance, tag)
			instance._tags[tag] = true
		end,
	},
	Selection = {
		Get = function()
			return selection
		end,
		Set = function(_, list)
			selection = list
		end,
	},
	ServerStorage = newInstance("ServerStorage"),
	ChangeHistoryService = {
		TryBeginRecording = function()
			return "recording"
		end,
		FinishRecording = function() end,
	},
}
game = {
	GetService = function(_, name)
		return assert(services[name], "mock has no service " .. name)
	end,
}

warn = function(...)
	print("WARN:", ...)
end
