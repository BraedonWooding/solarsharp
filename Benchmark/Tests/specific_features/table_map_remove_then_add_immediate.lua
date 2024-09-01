local t = {}

for i=1,100000,1 do
    t[tostring(i)] = i
end

for i=1,100000,1 do
    t[tostring(i)] = nil
    t[tostring(i)] = i
end
