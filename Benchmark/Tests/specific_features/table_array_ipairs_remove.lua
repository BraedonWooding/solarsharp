local t = {}

for i=1,100000,1 do
    t[i] = i
end

for index, value in ipairs(t) do
    t[index] = nil
end
