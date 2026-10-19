/// 金标准渲染：同一份源码分别对旧代码摘录与新库编译。
module UploadFactsRender
open System
open Wanxiang.UI

let all () =
    let sizes = [ 0; 1; 3; 256 * 1024 - 1; 256 * 1024; 256 * 1024 + 1; 600 * 1024 ]
    let bytesOf n = Array.init n (fun i -> byte ((i * 31 + 7) % 251))
    [ for n in sizes do
          let b = bytesOf n
          let cs = UploadFacts.chunks b
          yield sprintf "size %d sha %s chunks %d lens %A first %s" n (UploadFacts.sha256Hex b) cs.Length
                    (cs |> List.map (fun (i, s) -> i, s.Length))
                    (cs |> List.tryHead |> Option.map (fun (_, s) -> s.Substring(0, min 16 s.Length)) |> Option.defaultValue "-")
      for need in [ false; true ] do
          for size in [ 0L; 64L * 1024L * 1024L; 64L * 1024L * 1024L + 1L ] ->
              sprintf "shrinkFirst %b %d => %b" need size (UploadFacts.shrinkFirst need size)
      for auth in [ false; true ] do
          for inst in [ "a"; "b" ] do
              for conn in [ 1; 2 ] ->
                  sprintf "owned %b %s %d => %b" auth inst conn (UploadFacts.stillOwned auth "a" 1 inst conn)
      yield UploadFacts.pasteFileName (DateTime(2026, 10, 14, 5, 7, 9))
      for f in [ false; true ] do
          for b in [ false; true ] -> sprintf "paste %b %b => %A" f b (UploadFacts.pasteSource f b)
      yield! [ UploadFacts.disconnectedText; UploadFacts.shrinkingText "图.png"; UploadFacts.shrunkText "图.png" "已自动缩小"
               UploadFacts.readFailedText "r"; UploadFacts.dropReadFailedText "r"; UploadFacts.clipboardFilesFailedText "r"
               UploadFacts.clipboardImageFailedText "r"; UploadFacts.savedText "a.pdf"; UploadFacts.saveFailedText "r"
               UploadFacts.uploadFailedText "a.pdf"; UploadFacts.downloadCappedText; string UploadFacts.chunkSize; string UploadFacts.maxShrinkInput ] ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
