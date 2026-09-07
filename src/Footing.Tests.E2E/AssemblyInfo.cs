using System.Runtime.CompilerServices;

// The RenderSweep tool under tools/ drives the same pages the suite does, through the same
// SitePage paths and the same ToolStorage seeds. Re-declaring either there would let the
// renders drift away from what is actually asserted, which is the one thing a
// human-inspection sweep must not do.
[assembly: InternalsVisibleTo("RenderSweep")]
