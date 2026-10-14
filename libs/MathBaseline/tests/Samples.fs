module Samples

let boxes = [ None; Some(10.0, 7.5, 2.25); Some(0.0, 0.0, 0.0); Some(33.3, 12.1, 0.0) ]
let draws = [ for d in [ true; false ] do for h in [ None; Some 14.0; Some 0.0 ] do for y in [ 0.0; 3.5; -2.0 ] -> d, 9.75, h, y ]
