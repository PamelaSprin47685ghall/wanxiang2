module Samples

open Wanxiang.UI

let up = { stage = AttachmentStage.Uploading; kind = AttachmentKind.OtherFile }
let ok = { stage = AttachmentStage.Ready; kind = AttachmentKind.OtherFile }
let bad = { stage = AttachmentStage.Failed; kind = AttachmentKind.OtherFile }
let restored = { stage = AttachmentStage.Restored; kind = AttachmentKind.OtherFile }
let img = { stage = AttachmentStage.Ready; kind = AttachmentKind.Image }
let lists = [ []; [ ok ]; [ up ]; [ bad ]; [ restored ]; [ img ]; [ ok; up ]; [ ok; bad ]; [ img; ok ] ]
let inputs =
    [ for host in [ HostAvailable; HostDisabled ""; HostDisabled "主机忙" ] do
        for gen in [ Idle; Generating CanStop; Generating WaitingHost ] do
          for text in [ EmptyText; HasText ] do
            for atts in lists do
              for vision in [ VisionAllowed; VisionForbidden "当前模型不支持图片" ] do
                for sendKey in [ EnterSends; CtrlEnterSends ] ->
                    { host = host
                      generation = gen
                      text = text
                      attachments = atts
                      vision = vision
                      sendKey = sendKey } ]
let texts = [ EmptyText; HasText ]
let chipStages = [ AttachmentStage.Uploading; AttachmentStage.Ready; AttachmentStage.Failed; AttachmentStage.Restored ]
