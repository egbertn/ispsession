namespace NCV.ISPSession;

/// <summary>
/// Specifies which Session Affinity you require for Sessions
/// Note that Application State obviously does not have an 'affinity'. See <see cref="ISPSessionRuntimeOptions.ApplicationName"/>
/// </summary>
public enum AffinityMethods
{
    /// <summary>
    /// the default method
    /// Most easy when your client runs a browser with cookie support
    /// </summary>
    Cookie = 0,

    /// <summary>
    /// define your own http header which contains
    /// the value for a session key. This would be a good option
    /// for REST-api client state.
    /// Obviously the remote client must return the same header and value
    /// in order to maintain session state.
    /// </summary>
    CustomHeader,

    /// <summary>
    /// Combines all IP Addresses including the X-Forwarded-For http header into a single key
    /// NOTE: BE AWARE what you are doing if you use this method
    /// Theoretically HTTP headers can be spoofed so use this if you trust your remote connections
    /// When to use: If client connections have unique ip addresses.
    /// when not to use: When some of your clients are using VPN, behind a NAT or use a proxy server
    /// </summary>
    IPAddress,

    /// <summary>
    /// Would be ideal if you e.g. have your endpoint as a web-hook, where you none of above options
    /// would help you to establish a session. E.g. a serverside webhook, has specific unique variables
    /// that would allow to maintain a session
    /// </summary>
    FormField,

    /// <summary>
    /// Allows late initialisation of the sessionid, use when e.g. your unique token
    /// is based upon something in your Request.Body (e.g. JSON)
    /// So you can do your own deep inspection for this feature
    /// </summary>
    CustomInit
}