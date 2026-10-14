module Samples

open Wanxiang.UI

let counts =
    [ for r in 0..1 do
        for a in 0..1 do
          for s in 0..2 do
            for f in 0..1 do
              for u in 0..1 -> { running = r; awaiting = a; success = s; failed = f; unknown = u } ]
