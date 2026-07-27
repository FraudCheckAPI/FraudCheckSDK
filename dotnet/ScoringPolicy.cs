using System;
using System.Collections.Generic;
using System.Linq;

namespace FraudCheck.Client;

/// <summary>
/// Where a contribution's weight came from. Worth checking: <see cref="ApiWeight"/> means your policy had
/// no opinion about that code and fell back to FraudCheck's, which is how a newly-shipped reason code
/// behaves until you tune it.
/// </summary>
public enum WeightSource
{
    /// <summary>The weight you set in <see cref="ScoringPolicy.Weights"/>.</summary>
    Policy,

    /// <summary>FraudCheck's own weight, used because your policy doesn't mention this code.</summary>
    ApiWeight,

    /// <summary>Counted as zero — your policy ignores unlisted codes, or the API sent no weight to fall back on.</summary>
    None,
}

/// <summary>How to treat a reason code your policy doesn't list a weight for.</summary>
public enum UnknownCodeBehavior
{
    /// <summary>
    /// Use the weight FraudCheck applied (from <c>reason_details</c>). The default, and the safe one:
    /// new reason codes ship over time, and a policy that silently scored them zero would quietly stop
    /// reacting to new signals the day they appeared, with nothing failing to tell you.
    /// </summary>
    UseApiWeight,

    /// <summary>Score unlisted codes as zero. Only the codes you name can ever move the number.</summary>
    Ignore,
}

/// <summary>
/// Your own weighting of FraudCheck's reason codes, so the risk number you act on reflects YOUR business
/// rather than an average of everyone's.
///
/// FraudCheck's <see cref="ScreenResult.Score"/> is deliberately identical for every account — it can't know
/// that throwaway emails are the main problem for a digital-goods seller while a B2B shop sees corporate VPNs
/// all day. This is entirely client-side: no API call, no configuration stored with us, and you can unit-test
/// a policy without a network.
///
/// It produces a number and an explanation — never a verdict. There is no "block" here for the same reason
/// there is no <c>is_fraud</c> field in the API: the decision, and the responsibility for it, is yours.
/// </summary>
public sealed class ScoringPolicy
{
    /// <summary>
    /// Names this policy, e.g. <c>"checkout-v3"</c>. It's copied onto every
    /// <see cref="PolicyAssessment"/> — log it alongside the score, or a number in your records six months
    /// from now won't be reproducible because you'll have retuned the weights since.
    /// </summary>
    public string Name { get; }

    /// <param name="name">A name you can version, e.g. <c>"checkout-v3"</c>.</param>
    public ScoringPolicy(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Give the policy a name you can version — it lands in your audit trail.", nameof(name));
        Name = name;
    }

    /// <summary>
    /// What each reason code is worth to you. Keys are <see cref="ReasonCodes"/> constants. Negative weights
    /// are allowed if some signal should pull a score down.
    /// </summary>
    public IDictionary<string, int> Weights { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>What to do with a code <see cref="Weights"/> doesn't mention. Defaults to using FraudCheck's weight.</summary>
    public UnknownCodeBehavior UnknownCodes { get; set; } = UnknownCodeBehavior.UseApiWeight;

    /// <summary>
    /// Optional score thresholds mapped to your own labels, e.g. <c>{ [40] = "review", [80] = "decline" }</c>.
    /// The highest threshold at or below the score wins. Add a <c>[0]</c> entry if you want everything to land
    /// in a named band rather than leaving <see cref="PolicyAssessment.Band"/> null.
    /// </summary>
    public IDictionary<int, string> Bands { get; } = new Dictionary<int, string>();
}

/// <summary>One reason code's contribution to a policy score.</summary>
public sealed class PolicyContribution
{
    internal PolicyContribution(string code, int weight, WeightSource source)
    {
        Code = code;
        Weight = weight;
        Source = source;
    }

    /// <summary>The stable reason code, e.g. <c>DISPOSABLE_EMAIL</c>.</summary>
    public string Code { get; }

    /// <summary>What it added to the score under this policy.</summary>
    public int Weight { get; }

    /// <summary>Whether this used your weight, FraudCheck's, or nothing.</summary>
    public WeightSource Source { get; }
}

/// <summary>
/// The result of scoring a screen against your own <see cref="ScoringPolicy"/>. This is YOUR number, not
/// FraudCheck's — <see cref="ScreenResult.Score"/> is untouched and still available for comparison.
/// </summary>
public sealed class PolicyAssessment
{
    internal PolicyAssessment(string policy, int score, string? band, IReadOnlyList<PolicyContribution> contributions)
    {
        Policy = policy;
        Score = score;
        Band = band;
        Contributions = contributions;
    }

