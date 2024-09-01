local t = {}

for i=1,10000,1 do
    t[i] = i
end

for i=1,10000,1 do
    t[i] = nil
end
