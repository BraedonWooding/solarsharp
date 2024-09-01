window.BENCHMARK_DATA = {
  "lastUpdate": 1725205646300,
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
          "id": "f9a18733b63917b44638c236ecbf77a7a80d5a9c",
          "message": "Merge pull request #3 from BraedonWooding/perf/dictionary-baseline\n\n[Perf] Custom Dictionary Impl",
          "timestamp": "2024-09-01T23:16:09+10:00",
          "tree_id": "306d6af88583ded1b5050c74171f010a9db4f529",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/f9a18733b63917b44638c236ecbf77a7a80d5a9c"
        },
        "date": 1725200775749,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 183789805.2307692,
            "unit": "ns",
            "range": "± 404180.8556059275"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1605365.0990084135,
            "unit": "ns",
            "range": "± 7620.897570083095"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 3853.013393674578,
            "unit": "ns",
            "range": "± 315.4274808568962"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 79253258.51020409,
            "unit": "ns",
            "range": "± 154302.93971181347"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 6076190311.866667,
            "unit": "ns",
            "range": "± 9765638.777849264"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 8690738.47767857,
            "unit": "ns",
            "range": "± 13951.65592356978"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: regexredux.lua-2.lua)",
            "value": 156584.6250406901,
            "unit": "ns",
            "range": "± 7236.276917377171"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 103481408.89333333,
            "unit": "ns",
            "range": "± 447003.31739219517"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: startup.lua)",
            "value": 115781.67307942708,
            "unit": "ns",
            "range": "± 1650.7541909143652"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_function.lua)",
            "value": 406535.5972981771,
            "unit": "ns",
            "range": "± 5559.889329497036"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_indexed.lua)",
            "value": 2043766.29140625,
            "unit": "ns",
            "range": "± 9381.461395015065"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 4681178.033854167,
            "unit": "ns",
            "range": "± 34126.7772228391"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 1328801831.5714285,
            "unit": "ns",
            "range": "± 2145944.7504024426"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_ipairs.lua)",
            "value": 1273904.0952845982,
            "unit": "ns",
            "range": "± 5362.39363011538"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1428100.5802083334,
            "unit": "ns",
            "range": "± 8981.318591956111"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_next.lua)",
            "value": 7325780.980769231,
            "unit": "ns",
            "range": "± 18820.955468050302"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_next_remove.lua)",
            "value": 1954573.4324776786,
            "unit": "ns",
            "range": "± 9866.841433733975"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove.lua)",
            "value": 630440.5642438616,
            "unit": "ns",
            "range": "± 6598.680061732011"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove_then_add.lua)",
            "value": 947342.2201450893,
            "unit": "ns",
            "range": "± 13404.086976507717"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 853990.509765625,
            "unit": "ns",
            "range": "± 4503.0217393698085"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_insert.lua)",
            "value": 5592698.762276785,
            "unit": "ns",
            "range": "± 19360.815716590074"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_next.lua)",
            "value": 7295631.237723215,
            "unit": "ns",
            "range": "± 22995.93974205567"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_next_remove.lua)",
            "value": 9396572.426041666,
            "unit": "ns",
            "range": "± 38982.46104694198"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_pairs.lua)",
            "value": 6739067.292708334,
            "unit": "ns",
            "range": "± 25550.63313530056"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_pairs_remove.lua)",
            "value": 7124696.237723215,
            "unit": "ns",
            "range": "± 14957.565596696671"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove.lua)",
            "value": 8814539.61607143,
            "unit": "ns",
            "range": "± 14726.187337004512"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove_then_add.lua)",
            "value": 12575763.276041666,
            "unit": "ns",
            "range": "± 24933.280386981587"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 12369634.7890625,
            "unit": "ns",
            "range": "± 33333.88336293871"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1143302955.0666666,
            "unit": "ns",
            "range": "± 11347974.655380288"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4039017.125,
            "unit": "ns",
            "range": "± 16970.885581874092"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 10508.067466227214,
            "unit": "ns",
            "range": "± 592.6511624067169"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 712116151.9333333,
            "unit": "ns",
            "range": "± 6342452.7417320125"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: mandel.lua)",
            "value": 12386015949.214285,
            "unit": "ns",
            "range": "± 24983998.8483025"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 39321068.19487179,
            "unit": "ns",
            "range": "± 195074.32782034055"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 616394020.2666667,
            "unit": "ns",
            "range": "± 4717963.448700652"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: startup.lua)",
            "value": 1877505.1958512932,
            "unit": "ns",
            "range": "± 102655.37392276655"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_function.lua)",
            "value": 4326552.911458333,
            "unit": "ns",
            "range": "± 685060.7850954107"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_indexed.lua)",
            "value": 573982125.6,
            "unit": "ns",
            "range": "± 10473692.257109107"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 1148683184.9285715,
            "unit": "ns",
            "range": "± 6075756.474201375"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 2629236169.857143,
            "unit": "ns",
            "range": "± 10088742.218886998"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_ipairs.lua)",
            "value": 8166457.347395834,
            "unit": "ns",
            "range": "± 522153.55867838283"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 9579608.696354168,
            "unit": "ns",
            "range": "± 707717.459921157"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_next.lua)",
            "value": 15648204.013469828,
            "unit": "ns",
            "range": "± 677454.3344600205"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_next_remove.lua)",
            "value": 10372781.463020833,
            "unit": "ns",
            "range": "± 779293.7560585726"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove.lua)",
            "value": 6640640.574084052,
            "unit": "ns",
            "range": "± 784388.2873500969"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove_then_add.lua)",
            "value": 9886756.962760417,
            "unit": "ns",
            "range": "± 700734.9493034552"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 532247818.14285713,
            "unit": "ns",
            "range": "± 2075627.1745391844"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_insert.lua)",
            "value": 9730778.216666667,
            "unit": "ns",
            "range": "± 793907.9989605792"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_next.lua)",
            "value": 15648055.465625,
            "unit": "ns",
            "range": "± 662073.5632557546"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_next_remove.lua)",
            "value": 19913358.421875,
            "unit": "ns",
            "range": "± 728725.9407518959"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_pairs.lua)",
            "value": 14569346.794791667,
            "unit": "ns",
            "range": "± 955852.3229677737"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_pairs_remove.lua)",
            "value": 16783906.002083335,
            "unit": "ns",
            "range": "± 1018199.8634516111"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove.lua)",
            "value": 17539432.577083334,
            "unit": "ns",
            "range": "± 319229.6286536827"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove_then_add.lua)",
            "value": 27135661.075,
            "unit": "ns",
            "range": "± 1229322.1029399836"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 543812238,
            "unit": "ns",
            "range": "± 4293242.776074497"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 183792117.79999998,
            "unit": "ns",
            "range": "± 292175.1449027336"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1587448.2417689732,
            "unit": "ns",
            "range": "± 11982.7095984402"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 3893.6240283421107,
            "unit": "ns",
            "range": "± 276.2301520703922"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 78067145.04761906,
            "unit": "ns",
            "range": "± 149194.34647032386"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 6011755197.933333,
            "unit": "ns",
            "range": "± 9289050.947930193"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 8687290.184151785,
            "unit": "ns",
            "range": "± 19670.756278595305"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 104698433.26666665,
            "unit": "ns",
            "range": "± 1117902.5161179632"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: startup.lua)",
            "value": 226271.77853190104,
            "unit": "ns",
            "range": "± 11266.095052545026"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_function.lua)",
            "value": 400999.6782877604,
            "unit": "ns",
            "range": "± 5040.878976629828"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_indexed.lua)",
            "value": 2047914.1091145833,
            "unit": "ns",
            "range": "± 8125.4117605367555"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 4653810.913020833,
            "unit": "ns",
            "range": "± 15277.135581606757"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 1326401292.4615386,
            "unit": "ns",
            "range": "± 1293663.5143113025"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_ipairs.lua)",
            "value": 1268230.5516183036,
            "unit": "ns",
            "range": "± 6177.93198342979"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1430786.3178385417,
            "unit": "ns",
            "range": "± 7934.964155070046"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_next.lua)",
            "value": 7442807.383413462,
            "unit": "ns",
            "range": "± 12861.809833247802"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_next_remove.lua)",
            "value": 1959554.7408854167,
            "unit": "ns",
            "range": "± 6739.49783136573"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove.lua)",
            "value": 638604.609765625,
            "unit": "ns",
            "range": "± 6095.13734643623"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove_then_add.lua)",
            "value": 949561.2027994791,
            "unit": "ns",
            "range": "± 9195.724188545357"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 1095767.435611979,
            "unit": "ns",
            "range": "± 201862.4198940918"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_insert.lua)",
            "value": 5467810.472956731,
            "unit": "ns",
            "range": "± 7584.835565198015"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_next.lua)",
            "value": 7305784.873958333,
            "unit": "ns",
            "range": "± 30325.85067440667"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_next_remove.lua)",
            "value": 9309612.654166667,
            "unit": "ns",
            "range": "± 14532.866492294994"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_pairs.lua)",
            "value": 6683406.521763393,
            "unit": "ns",
            "range": "± 15818.030202197573"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_pairs_remove.lua)",
            "value": 7065272.619270833,
            "unit": "ns",
            "range": "± 22810.095790229803"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove.lua)",
            "value": 8874502.34014423,
            "unit": "ns",
            "range": "± 16352.862517565485"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove_then_add.lua)",
            "value": 12588817.425,
            "unit": "ns",
            "range": "± 23669.277676153106"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 12165766.03013393,
            "unit": "ns",
            "range": "± 63228.886167323806"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 193803212.77777776,
            "unit": "ns",
            "range": "± 834358.2019632615"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 102744.59165736607,
            "unit": "ns",
            "range": "± 518.1435309065853"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 227602626.5,
            "unit": "ns",
            "range": "± 2253337.6785258846"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 12033191.589583334,
            "unit": "ns",
            "range": "± 49523.11445454348"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 219084788.1190476,
            "unit": "ns",
            "range": "± 2098683.1219668053"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: startup.lua)",
            "value": 104028.79584209736,
            "unit": "ns",
            "range": "± 850.4882269302693"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_function.lua)",
            "value": 1045880.3874162947,
            "unit": "ns",
            "range": "± 6068.304858218259"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_indexed.lua)",
            "value": 1562889.0802083334,
            "unit": "ns",
            "range": "± 6644.750988044079"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 2496072.9265625,
            "unit": "ns",
            "range": "± 14744.818571126953"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 12082286.877083333,
            "unit": "ns",
            "range": "± 32876.418357013616"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_ipairs.lua)",
            "value": 2684839.267020089,
            "unit": "ns",
            "range": "± 13029.592823677878"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1722714.5674479166,
            "unit": "ns",
            "range": "± 15771.225018284851"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_next.lua)",
            "value": 5001282.620535715,
            "unit": "ns",
            "range": "± 38174.38074813308"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_next_remove.lua)",
            "value": 2876378.1149553573,
            "unit": "ns",
            "range": "± 9987.267546466379"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove.lua)",
            "value": 1398074.7533482143,
            "unit": "ns",
            "range": "± 8939.774747564034"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove_then_add.lua)",
            "value": 1913199.281529018,
            "unit": "ns",
            "range": "± 10323.47909277542"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 43139502.449999996,
            "unit": "ns",
            "range": "± 59424.1006180472"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_insert.lua)",
            "value": 3280437.740131579,
            "unit": "ns",
            "range": "± 69222.87108386909"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_next.lua)",
            "value": 5052988.5875,
            "unit": "ns",
            "range": "± 64484.4291754373"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_next_remove.lua)",
            "value": 4137400.3203125,
            "unit": "ns",
            "range": "± 37845.60730018939"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_pairs.lua)",
            "value": 4676561.29375,
            "unit": "ns",
            "range": "± 35946.16759801947"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove.lua)",
            "value": 5018491.5484375,
            "unit": "ns",
            "range": "± 37649.671524361045"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove_then_add.lua)",
            "value": 6883296.537946428,
            "unit": "ns",
            "range": "± 60274.031038448265"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 6821709.984895834,
            "unit": "ns",
            "range": "± 65946.5569754451"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1115858213.0769231,
            "unit": "ns",
            "range": "± 5128598.325996076"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3831500.606971154,
            "unit": "ns",
            "range": "± 9364.377322732991"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 10172.053768026417,
            "unit": "ns",
            "range": "± 575.176924183007"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 488291094.64285713,
            "unit": "ns",
            "range": "± 6248033.78474832"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: mandel.lua)",
            "value": 11505067948.666666,
            "unit": "ns",
            "range": "± 45017868.12023086"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 40997415.599999994,
            "unit": "ns",
            "range": "± 177105.71853744262"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 472206383.71428573,
            "unit": "ns",
            "range": "± 1608372.6334536662"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: startup.lua)",
            "value": 1818384.3924479166,
            "unit": "ns",
            "range": "± 130385.95378135968"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_function.lua)",
            "value": 2679067.4009114583,
            "unit": "ns",
            "range": "± 262811.4159026806"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_indexed.lua)",
            "value": 35001422.466666676,
            "unit": "ns",
            "range": "± 23699.09406997126"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 69966373.60204081,
            "unit": "ns",
            "range": "± 96353.78404525947"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 2168082519.3333335,
            "unit": "ns",
            "range": "± 5606644.9256517375"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_ipairs.lua)",
            "value": 5681488.515885416,
            "unit": "ns",
            "range": "± 83191.74495491016"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 4686128.0703125,
            "unit": "ns",
            "range": "± 24638.073188206687"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_next.lua)",
            "value": 762282995.8666667,
            "unit": "ns",
            "range": "± 10396651.748790782"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_next_remove.lua)",
            "value": 5145392.583854167,
            "unit": "ns",
            "range": "± 39084.59947474441"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove.lua)",
            "value": 3521382.6256510415,
            "unit": "ns",
            "range": "± 1042671.5330221404"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove_then_add.lua)",
            "value": 5875210.505766369,
            "unit": "ns",
            "range": "± 137251.46052567428"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 5492121.944393382,
            "unit": "ns",
            "range": "± 107359.45229284793"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_insert.lua)",
            "value": 8443226.546875,
            "unit": "ns",
            "range": "± 981461.6286000528"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_next.lua)",
            "value": 790280035.8666667,
            "unit": "ns",
            "range": "± 10443558.54175524"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_next_remove.lua)",
            "value": 9635413.454947917,
            "unit": "ns",
            "range": "± 965103.4027366947"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_pairs.lua)",
            "value": 753979047.4666667,
            "unit": "ns",
            "range": "± 11173767.994385716"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_pairs_remove.lua)",
            "value": 9886968.887760418,
            "unit": "ns",
            "range": "± 774433.5238008857"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove.lua)",
            "value": 16303534.745833334,
            "unit": "ns",
            "range": "± 1201604.672830671"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove_then_add.lua)",
            "value": 22865550.165625,
            "unit": "ns",
            "range": "± 517710.40066037304"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 23813291.853125,
            "unit": "ns",
            "range": "± 1233104.3074292478"
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
          "id": "3859e59b187468078ee71cefda013ef40038bc69",
          "message": "Merge pull request #4 from BraedonWooding/perf/dictionary-improvements\n\n[Perf]: Table Improvements",
          "timestamp": "2024-09-02T00:38:40+10:00",
          "tree_id": "27f7fe9d2352367f971d5cfc8a3f9da568774edb",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/3859e59b187468078ee71cefda013ef40038bc69"
        },
        "date": 1725205645421,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: ack.lua)",
            "value": 182913091.11904764,
            "unit": "ns",
            "range": "± 797306.7370037269"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1727588.3824869792,
            "unit": "ns",
            "range": "± 52392.00043385366"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 3863.2941237858363,
            "unit": "ns",
            "range": "± 325.7534375269973"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: heapsort.lua)",
            "value": 75650302.06122449,
            "unit": "ns",
            "range": "± 106456.7630812986"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: mandel.lua)",
            "value": 5959948423.928572,
            "unit": "ns",
            "range": "± 8622751.899656678"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: queen.lua)",
            "value": 8484674.315104166,
            "unit": "ns",
            "range": "± 21687.12523439165"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: regexredux.lua-2.lua)",
            "value": 150024.87205403644,
            "unit": "ns",
            "range": "± 7359.191781730846"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: sieve.lua)",
            "value": 106058819.13846153,
            "unit": "ns",
            "range": "± 720618.0812214329"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: startup.lua)",
            "value": 109764.2623098273,
            "unit": "ns",
            "range": "± 2326.0842236948856"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_function.lua)",
            "value": 403753.290108817,
            "unit": "ns",
            "range": "± 4149.256074721963"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_indexed.lua)",
            "value": 2094036.03125,
            "unit": "ns",
            "range": "± 7504.487209917811"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 4708412.161197917,
            "unit": "ns",
            "range": "± 14858.735137970345"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 1333568583.7857144,
            "unit": "ns",
            "range": "± 4599217.397906922"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_ipairs.lua)",
            "value": 1286386.6296875,
            "unit": "ns",
            "range": "± 7953.85877410091"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1450352.621372768,
            "unit": "ns",
            "range": "± 8498.53153881295"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_next.lua)",
            "value": 7381928.834263393,
            "unit": "ns",
            "range": "± 22603.480180263825"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_next_remove.lua)",
            "value": 1883044.6555524555,
            "unit": "ns",
            "range": "± 8062.022203217425"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove.lua)",
            "value": 634201.8783854167,
            "unit": "ns",
            "range": "± 6428.235622399484"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove_then_add.lua)",
            "value": 960818.5560709635,
            "unit": "ns",
            "range": "± 5103.327532120665"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 832000.3244791667,
            "unit": "ns",
            "range": "± 6048.933944660646"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_insert.lua)",
            "value": 5427500.1625,
            "unit": "ns",
            "range": "± 17402.874977806565"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_next.lua)",
            "value": 7394831.547916667,
            "unit": "ns",
            "range": "± 21634.37253483918"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_next_remove.lua)",
            "value": 9205224.060416667,
            "unit": "ns",
            "range": "± 35230.1554430193"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_pairs.lua)",
            "value": 6683083.171354166,
            "unit": "ns",
            "range": "± 20836.09241640576"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_pairs_remove.lua)",
            "value": 7109544.8109375,
            "unit": "ns",
            "range": "± 27869.76774398169"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove.lua)",
            "value": 8849882.645089285,
            "unit": "ns",
            "range": "± 30134.08267719506"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove_then_add.lua)",
            "value": 12445681.296875,
            "unit": "ns",
            "range": "± 17087.205102698324"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 12147433.744791666,
            "unit": "ns",
            "range": "± 18447.003729670912"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: ack.lua)",
            "value": 1180825604.0714285,
            "unit": "ns",
            "range": "± 8911342.34096766"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4062691.15234375,
            "unit": "ns",
            "range": "± 21365.727752044422"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 8490.724329630533,
            "unit": "ns",
            "range": "± 1105.1386103531108"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: heapsort.lua)",
            "value": 690618714,
            "unit": "ns",
            "range": "± 7245062.258831203"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: mandel.lua)",
            "value": 12636532973,
            "unit": "ns",
            "range": "± 41889424.403971195"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: queen.lua)",
            "value": 40239955.7025641,
            "unit": "ns",
            "range": "± 303054.73873041256"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: sieve.lua)",
            "value": 618518241.4666667,
            "unit": "ns",
            "range": "± 5868177.3626942765"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: startup.lua)",
            "value": 1919142.8580280172,
            "unit": "ns",
            "range": "± 106122.66294037263"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_function.lua)",
            "value": 4331970.809765625,
            "unit": "ns",
            "range": "± 701962.3193923306"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_indexed.lua)",
            "value": 573476622.8666667,
            "unit": "ns",
            "range": "± 9572247.707280172"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 1148022305.5714285,
            "unit": "ns",
            "range": "± 8357003.543560103"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 2622250388.928571,
            "unit": "ns",
            "range": "± 9723921.344175188"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_ipairs.lua)",
            "value": 8317638.202083333,
            "unit": "ns",
            "range": "± 604945.4913566452"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 9864589.866666667,
            "unit": "ns",
            "range": "± 757809.6341815554"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_next.lua)",
            "value": 16464515.8140625,
            "unit": "ns",
            "range": "± 887244.5670502866"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_next_remove.lua)",
            "value": 10487857.1390625,
            "unit": "ns",
            "range": "± 814092.3721701734"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove.lua)",
            "value": 6879596.210677084,
            "unit": "ns",
            "range": "± 848341.1464482412"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove_then_add.lua)",
            "value": 8764934.977083333,
            "unit": "ns",
            "range": "± 523405.95764290565"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 540971973.2666667,
            "unit": "ns",
            "range": "± 1389094.4212931092"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_insert.lua)",
            "value": 10241670.55625,
            "unit": "ns",
            "range": "± 809437.0281333495"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_next.lua)",
            "value": 16098803.115625,
            "unit": "ns",
            "range": "± 831344.6775550048"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_next_remove.lua)",
            "value": 20746864.908333335,
            "unit": "ns",
            "range": "± 1386517.9780695806"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_pairs.lua)",
            "value": 15051766.8640625,
            "unit": "ns",
            "range": "± 1135066.655710874"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_pairs_remove.lua)",
            "value": 17067685.188541666,
            "unit": "ns",
            "range": "± 1305662.1974088012"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove.lua)",
            "value": 18638543.451041665,
            "unit": "ns",
            "range": "± 926843.1526958479"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove_then_add.lua)",
            "value": 27926829.263541665,
            "unit": "ns",
            "range": "± 976388.8330912637"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 548491056.5714285,
            "unit": "ns",
            "range": "± 4270529.414259705"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: ack.lua)",
            "value": 181645450.28205135,
            "unit": "ns",
            "range": "± 185676.8911599683"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1595341.119140625,
            "unit": "ns",
            "range": "± 14555.377398765193"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 3923.9863687242782,
            "unit": "ns",
            "range": "± 306.80361417844654"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: heapsort.lua)",
            "value": 75604589.41904761,
            "unit": "ns",
            "range": "± 69487.57630123475"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: mandel.lua)",
            "value": 5944909501.4,
            "unit": "ns",
            "range": "± 12249859.564110415"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: queen.lua)",
            "value": 8509309.238839285,
            "unit": "ns",
            "range": "± 31349.205485351882"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: sieve.lua)",
            "value": 105364064.89333333,
            "unit": "ns",
            "range": "± 472279.9232079147"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: startup.lua)",
            "value": 223735.2817220052,
            "unit": "ns",
            "range": "± 11937.397116796225"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_function.lua)",
            "value": 405204.8966796875,
            "unit": "ns",
            "range": "± 6117.454454172581"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_indexed.lua)",
            "value": 1973186.8348958334,
            "unit": "ns",
            "range": "± 11162.454409952119"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 4551205.810997596,
            "unit": "ns",
            "range": "± 11943.67257581991"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 1328377958.7142856,
            "unit": "ns",
            "range": "± 3648141.879658116"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_ipairs.lua)",
            "value": 1275105.234765625,
            "unit": "ns",
            "range": "± 8696.239090696465"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1439813.1171875,
            "unit": "ns",
            "range": "± 3795.178375223874"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_next.lua)",
            "value": 7300637.833333333,
            "unit": "ns",
            "range": "± 35020.666431812504"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_next_remove.lua)",
            "value": 1904476.24296875,
            "unit": "ns",
            "range": "± 33913.502590905424"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove.lua)",
            "value": 629141.2561848959,
            "unit": "ns",
            "range": "± 8301.711512862401"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove_then_add.lua)",
            "value": 950714.7875976562,
            "unit": "ns",
            "range": "± 9729.043560474898"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 838546.8615885417,
            "unit": "ns",
            "range": "± 10380.380458034599"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_insert.lua)",
            "value": 5532962.698918269,
            "unit": "ns",
            "range": "± 16705.5271313726"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_next.lua)",
            "value": 7422153.756696428,
            "unit": "ns",
            "range": "± 30154.41535195132"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_next_remove.lua)",
            "value": 9373159.259014424,
            "unit": "ns",
            "range": "± 31275.270769648378"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_pairs.lua)",
            "value": 6582123.304086538,
            "unit": "ns",
            "range": "± 14399.02375809811"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_pairs_remove.lua)",
            "value": 7128250.540104167,
            "unit": "ns",
            "range": "± 15664.58420582902"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove.lua)",
            "value": 8796293.216517856,
            "unit": "ns",
            "range": "± 27420.43265087049"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove_then_add.lua)",
            "value": 12525702.619419644,
            "unit": "ns",
            "range": "± 45782.65807667602"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 12307841.475,
            "unit": "ns",
            "range": "± 28619.697634028533"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: ack.lua)",
            "value": 194202077.73809525,
            "unit": "ns",
            "range": "± 1471076.0946981988"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: empty_test.lua)",
            "value": 102974.63938802083,
            "unit": "ns",
            "range": "± 992.3167979863754"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: heapsort.lua)",
            "value": 201932428.86666667,
            "unit": "ns",
            "range": "± 2975540.8713764693"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: queen.lua)",
            "value": 11995866.15513393,
            "unit": "ns",
            "range": "± 30331.693882472016"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: sieve.lua)",
            "value": 218658626.15555555,
            "unit": "ns",
            "range": "± 3618507.276368005"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: startup.lua)",
            "value": 104758.35678710937,
            "unit": "ns",
            "range": "± 692.1463844081368"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_function.lua)",
            "value": 1046350.8296595982,
            "unit": "ns",
            "range": "± 15418.812146812386"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_indexed.lua)",
            "value": 1615856.5764322917,
            "unit": "ns",
            "range": "± 17744.613035479048"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 2463750.9973958335,
            "unit": "ns",
            "range": "± 14212.203751806992"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 12065285.245833334,
            "unit": "ns",
            "range": "± 25145.160987079475"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_ipairs.lua)",
            "value": 2736525.6458333335,
            "unit": "ns",
            "range": "± 39701.12828546548"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 1727516.359375,
            "unit": "ns",
            "range": "± 12586.091127404237"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_next.lua)",
            "value": 5042378.966346154,
            "unit": "ns",
            "range": "± 59562.922538709674"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_next_remove.lua)",
            "value": 2886240.947395833,
            "unit": "ns",
            "range": "± 18229.00490207512"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove.lua)",
            "value": 1346252.215122768,
            "unit": "ns",
            "range": "± 19568.515899404167"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove_then_add.lua)",
            "value": 1921054.4002604166,
            "unit": "ns",
            "range": "± 19995.841588511503"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 42937255.47435898,
            "unit": "ns",
            "range": "± 70997.21904061917"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_insert.lua)",
            "value": 3196488.2473958335,
            "unit": "ns",
            "range": "± 20137.020650655904"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_next.lua)",
            "value": 4977191.030133928,
            "unit": "ns",
            "range": "± 46305.34209535048"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_next_remove.lua)",
            "value": 4105408.453125,
            "unit": "ns",
            "range": "± 32073.78164817908"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_pairs.lua)",
            "value": 4602754.676339285,
            "unit": "ns",
            "range": "± 27871.618982169897"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove.lua)",
            "value": 5069821.905208333,
            "unit": "ns",
            "range": "± 45036.115867330744"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove_then_add.lua)",
            "value": 6813460.658333333,
            "unit": "ns",
            "range": "± 33809.9067547474"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NeoImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 6753666.19921875,
            "unit": "ns",
            "range": "± 27780.61144620273"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: ack.lua)",
            "value": 1126046120.3846154,
            "unit": "ns",
            "range": "± 10360523.579777773"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 3620717.5909598214,
            "unit": "ns",
            "range": "± 30956.2335567521"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 9935.50806020101,
            "unit": "ns",
            "range": "± 583.8170609129525"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: heapsort.lua)",
            "value": 826294782.6,
            "unit": "ns",
            "range": "± 6801982.368694551"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: mandel.lua)",
            "value": 11343522666.933332,
            "unit": "ns",
            "range": "± 34016291.54804696"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: queen.lua)",
            "value": 39253068.384615384,
            "unit": "ns",
            "range": "± 214900.0959475115"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: sieve.lua)",
            "value": 497747316,
            "unit": "ns",
            "range": "± 2297640.7637435766"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: startup.lua)",
            "value": 1775909.9505208333,
            "unit": "ns",
            "range": "± 139379.48359511406"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_function.lua)",
            "value": 2929825.475390625,
            "unit": "ns",
            "range": "± 271944.83648127643"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_indexed.lua)",
            "value": 4959529.226041666,
            "unit": "ns",
            "range": "± 85441.57730262971"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_remove_end.lua)",
            "value": 6327897.754557292,
            "unit": "ns",
            "range": "± 33075.45047586589"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_insert_remove_start.lua)",
            "value": 2200239541.642857,
            "unit": "ns",
            "range": "± 4499175.325130127"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_ipairs.lua)",
            "value": 6435404.624441965,
            "unit": "ns",
            "range": "± 93245.56604452021"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_ipairs_remove.lua)",
            "value": 4845883.042708334,
            "unit": "ns",
            "range": "± 27928.64941246724"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_next.lua)",
            "value": 13477934.63125,
            "unit": "ns",
            "range": "± 823836.1961293039"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_next_remove.lua)",
            "value": 5669911.274479167,
            "unit": "ns",
            "range": "± 28565.694092793226"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove.lua)",
            "value": 2924045.210677083,
            "unit": "ns",
            "range": "± 31612.309908710144"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove_then_add.lua)",
            "value": 6387715.295703125,
            "unit": "ns",
            "range": "± 144265.80184947292"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_array_remove_then_add_immediate.lua)",
            "value": 47161791.7077922,
            "unit": "ns",
            "range": "± 73325.62240649288"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_insert.lua)",
            "value": 8624042.284375,
            "unit": "ns",
            "range": "± 931656.7091083546"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_next.lua)",
            "value": 12851489.4796875,
            "unit": "ns",
            "range": "± 877224.8305502185"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_pairs.lua)",
            "value": 11723915.3546875,
            "unit": "ns",
            "range": "± 822241.10545566"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_pairs_remove.lua)",
            "value": 12306614.206597222,
            "unit": "ns",
            "range": "± 860589.8724067776"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove.lua)",
            "value": 14185421.9328125,
            "unit": "ns",
            "range": "± 840502.353135381"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove_then_add.lua)",
            "value": 22019526.698958334,
            "unit": "ns",
            "range": "± 1991419.6087250947"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: table_map_remove_then_add_immediate.lua)",
            "value": 22841368.244791668,
            "unit": "ns",
            "range": "± 2065343.0108886368"
          }
        ]
      }
    ]
  }
}