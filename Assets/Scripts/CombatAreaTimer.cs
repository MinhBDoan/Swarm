using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        // One clock for the entire canyon, not a separate clock per POI.
        // Future map transitions can reset elapsed while carrying the build.
        public const float AreaDurationSeconds = 600;
        public float AreaSecondsRemaining => Mathf.Max(0,AreaDurationSeconds-elapsed);
        public float AreaOvertimeSeconds => Mathf.Max(0,elapsed-AreaDurationSeconds);
        public bool IsAreaOvertime => elapsed>=AreaDurationSeconds;
        public string AreaTimerText
        {
            get
            {
                int seconds=IsAreaOvertime ? Mathf.FloorToInt(AreaOvertimeSeconds) : Mathf.CeilToInt(AreaSecondsRemaining);
                return IsAreaOvertime ? $"OVERTIME +{seconds/60:00}:{seconds%60:00}" : $"AREA 1 / 3  {seconds/60:00}:{seconds%60:00}";
            }
        }
        public string SwarmPressureSummary => IsAreaOvertime ?
            "Strength and numbers keep rising. Defeat the Sentinel!" : "Summon and defeat the Sentinel before overtime.";

        float OvertimeMinutes => AreaOvertimeSeconds/60;
        // Deliberately stronger at expiry, then continuously stronger. Performance
        // bounds limit live bodies, not the damage/health threat of the overtime.
        float OvertimeHealthMultiplier => IsAreaOvertime ? 1.5f+OvertimeMinutes*.8f+OvertimeMinutes*OvertimeMinutes*.4f : 1;
        float OvertimeDamageMultiplier => IsAreaOvertime ? 1.5f+OvertimeMinutes*.65f+OvertimeMinutes*OvertimeMinutes*.25f : 1;
        int SwarmPackSize => IsAreaOvertime ? Mathf.Min(32,16+Mathf.FloorToInt(OvertimeMinutes*4)+ExtraPartyMembers) :
            Mathf.Min(10,3+Mathf.FloorToInt(RunMinutes/2)+ExtraPartyMembers);
        int SwarmEnemyLimit => IsAreaOvertime ? Mathf.Min(128,96+Mathf.FloorToInt(OvertimeMinutes*16)+ExtraPartyMembers*4) :
            Mathf.Min(96,24+Mathf.FloorToInt(RunMinutes*5)+ExtraPartyMembers*6);
        float PackRecovery => IsAreaOvertime ? Mathf.Max(.15f,.65f/(1+OvertimeMinutes*.5f)) : Mathf.Max(1.5f,4.5f-RunMinutes*.3f);
        float EnemyHealthScale => (1+RunMinutes*.35f+RunMinutes*RunMinutes*.05f)*OvertimeHealthMultiplier;
        float EnemyDamageScale => (1+Mathf.Min(2,RunMinutes*.09f))*OvertimeDamageMultiplier;

        void ApplyOvertimePressure(Enemy enemy)
        {
            if (!IsAreaOvertime || enemy.elite) return;
            // The summoned boss keeps its spawn health; overtime strengthens the swarm.
            // Existing swarm enemies also strengthen, preventing a full live-enemy cap
            // of weak survivors from turning overtime into an indefinite farm.
            float health=OvertimeHealthMultiplier, damage=OvertimeDamageMultiplier;
            float ratio=health/enemy.overtimeHealth;
            enemy.health*=ratio;
            enemy.maxHealth*=ratio;
            enemy.damageScale*=damage/enemy.overtimeDamage;
            enemy.overtimeHealth=health;
            enemy.overtimeDamage=damage;
        }
    }
}
