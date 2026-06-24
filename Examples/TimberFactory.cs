//
// PicoGK sample: "Timber Factory" (木造工場)
// ------------------------------------------------------------------
// A parametric structural frame for a gable-roof timber factory /
// workshop building, generated entirely from code.
//
// Everything is driven by the parameters in TimberFactory.Run(), so
// changing the span, length, bay spacing or member sizes regenerates
// the whole building — this is the "generative engineering" idea.
//
// Members are rectangular (glulam-style) timber, modelled as oriented
// box signed-distance functions so rafters can sit at any roof pitch.
//
// How to run (needs a host app + native runtime, macOS-arm64/Win-x64):
//     PicoGK.Examples.TimberFactory.Run();
//
// Coordinate system (mm):
//     X = building length        (along the ridge)
//     Y = building span / width  (gable direction)
//     Z = height
//

using System.Numerics;

namespace PicoGK.Examples
{
    /// <summary>
    /// A rectangular timber member (beam / column / purlin) between two
    /// centre-line points A and B, with cross-section width x height.
    /// Implemented as an oriented-box signed distance function so it can
    /// be placed at any angle (e.g. sloped roof rafters).
    /// </summary>
    public class TimberMember : IBoundedImplicit
    {
        public TimberMember(Vector3 vecA, Vector3 vecB, float fWidthMM, float fHeightMM)
        {
            Vector3 vecDir  = vecB - vecA;
            float   fLen    = vecDir.Length();

            m_vecX = vecDir / fLen;                              // along the member

            // Pick a stable reference 'up' to build the cross-section frame.
            Vector3 vecRef  = MathF.Abs(m_vecX.Z) > 0.99f
                            ? new Vector3(0, 1, 0)
                            : new Vector3(0, 0, 1);

            m_vecY = Vector3.Normalize(Vector3.Cross(vecRef, m_vecX));
            m_vecZ = Vector3.Cross(m_vecX, m_vecY);

            m_vecCenter = (vecA + vecB) * 0.5f;
            m_vecHalf   = new Vector3(fLen * 0.5f, fWidthMM * 0.5f, fHeightMM * 0.5f);

            // Bounding box from the 8 oriented corners.
            Vector3 vecMin = new(float.MaxValue), vecMax = new(float.MinValue);
            for (int s = 0; s < 8; s++)
            {
                Vector3 vecC = m_vecCenter
                    + m_vecX * (((s & 1) == 0 ? -1 : 1) * m_vecHalf.X)
                    + m_vecY * (((s & 2) == 0 ? -1 : 1) * m_vecHalf.Y)
                    + m_vecZ * (((s & 4) == 0 ? -1 : 1) * m_vecHalf.Z);

                vecMin = Vector3.Min(vecMin, vecC);
                vecMax = Vector3.Max(vecMax, vecC);
            }
            oBounds = new BBox3(vecMin, vecMax);
        }

        public float fSignedDistance(in Vector3 vec)
        {
            Vector3 vecRel = vec - m_vecCenter;

            float qx = MathF.Abs(Vector3.Dot(vecRel, m_vecX)) - m_vecHalf.X;
            float qy = MathF.Abs(Vector3.Dot(vecRel, m_vecY)) - m_vecHalf.Y;
            float qz = MathF.Abs(Vector3.Dot(vecRel, m_vecZ)) - m_vecHalf.Z;

            float fOutside = new Vector3(MathF.Max(qx, 0), MathF.Max(qy, 0), MathF.Max(qz, 0)).Length();
            float fInside  = MathF.Min(MathF.Max(qx, MathF.Max(qy, qz)), 0.0f);
            return fOutside + fInside;
        }

        public BBox3 oBounds { get; }

        readonly Vector3 m_vecCenter, m_vecHalf, m_vecX, m_vecY, m_vecZ;
    }

    public static class TimberFactory
    {
        public static void Run()
        {
            // 30 mm voxels: a good compromise between detail and speed for
            // a ~20 m building (members are 100–300 mm, so 3–10 voxels thick).
            Library.Go(30.0f, Task);
        }

        // --- building parameters (mm) -----------------------------------
        const float fLength   = 20000;   // along the ridge (X)
        const float fSpan     = 15000;   // gable width      (Y)
        const float fEave     = 5000;    // eave height      (Z)
        const float fRidge    = 7500;    // ridge height     (Z)
        const int   nBays     = 5;       // -> 6 transverse frames
        const int   nPurlins  = 2;       // intermediate purlins per roof slope

