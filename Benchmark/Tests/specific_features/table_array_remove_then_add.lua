local t = {}

for i=1,100000,1 do
    t[i] = i
end

for i=1,100000,1 do
    t[i] = nil
end

for i=1,100000,1 do
    t[i] = i
end
