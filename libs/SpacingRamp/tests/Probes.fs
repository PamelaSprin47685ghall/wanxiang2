module Probes
open Wanxiang.UI
let widths = [ 0.0; 320.0; 439.9; 440.0; 719.9; 720.0; 1023.9; 1024.0; 4000.0 ]
let values = [ -1.0; 0.0; 2.0; 3.0; 5.0; 6.0; 14.0; 20.0; 44.0; 112.0; 999.0; 0.3; 0.5 ]
let spacing () : (string * obj) list =
    [ for v in values do
        yield sprintf "isRamp %g" v, box (Spacing.isRamp v)
        yield sprintf "isSlot %g" v, box (Spacing.isSlot v)
        yield sprintf "isRadius %g" v, box (Spacing.isRadius v)
        yield sprintf "isStroke %g" v, box (Spacing.isStroke v)
        yield sprintf "isTracking %g" v, box (Spacing.isTracking v)
        yield sprintf "isGlyph %g" v, box (Spacing.isGlyph v)
        yield sprintf "isHit %g" v, box (Spacing.isHit v) ]
