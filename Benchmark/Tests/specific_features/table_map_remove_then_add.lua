local t = {}

for i=1,10000,1 do
    t[tostring(i)] = i
end

for i=1,10000,1 do
    t[tostring(i)] = nil
end

for i=1,10000,1 do
    t[tostring(i)] = i
end
