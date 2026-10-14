module Samples

open Wanxiang.Client

let issues =
    [ ConnectionIssue.Rejected "refused"; ConnectionIssue.TimedOut; ConnectionIssue.TokenInvalid; ConnectionIssue.PairingFrozen 5
      ConnectionIssue.PairingFrozen 0; ConnectionIssue.PairingRejected "invalid pairing code"; ConnectionIssue.PairingRejected "pairing code expired"
      ConnectionIssue.PairingRejected "pairing code already used"; ConnectionIssue.PairingRejected "no pairing session active"
      ConnectionIssue.PairingRejected "x"; ConnectionIssue.Disconnected ""; ConnectionIssue.Disconnected "  "; ConnectionIssue.Disconnected "socket closed" ]
