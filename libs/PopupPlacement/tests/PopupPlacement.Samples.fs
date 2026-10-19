module PopupPlacementSamples
/// (锚点 x, y, 宽, 高, 浮层宽, 高, 视口宽, 高, 右对齐)
let cases =
    [ for ax, ay in [ 10.0, 10.0; 300.0, 20.0; 380.0, 560.0; 0.0, 300.0; -50.0, 700.0 ] do
        for w, h in [ 180.0, 120.0; 500.0, 900.0; 200.0, 40.0 ] do
          for right in [ false; true ] -> ax, ay, 32.0, 28.0, w, h, 400.0, 600.0, right ]
