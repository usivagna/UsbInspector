namespace UsbInspector.Core.Services;

/// <summary>Pure helpers for interpreting USB4 fabric data (topology IDs, link bandwidth).</summary>
public static class Usb4Fabric
{
    /// <summary>Formats a 7-byte topology ID as "a:b:c:d:e:f:g".</summary>
    public static string FormatTopologyId(byte[]? topologyId)
    {
        if (topologyId is null || topologyId.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(":", topologyId.Select(b => b.ToString()));
    }

    /// <summary>
    /// The topology ID of a router's upstream parent: the same route with the deepest non-zero hop
    /// cleared. The host router's own topology ID is all-zeros and has no parent (returns null).
    /// </summary>
    public static byte[]? ParentTopologyId(byte[]? topologyId)
    {
        if (topologyId is null)
        {
            return null;
        }

        int lastNonZero = -1;
        for (int i = 0; i < topologyId.Length; i++)
        {
            if (topologyId[i] != 0)
            {
                lastNonZero = i;
            }
        }

        if (lastNonZero < 0)
        {
            return null; // all zeros => host/root, no parent
        }

        var parent = (byte[])topologyId.Clone();
        parent[lastNonZero] = 0;
        return parent;
    }

    /// <summary>Result of interpreting a USB4 upstream port's negotiated link.</summary>
    public readonly record struct LinkBandwidth(double Gbps, int Generation, int Lanes, string Label);

    /// <summary>
    /// Computes USB4 link bandwidth from the lane-adapter "Current Link Speed" and
    /// "Negotiated Link Width" register fields plus the lane-bonded flag. Gen 3 ≈ 20 Gbps/lane,
    /// Gen 2 ≈ 10 Gbps/lane; a bonded (dual-lane) Gen 3 link is 40 Gbps.
    /// </summary>
    public static LinkBandwidth? ComputeBandwidth(byte currentLinkSpeed, byte negotiatedLinkWidth, bool laneBonded)
    {
        int generation =
            (currentLinkSpeed & 0x2) != 0 ? 3 :
            (currentLinkSpeed & 0x1) != 0 ? 2 : 0;

        if (generation == 0)
        {
            return null;
        }

        int perLaneGbps = generation == 3 ? 20 : 10;
        int lanes = laneBonded || (negotiatedLinkWidth & 0x2) != 0 ? 2 : 1;
        double gbps = perLaneGbps * lanes;
        string label = $"{gbps:0} Gbps (Gen {generation}, {(lanes == 2 ? "dual" : "single")} lane)";
        return new LinkBandwidth(gbps, generation, lanes, label);
    }
}
