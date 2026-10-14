module Samples

open Wanxiang.UI

let up = { ready = false; failed = false; isImage = false }
let ok = { ready = true; failed = false; isImage = false }
let bad = { ready = false; failed = true; isImage = false }
let img = { ready = true; failed = false; isImage = true }
let lists = [ []; [ ok ]; [ up ]; [ bad ]; [ img ]; [ ok; up ]; [ ok; bad ]; [ img; ok ] ]
let inputs =
    [ for enabled in [ true; false ] do
        for generating, canStop in [ false, false; true, true; true, false ] do
          for hasText in [ false; true ] do
            for atts in lists do
              for visionNo in [ false; true ] do
                for reason in [ ""; "主机忙" ] do
                  for enterSends in [ true; false ] ->
                    { enabled = enabled; generating = generating; canStop = canStop; disabledReason = reason
                      hasText = hasText; attachments = atts; visionNo = visionNo
                      visionBlockedMessage = "当前模型不支持图片"; enterSends = enterSends } ]
let texts = [ ""; "  "; "hi" ]
let chips = [ up; ok; bad ]
