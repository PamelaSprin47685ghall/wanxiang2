module OnboardingRulesSamples
let bodies =
    [ ""; "not json"; "[]"; "{}"; "{\"data\":{}}"; "{\"data\":[]}"; "{\"data\":[{\"id\":\"a\"},{\"id\":\"b\"},{\"id\":\"a\"},{\"x\":1}]}"
      "{\"object\":\"list\",\"data\":[{\"id\":\"qwen3:8b\"},{\"id\":\"llama3.2\"}]}"; "{\"data\":[{\"id\":\" \"},{\"id\":5},{\"id\":null},\"s\",{\"id\":\"ok\"}]}" ]
let urls = [ ""; "ws://127.0.0.1:8765/ws"; "http://localhost:8765"; "https://wanxiang.example.com"; "ws://[::1]:1/ws"; "relative/path" ]
let picks =
    [ [ "gpt-5"; "gpt-5-mini" ], [ "zzz"; "gpt-5"; "gpt-4o" ]
      [ "gpt-5"; "gpt-5-mini" ], [ "aaa"; "zzz" ]
      [ "gpt-5"; "gpt-5-mini" ], [ "gpt-5-mini"; "gpt-5" ]
      [ "gpt-5" ], []
      [], []
      [], [ "only" ] ]
