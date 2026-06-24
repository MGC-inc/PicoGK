//
// PicoGK sample: "Gyroid Sphere"
// ------------------------------------------------------------------
// A small showcase of computational geometry with PicoGK.
//
// It builds a classic TPMS (Triply Periodic Minimal Surface) gyroid
// lattice and clips it to the inside of a hollow sphere shell. The
// result is the kind of intricate, 3D-printable structure PicoGK is
// designed for (lightweight infills, heat exchangers, bio-inspired
// parts, ...).
//
// How to run:
//   PicoGK is a *library*, so it needs a host app to launch it.
//   In a .NET app that references PicoGK, simply call:
//
//       PicoGK.Examples.GyroidShowcase.Run();
//
//   (Requires the matching native runtime for your platform:
//    macOS-arm64 or Windows-x64 — see the /native folder.)
//

using System.Numerics;

namespace PicoGK.Examples
{
    /// <summary>
    /// An infinite gyroid expressed as a signed-distance implicit.
    /// f(x,y,z) = sin(x)cos(y) + sin(y)cos(z) + sin(z)cos(x)
    /// We turn the zero-isosurface into a solid "sheet" of a given
    /// wall thickness by returning |f| - thickness.
    /// </summary>
    public class Gyroid : IImplicit
    {
        public Gyroid(float fUnitCellMM, float fWallThicknessMM)
        {
            // 2*PI maps one full sine period onto one unit cell.
            m_fFrequency    = 2.0f * MathF.PI / fUnitCellMM;

            // Rough conversion from the dimensionless gyroid value to a
            // real-world half-wall-thickness. Good enough for a demo.
            m_fIso          = fWallThicknessMM * 0.5f * m_fFrequency;
        }

        public float fSignedDistance(in Vector3 vec)
        {
            float x = vec.X * m_fFrequency;
            float y = vec.Y * m_fFrequency;
            float z = vec.Z * m_fFrequency;

            float fGyroid =   MathF.Sin(x) * MathF.Cos(y)
                            + MathF.Sin(y) * MathF.Cos(z)
                            + MathF.Sin(z) * MathF.Cos(x);

            // |g| - iso  ->  negative inside the sheet, positive outside.
            // Divide by the frequency to bring it back to (approx.) mm.
            return (MathF.Abs(fGyroid) - m_fIso) / m_fFrequency;
        }

        readonly float m_fFrequency;
        readonly float m_fIso;
    }

    /// <summary>
    /// A solid sphere as a bounded signed-distance implicit, so we can
    /// render it directly into voxels without guessing the bounds.
    /// </summary>
    public class Sphere : IBoundedImplicit
    {
        public Sphere(Vector3 vecCenter, float fRadiusMM)
        {
            m_vecCenter = vecCenter;
            m_fRadius   = fRadiusMM;

            oBounds = new BBox3(vecCenter - new Vector3(fRadiusMM),
                                vecCenter + new Vector3(fRadiusMM));
        }

        public float fSignedDistance(in Vector3 vec)
            => (vec - m_vecCenter).Length() - m_fRadius;

        public BBox3 oBounds { get; }

        readonly Vector3    m_vecCenter;
        readonly float      m_fRadius;
    }

    public static class GyroidShowcase
    {
        /// <summary>
        /// Entry point — launches the PicoGK runtime + viewer and builds
        /// the geometry. Call this from your host app's Main.
        /// </summary>
        public static void Run()
        {
            // 0.4 mm voxel size: fine enough to resolve the gyroid walls,
            // coarse enough to compute quickly.
            Library.Go(0.4f, Task);
        }

        static void Task()
        {
            Library lib     = Library.oLibrary();
            Viewer  oView   = Library.oViewer();

            float   fRadius = 30.0f;                 // 60 mm sphere
            Vector3 vecOrg  = Vector3.Zero;

            // --- 1. The outer hollow sphere shell -----------------------
            Sphere sphSolid = new(vecOrg, fRadius);
            Voxels voxShell = new(lib, sphSolid);    // solid ball

            // Hollow it out: keep a 2 mm wall.
            Voxels voxInner = voxShell.voxOffset(-2.0f);
            voxShell.BoolSubtract(voxInner);

            // --- 2. The gyroid lattice ----------------------------------
            // Render the infinite gyroid only inside the sphere's bounds.
            Gyroid gyr      = new(fUnitCellMM: 12.0f, fWallThicknessMM: 1.2f);
            Voxels voxGyroid = new(lib, gyr, sphSolid.oBounds);

            // Clip the lattice to the *inner* volume of the shell so it
            // fills the cavity and fuses with the wall.
            voxGyroid.BoolIntersect(voxInner);

            // --- 3. Combine + show --------------------------------------
            Voxels voxResult = voxShell.voxBoolAdd(voxGyroid);

            // Group 0: shell (brushed metal), Group 1: lattice (copper)
            oView.SetGroupMaterial(0, "AAAAAAFF", fMetallic: 0.6f, fRoughness: 0.3f);
            oView.SetGroupMaterial(1, "CC6644FF", fMetallic: 0.8f, fRoughness: 0.4f);

            oView.Add(voxShell,  0);
            oView.Add(voxGyroid, 1);

            // --- 4. Export an STL you can 3D-print -----------------------
            Mesh msh = voxResult.mshAsMesh();
            msh.SaveToStlFile("GyroidSphere.stl");

            Library.Log("Done — wrote GyroidSphere.stl");
        }
    }
}