        static Library s_lib  = null!;
        static Voxels  s_vox  = null!;   // accumulator for all timber

        static void Task()
        {
            s_lib = Library.oLibrary();
            s_vox = new Voxels(s_lib);

            float fBay   = fLength / nBays;
            float fHalfY = fSpan * 0.5f;

            // ===== Transverse frames (every bay line) ===================
            for (int i = 0; i <= nBays; i++)
            {
                float x = i * fBay;

                // Posts (200 x 200)
                AddTimber(new(x, 0,     0), new(x, 0,     fEave), 200, 200);
                AddTimber(new(x, fSpan, 0), new(x, fSpan, fEave), 200, 200);

                // Rafters forming the gable (200 x 300)
                AddTimber(new(x, 0,     fEave), new(x, fHalfY, fRidge), 200, 300);
                AddTimber(new(x, fSpan, fEave), new(x, fHalfY, fRidge), 200, 300);

                // Tie beam across the span + king post (truss action)
                AddTimber(new(x, 0,     fEave), new(x, fSpan,  fEave), 150, 200);
                AddTimber(new(x, fHalfY, fEave), new(x, fHalfY, fRidge), 150, 150);

                // Knee braces stiffening the post/rafter joints
                AddTimber(new(x, 1500,        fEave), new(x, 0,           fEave - 1500), 120, 150);
                AddTimber(new(x, fSpan - 1500, fEave), new(x, fSpan,      fEave - 1500), 120, 150);
            }

            // ===== Longitudinal members (full length, along X) ==========
            // Ridge beam
            AddTimber(new(0, fHalfY, fRidge), new(fLength, fHalfY, fRidge), 200, 300);

            // Eave / wall plates
            AddTimber(new(0, 0,     fEave), new(fLength, 0,     fEave), 150, 200);
            AddTimber(new(0, fSpan, fEave), new(fLength, fSpan, fEave), 150, 200);

            // Roof purlins on both slopes
            for (int p = 1; p <= nPurlins; p++)
            {
                float t = (float)p / (nPurlins + 1);          // 0..1 up the slope

                float yL = t * fHalfY;
                float yR = fSpan - t * fHalfY;
                float z  = fEave + t * (fRidge - fEave);

                AddTimber(new(0, yL, z), new(fLength, yL, z), 100, 150);
                AddTimber(new(0, yR, z), new(fLength, yR, z), 100, 150);
            }

            // Side-wall girts (mid height)
            AddTimber(new(0, 0,     fEave * 0.5f), new(fLength, 0,     fEave * 0.5f), 100, 150);
            AddTimber(new(0, fSpan, fEave * 0.5f), new(fLength, fSpan, fEave * 0.5f), 100, 150);

            // ===== Concrete-ish ground slab =============================
            Voxels voxSlab = SlabVoxels(150);

            // ===== Show + export ========================================
            Viewer oView = Library.oViewer();
            oView.SetGroupMaterial(0, "B5853FFF", 0.0f, 0.7f);   // timber
            oView.SetGroupMaterial(1, "999999FF", 0.0f, 0.9f);   // slab
            oView.Add(s_vox,   0);
            oView.Add(voxSlab, 1);

            Voxels voxAll = s_vox.voxBoolAdd(voxSlab);
            voxAll.mshAsMesh().SaveToStlFile("TimberFactory.stl");
            Library.Log("Timber factory: frames={0}, wrote TimberFactory.stl", nBays + 1);
        }

        static void AddTimber(Vector3 vecA, Vector3 vecB, float w, float h)
        {
            TimberMember oMember = new(vecA, vecB, w, h);
            s_vox.BoolAdd(new Voxels(s_lib, oMember, oMember.oBounds));
        }

        static Voxels SlabVoxels(float fThkMM)
        {
            // A flat slab slightly larger than the footprint, sitting at z=0.
            TimberMember oSlab = new(new(-500, fSpan * 0.5f, -fThkMM * 0.5f),
                                     new(fLength + 500, fSpan * 0.5f, -fThkMM * 0.5f),
                                     fSpan + 1000, fThkMM);
            return new Voxels(s_lib, oSlab, oSlab.oBounds);
        }
    }
}
