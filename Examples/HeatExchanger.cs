//
// PicoGK sample: "Gyroid Heat Exchanger" (TPMS 熱交換器)
// ------------------------------------------------------------------
// A two-fluid heat exchanger built on a gyroid Triply Periodic Minimal
// Surface (TPMS). The gyroid surface splits space into two completely
// separate, interpenetrating channel networks. By printing the surface
// as a thin wall inside a sealed casing, hot fluid flows on one side
// and cold fluid on the other — with an enormous shared heat-transfer
// area and no leakage path between them.
//
// This is the quintessential PicoGK / Computational Engineering part:
// impossible to draw by hand, trivial to express as an implicit.
//
// The PRINTED SOLID = gyroid separating wall  +  outer casing shell.
// The two FLUID VOIDS are the complementary g>0 and g<0 networks.
//
// How to run (host app + native runtime, macOS-arm64 / Win-x64):
//     PicoGK.Examples.HeatExchanger.Run();
//

using System.Numerics;

namespace PicoGK.Examples
{
    /// <summary>
    /// Gyroid-based implicit with three "modes": the separating WALL, and
    /// the two fluid channels (ChannelA = g>+iso, ChannelB = g<-iso).
    /// </summary>
    public class GyroidRegion : IImplicit
    {
        public enum EMode { Wall, ChannelA, ChannelB }

        public GyroidRegion(EMode eMode, float fUnitCellMM, float fWallThicknessMM)
        {
            m_eMode = eMode;
            m_fFreq = 2.0f * MathF.PI / fUnitCellMM;
            m_fIso  = fWallThicknessMM * 0.5f * m_fFreq;   // dimensionless half-wall
        }

        public float fSignedDistance(in Vector3 vec)
        {
            float x = vec.X * m_fFreq, y = vec.Y * m_fFreq, z = vec.Z * m_fFreq;

            float g =   MathF.Sin(x) * MathF.Cos(y)
                      + MathF.Sin(y) * MathF.Cos(z)
                      + MathF.Sin(z) * MathF.Cos(x);

            float d = m_eMode switch
            {
                EMode.Wall     => MathF.Abs(g) - m_fIso,   // inside the thin wall
                EMode.ChannelA =>  m_fIso - g,             // inside the g>+iso void
                EMode.ChannelB =>  g + m_fIso,             // inside the g<-iso void
                _              => 0.0f
            };

            return d / m_fFreq;   // bring back to (approx.) mm
        }

        readonly EMode m_eMode;
        readonly float m_fFreq, m_fIso;
    }

    /// <summary>Axis-aligned box as a bounded signed-distance implicit.</summary>
    public class Box : IBoundedImplicit
    {
        public Box(Vector3 vecCenter, Vector3 vecSize)
        {
            m_vecCenter = vecCenter;
            m_vecHalf   = vecSize * 0.5f;
            oBounds     = new BBox3(vecCenter - m_vecHalf, vecCenter + m_vecHalf);
        }

        public float fSignedDistance(in Vector3 vec)
        {
            Vector3 q = Vector3.Abs(vec - m_vecCenter) - m_vecHalf;
            float fOut = Vector3.Max(q, Vector3.Zero).Length();
            float fIn  = MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0.0f);
            return fOut + fIn;
        }

        public BBox3 oBounds { get; }

        readonly Vector3 m_vecCenter, m_vecHalf;
    }

    public static class HeatExchanger
    {
        public static void Run() => Library.Go(0.8f, Task);

        // --- parameters (mm) --------------------------------------------
        const float fCore   = 80;    // cubic heat-exchange core (mm)
        const float fCell   = 16;    // gyroid unit cell
        const float fWall   = 1.2f;  // separating wall thickness
        const float fCase   = 3.0f;  // outer casing thickness

        static void Task()
        {
            Library lib = Library.oLibrary();

            Vector3 vecMid  = new(0, 0, 0);
            Vector3 vecCore = new(fCore);                       // core cube size
            Box     boxCore = new(vecMid, vecCore);

            // 1) Core volume + the gyroid separating wall inside it -------
            Voxels voxCore = new(lib, boxCore);
            Voxels voxWall = new(lib, new GyroidRegion(GyroidRegion.EMode.Wall, fCell, fWall), boxCore.oBounds);
            voxWall.BoolIntersect(voxCore);

            // 2) Outer casing: shell around the core (seals the sides) ----
            Box    boxOuter = new(vecMid, vecCore + new Vector3(2 * fCase));
            Voxels voxCase  = new Voxels(lib, boxOuter);
            voxCase.BoolSubtract(voxCore);                      // hollow it around the core

            // 3) The solid metal part = wall + casing --------------------
            Voxels voxSolid = voxWall.voxBoolAdd(voxCase);

            // 4) The two fluid networks (for visualization) --------------
            Voxels voxFluidA = FluidVoid(lib, GyroidRegion.EMode.ChannelA, boxCore, voxSolid);
            Voxels voxFluidB = FluidVoid(lib, GyroidRegion.EMode.ChannelB, boxCore, voxSolid);

            // 5) Show + export -------------------------------------------
            Viewer oView = Library.oViewer();
            oView.SetGroupMaterial(0, "9AA0A8FF", 0.9f, 0.25f);  // metal solid
            oView.SetGroupMaterial(1, "D6402EFF", 0.0f, 0.5f);   // hot fluid (red)
            oView.SetGroupMaterial(2, "2E7BD6FF", 0.0f, 0.5f);   // cold fluid (blue)

            oView.Add(voxSolid,  0);
            oView.Add(voxFluidA, 1);
            oView.Add(voxFluidB, 2);

            voxSolid.mshAsMesh().SaveToStlFile("GyroidHeatExchanger.stl");
            Library.Log("Heat exchanger: core {0}mm, cell {1}mm, wrote GyroidHeatExchanger.stl", fCore, fCell);
        }

        static Voxels FluidVoid(Library lib, GyroidRegion.EMode eMode, Box boxCore, Voxels voxSolid)
        {
            Voxels vox = new(lib, new GyroidRegion(eMode, fCell, fWall), boxCore.oBounds);
            vox.BoolIntersect(new Voxels(lib, boxCore));
            vox.BoolSubtract(voxSolid);     // keep only the open fluid passage
            return vox;
        }
    }
}
