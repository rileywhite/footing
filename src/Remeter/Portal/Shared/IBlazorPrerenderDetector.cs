using System;

namespace Remeter.Portal.Shared
{
    public interface IBlazorPrerenderDetector
    {
        bool IsPrerendering { get; }
    }
}
