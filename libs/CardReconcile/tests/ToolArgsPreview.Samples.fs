module ToolArgsPreviewSamples
let args =
    [ ""; "  "; "{}"; "{\"a\":1}"; "{\"path\":\"/tmp/x\",\"n\":2.5,\"b\":true,\"c\":false,\"d\":null,\"e\":[1,2],\"f\":{\"g\":1}}"
      "{\"long\":\"" + String.replicate 40 "长" + "\"}"; "{\"nl\":\"a\\nb\\rc\"}"; "{\"exact\":\"" + String.replicate 32 "x" + "\"}"
      "{" + String.concat "," [ for i in 1..20 -> sprintf "\"key%d\":%d" i i ] + "}"; "[1,2,3]"; "[]"; "42"; "\"str\""
      "not json\nsecond line"; String.replicate 120 "z"; "{broken" ]
