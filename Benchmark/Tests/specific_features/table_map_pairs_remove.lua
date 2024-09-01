local t = {}

for i=1,100000,1 do
    t[tostring(i)] = i
end

for index, value in pairs(t) do
    t[index] = nil
end
