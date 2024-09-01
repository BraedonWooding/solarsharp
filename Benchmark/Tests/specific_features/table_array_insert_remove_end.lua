local t = {}

for i=1,100000,1 do
    table.insert(t, i)
end

for i=1,100000,1 do
    table.remove(t)
end
