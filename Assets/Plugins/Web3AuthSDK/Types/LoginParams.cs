using System;

public class LoginParams
{
    public Provider? loginProvider { get; set; }  // null = show all providers
    /// <summary>Dashboard Auth Connection ID (e.g. arc-unity) when using CUSTOM_VERIFIER — required by hosted v10.</summary>
    public string authConnectionId { get; set; }
    /// <summary>OAuth / project client id depending on flow — see Web3Auth docs.</summary>
    public string clientId { get; set; }
    public string dappShare { get; set; }
    public ExtraLoginOptions extraLoginOptions { get; set; }
    public Uri redirectUrl { get; set; }
    public string appState { get; set; }
    public MFALevel mfaLevel { get; set; }

    public Curve curve { get; set; } = Curve.SECP256K1;
    public string dappUrl { get; set; }
}