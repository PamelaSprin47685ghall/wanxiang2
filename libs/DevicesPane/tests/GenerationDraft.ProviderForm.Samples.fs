module GenerationDraftProviderFormSamples
open Wanxiang.UI

let baseInput =
    { isNew = true; takenIds = [ "openai" ]; existing = None; id = "local"; label = "本地"; url = "http://localhost:11434/v1"
      models = "qwen\nllama"; defaultModel = ""; timeout = ""; retries = ""; visionYes = ""; visionNo = "" }
let edits : (string * (ProviderFormInput -> ProviderFormInput)) list =
    [ "base", id
      "noId", fun i -> { i with id = " " }
      "takenNew", fun i -> { i with id = "openai" }
      "takenEdit", fun i -> { i with id = "openai"; isNew = false }
      "badId", fun i -> { i with id = "a b" }
      "noLabel", fun i -> { i with label = "" }
      "noUrl", fun i -> { i with url = "" }
      "ftp", fun i -> { i with url = "ftp://x" }
      "badUrl", fun i -> { i with url = "http://" }
      "upperScheme", fun i -> { i with url = "HTTPS://API.X.COM/v1" }
      "noModels", fun i -> { i with models = " ,\n" }
      "dupModels", fun i -> { i with models = "a, b,a\r\nc" }
      "defaultIn", fun i -> { i with defaultModel = " llama " }
      "defaultOut", fun i -> { i with defaultModel = "gpt" }
      "defaultNoModels", fun i -> { i with models = ""; defaultModel = "gpt" }
      "timeoutOk", fun i -> { i with timeout = " 30 " }
      "timeout0", fun i -> { i with timeout = "0" }
      "timeoutBad", fun i -> { i with timeout = "x" }
      "existingKeep", fun i -> { i with existing = Some(45, 7) }
      "retries10", fun i -> { i with retries = "10" }
      "retries11", fun i -> { i with retries = "11" }
      "retriesNeg", fun i -> { i with retries = "-1" }
      "visionOk", fun i -> { i with visionYes = "qwen"; visionNo = "llama" }
      "visionUnknownYes", fun i -> { i with visionYes = "gpt" }
      "visionUnknownNo", fun i -> { i with visionYes = "qwen"; visionNo = "gpt,qwen" }
      "visionBoth", fun i -> { i with visionYes = "qwen"; visionNo = "qwen" }
      "allBad", fun i -> { i with id = ""; label = ""; url = "x"; models = ""; timeout = "-3"; retries = "z"; visionNo = "m" } ]
let urls = [ ""; "  "; "http://127.0.0.1:8080"; "http://localhost/v1"; "http://[::1]:1234"; "https://api.openai.com/v1"; "LOCALHOST:11434"; "http://10.0.0.2"; "my-localhost-proxy.com" ]
