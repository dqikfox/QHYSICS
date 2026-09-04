using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Nile floodplain silt, schematic harbor basin, and valley workers' settlement
    /// east of the Giza east escarpment. Reconstructed original (Petrie/Lehner):
    /// cultivation and harbors sit at the escarpment foot. The Nile channel itself
    /// is ~8 km further east and is not modeled at 1:1 this slice.
    /// Local +X east, +Z north, 1 unit = 1 m.
    /// </summary>
    public static class GizaNile
    {
        public const string FloodplainName = "GizaNileFloodplain";
        public const string HarborName = "GizaNileHarbor";
        public const string SettlementName = "GizaValleySettlement";
        public const string HonestyName = "GizaNile_Honesty";

        public const string BasinName = "GizaNileHarbor_Basin";
        public const string QuayName = "GizaNileHarbor_Quay";
        public const string CanalName = "GizaNileHarbor_Canal";
        public const string WaterName = "GizaNileHarbor_Water";
        public const string CanalWaterName = "GizaNileHarbor_CanalWater";

        public const float GapFromCliffM = 6f;
        public const float FloodplainWidthM = 720f;
        public const float NorthPadM = 80f;
        public const float SitAboveDesertM = 0.10f;
        public const float HarborEastOfCliffM = 80f;
        public const float HarborEW = 90f;
        public const float HarborNS = 45f;
        /// <summary>Thin opaque water sheet in the recessed basin (was a 1.2 m solid box).</summary>
        public const float WaterThickM = 0.28f;
        public const float BasinDepthM = 1.55f;
        public const float BasinFloorThickM = 0.45f;
        public const float QuayWidthM = 4.5f;
        public const float QuayThickM = 0.40f;
        public const float CanalLengthM = 58f;
        public const float CanalWidthM = 10f;
        public const float CanalBankM = 2.4f;
        public const float RimWidthM = 3f;
        public const float RimThickM = 0.35f;
        const float PlazaKeepoutM = 32f;

        public const string Honesty =
            "Reconstructed original (undamaged). Lehner: cultivation and harbors at the Giza escarpment foot. Not photogrammetry. Not modern Nazlet el-Samman.\n" +
            "Nile channel is ~8 km further east — not modeled at 1:1 this slice. Floodplain silt + recessed walkable harbor basin (stone quay, thin water, west feeder canal toward Khafre valley / Sphinx) + valley settlement.";

        public static void Ensure(Transform root, GizaComplex.Pose pose, Vector3 plazaPos,
            float xMin, float xMax, float zMin, float zMax)
        {
            if (root == null)
                return;

            float east0 = xMax + GapFromCliffM;
            float east1 = east0 + FloodplainWidthM;
            float north0 = zMin - NorthPadM;
            float north1 = zMax + NorthPadM;
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            float sizeX = east1 - east0;
            float sizeZ = north1 - north0;
            float y = pose.surfaceY - GizaComplex.CliffHeightM + SitAboveDesertM;

            Vector3 floodCenter = pose.khufuCenter + pose.rot * new Vector3(cx, 0f, cz);
            floodCenter.y = y;
            if (TooClose(floodCenter, plazaPos, PlazaKeepoutM))
                return;

            PlaceFloodplain(root, pose, floodCenter, sizeX, sizeZ);
            PlaceHarbor(root, pose, plazaPos, east0, y);
            PlaceSettlement(root, pose, plazaPos, east0, y);
        }

        static void PlaceFloodplain(Transform root, GizaComplex.Pose pose, Vector3 worldCenter,
            float sizeX, float sizeZ)
        {
            Mesh mesh = LabWorldMeshes.BuildFloodplain(sizeX, sizeZ, 24, 0.15f);
            ApplyWorldMesh(root, FloodplainName, mesh, GizaBuild.NileSilt(), worldCenter, pose.rot, true);

            Transform flood = root.Find(FloodplainName);
            if (flood == null)
                return;

            Material fieldMat = GizaBuild.NileField();
            const int strips = 6;
            float stripX = 48f;
            float stripZ = Mathf.Min(420f, sizeZ * 0.55f);
            Mesh stripMesh = LabWorldMeshes.BuildFlatPad(stripX, stripZ, 12f);
            for (int i = 0; i < strips; i++)
            {
                float u = (i + 0.65f) / strips;
                float localEast = Mathf.Lerp(-sizeX * 0.42f, sizeX * 0.42f, u);
                float localNorth = sizeZ * 0.16f;
                Vector3 lp = new Vector3(localEast, 0.05f, localNorth);
                ApplyLocalMesh(flood, "GizaNileField_" + i, stripMesh, fieldMat, lp, Quaternion.identity, true);
            }
        }

        static void PlaceHarbor(Transform root, GizaComplex.Pose pose, Vector3 plazaPos, float east0, float y)
        {
            GizaPrecinct.Layout L = GizaPrecinct.Compute();
            float east = east0 + HarborEastOfCliffM + HarborEW * 0.5f;
            float north = (L.valleyNorth + L.sphinxTempleNorth) * 0.5f;
            Vector3 world = pose.khufuCenter + pose.rot * new Vector3(east, 0f, north);
            world.y = y;
            if (TooClose(world, plazaPos, PlazaKeepoutM + 20f))
                return;

            // Force-rebuild when recessed-basin markers missing (replaces solid water box).
            Transform oldHarbor = root.Find(HarborName);
            if (oldHarbor != null
                && (oldHarbor.Find(QuayName) == null || oldHarbor.Find(BasinName) == null))
            {
                DestroyHarbor(oldHarbor.gameObject);
            }

            Transform harbor = EnsureRoot(root, HarborName, world, pose.rot);

            float floorTop = -BasinDepthM;
            float floorCy = floorTop - BasinFloorThickM * 0.5f;
            float waterCy = floorTop + WaterThickM * 0.5f;
            float quayCy = QuayThickM * 0.5f;

            Mesh basin = BuildBasinFloor(HarborEW, HarborNS, BasinFloorThickM);
            ApplyLocalMesh(harbor, BasinName, basin, GizaBuild.Pavement(),
                new Vector3(0f, floorCy, 0f), Quaternion.identity, true);

            Mesh water = BuildWaterSheet(HarborEW - 1.2f, HarborNS - 1.2f, WaterThickM);
            ApplyLocalMesh(harbor, WaterName, water, GizaBuild.NileWater(),
                new Vector3(0f, waterCy, 0f), Quaternion.identity, false);

            Mesh quay = BuildQuayWithWestSlip(HarborEW, HarborNS, QuayWidthM, QuayThickM, BasinDepthM);
            ApplyLocalMesh(harbor, QuayName, quay, GizaBuild.Pavement(),
                new Vector3(0f, quayCy, 0f), Quaternion.identity, true);

            PlaceFeederCanal(harbor, floorTop, waterCy, quayCy);

            Transform plate = harbor.Find(HonestyName);
            if (plate == null)
            {
                GizaBuild.HonestyPlate(harbor, HonestyName, Honesty, 20f);
                plate = harbor.Find(HonestyName);
            }
            if (plate != null)
            {
                plate.localPosition = new Vector3(-HarborEW * 0.5f - 8f, 1.55f, HarborNS * 0.2f);
                plate.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
        }

        static void PlaceFeederCanal(Transform harbor, float floorTop, float waterCy, float quayCy)
        {
            // Schematic feeder west (local -X) toward Khafre valley / Sphinx temple alignment.
            float canalEast = -(HarborEW * 0.5f + CanalLengthM * 0.5f + QuayWidthM * 0.35f);
            float canalInnerW = CanalWidthM;
            float canalInnerL = CanalLengthM;

            Mesh banks = BuildCanalBanks(canalInnerL, canalInnerW, CanalBankM, QuayThickM, BasinDepthM);
            ApplyLocalMesh(harbor, CanalName, banks, GizaBuild.Pavement(),
                new Vector3(canalEast, quayCy, 0f), Quaternion.identity, true);

            Mesh canalWater = BuildWaterSheet(canalInnerL - 0.6f, canalInnerW - 0.8f, WaterThickM);
            ApplyLocalMesh(harbor, CanalWaterName, canalWater, GizaBuild.NileWater(),
                new Vector3(canalEast, waterCy, 0f), Quaternion.identity, false);

            // Recessed canal floor under the thin water (walkable banks only; floor for visual depth).
            Mesh canalFloor = BuildBasinFloor(canalInnerL - 0.4f, canalInnerW - 0.6f, BasinFloorThickM);
            float canalFloorCy = floorTop - BasinFloorThickM * 0.5f;
            ApplyLocalMesh(harbor, "GizaNileHarbor_CanalFloor", canalFloor, GizaBuild.Bedrock(),
                new Vector3(canalEast, canalFloorCy, 0f), Quaternion.identity, true);
        }

        static void PlaceSettlement(Transform root, GizaComplex.Pose pose, Vector3 plazaPos, float east0, float y)
        {
            GizaPrecinct.Layout L = GizaPrecinct.Compute();
            float harborEast = east0 + HarborEastOfCliffM + HarborEW * 0.5f;
            float harborNorth = (L.valleyNorth + L.sphinxTempleNorth) * 0.5f;
            float east = harborEast + 72f;
            float north = harborNorth - 68f;
            Vector3 world = pose.khufuCenter + pose.rot * new Vector3(east, 0f, north);
            world.y = y;
            if (TooClose(world, plazaPos, PlazaKeepoutM + 20f))
                return;

            Transform village = EnsureRoot(root, SettlementName, world, pose.rot);
            ApplyLocalMesh(village, "GizaValleyHouses", BuildHouseCluster(), GizaBuild.Mudbrick(),
                Vector3.zero, Quaternion.identity, true);
            ApplyLocalMesh(village, "GizaValleyYards", BuildYardPads(), GizaBuild.NileSilt(),
                new Vector3(0f, 0.04f, 0f), Quaternion.identity, true);
        }

        static readonly Vector2[] HouseSpots =
        {
            new Vector2(0f, 0f), new Vector2(13.5f, 1.4f), new Vector2(27f, -0.7f), new Vector2(40.5f, 0.8f),
            new Vector2(5f, -15.2f), new Vector2(18.5f, -16.4f), new Vector2(32f, -14.1f), new Vector2(45f, -15.8f),
            new Vector2(8.5f, -30.2f), new Vector2(23f, -31.6f)
        };

        static Mesh BuildHouseCluster()
        {
            var b = new LabMeshBuilder(960, 2880);
            Color c = Color.white;
            for (int i = 0; i < HouseSpots.Length; i++)
            {
                float hx = HouseSpots[i].x;
                float hz = HouseSpots[i].y;
                b.AddBox(new Vector3(hx, 1.35f, hz), new Vector3(4.4f, 2.7f, 3.6f), c);
                b.AddBox(new Vector3(hx + 3.1f, 1.1f, hz - 0.2f), new Vector3(2.4f, 2.2f, 2.6f), c);
                b.AddBox(new Vector3(hx - 3.4f, 0.85f, hz + 1.4f), new Vector3(0.38f, 1.7f, 6.4f), c);
                b.AddBox(new Vector3(hx + 0.2f, 0.85f, hz + 4.5f), new Vector3(7.2f, 1.7f, 0.38f), c);
                b.AddBox(new Vector3(hx + 0.2f, 0.85f, hz - 1.8f), new Vector3(7.2f, 1.7f, 0.38f), c);
            }
            return b.Build("GizaValleyHouses");
        }

        static Mesh BuildYardPads()
        {
            var b = new LabMeshBuilder(80, 120);
            Color c = Color.white;
            for (int i = 0; i < HouseSpots.Length; i++)
            {
                float hx = HouseSpots[i].x + 0.4f;
                float hz = HouseSpots[i].y + 1.3f;
                float hw = 4.2f;
                float hd = 3.6f;
                Vector3 a = new Vector3(hx - hw, 0f, hz - hd);
                Vector3 br = new Vector3(hx + hw, 0f, hz - hd);
                Vector3 tr = new Vector3(hx + hw, 0f, hz + hd);
                Vector3 tl = new Vector3(hx - hw, 0f, hz + hd);
                b.AddQuad(a, br, tr, tl, Vector3.up, c);
            }
            return b.Build("GizaValleyYards");
        }

        static Mesh BuildBasinFloor(float ew, float ns, float thick)
        {
            var b = new LabMeshBuilder(24, 36);
            b.AddBox(Vector3.zero, new Vector3(ew, thick, ns), Color.white);
            return b.Build(BasinName);
        }

        static Mesh BuildWaterSheet(float ew, float ns, float thick)
        {
            var b = new LabMeshBuilder(24, 36);
            b.AddBox(Vector3.zero, new Vector3(ew, thick, ns), Color.white);
            return b.Build(WaterName);
        }

        /// <summary>
        /// Walkable stone quay rim around the basin, plus a west-side stepped slip toward the cliff.
        /// Mesh is authored around local Y=0; caller offsets by quayCy.
        /// </summary>
        static Mesh BuildQuayWithWestSlip(float ew, float ns, float rim, float thick, float basinDepth)
        {
            var b = new LabMeshBuilder(220, 360);
            Color c = Color.white;
            float outerN = ns + rim * 2f;
            // Four rim sides (local Y = 0 center; thick = QuayThickM).
            b.AddBox(new Vector3(-(ew * 0.5f + rim * 0.5f), 0f, 0f), new Vector3(rim, thick, outerN), c);
            b.AddBox(new Vector3(ew * 0.5f + rim * 0.5f, 0f, 0f), new Vector3(rim, thick, outerN), c);
            b.AddBox(new Vector3(0f, 0f, -(ns * 0.5f + rim * 0.5f)), new Vector3(ew, thick, rim), c);
            b.AddBox(new Vector3(0f, 0f, ns * 0.5f + rim * 0.5f), new Vector3(ew, thick, rim), c);

            // West slip: three descending treads from quay into the basin (toward local -X / cliff).
            float quayTop = thick * 0.5f;
            float westInner = -(ew * 0.5f);
            float treadD = 1.55f;
            float treadSpan0 = 14f;
            const int steps = 3;
            for (int i = 0; i < steps; i++)
            {
                float drop = (i + 1) * (basinDepth / (steps + 1f));
                float treadH = thick + 0.08f;
                float top = quayTop - drop;
                float cy = top - treadH * 0.5f;
                float x = westInner + treadD * 0.5f + i * (treadD * 0.95f);
                float span = treadSpan0 - i * 1.4f;
                b.AddBox(new Vector3(x, cy, 0f), new Vector3(treadD, treadH, span), c);
            }

            return b.Build(QuayName);
        }

        static Mesh BuildCanalBanks(float length, float width, float bank, float thick, float basinDepth)
        {
            var b = new LabMeshBuilder(160, 240);
            Color c = Color.white;
            float outerL = length + bank * 2f;
            // N/S banks and west head; east end opens into harbor basin (no east bank).
            b.AddBox(new Vector3(0f, 0f, width * 0.5f + bank * 0.5f), new Vector3(outerL, thick, bank), c);
            b.AddBox(new Vector3(0f, 0f, -(width * 0.5f + bank * 0.5f)), new Vector3(outerL, thick, bank), c);
            b.AddBox(new Vector3(-(length * 0.5f + bank * 0.5f), 0f, 0f), new Vector3(bank, thick, width + bank * 2f), c);

            // Small west landing / slip at canal head (toward cliff).
            float landW = 3.2f;
            float landD = 2.4f;
            b.AddBox(new Vector3(-(length * 0.5f - landD * 0.4f), 0f, 0f), new Vector3(landD, thick, landW), c);
            float midDrop = basinDepth * 0.45f;
            b.AddBox(new Vector3(-(length * 0.5f - landD * 1.1f), -midDrop * 0.35f, 0f),
                new Vector3(landD * 0.85f, thick + midDrop * 0.5f, landW * 0.75f), c);

            return b.Build(CanalName);
        }

        static void DestroyHarbor(GameObject go)
        {
            if (go == null)
                return;
            go.name = go.name + "_Obsolete";
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        static Transform EnsureRoot(Transform parent, string name, Vector3 worldPos, Quaternion worldRot)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, true);
                t = go.transform;
            }
            t.SetPositionAndRotation(worldPos, worldRot);
            return t;
        }

        static Transform ApplyWorldMesh(Transform parent, string name, Mesh mesh, Material mat,
            Vector3 worldPos, Quaternion worldRot, bool meshCol)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, true);
                t = go.transform;
            }
            else
                go = t.gameObject;
            t.SetPositionAndRotation(worldPos, worldRot);
            BindMesh(go, mesh, mat, meshCol);
            return t;
        }

        static Transform ApplyLocalMesh(Transform parent, string name, Mesh mesh, Material mat,
            Vector3 localPos, Quaternion localRot, bool meshCol)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            else
                go = t.gameObject;
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = Vector3.one;
            BindMesh(go, mesh, mat, meshCol);
            return t;
        }

        static void BindMesh(GameObject go, Mesh mesh, Material mat, bool meshCol)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null)
                mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr == null)
                    mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            else
                Debug.LogError("GizaNile: skipped renderer on '" + go.name + "' (no URP Lit from RELab_Graphite).");

            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (meshCol)
            {
                if (mc == null)
                    mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
            else if (mc != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(mc);
                else
                    Object.DestroyImmediate(mc);
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(rb);
                else
                    Object.DestroyImmediate(rb);
            }
        }

        static bool TooClose(Vector3 a, Vector3 b, float radius)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude < radius * radius;
        }
    }
}
