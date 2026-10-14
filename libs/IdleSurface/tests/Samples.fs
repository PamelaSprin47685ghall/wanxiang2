module Samples

let cases =
    [ for a in [ false; true ] do
        for e in [ None; Some "主机拒绝了连接。" ] do
          for r in [ false; true ] do
            for h in [ false; true ] do
              for c in [ false; true ] do
                for o in [ false; true ] -> a, e, r, h, c, o ]
