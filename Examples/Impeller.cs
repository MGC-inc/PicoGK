//
// PicoGK sample: "Centrifugal Impeller" (遠心インペラ / 羽根車)
// ------------------------------------------------------------------
// A parametric centrifugal pump/compressor impeller: a hub and back
// plate carrying N backward-curved blades. Each blade follows a swept
// path whose angle increases from hub to tip (a log-spiral-style
// sweep), so the whole blade row is generated from a single implicit
// expressed in cylindrical coordinates.
//
// This is the kind of rotating turbomachinery part used in pumps,
// turbochargers and rocket turbopumps — naturally curved, hard to draw
// by hand, easy to express as a function of (radius, angle, height).
//
// How to run (host app + native runtime, macOS-arm64 / Win-x64):
//     PicoGK.Examples.Impeller.Run();
//
// Axis: Z is the spin axis; the part sits on a back plate at z<=0.
//

using System.Numerics;

namespace PicoGK.Examples
{
    /// <summary>
    /// The complete impeller as a single bounded signed-distance implicit:
    /// back plate ∪ hub ∪ N swept blades, minus a central shaft bore.
    /// </summary>
    public class ImpellerField : IBoundedImplicit
    {
        public ImpellerField()
        {
            float fOuter = m_fPlateR + 1;
            oBounds = new BBox3(new(-fOuter, -fOuter, -m_fPlateThk - 1),
                                new( fOuter,  fOuter,  m_fHubH + 1));
        }

        public float fSignedDistance(in Vector3 vec)
        {
            float r = MathF.Sqrt(vec.X * vec.X + vec.Y * vec.Y);

            // Solid features (each is a signed distance; union = min) ----
            float dPlate = Cylinder(r, vec.Z, m_fPlateR, -m_fPlateThk, 0);
            float dHub   = Cylinder(r, vec.Z, m_fHubR,            0, m_fHubH);
            float dBlade = Blade(r, vec.Z, MathF.Atan2(vec.Y, vec.X));

            float d = MathF.Min(dPlate, MathF.Min(dHub, dBlade));

            // Subtract the central shaft bore (infinite cylinder) --------
            d = MathF.Max(d, m_fBoreR - r);
            return d;
        }

        // Signed distance of a finite cylinder r<R, z in [z0,z1]
        static float Cylinder(float r, float z, float R, float z0, float z1)
        {
            float dr = r - R;
            float dz = MathF.Max(z0 - z, z - z1);
            float fOut = MathF.Sqrt(MathF.Max(dr, 0) * MathF.Max(dr, 0)
                                  + MathF.Max(dz, 0) * MathF.Max(dz, 0));
            float fIn  = MathF.Min(MathF.Max(dr, dz), 0);
            return fOut + fIn;
        }

        // One blade row: thin sheet whose angle sweeps with radius,
        // repeated N times around the axis, bounded in r and z.
        float Blade(float r, float z, float fTheta)
        {
            float fSpan  = (r - m_fHubR) / (m_fTipR - m_fHubR);   // 0 at hub, 1 at tip

            // Target blade angle at this radius (backward-curved sweep)
            float fPhi   = m_fSweep * fSpan;

            // Angular offset to the nearest of N blades
            float fSeg   = 2 * MathF.PI / m_nBlades;
            float fRel   = fTheta - fPhi;
            float fDelta = fRel - fSeg * MathF.Round(fRel / fSeg);
            float fArc   = r * fDelta;                            // arc-length offset

            // Blade height tapers from hub to tip (shroud-less profile)
            float fHloc  = m_fHubH * (1.0f - 0.7f * fSpan);

            float dPlane = MathF.Abs(fArc) - m_fBladeThk * 0.5f;  // close to blade sheet
            float dR     = MathF.Max(m_fHubR - r, r - m_fTipR);   // radial band
            float dZ     = MathF.Max(-z, z - fHloc);              // height band

            // Intersection of the three bands = max
            return MathF.Max(dPlane, MathF.Max(dR, dZ));
        }

        public BBox3 oBounds { get; }

        // --- parameters (mm / rad) --------------------------------------
        const float m_fHubR     = 15;
        const float m_fTipR     = 60;
        const float m_fHubH     = 40;
        const float m_fPlateR   = 65;
        const float m_fPlateThk = 4;
        const float m_fBoreR    = 6;
        const float m_fBladeThk = 2.5f;
        const int   m_nBlades   = 7;
        readonly float m_fSweep = 75.0f * MathF.PI / 180.0f;       // total hub->tip sweep
    }

    public static class Impeller
    {
        public static void Run() => Library.Go(0.6f, Task);

        static void Task()
        {
            Library lib = Library.oLibrary();

            ImpellerField oField = new();
            Voxels voxImpeller   = new(lib, oField, oField.oBounds);

            // A light fillet smooths the blade roots into the hub/plate.
            Voxels voxSmooth = voxImpeller.voxFillet(1.5f);

            Viewer oView = Library.oViewer();
            oView.SetGroupMaterial(0, "C8CCD0FF", 0.9f, 0.2f);   // machined metal
            oView.Add(voxSmooth, 0);

            voxSmooth.mshAsMesh().SaveToStlFile("Impeller.stl");
            Library.Log("Impeller: 7 backward-curved blades, wrote Impeller.stl");
        }
    }
}
