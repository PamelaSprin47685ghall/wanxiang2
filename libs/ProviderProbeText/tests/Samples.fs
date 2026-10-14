module Samples

open Wanxiang.UI

let verdicts = [ VisionVerdict.Yes; VisionVerdict.No; VisionVerdict.Unknown ]
let whys = [ VisionWhy.User; VisionWhy.Provider; VisionWhy.Name ]
let models = [ for i in 1..14 -> sprintf "m%d" i ]
let judge (m: string) = verdicts.[m.Length % 3], whys.[(int m.[m.Length - 1]) % 3]