    /// <summary>The <see cref="ScoringPolicy.Name"/> that produced this. Log it with the score.</summary>
    public string Policy { get; }

    /// <summary>
    /// The sum of every contributing weight. Deliberately NOT clamped to 0–100: an unbounded total keeps
    /// ordering meaningful when you sort a review queue worst-first. Set your bands in the same units.
    /// </summary>
    public int Score { get; }

    /// <summary>
    /// Your label for this score from <see cref="ScoringPolicy.Bands"/>, or null when the score sits below
    /// every threshold you defined (or you defined none).
    /// </summary>
    public string? Band { get; }

    /// <summary>Every code that fired and what it contributed — the "why" behind <see cref="Score"/>.</summary>
    public IReadOnlyList<PolicyContribution> Contributions { get; }

    /// <summary>
    /// True when at least one code fell back to FraudCheck's weight because your policy doesn't list it.
    /// Useful as an alert: it's how you find out a new reason code started firing on your traffic before
    /// you've decided what it's worth to you.
    /// </summary>
    public bool UsedFallbackWeight => Contributions.Any(c => c.Source == WeightSource.ApiWeight);

    /// <summary>
    /// A one-line, human-readable account of how the score was reached — e.g.
    /// <c>checkout-v3 scored 50 (review): DISPOSABLE_EMAIL +45, DATACENTER_IP +5</c>.
    /// Worth storing next to the decision: when you're contesting a chargeback or answering an auditor,
    /// this is evidence in a way a bare number is not.
    /// </summary>
    public string Explain()
    {
        var head = Band is null
            ? $"{Policy} scored {Score}"
            : $"{Policy} scored {Score} ({Band})";

        if (Contributions.Count == 0)
            return head + ": nothing flagged";

        var parts = Contributions.Select(c =>
        {
            var note = c.Source == WeightSource.ApiWeight ? " (FraudCheck's weight)"
                     : c.Source == WeightSource.None ? " (unweighted)"
                     : "";
            return $"{c.Code} {(c.Weight >= 0 ? "+" : "")}{c.Weight}{note}";
        });

        return head + ": " + string.Join(", ", parts);
    }
}

/// <summary>Applies your own <see cref="ScoringPolicy"/> to a screen result.</summary>
public static class ScoringPolicyExtensions
{
    /// <summary>
    /// Re-scores a screen using your weights instead of FraudCheck's, and returns the number plus the
    /// per-code breakdown behind it.
    ///
    /// Note there's no double-counting to worry about: the API already resolves interacting signals
    /// server-side and emits only the winning code (<c>VPN_IP</c> instead of <c>DATACENTER_IP</c>,
    /// <c>SANCTIONED_NAME_PARTIAL</c> instead of <c>SANCTIONED_NAME</c>), so your weights apply to codes
    /// that are already mutually exclusive.
    /// </summary>
    /// <exception cref="ArgumentNullException">The result or policy is null.</exception>
    public static PolicyAssessment Assess(this ScreenResult result, ScoringPolicy policy)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (policy == null) throw new ArgumentNullException(nameof(policy));

        // reason_details carries the weight the API applied; index it so an unlisted code can fall back to it.
        var apiWeights = new Dictionary<string, int>(StringComparer.Ordinal);
        if (result.ReasonDetails != null)
        {
            foreach (var d in result.ReasonDetails)
            {
                if (d?.Code != null)
                    apiWeights[d.Code] = d.Weight;
            }
        }

        var contributions = new List<PolicyContribution>();
        var total = 0;

        // Reasons[] is the authoritative list of what fired; details are supporting information.
        foreach (var code in result.Reasons ?? (IReadOnlyList<string>)Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(code))
                continue;

            int weight;
            WeightSource source;

            if (policy.Weights.TryGetValue(code, out var mine))
            {
                weight = mine;
                source = WeightSource.Policy;
            }
            else if (policy.UnknownCodes == UnknownCodeBehavior.UseApiWeight && apiWeights.TryGetValue(code, out var theirs))
            {
                weight = theirs;
                source = WeightSource.ApiWeight;
            }
            else
            {
                // Either the policy ignores unlisted codes, or we asked for the API's weight and it sent
                // none. Still recorded (at zero) rather than dropped — a silently missing code is how a
                // policy goes stale without anyone noticing.
                weight = 0;
                source = WeightSource.None;
            }

            contributions.Add(new PolicyContribution(code, weight, source));
            total += weight;
        }

        string? band = null;
        var bestThreshold = int.MinValue;
        foreach (var pair in policy.Bands)
        {
            if (pair.Key <= total && pair.Key > bestThreshold)
            {
                bestThreshold = pair.Key;
                band = pair.Value;
            }
        }

        return new PolicyAssessment(policy.Name, total, band, contributions);
    }
}
