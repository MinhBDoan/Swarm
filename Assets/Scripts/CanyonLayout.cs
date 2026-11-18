using UnityEngine;

namespace Swarm
{
    public static class CanyonLayout
    {
        public static readonly Vector3 PumpCenter=new Vector3(-300,6,350);
        public static Vector3 BossConsole=PumpCenter+new Vector3(6,0,-12);
        public static Vector3 BossSpawn=PumpCenter+new Vector3(6,0,22);
        // Route points describe floor heights; every connection supports ordinary walking.
        public static readonly Vector3[] StationPumpRoute={new Vector3(-46,0,650),new Vector3(-95,2,650),new Vector3(-150,10,610),new Vector3(-210,12,540),new Vector3(-280,6,460),new Vector3(-300,6,398)};
        public static readonly Vector3[] PumpBasinRoute={new Vector3(-300,6,302),new Vector3(-300,12,270),new Vector3(-250,10,210),new Vector3(-190,2,160),new Vector3(-150,0,100),new Vector3(-85,0,20),new Vector3(-29,0,20)};
        public static readonly Vector3[] MiddleRoute={new Vector3(-40,1,390),new Vector3(-90,-2,390),new Vector3(-145,-4,375),new Vector3(-200,0,350),new Vector3(-256,6,350)};
        public static readonly Vector3 StationCenter=new Vector3(0,0,650);
        public static Vector3 StationPoint(float x,float y,float z) => StationCenter+new Vector3(x,y,z);
        public static readonly Vector3[] JourneyPoints={
            new Vector3(0,0,76),new Vector3(0,6,110),new Vector3(0,6,125),
            new Vector3(28,-2,185),new Vector3(28,-2,235),new Vector3(-25,-2,300),
            new Vector3(-25,-2,340),new Vector3(-40,1,390),new Vector3(15,3,455),
            new Vector3(25,2,510),new Vector3(-15,0,565),new Vector3(0,0,600),StationPoint(0,8,-32)
        };
        public static readonly Vector3 BridgeStart=new Vector3(28,8,215),BridgeEnd=new Vector3(-25,8,300);
        public const float BridgeGap=5.5f;
        public static Vector3 QuarterPipeAxis => Vector3.ProjectOnPlane(JourneyPoints[3]-JourneyPoints[2],Vector3.up).normalized;
        public static Vector3 QuarterPipePoint(float along,float side)
            => JourneyPoints[2]+QuarterPipeAxis*along+Vector3.Cross(Vector3.up,QuarterPipeAxis)*side;
        public static readonly Vector3[] BankPoints={new Vector3(-31,-.8f,360),new Vector3(-37,.4f,380),new Vector3(-40,1,390),new Vector3(-26.25f,1.5f,406.25f),new Vector3(-12.5f,2,422.5f)};
        public static Vector3 NarrowsAxis => Vector3.ProjectOnPlane(JourneyPoints[9]-JourneyPoints[8],Vector3.up).normalized;
        public static Vector3 NarrowsPoint(float along,float side,float height)
        {
            Vector3 p=JourneyPoints[8]+NarrowsAxis*along+Vector3.Cross(Vector3.up,NarrowsAxis)*side;
            p.y=height; return p;
        }
        public static Vector3 DownloadCenter => StationPoint(0,12,42);
        public static readonly Vector3[] DownloadCenters={new Vector3(12,0,-5),new Vector3(-36,-1.9f,342),DownloadCenter,new Vector3(23,7,53),StationPoint(-30,8,0),new Vector3(-100,-3.3889f,235),new Vector3(-170,-4,405),new Vector3(-115,-2.4f,530)};
        public static int DownloadCount => DownloadCenters.Length;
        static readonly int[] DownloadTiers={0,1,2,3,4,1,2,3};
        public static int DownloadTierIndex(int terminal) => DownloadTiers[terminal];
        public static float DownloadDuration(int terminal) => DownloadSeconds[DownloadTierIndex(terminal)];
        public static readonly float[] DownloadSeconds={10,15,20,30,40};
        public const float DownloadRadius=3.5f;
        public static Vector3 CachePosition => new Vector3(23,7.75f,46);
        public static readonly Vector3[] CachePositions={CachePosition,StationPoint(37,14.25f,42),new Vector3(-82,-3,457)};
        public static readonly string[] CacheNames={"BASIN CACHE","STATION CACHE","FLATS CACHE"};
        public static Vector3 BasinEncounterPosition => new Vector3(0,0,10);
        public static bool InCentralFlats(Vector3 p) =>
            p.z>100 && p.z<630 && p.x< -60 && p.x> -248 && p.y<2;
        public static string Area(Vector3 position)
        {
            if (InCentralFlats(position)) return "CENTRAL FLATS";
            if (position.x < -245 && Mathf.Abs(position.z-350)<62) return "PUMPING YARD / RELAY WORKS";
            if (position.x < -65 && position.z>310 && position.z<420) return "CENTRAL CUT / SHELTER SHORTCUT";
            if (position.x < -60) return position.z>420 ? "WESTERN RIDGE / PUMPING YARD ROUTE" : "RETURN GULLY / BASIN ROUTE";
            if (position.z>StationCenter.z+33 && position.y>10) return "DRILL STATION / MAINTENANCE DECK";
            if (position.z>StationCenter.z-35) return position.x < -24 ? "DRILL STATION / OUTER ROUTE" : "DRILL STATION / BOWL";
            if (position.z>550) return "CANYON PASSAGE / STATION APPROACH";
            if (position.z>330) return "CANYON PASSAGE / WINDING NARROWS";
            if (position.z>210) return position.y>4 ? "CANYON PASSAGE / BROKEN BRIDGE" : "CANYON PASSAGE / DRY GULLY";
            if (position.z>76) return "CANYON PASSAGE / DOWNHILL RUN";
            if (position.y > 9 && position.x > -10.5f && position.x < -3.5f && position.z > 14 && position.z < 22)
                return "MINING BASIN / HOIST PERCH";
            if (position.z < -3) return "MINING BASIN / ENTRY YARD";
            if (position.z > 40 || (position.x > 16 && position.z > 32)) return "MINING BASIN / ORE CONVEYOR";
            return "MINING BASIN / PIT AND HOIST";
        }
        public static string RouteHintAt(Vector3 position)
        {
            if (InCentralFlats(position)) return "North: drill station / west: pumping yard / south: mining basin";
            if (position.x < -245 && Mathf.Abs(position.z-350)<62) return "North: drill station / east: shelter shortcut / south: mining basin";
            if (position.x < -60) return "Gold: outer loop / teal: middle shortcut / ramps provide a walking route";
            if (position.z>StationCenter.z-35) return "Safe rim / east escape zip / deck return slide";
            if (position.z>76) return "Sand: lower trail / teal: upper bridge / E: zip transfer";
            return "Ground loop / side ramps / hoist lift and crossing zip lines";
        }
    }
}
