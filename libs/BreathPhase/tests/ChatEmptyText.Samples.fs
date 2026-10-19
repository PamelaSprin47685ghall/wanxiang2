module ChatEmptyTextSamples
open Wanxiang.UI

let states = [ NotConnected; ConnectionFailed "连接被拒绝"; ConnectionFailed ""; NoProvider; NoConversation; EmptyConversation ]
