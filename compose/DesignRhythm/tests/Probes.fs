module Probes
open Wanxiang.UI
let widths = [ 0.0; 320.0; 439.9; 440.0; 719.9; 720.0; 1023.9; 1024.0; 4000.0 ]
let values = [ -1.0; 0.0; 2.0; 3.0; 5.0; 6.0; 14.0; 20.0; 44.0; 112.0; 999.0; 0.3; 0.5 ]
let rhythm () : (string * obj) list =
    [ for w in widths do yield sprintf "horizontalInset %g" w, box (LayoutPolicy.horizontalInset w)
      for s in [ 12.0; 14.5; 16.0 ] do
          yield sprintf "proseLineHeight %g" s, box (ReadingRhythm.proseLineHeight s)
          yield sprintf "secondaryLineHeight %g" s, box (ReadingRhythm.secondaryLineHeight s)
          yield sprintf "technicalLineHeight %g" s, box (ReadingRhythm.technicalLineHeight s)
          for l in 0 .. 7 do yield sprintf "headingFontSize %g %d" s l, box (ReadingRhythm.headingFontSize s l)
      for l in 0 .. 7 do
          yield sprintf "headingBefore %d" l, box (ReadingRhythm.headingBefore l)
          yield sprintf "headingAfter %d" l, box (ReadingRhythm.headingAfter l) ]
