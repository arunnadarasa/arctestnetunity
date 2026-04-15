using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogoutApiRequest
{
    public string key { get; set; }
    public string data { get; set; }
    public string signature { get; set; }
    public long timeout { get; set; }
    /// <summary>Origin allowed to read this session (e.g. http://localhost:12345). Required for v2 session store.</summary>
    public string allowedOrigin { get; set; } = "*";
}
