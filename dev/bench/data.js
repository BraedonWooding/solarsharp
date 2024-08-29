window.BENCHMARK_DATA = {
  "lastUpdate": 1724929970826,
  "repoUrl": "https://github.com/BraedonWooding/solarsharp",
  "entries": {
    "Benchmark": [
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
          "id": "d5b26052d9702b7f6ef0846abb5338e9a8c9331b",
          "message": "Update main.yml",
          "timestamp": "2024-08-29T00:33:19+10:00",
          "tree_id": "dd73c6e8f01376a9bc4c2aa03d2e9d7edd8577df",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/d5b26052d9702b7f6ef0846abb5338e9a8c9331b"
        },
        "date": 1724855751159,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1598800.7864118305,
            "unit": "ns",
            "range": "± 10185.108440357035"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 6364582.137867647,
            "unit": "ns",
            "range": "± 130190.38609300366"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1608893.1978824013,
            "unit": "ns",
            "range": "± 13677.649606849993"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4387640.215401785,
            "unit": "ns",
            "range": "± 28332.789281425303"
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
          "id": "e6e50025e371b41734746b0e82ccdc6004ef8e4f",
          "message": "Fix",
          "timestamp": "2024-08-29T01:43:12+10:00",
          "tree_id": "a39fe20731a9fc38146f3174d3d64b8c2a4cadba",
          "url": "https://github.com/BraedonWooding/solarsharp/commit/e6e50025e371b41734746b0e82ccdc6004ef8e4f"
        },
        "date": 1724861263980,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1587262.9654017857,
            "unit": "ns",
            "range": "± 9071.457541924286"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: KeraImplementation, Test: empty_test.lua)",
            "value": 4790.53951687283,
            "unit": "ns",
            "range": "± 96.6480141870578"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 6139904.4671875,
            "unit": "ns",
            "range": "± 136493.9902322609"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: MoonSharpImplementation, Test: empty_test.lua)",
            "value": 1429263.3980384744,
            "unit": "ns",
            "range": "± 93844.621676853"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: binarytrees.lua-2.lua)",
            "value": 1589186.5990084135,
            "unit": "ns",
            "range": "± 12844.115125312346"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: NLuaImplementation, Test: empty_test.lua)",
            "value": 4853.162534586589,
            "unit": "ns",
            "range": "± 53.78215246682554"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: binarytrees.lua-2.lua)",
            "value": 4079077.414620536,
            "unit": "ns",
            "range": "± 20520.760680324587"
          },
          {
            "name": "Benchmark.Benchmarks.Benchmark(Implementation: SolarSharpImplementation, Test: empty_test.lua)",
            "value": 12793.745280472274,
            "unit": "ns",
            "range": "± 985.857999524469"
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
      }
    ]
  }
}