namespace Wanxiang.UI

module PairingHint =
    let pairingFirst t h e = ConnectDialogMode.pairingFirst t h e
    let isLoopbackUrl u = Dialogs.isLoopbackUrl u
    let codeHint u = Dialogs.pairingCodeHint u
