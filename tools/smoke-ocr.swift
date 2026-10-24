// 截图文字与包围盒识别（macOS 侧实现）。
//
// 输出格式与 `tesseract <img> stdout -l chi_sim+eng --psm 6 tsv` 逐列对齐，
// 这样 Node 侧只有一个解析器、一条判据，不必为两套 OCR 各写一遍：
//   level page_num block_num par_num line_num word_num left top width height conf text
// left/top/width/height 以**像素**计，conf 为 0–100 的整数。
//
// 用法：swift tools/smoke-ocr.swift <图片路径>
import Foundation
import Vision

guard CommandLine.arguments.count > 1 else {
    FileHandle.standardError.write(Data("usage: smoke-ocr.swift <image>\n".utf8))
    exit(2)
}

let url = URL(fileURLWithPath: CommandLine.arguments[1])
guard
    let source = CGImageSourceCreateWithURL(url as CFURL, nil),
    let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
else {
    FileHandle.standardError.write(Data("cannot read image: \(url.path)\n".utf8))
    exit(2)
}

let width = CGFloat(image.width)
let height = CGFloat(image.height)

let request = VNRecognizeTextRequest()
request.recognitionLevel = .accurate
request.recognitionLanguages = ["zh-Hans", "en-US"]
request.usesLanguageCorrection = false

try VNImageRequestHandler(cgImage: image, options: [:]).perform([request])

print("level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext")

for (index, observation) in (request.results ?? []).enumerated() {
    guard let candidate = observation.topCandidates(1).first else { continue }
    let box = observation.boundingBox
    // Vision 的坐标原点在左下角且归一化；TSV 要左上角原点、像素单位。
    let left = Int((box.minX * width).rounded())
    let top = Int(((1.0 - box.maxY) * height).rounded())
    let w = Int((box.width * width).rounded())
    let h = Int((box.height * height).rounded())
    let conf = Int((candidate.confidence * 100).rounded())
    let text = candidate.string.replacingOccurrences(of: "\t", with: " ")
    print("5\t1\t1\t1\t\(index + 1)\t1\t\(left)\t\(top)\t\(w)\t\(h)\t\(conf)\t\(text)")
}
