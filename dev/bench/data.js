window.BENCHMARK_DATA = {
  "lastUpdate": 1725087778149,
  "repoUrl": "https://github.com/BraedonWooding/solarsharp",
  "entries": {
    "Benchmark": [
      {
        "commit": {
          "author": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "distinct": true,
          "id": "c285d3771cb1d9ad3fb1c8082a952ecbdd2bfcf0",
          "message": "Make moonsharp match solarsharp for impl to get a better baseline",
          "timestamp": "2024-08-29T01:49:37+10:00",
          "tree_id": "0d22352a47645c73a38ef492d639f39891489697",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/c285d3771cb1d9ad3fb1c8082a952ecbdd2bfcf0"
        },
        "date": 1724863111574,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1616477.4053385416,
            "unit": "ns",
            "range": "± 15869.977744049527"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 4865.945603506906,
            "unit": "ns",
            "range": "± 113.36565904229154"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4136874.3186383927,
            "unit": "ns",
            "range": "± 47020.20248609022"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12015.80053527832,
            "unit": "ns",
            "range": "± 1218.3790273509403"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1593852.2731770833,
            "unit": "ns",
            "range": "± 12840.697987936306"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 4816.085883076986,
            "unit": "ns",
            "range": "± 69.87823901441433"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4120726.930245536,
            "unit": "ns",
            "range": "± 32435.940165119202"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 13168.990672475284,
            "unit": "ns",
            "range": "± 886.3877639843021"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "distinct": true,
          "id": "e185ec3dbb4b059995c8f261735d3eb67d566416",
          "message": "Restrict iterations to 30, this will increase errors but make builds take significantly less time",
          "timestamp": "2024-08-29T12:43:14+10:00",
          "tree_id": "4a243a265e60b7185e38e1aea09a8c76573bdb9c",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/e185ec3dbb4b059995c8f261735d3eb67d566416"
        },
        "date": 1724899859381,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1603046.7707868305,
            "unit": "ns",
            "range": "± 7245.523519172631"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 4591.134633890788,
            "unit": "ns",
            "range": "± 62.62712493865589"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4006684.191145833,
            "unit": "ns",
            "range": "± 29054.05434423527"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 10466.86192220052,
            "unit": "ns",
            "range": "± 763.9765542703537"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1755703.7183314732,
            "unit": "ns",
            "range": "± 9866.058599029253"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 4843.024039459228,
            "unit": "ns",
            "range": "± 88.48782983105922"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3992318.56640625,
            "unit": "ns",
            "range": "± 20763.559332111974"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12229.078791809083,
            "unit": "ns",
            "range": "± 1347.4393382802934"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "Braedonww@gmail.com",
            "name": "Braedon",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6fbf43e36a947d3220b1bb3db853340207a7357d",
          "message": "Update dotnet.yml",
          "timestamp": "2024-08-29T12:54:23+10:00",
          "tree_id": "0a1f34261dc14b335c8bd2d62065836d696aac1e",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/6fbf43e36a947d3220b1bb3db853340207a7357d"
        },
        "date": 1724900662595,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1803989.6185825893,
            "unit": "ns",
            "range": "± 7393.344857525778"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 4840.925293477377,
            "unit": "ns",
            "range": "± 65.51314832791793"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4003530.0677083335,
            "unit": "ns",
            "range": "± 24434.01627849049"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12299.691909263875,
            "unit": "ns",
            "range": "± 992.6701803448357"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1622439.8264322917,
            "unit": "ns",
            "range": "± 6049.499395109004"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 4812.66145324707,
            "unit": "ns",
            "range": "± 105.20422832510228"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4029068.95703125,
            "unit": "ns",
            "range": "± 21478.398841852264"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12057.94766857006,
            "unit": "ns",
            "range": "± 785.732417688066"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "distinct": true,
          "id": "5cd73168c295e4685741659ac02f9100ef472b7c",
          "message": "[Perf/Interperter]: Remove AutoYield\n\nThis was a feature that let you have the interpreter yield every X\ninstructions, the intention was to allow you to limit how much a script\ncould run in a given frame.\n\nThis sounds like a good idea but in practice doesn't work;\n- Games want to separate rendering from computation anyways this is what\n  TPS vs FPS represents.  You also want your LUA mods to execute the\n  same across all clients so you need them to execute their entire\n  \"tick\" within a tick, so switching a simple call into a for loop\n  just to spread TPS over multiple frames is a very complicated way to\n  solve that problem.  Instead just perform your rendering on a\n  separate thread...\n- You pay for the cost of managing the special forced yields,\n  this results in a compariosn on *every single instruction*.  In\n  practice most modern languages don't support instruction trapping down\n  to every singl eone, instead they do it on boundaries i.e. calling\n  into functions.",
          "timestamp": "2024-08-29T15:37:30+10:00",
          "tree_id": "1a18fd741f015347e88932d78f2c052d42f146bb",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/5cd73168c295e4685741659ac02f9100ef472b7c"
        },
        "date": 1724910547410,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1613351.255580357,
            "unit": "ns",
            "range": "± 11067.485199922192"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5273.570155779521,
            "unit": "ns",
            "range": "± 136.46550998446085"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4067509.7265625,
            "unit": "ns",
            "range": "± 40517.61732365159"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12202.978253682455,
            "unit": "ns",
            "range": "± 539.0390188459484"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1611960.5153645833,
            "unit": "ns",
            "range": "± 14953.139074670136"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5197.181037113584,
            "unit": "ns",
            "range": "± 253.60359010536348"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 101771.51891559103,
            "unit": "ns",
            "range": "± 2551.346599096026"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3947788.775,
            "unit": "ns",
            "range": "± 36008.61854444085"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12628.270559183757,
            "unit": "ns",
            "range": "± 692.7442868851618"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "distinct": true,
          "id": "6134cb9b6ca5c396e6c3c492b58c57f3cc3f3277",
          "message": "[Perf/Interpreter]: Remove some yield cases from opcodes\n\nSome opcodes had special handling for yield but this wasn't actually\npossible to be hit (to my knowledge) for example ExecEq/Add/... all\nchecked whether or not the instructionPtr would be < 0 and if so threw\nexceptions so having another check checking if the value is == -99 after\nthat doesn't make any sense...\n\nThis is unlikely to significantly improve performance because well it's\njust another conditional on each loop but given the last one had about a\n50ms impact I would expect something around that.\n\nThere are still some cases that check it (where it's possible to be hit)\nbut I'll probably remove those too.\n\nI want to change how coroutines are handled anyways for performance\nreasons (they should operate more like a c# coroutine).",
          "timestamp": "2024-08-29T21:01:06+10:00",
          "tree_id": "91c3a1cc342fad823eac610d206ef4e100220c37",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/6134cb9b6ca5c396e6c3c492b58c57f3cc3f3277"
        },
        "date": 1724929970200,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1677791.8960658482,
            "unit": "ns",
            "range": "± 46566.057489410116"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5439.568394380457,
            "unit": "ns",
            "range": "± 106.22780323962694"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4081401.909375,
            "unit": "ns",
            "range": "± 45426.361004359045"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12776.438686116537,
            "unit": "ns",
            "range": "± 757.1767503563757"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1742264.9343098958,
            "unit": "ns",
            "range": "± 84141.66652207864"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5412.583927154541,
            "unit": "ns",
            "range": "± 124.49291488608354"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 98442.43623234675,
            "unit": "ns",
            "range": "± 2501.215091361995"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4015850.927604167,
            "unit": "ns",
            "range": "± 28449.531518480722"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12910.801413536072,
            "unit": "ns",
            "range": "± 220.53326856384786"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "braedonww@gmail.com",
            "name": "Braedon Wooding",
            "username": "BraedonWooding"
          },
          "distinct": true,
          "id": "9f692f2df7d0435037883d2edc0067c4bed46b13",
          "message": "Set values for mandel/queen rather than args",
          "timestamp": "2024-08-29T21:35:48+10:00",
          "tree_id": "5c6282c62e43d8058ec0b782c987b31ff6db130d",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/9f692f2df7d0435037883d2edc0067c4bed46b13"
        },
        "date": 1724933022748,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 184821241.6,
            "unit": "ns",
            "range": "± 695866.634370108"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1602062.1140625,
            "unit": "ns",
            "range": "± 11585.052679171387"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5314.451402791341,
            "unit": "ns",
            "range": "± 290.74490498044713"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 76254906.45714286,
            "unit": "ns",
            "range": "± 299540.67972703354"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 5917314776.8,
            "unit": "ns",
            "range": "± 14508307.14957355"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 9833702.394791666,
            "unit": "ns",
            "range": "± 35213.51243125888"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 104900444.75384615,
            "unit": "ns",
            "range": "± 322597.88348137564"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1199276431.5333333,
            "unit": "ns",
            "range": "± 13142598.927502956"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4060222.6489583333,
            "unit": "ns",
            "range": "± 38520.96842642407"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 11688.315604654948,
            "unit": "ns",
            "range": "± 1242.2751220006094"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 654310441,
            "unit": "ns",
            "range": "± 7756080.04549847"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 40827525.169230774,
            "unit": "ns",
            "range": "± 433479.2744873363"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 617399779.1428572,
            "unit": "ns",
            "range": "± 3034009.7003181186"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 182224766.14285713,
            "unit": "ns",
            "range": "± 783423.0555150497"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1694540.812565104,
            "unit": "ns",
            "range": "± 57226.74163906333"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5118.132065073649,
            "unit": "ns",
            "range": "± 237.85598320112146"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 76121574.92857143,
            "unit": "ns",
            "range": "± 200008.94114698409"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 5915836061.133333,
            "unit": "ns",
            "range": "± 8635403.939454133"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 9816645.58984375,
            "unit": "ns",
            "range": "± 16416.501842321726"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 103969710.17333333,
            "unit": "ns",
            "range": "± 176232.04777759482"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 192091092.4,
            "unit": "ns",
            "range": "± 1384284.1049806192"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 101738.22896634616,
            "unit": "ns",
            "range": "± 678.6194497293731"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 206540153.61904764,
            "unit": "ns",
            "range": "± 1525292.2653854175"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 11999954.458333334,
            "unit": "ns",
            "range": "± 32681.55178157412"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 217726386.61538458,
            "unit": "ns",
            "range": "± 1638822.829534693"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1138552356.6153846,
            "unit": "ns",
            "range": "± 4125109.9563621846"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3968797.6869791667,
            "unit": "ns",
            "range": "± 28555.047314196283"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12633.909031835095,
            "unit": "ns",
            "range": "± 621.9796586777684"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 671188352.4,
            "unit": "ns",
            "range": "± 6636348.429686084"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 41375796.95384614,
            "unit": "ns",
            "range": "± 279136.7382763332"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 588173754.4285715,
            "unit": "ns",
            "range": "± 2672740.5555652683"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "Braedonww@gmail.com",
            "name": "Braedon",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3aafed2593f4e83fa5255d923f0f980d9889208f",
          "message": "Update README.md",
          "timestamp": "2024-08-31T11:53:50+10:00",
          "tree_id": "255939cce31dd450c11acfb90c8dd2c3b7622c88",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/3aafed2593f4e83fa5255d923f0f980d9889208f"
        },
        "date": 1725070412348,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 186276223.73809525,
            "unit": "ns",
            "range": "± 1202193.7567449987"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1663003.379296875,
            "unit": "ns",
            "range": "± 27755.488018564352"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5278.836888631185,
            "unit": "ns",
            "range": "± 204.77536801168023"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 77003149.97619046,
            "unit": "ns",
            "range": "± 124432.03874699569"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 6098501660.357142,
            "unit": "ns",
            "range": "± 13424754.400782702"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 9253896.016666668,
            "unit": "ns",
            "range": "± 82389.61396376992"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 107215550.24,
            "unit": "ns",
            "range": "± 242214.16466860467"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1163120286.7333333,
            "unit": "ns",
            "range": "± 11570023.984618302"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3991771.2489583334,
            "unit": "ns",
            "range": "± 34868.38408395419"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12056.980607657597,
            "unit": "ns",
            "range": "± 542.7996800017474"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 676270922.9333333,
            "unit": "ns",
            "range": "± 7074653.5272422405"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 40574354.81318681,
            "unit": "ns",
            "range": "± 145845.91874066953"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 609280829.6428572,
            "unit": "ns",
            "range": "± 5256864.58242673"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 185194915.26190478,
            "unit": "ns",
            "range": "± 743909.823420049"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1766272.914428711,
            "unit": "ns",
            "range": "± 32691.423477375585"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5199.173039140373,
            "unit": "ns",
            "range": "± 253.0006344928667"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 76239710.76530612,
            "unit": "ns",
            "range": "± 199649.03592895588"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 6113836687.666667,
            "unit": "ns",
            "range": "± 20187410.47985265"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 9327662.558035715,
            "unit": "ns",
            "range": "± 31663.642319764873"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 103547215.27142856,
            "unit": "ns",
            "range": "± 300825.02110757394"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 199528872.57142857,
            "unit": "ns",
            "range": "± 787932.3944448073"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 100334.29438273112,
            "unit": "ns",
            "range": "± 2542.6062536925233"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 226142808.76923078,
            "unit": "ns",
            "range": "± 2361070.1901697093"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 11933015.564583333,
            "unit": "ns",
            "range": "± 40805.25967936888"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 226758311.1777778,
            "unit": "ns",
            "range": "± 11896552.275054842"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1172472584.1333334,
            "unit": "ns",
            "range": "± 13092806.937524036"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3925866.5640625,
            "unit": "ns",
            "range": "± 43573.52660024708"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 11897.731458536784,
            "unit": "ns",
            "range": "± 212.23328660706522"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 649463352,
            "unit": "ns",
            "range": "± 4717125.911884618"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 38950423.884615384,
            "unit": "ns",
            "range": "± 138060.0445455551"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 598448063.5333333,
            "unit": "ns",
            "range": "± 2061595.7663827818"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "Braedonww@gmail.com",
            "name": "Braedon",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e62a170096a9f511dad4653733700cf9899de590",
          "message": "Merge pull request #1 from BraedonWooding/feature/perf-improvements\n\nFeature/perf improvements for table",
          "timestamp": "2024-08-31T13:47:42+10:00",
          "tree_id": "3c573926e897782aeb3da13cde68fe3598189185",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/e62a170096a9f511dad4653733700cf9899de590"
        },
        "date": 1725077960148,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 184949017.04444447,
            "unit": "ns",
            "range": "± 1793639.4636464342"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1612779.8052083333,
            "unit": "ns",
            "range": "± 7756.471206204079"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5331.734577433268,
            "unit": "ns",
            "range": "± 340.31644633907115"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 79088869.75238094,
            "unit": "ns",
            "range": "± 209919.87936808637"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 6030547424.066667,
            "unit": "ns",
            "range": "± 12448309.069019537"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 8767577.71986607,
            "unit": "ns",
            "range": "± 9626.84940450658"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: regexredux.lua-2.lua)",
            "value": 147158.6968343099,
            "unit": "ns",
            "range": "± 6761.166957141123"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 103862705.32307695,
            "unit": "ns",
            "range": "± 553898.482109435"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1171814746.9285715,
            "unit": "ns",
            "range": "± 10659085.379611053"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3960647.6688701925,
            "unit": "ns",
            "range": "± 23168.82009473441"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 12368.01949441844,
            "unit": "ns",
            "range": "± 712.9758040050493"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 656494734.8571428,
            "unit": "ns",
            "range": "± 7085380.797399789"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: mandel.lua)",
            "value": 12213661337.333334,
            "unit": "ns",
            "range": "± 34891723.35575117"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 43570702.33928571,
            "unit": "ns",
            "range": "± 161088.8166340288"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 613622203.2142857,
            "unit": "ns",
            "range": "± 2931251.5392328235"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 183865008.89743587,
            "unit": "ns",
            "range": "± 413648.332938594"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1756883.3956473214,
            "unit": "ns",
            "range": "± 10109.349233853585"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5141.270914713542,
            "unit": "ns",
            "range": "± 390.71334092964054"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 78522194.29523809,
            "unit": "ns",
            "range": "± 130761.70214999917"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 6056156685.230769,
            "unit": "ns",
            "range": "± 5225274.378816185"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 8782976.538541667,
            "unit": "ns",
            "range": "± 15066.494315678938"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 106296365.74666665,
            "unit": "ns",
            "range": "± 518453.0399371925"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 191387009.57777777,
            "unit": "ns",
            "range": "± 425497.2674859285"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 104260.61385091145,
            "unit": "ns",
            "range": "± 911.1315209247915"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 197125326.85714287,
            "unit": "ns",
            "range": "± 2187837.862302339"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 12035094.965625,
            "unit": "ns",
            "range": "± 53603.925039713904"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 218573715.7179487,
            "unit": "ns",
            "range": "± 1977681.0841822647"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1153204035.857143,
            "unit": "ns",
            "range": "± 7556601.613336166"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3610075.5166666666,
            "unit": "ns",
            "range": "± 17533.630724741535"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12228.085065205893,
            "unit": "ns",
            "range": "± 842.9566250780222"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 482604917.71428573,
            "unit": "ns",
            "range": "± 2399809.895871832"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: mandel.lua)",
            "value": 11946361749.933332,
            "unit": "ns",
            "range": "± 27123420.14330345"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 38217565.74871794,
            "unit": "ns",
            "range": "± 225405.6007493611"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 482522163.28571427,
            "unit": "ns",
            "range": "± 3953143.8225834193"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "Braedonww@gmail.com",
            "name": "Braedon",
            "username": "BraedonWooding"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "44ec311786d5c393db1c167200361128793817b4",
          "message": "Merge pull request #2 from BraedonWooding/perf/exec-index-improvements\n\nExecIndex previously had one large method that combined both multi in…",
          "timestamp": "2024-08-31T16:33:06+10:00",
          "tree_id": "36191ce396a449fc3c719a9a9c8fcd0993d0f7cf",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/44ec311786d5c393db1c167200361128793817b4"
        },
        "date": 1725087777608,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 182367274.12820512,
            "unit": "ns",
            "range": "± 872671.6216111623"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1594855.6654575893,
            "unit": "ns",
            "range": "± 5257.74051585606"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 5476.224819691976,
            "unit": "ns",
            "range": "± 385.6792498182623"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 75339385.04081632,
            "unit": "ns",
            "range": "± 242881.0074443301"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 6064422656.533334,
            "unit": "ns",
            "range": "± 9227220.365334312"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 8678907.61875,
            "unit": "ns",
            "range": "± 38816.18564999518"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: regexredux.lua-2.lua)",
            "value": 152750.43492024738,
            "unit": "ns",
            "range": "± 8832.928717245211"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 103500870.95714286,
            "unit": "ns",
            "range": "± 192792.35894961018"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1223018423.9333334,
            "unit": "ns",
            "range": "± 7296580.765678445"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4139449.2234375,
            "unit": "ns",
            "range": "± 31127.503607148574"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 11680.38044896619,
            "unit": "ns",
            "range": "± 970.9873262750139"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 721297844.5333333,
            "unit": "ns",
            "range": "± 11339881.187901087"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: mandel.lua)",
            "value": 12522629116.2,
            "unit": "ns",
            "range": "± 41323248.843784764"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 42458237.90555556,
            "unit": "ns",
            "range": "± 252834.94569467125"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 630841761.8571428,
            "unit": "ns",
            "range": "± 6436176.876162884"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 182086562.84444442,
            "unit": "ns",
            "range": "± 419300.11678704934"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1576450.2294921875,
            "unit": "ns",
            "range": "± 6193.36861671561"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 5244.966031567804,
            "unit": "ns",
            "range": "± 329.7433675812414"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 75423777.87755102,
            "unit": "ns",
            "range": "± 48205.265991050466"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 6041282780.4,
            "unit": "ns",
            "range": "± 10221179.737766238"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 8711649.165625,
            "unit": "ns",
            "range": "± 21129.46767087722"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 104786084.96000001,
            "unit": "ns",
            "range": "± 784254.5414216962"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 192324502.35714287,
            "unit": "ns",
            "range": "± 2257542.4972680868"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 103322.39580078125,
            "unit": "ns",
            "range": "± 1782.892038822627"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 227724904.2,
            "unit": "ns",
            "range": "± 3085640.105418652"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 12033972.351041667,
            "unit": "ns",
            "range": "± 117642.27363485142"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 217519516.26190478,
            "unit": "ns",
            "range": "± 2084046.6976791916"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1135424993.6666667,
            "unit": "ns",
            "range": "± 14160281.61905806"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4011244.0162760415,
            "unit": "ns",
            "range": "± 12036.711498494597"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12762.112305196126,
            "unit": "ns",
            "range": "± 646.4071355364142"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 520970575.53333336,
            "unit": "ns",
            "range": "± 8046168.299014576"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: mandel.lua)",
            "value": 11529459023.333334,
            "unit": "ns",
            "range": "± 89024206.69460961"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 38805217.78974359,
            "unit": "ns",
            "range": "± 156731.42874257415"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 481556046.71428573,
            "unit": "ns",
            "range": "± 2149655.4245118317"
          }
        ]
      }
    ]
  }
}