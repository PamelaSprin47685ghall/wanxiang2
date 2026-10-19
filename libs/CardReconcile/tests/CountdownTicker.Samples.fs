module CountdownTickerSamples
/// 两段刷新序列：从 3 秒走到 0 之后再拍几次；以及一开就走完（挂树后又刷新）
let runs = [ [ 3; 2; 1; 0; 0; -1 ]; [ 0; 0 ]; [ 65; 64 ]; [ 1; -2; 5; 0 ] ]
