local t = {}

for i=1,100000,1 do
    t[i] = i
end

local i, v = next(t, nil)  -- i is an index of t, v = t[i]
while i do
    t[i] = nil
    i, v = next(t, i)        -- get next index
end
