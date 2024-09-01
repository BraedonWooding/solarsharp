local t = {}

for i=1,10000,1 do
    table.insert(t, i)
end

for i=1,10000,1 do
    table.remove(t, 1)
end
