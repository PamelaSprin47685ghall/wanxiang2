namespace Wanxiang.UI

module OnboardingRules =
    [<Literal>]
    let FirstPrompt = Onboarding.FirstPrompt
    let parseModelIds b = Onboarding.parseModelIds b
    let discoverOllamaAt u = Onboarding.discoverOllamaAt u
    let canDiscoverLocal u = Onboarding.canDiscoverLocal u
    let pickDefaultModel m p = Onboarding.pickDefaultModel m p
