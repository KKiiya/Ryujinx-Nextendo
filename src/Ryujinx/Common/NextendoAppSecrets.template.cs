namespace Ryujinx.Ava.Common
{
    /// <summary>
    /// [Nextendo] Application credential template. The official release bakes the registered
    /// application token here into NextendoAppSecrets.cs (which is gitignored). In custom/dev
    /// builds, this defaults to empty string and can be filled with a developer app id from
    /// https://nextendo.network/developers.
    ///
    /// Without any id, "Sign in with Nextendo" refuses immediately instead of opening the
    /// browser on a server 403: set the NEXTENDO_CLIENT_ID environment variable to a
    /// registered client_id to log in from an unbaked build (public value, not a secret).
    /// </summary>
    public static class NextendoAppSecrets
    {
        public const string AppToken = "";
    }
}
