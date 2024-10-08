-- The Computer Language Benchmarks Game
-- http://benchmarksgame.alioth.debian.org/
-- regex-dna program contributed by Jim Roseborough
-- modified by Victor Tang
-- optimized & replaced inefficient use of gsub with gmatch
-- partitioned sequence to prevent extraneous redundant string copy
-- converted from regex-dna program
-- fixed by saito tanaka 

seq = io.read("*a")
ilen, seq = #seq, seq:gsub('>[^%c]*%c*', ''):gsub('%c+', '')
clen = #seq

local variants = { 'agggtaaa|tttaccct',
                   '[cgt]gggtaaa|tttaccc[acg]',
                   'a[act]ggtaaa|tttacc[agt]t',
                   'ag[act]gtaaa|tttac[agt]ct',
                   'agg[act]taaa|ttta[agt]cct',
                   'aggg[acg]aaa|ttt[cgt]ccct',
                   'agggt[cgt]aa|tt[acg]accct',
                   'agggta[cgt]a|t[acg]taccct',
                   'agggtaa[cgt]|[acg]ttaccct', }

            	-- illegal key names should by between [] like a={['@&!']=4}

local subst = { ['tHa[Nt]']='<4>', ['aND|caN|Ha[DS]|WaS']='<3>', ['a[NSt]|BY']='<2>', 
                ['<[^>]*>']='|', ['\\|[^|][^|]*\\|']='-' ,}

function countmatches(variant)
   local n = 0
   variant:gsub('([^|]+)|?', function(pattern)
      for _ in seq:gmatch(pattern) do n = n + 1 end
   end)
   return n
end

for _, p in ipairs(variants) do
  string.format('%s %d\n', p, countmatches(p))
end

function partitionstring(seq)
  local seg = math.floor( math.sqrt(#seq) )
  local seqtable = {}
  for nextstart = 1, #seq, seg do
    table.insert(seqtable, seq:sub(nextstart, nextstart + seg - 1))
  end
  return seqtable
end
function chunk_gsub(t, k, v)
  for i, p in ipairs(t) do
    t[i] = p:find(k) and p:gsub(k, v) or t[i]
  end
  return t
end

seq = partitionstring(seq)
for k, v in pairs(subst) do
  chunk_gsub(seq, k, v)
end
seq = table.concat(seq)
string.format('\n%d\n%d\n%d\n', ilen, clen, #seq)

