namespace BullEvents.Api.Services;

/// <summary>
/// Whether the sign-in second factor is switched off.
///
/// This exists so that turning the code step off for local work cannot follow
/// the build to a real deployment. Two conditions have to hold, not one: the
/// setting has to be present <em>and</em> the process has to be running in
/// Development. Copying appsettings.Development.json onto a server, or setting
/// the flag in an environment variable there, does nothing — the environment
/// check refuses it.
///
/// That is deliberate. A configuration flag alone is one careless copy away
/// from a production CRM that accepts a password and asks for nothing else, and
/// nobody would notice until it mattered. Hosting the API anywhere that is not
/// Development turns the second factor back on by itself, with no code change
/// to remember.
/// </summary>
public class SignInPolicy
{
    public SignInPolicy(
        IConfiguration config,
        IHostEnvironment environment,
        ILogger<SignInPolicy> logger)
    {
        var asked = config.GetValue("Auth:SkipSecondFactor", false);

        SkipSecondFactor = asked && environment.IsDevelopment();

        if (SkipSecondFactor)
        {
            // Loud on purpose, on every boot. Somebody reading the log has to be
            // able to see that this instance is not asking for a code.
            logger.LogWarning(
                "SIGN-IN SECOND FACTOR IS OFF. A correct password signs in directly. "
                + "This is allowed only because the environment is Development.");
        }
        else if (asked)
        {
            logger.LogWarning(
                "Auth:SkipSecondFactor is set but the environment is {Environment}, "
                + "so it has been ignored and the sign-in code is still required.",
                environment.EnvironmentName);
        }
    }

    /// <summary>True when a correct password alone completes sign-in.</summary>
    public bool SkipSecondFactor { get; }
}
