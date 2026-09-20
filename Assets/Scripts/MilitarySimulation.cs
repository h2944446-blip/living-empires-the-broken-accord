using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingEmpires
{
    [Serializable]
    public sealed class MilitaryPoint
    {
        public double X, Z;
        public MilitaryPoint() { }
        public MilitaryPoint(double x, double z) { X = x; Z = z; }
        public MilitaryPoint Copy() { return new MilitaryPoint(X, Z); }
    }

    [Serializable]
    public sealed class SquadState
    {
        public int Id, Members = 4, TargetId = -1, PathIndex;
        public string Kind = "spearmen", Order = "hold";
        public bool Hostile, IsGuard, Recovering, Demobilizing, Raided;
        public double X, Z, HP = 200, MaxHP = 200, Morale = 100, Experience;
        public double TargetX, TargetZ, PatrolX, PatrolZ, AttackCooldown, RepathAt, RecoverySeconds;
        public List<MilitaryPoint> Path = new List<MilitaryPoint>();
        public SquadState Copy()
        {
            var copy = (SquadState)MemberwiseClone();
            copy.Path = Path == null ? null : Path.Select(p => p == null ? null : p.Copy()).ToList();
            return copy;
        }
    }

    [Serializable]
    public sealed class RecruitmentState
    {
        public int Id;
        public string Kind;
        public double SecondsLeft, TotalSeconds;
        public RecruitmentState Copy() { return (RecruitmentState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MilitaryState
    {
        public bool Enabled, RaidWarning;
        public int BarracksLevel = 1, EquipmentLevel, NextId = 1, RaidsSpawned, RaidsDefeated;
        public double RallyX = -7.7, RallyZ = 5.5, Clock, Accumulator, DayFraction, NextRaidAt = 180;
        public double CampX = 40.7, CampZ = 12.1, CampHP = 300, UpkeepGoldSpent, UpkeepFoodSpent;
        public string LastEvent = "", Status = "active";
        public List<SquadState> Squads = new List<SquadState>();
        public List<RecruitmentState> Queue = new List<RecruitmentState>();
        public MilitaryState Copy()
        {
            var copy = (MilitaryState)MemberwiseClone();
            copy.Squads = Squads == null ? null : Squads.Select(s => s == null ? null : s.Copy()).ToList();
            copy.Queue = Queue == null ? null : Queue.Select(s => s == null ? null : s.Copy()).ToList();
            return copy;
        }
    }

    /// <summary>Offline, fixed-step military simulation. Positions and routes are authoritative;
    /// renderers follow these values. Four-person squads retain their workers while wounded.
    /// The three finite raiding parties and finite starting supplies are explicit scenario rules.</summary>
    public sealed partial class Simulation
    {
        public MilitaryState Military { get { return State.Military; } }
        public const int MilitarySquadLimit = 6;
        public const int CampTargetId = -2;
        public static double RecruitmentGold(string kind) { return kind == "archers" ? 50 : 45; }
        public static double RecruitmentWood(string kind) { return kind == "archers" ? 12 : 8; }
        public static double RecruitmentIron(string kind) { return kind == "archers" ? 2 : 4; }
        public static double RecruitmentSeconds(string kind) { return kind == "archers" ? 24 : 20; }
        public int MilitaryReservedWorkers()
        {
            var m = State.Military;
            return m == null || !m.Enabled ? 0 : m.Squads.Where(s => !s.Hostile).Sum(s => s.Members) + m.Queue.Count * 4;
        }
        public double DailyMilitaryGold() { return MilitaryReservedWorkers() * .15; }
        public double DailyMilitaryFood() { return MilitaryReservedWorkers() * .08; }

        public string StartMilitaryChapter()
        {
            if (Military != null && Military.Enabled) return "The Toll War is already in progress.";
            // This is a separately disclosed established-town scenario, never an earned campaign reward.
            State = new Simulation().State;
            State.Day = 30; State.Towns[0].Population = 72; State.Towns[0].Gold = 560;
            State.Military = new MilitaryState { Enabled = true };
            Military.Squads.Add(new SquadState { Id = Military.NextId++, Kind = "raiders", Hostile = true, IsGuard = true,
                X = Military.CampX, Z = Military.CampZ, TargetX = Military.CampX, TargetZ = Military.CampZ, HP = 240, MaxHP = 240 });
            State.Buildings.RemoveAll(b => b.Town == 0);
            AddBuilding("farm", 0, 9, 12); AddBuilding("farm", 0, 9, 14);
            AddBuilding("lumberyard", 0, 7, 16); AddBuilding("mill", 0, 11, 12);
            AddBuilding("bakery", 0, 13, 12); AddBuilding("mine", 0, 5, 11);
            AddBuilding("smelter", 0, 7, 11); AddBuilding("toolsmith", 0, 7, 13);
            AddBuilding("warehouse", 0, 11, 16); AddBuilding("warehouse", 0, 11, 18);
            AddBuilding("warehouse", 0, 13, 18); AddBuilding("barracks", 0, 15, 16);
            AddBuilding("house", 0, 10, 9); AddBuilding("house", 0, 12, 9);
            AddBuilding("house", 0, 10, 21); AddBuilding("house", 0, 12, 21);
            double[] starting = {140, 48, 100, 130, 35, 48, 36, 130};
            for (int i = 0; i < Goods.Length; i++) State.Towns[0].SetStock(Goods[i], starting[i]);
            State.Crossing.Mode = "bridge"; State.Scenario.Status = "won";
            State.Scenario.Delivered = 2; State.Relations.ForEach(r => r.Fulfilled = 1);
            State.PendingStory.Clear(); State.Events.Clear();
            MilitaryEvent("The Toll War: established Riverhold, day 30. Starting treasury 560 gold; 72 residents; no standing army. Three raiding parties threaten the restored bridge.");
            return "";
        }

        private void MigrateMilitaryState()
        {
            if (State.Military == null) State.Military = new MilitaryState();
            int next = Math.Max(1, State.NextCaravanId);
            foreach (var cargo in State.Caravans) if (cargo.Id >= next) next = cargo.Id + 1;
            foreach (var cargo in State.Caravans) if (cargo.Id == 0) cargo.Id = next++;
            State.NextCaravanId = next;
        }

        private string MilitaryProblem()
        {
            if (Military == null || !Military.Enabled) return "Start The Toll War chapter from the main menu to command troops.";
            if (!State.Buildings.Any(b => b.Town == 0 && b.Type == "barracks")) return "Build a Muster Yard before recruiting or upgrading.";
            return "";
        }
        public string RecruitSquad(string kind)
        {
            string problem = MilitaryProblem(); if (problem != "") return problem;
            if (!OneOf(kind, "spearmen", "archers")) return "Choose spearmen or archers.";
            if (Military.Queue.Count + Military.Squads.Count(s => !s.Hostile) >= MilitarySquadLimit) return "The army limit is six squads, including recruits and wounded.";
            if (WorkforceTotal() - WorkersUsed() < 4) return "Recruitment needs four available workers. Reassign production workers or expand housing.";
            var cost = Def(kind, kind, 0, RecruitmentGold(kind), R("wood", RecruitmentWood(kind), "iron", RecruitmentIron(kind)), R(), R(), "");
            problem = CostProblem(State.Towns[0], cost); if (problem != "") return problem;
            PayCost(State.Towns[0], cost);
            double duration = RecruitmentSeconds(kind) * (Military.BarracksLevel == 2 ? .75 : 1);
            Military.Queue.Add(new RecruitmentState { Id = Military.NextId++, Kind = kind, SecondsLeft = duration, TotalSeconds = duration });
            MilitaryEvent("Four workers entered " + kind + " training. Equipment paid; wages and field rations begin now."); return "";
        }
        public string CancelRecruitment(int id)
        {
            string problem = MilitaryProblem(); if (problem != "") return problem;
            var item = Military.Queue.Find(q => q.Id == id); if (item == null) return "This recruitment order has already completed or was canceled.";
            // Refund only if all returned equipment fits. This avoids losing supplies silently or exceeding storage.
            var town = State.Towns[0];
            if (town.Stock("wood") + RecruitmentWood(item.Kind) + IncomingQuantity(0, "wood") + PromisedSpace(0, "wood") > TownCapacity(0) + Epsilon ||
                town.Stock("iron") + RecruitmentIron(item.Kind) + IncomingQuantity(0, "iron") + PromisedSpace(0, "iron") > TownCapacity(0) + Epsilon)
                return "Make storage room for the returned wood and iron before canceling training.";
            town.Gold += RecruitmentGold(item.Kind); town.SetStock("wood", town.Stock("wood") + RecruitmentWood(item.Kind));
            town.SetStock("iron", town.Stock("iron") + RecruitmentIron(item.Kind)); Military.Queue.Remove(item);
            MilitaryEvent("Training canceled. Recruitment cost refunded and four workers released; already consumed upkeep is not refunded."); return "";
        }
        public string UpgradeMilitary(string type)
        {
            string problem = MilitaryProblem(); if (problem != "") return problem;
            if (!OneOf(type, "barracks", "equipment")) return "Unknown military upgrade.";
            if ((type == "barracks" && Military.BarracksLevel >= 2) || (type == "equipment" && Military.EquipmentLevel >= 1)) return "This military upgrade is already complete.";
            if (type == "equipment" && Military.Squads.Any(s => !s.Hostile && (Distance(s.X, s.Z, BasePoint().X, BasePoint().Z) > 5 || s.Recovering)))
                return "Return all squads to the Muster Yard and let the wounded recover before refitting equipment.";
            var cost = Def(type, type, 0, type == "barracks" ? 100 : 90,
                type == "barracks" ? R("wood", 24, "stone", 12) : R("iron", 12, "tools", 4), R(), R(), "");
            problem = CostProblem(State.Towns[0], cost); if (problem != "") return problem;
            PayCost(State.Towns[0], cost);
            if (type == "barracks") { Military.BarracksLevel = 2; MilitaryEvent("Barracks upgraded: new training orders finish 25% faster. Existing orders retain their promised time."); }
            else
            {
                Military.EquipmentLevel = 1;
                foreach (var squad in Military.Squads.Where(s => !s.Hostile)) { squad.MaxHP *= 1.2; squad.HP *= 1.2; }
                MilitaryEvent("Tempered equipment fitted: militia gain 20% health and 20% damage. Future recruits receive the same equipment.");
            }
            return "";
        }
        public string SetRally(double x, double z)
        {
            string problem = MilitaryProblem(); if (problem != "") return problem;
            var origin = BasePoint(); var path = MilitaryPath(origin.X, origin.Z, x, z);
            if (path == null) return "The rally point is blocked or has no working crossing.";
            Military.RallyX = x; Military.RallyZ = z; return "";
        }
        public string DemobilizeSquad(int id)
        {
            if (Military == null || !Military.Enabled) return "No active military chapter.";
            var squad = Military.Squads.Find(s => s.Id == id && !s.Hostile); if (squad == null) return "Select one of Riverhold's squads.";
            var home = BasePoint(); var path = MilitaryPath(squad.X, squad.Z, home.X, home.Z);
            if (path == null) return "The squad cannot return: restore its route to the Muster Yard.";
            squad.Demobilizing = true; squad.Order = "retreat"; SetPath(squad, home.X, home.Z, path);
            MilitaryEvent("Squad returning to demobilize. Its workers remain reserved until it reaches the Muster Yard."); return "";
        }
        public string OrderSquad(int id, string order, double targetX, double targetZ, int targetId = -1)
        {
            if (Military == null || !Military.Enabled) return "No active military chapter.";
            var squad = Military.Squads.Find(s => s.Id == id && !s.Hostile); if (squad == null) return "Select one of Riverhold's squads.";
            if (!OneOf(order, "move", "hold", "attack", "patrol", "escort", "retreat")) return "Unknown troop order.";
            if (!ValidPosition(targetX, targetZ)) return "Choose a position inside the playable map.";
            if (squad.Recovering) return "This wounded squad must recover at the Muster Yard first.";
            if (order == "hold") { squad.Order = order; squad.TargetId = -1; squad.Path.Clear(); squad.PathIndex = 0; squad.Demobilizing = false; return ""; }
            if (order == "retreat") { var home = BasePoint(); targetX = home.X; targetZ = home.Z; }
            if (order == "attack" && targetId >= 0)
            {
                var enemy = Military.Squads.Find(s => s.Id == targetId && s.Hostile && s.HP > 0);
                if (enemy == null) return "That enemy is no longer present."; targetX = enemy.X; targetZ = enemy.Z;
            }
            else if (order == "attack" && targetId == CampTargetId)
            {
                if (Military.CampHP <= 0) return "The raider camp has already been cleared.";
                targetX = Military.CampX; targetZ = Military.CampZ;
            }
            if (order == "escort")
            {
                var cargo = State.Caravans.Find(c => c.Id == targetId); if (cargo == null) return "Select an active caravan to escort.";
                var pos = EscortPoint(cargo); targetX = pos.X; targetZ = pos.Z;
            }
            var path = MilitaryPath(squad.X, squad.Z, targetX, targetZ);
            if (path == null) return "Route blocked by a building, river, or unfinished crossing. Choose clear ground.";
            squad.Order = order; squad.TargetId = targetId; squad.Demobilizing = false;
            squad.PatrolX = squad.X; squad.PatrolZ = squad.Z; SetPath(squad, targetX, targetZ, path); return "";
        }
        public void SetMilitaryDayFraction(double value)
        { if (Military != null && Finite(value)) Military.DayFraction = Clamp(value, 0, .999999); }
        public void AdvanceMilitary(double seconds)
        {
            if (Military == null || !Military.Enabled || !Finite(seconds) || seconds <= 0) return;
            // Bound each request while accepting long deterministic verification steps.
            Military.Accumulator += Math.Min(seconds, 3600);
            while (Military.Accumulator + 1e-9 >= .1)
            {
                Military.Accumulator = Math.Max(0, Military.Accumulator - .1);
                MilitaryStep(.1);
            }
        }
        private void MilitaryStep(double dt)
        {
            var m = Military; m.Clock += dt;
            if (m.Queue.Count > 0)
            {
                var item = m.Queue[0]; item.SecondsLeft = Math.Max(0, item.SecondsLeft - dt);
                if (item.SecondsLeft <= 1e-7)
                {
                    var origin = BasePoint(); double hp = (item.Kind == "archers" ? 150 : 210) * (m.EquipmentLevel > 0 ? 1.2 : 1);
                    var squad = new SquadState { Id = item.Id, Kind = item.Kind, X = origin.X, Z = origin.Z, HP = hp, MaxHP = hp, TargetX = origin.X, TargetZ = origin.Z };
                    m.Squads.Add(squad); m.Queue.RemoveAt(0);
                    var rally = RallySlot(squad.Id);
                    OrderSquad(squad.Id, "move", rally.X, rally.Z);
                    MilitaryEvent("Four " + item.Kind + " completed training and are moving to the rally point.");
                }
            }
            bool hostilesPresent = m.Squads.Any(s => s.Hostile && !s.IsGuard);
            if (m.CampHP > 0 && m.RaidsSpawned < 3 && m.Status != "won")
            {
                if (!m.RaidWarning && m.Clock >= m.NextRaidAt - 30 && !hostilesPresent)
                { m.RaidWarning = true; m.NextRaidAt = Math.Max(m.NextRaidAt, m.Clock + 30); MilitaryEvent("Scouts report raiders departing the eastern camp in 30 seconds. Prepare the bridge approach."); }
                if (m.Clock >= m.NextRaidAt && !hostilesPresent) SpawnRaid();
            }
            foreach (var squad in m.Squads.ToArray())
            {
                if (!m.Squads.Contains(squad)) continue;
                squad.AttackCooldown = Math.Max(0, squad.AttackCooldown - dt);
                if (squad.Recovering)
                {
                    var home = BasePoint();
                    if (m.Clock >= squad.RepathAt) { squad.RepathAt = m.Clock + 1; Repath(squad, home.X, home.Z); }
                    if (Distance(squad.X, squad.Z, home.X, home.Z) > .5) MoveSquad(squad, dt, 1.8);
                    else
                    {
                        squad.RecoverySeconds += dt;
                        if (squad.Demobilizing || squad.RecoverySeconds >= 30)
                        {
                            if (squad.Demobilizing) m.Squads.Remove(squad);
                            else { squad.Recovering = false; squad.HP = squad.MaxHP * .75; squad.Morale = 65; squad.Order = "hold"; MilitaryEvent("A wounded squad has recovered at the Muster Yard. Reinforce its position before returning to battle."); }
                        }
                    }
                    continue;
                }
                if (squad.HP <= 0) continue;
                if (squad.Hostile && squad.Raided)
                {
                    if (m.Clock >= squad.RepathAt) { squad.RepathAt = m.Clock + 1; Repath(squad, m.CampX, m.CampZ); }
                    MoveSquad(squad, dt, 2.3);
                    if (Distance(squad.X, squad.Z, m.CampX, m.CampZ) < .6) m.Squads.Remove(squad);
                    continue;
                }
                if (squad.Demobilizing)
                {
                    MoveSquad(squad, dt, 2.8); var home = BasePoint();
                    if (Distance(squad.X, squad.Z, home.X, home.Z) < .5) { m.Squads.Remove(squad); MilitaryEvent("Squad demobilized. Four workers returned to civilian life; issued equipment is retired."); }
                    continue;
                }
                var target = ChooseEnemy(squad);
                double range = squad.Kind == "archers" ? 9 : 2.6;
                bool retreating = squad.Order == "retreat";
                if (target != null && !retreating && Distance(squad.X, squad.Z, target.X, target.Z) <= range && ClearShot(squad.X, squad.Z, target.X, target.Z))
                {
                    if (squad.AttackCooldown <= 0)
                    {
                        double damage = (squad.Hostile ? 9 : squad.Kind == "archers" ? 15 : 19) * (!squad.Hostile && m.EquipmentLevel > 0 ? 1.2 : 1);
                        damage *= .75 + .25 * squad.Morale / 100; damage *= 1 + Math.Min(.2, squad.Experience * .025);
                        target.HP = Math.Max(0, target.HP - damage); target.Morale = Math.Max(5, target.Morale - 2);
                        squad.AttackCooldown = squad.Kind == "archers" ? 1.45 : 1.1;
                        if (target.HP <= 0) Defeat(target, squad);
                    }
                    continue;
                }
                if (!squad.Hostile && squad.Order == "attack" && squad.TargetId == CampTargetId && m.CampHP > 0 && Distance(squad.X, squad.Z, m.CampX, m.CampZ) <= range && ClearShot(squad.X, squad.Z, m.CampX, m.CampZ))
                {
                    if (squad.AttackCooldown <= 0)
                    {
                        m.CampHP = Math.Max(0, m.CampHP - (squad.Kind == "archers" ? 10 : 18)); squad.AttackCooldown = 1.2;
                        if (m.CampHP == 0) { m.Status = "won"; m.RaidWarning = false; MilitaryEvent("The raider camp is cleared. The Toll War is won; surviving raiders can still be driven off. Troops may now demobilize."); }
                    }
                    continue;
                }
                if (m.Clock >= squad.RepathAt)
                {
                    squad.RepathAt = m.Clock + 1;
                    if (target != null && (squad.Hostile || squad.Order == "attack" || squad.Order == "patrol" || squad.Order == "escort"))
                        Repath(squad, target.X, target.Z);
                    else if (squad.Hostile && !squad.IsGuard)
                    { var home = BasePoint(); Repath(squad, home.X, home.Z); }
                    else if (squad.IsGuard) Repath(squad, m.CampX, m.CampZ);
                    else if (squad.Order == "escort")
                    {
                        var cargo = State.Caravans.Find(c => c.Id == squad.TargetId);
                        if (cargo == null) { squad.Order = "hold"; squad.TargetId = -1; squad.Path.Clear(); squad.PathIndex = 0; MilitaryEvent("Escort complete: the caravan has arrived. The squad is holding position."); }
                        else { var p = EscortPoint(cargo); Repath(squad, p.X, p.Z); }
                    }
                }
                MoveSquad(squad, dt, squad.Hostile ? 1.7 : squad.Kind == "archers" ? 3 : 2.7);
                if (squad.PathIndex >= squad.Path.Count)
                {
                    if (squad.Hostile && !squad.IsGuard)
                    {
                        var home = BasePoint();
                        if (Distance(squad.X, squad.Z, home.X, home.Z) > 1) continue;
                        var town = State.Towns[0]; double gold = Math.Min(35, town.Gold); double bread = Math.Min(8, town.Stock("bread"));
                        town.Gold -= gold; town.SetStock("bread", town.Stock("bread") - bread); squad.Raided = true;
                        Repath(squad, m.CampX, m.CampZ); MilitaryEvent(F("Raiders seized {0:0} gold and {1:0} bread, then withdrew. Your settlement survives; regroup at the Muster Yard.", gold, bread));
                    }
                    else if (squad.Order == "patrol")
                    { double x = squad.PatrolX, z = squad.PatrolZ; squad.PatrolX = squad.X; squad.PatrolZ = squad.Z; Repath(squad, x, z); }
                    else if (squad.Order == "move" || squad.Order == "retreat") squad.Order = "hold";
                }
                var basePoint = BasePoint();
                if (!squad.Hostile && target == null && Distance(squad.X, squad.Z, basePoint.X, basePoint.Z) < 5)
                { squad.HP = Math.Min(squad.MaxHP, squad.HP + dt * 2); squad.Morale = Math.Min(100, squad.Morale + dt * 2); }
            }
            if (m.RaidsSpawned >= 3 && !m.Squads.Any(s => s.Hostile && !s.IsGuard) && m.Status == "active")
            {
                m.Status = m.RaidsDefeated >= 3 ? "won" : "recovery";
                MilitaryEvent(m.Status == "won" ? "All three raiding parties were defeated. The crossing is secure; The Toll War is won." : "The raiding parties have withdrawn. Regroup, recruit, and clear the eastern camp to secure the road. No additional raiding parties remain.");
            }
        }
        private void SpawnRaid()
        {
            var m = Military; ++m.RaidsSpawned; m.RaidWarning = false; m.NextRaidAt = m.Clock + 140;
            // One squad per finite party keeps the first chapter recoverable on a small economy.
            var home = BasePoint(); var squad = new SquadState { Id = m.NextId++, Kind = "raiders", Hostile = true, X = m.CampX, Z = m.CampZ, HP = 130 + m.RaidsSpawned * 20, MaxHP = 130 + m.RaidsSpawned * 20, Order = "attack", TargetX = home.X, TargetZ = home.Z };
            m.Squads.Add(squad); Repath(squad, home.X, home.Z);
            MilitaryEvent("Raiding party " + m.RaidsSpawned + " of 3 has departed the eastern camp. Defend the crossing or intercept it.");
        }
        private SquadState ChooseEnemy(SquadState squad)
        {
            if (squad.Order == "move" || squad.Order == "retreat")
                return Military.Squads.Where(s => s.Hostile != squad.Hostile && s.HP > 0 && !s.Recovering && Distance(s.X, s.Z, squad.X, squad.Z) <= (squad.Kind == "archers" ? 9 : 2.6)).OrderBy(s => s.Id).FirstOrDefault();
            if (squad.TargetId > 0 && squad.Order == "attack")
            { var explicitTarget = Military.Squads.Find(s => s.Id == squad.TargetId && s.Hostile != squad.Hostile && s.HP > 0 && !s.Recovering); if (explicitTarget != null) return explicitTarget; }
            double awareness = squad.IsGuard ? 12 : squad.Order == "hold" ? (squad.Kind == "archers" ? 9 : 2.6) : 11;
            return Military.Squads.Where(s => s.Hostile != squad.Hostile && s.HP > 0 && !s.Recovering && Distance(s.X, s.Z, squad.X, squad.Z) <= awareness)
                .OrderBy(s => Distance(s.X, s.Z, squad.X, squad.Z)).ThenBy(s => s.Id).FirstOrDefault();
        }
        private void Defeat(SquadState target, SquadState victor)
        {
            victor.Experience += 1;
            if (target.Hostile)
            {
                Military.Squads.Remove(target); if (!target.IsGuard) ++Military.RaidsDefeated;
                foreach(var squad in Military.Squads.Where(s=>s.Order=="attack"&&s.TargetId==target.Id))
                { squad.Order="hold";squad.TargetId=-1;squad.Path.Clear();squad.PathIndex=0; }
                MilitaryEvent(target.IsGuard ? "The camp guard was defeated. Militia gained experience; the camp can now be dismantled." : "A raiding party was defeated. The surviving militia gained experience.");
            }
            else
            {
                target.Recovering = true; target.Morale = 10; target.RecoverySeconds = 0; target.Order = "retreat";
                var home = BasePoint(); Repath(target, home.X, home.Z);
                MilitaryEvent("A militia squad was wounded and is withdrawing. Its workers remain mobilized; recovery takes 30 seconds at the Muster Yard.");
            }
        }
        private void MilitaryUpkeep()
        {
            if (Military == null || !Military.Enabled) return;
            var town = State.Towns[0]; double wage = DailyMilitaryGold(), food = DailyMilitaryFood();
            double paid = Math.Min(wage, town.Gold); town.Gold -= paid; Military.UpkeepGoldSpent += paid;
            double bread = Math.Min(food, town.Stock("bread")); town.SetStock("bread", town.Stock("bread") - bread);
            double grain = Math.Min(food - bread, town.Stock("grain")); town.SetStock("grain", town.Stock("grain") - grain); Military.UpkeepFoodSpent += bread + grain;
            if (paid + Epsilon < wage || bread + grain + Epsilon < food)
            {
                foreach (var squad in Military.Squads.Where(s => !s.Hostile)) squad.Morale = Math.Max(10, squad.Morale - 12);
                MilitaryEvent("Military upkeep shortage: unpaid wages or missing field rations reduced morale. Demobilize troops or restore income and food.");
            }
        }
        public string MilitaryDescription()
        {
            if (Military == null || !Military.Enabled) return "First Winter Delivery is peaceful. The Toll War is a separate established-town military chapter.";
            return F("{0} | {1}/3 raiding parties defeated | {2} workers mobilized | upkeep {3:0.00} gold + {4:0.00} field rations/day | {5}", Military.Status, Military.RaidsDefeated, MilitaryReservedWorkers(), DailyMilitaryGold(), DailyMilitaryFood(), Military.LastEvent);
        }
        private void MilitaryEvent(string message) { Military.LastEvent = message; Log(message); }
        private MilitaryPoint BasePoint()
        {
            var b = State.Buildings.Find(x => x.Town == 0 && x.Type == "barracks");
            if (b == null) return new MilitaryPoint(-7.7, 5.5);
            foreach (var offset in new[] { new[] {1,0}, new[] {0,1}, new[] {-1,0}, new[] {0,-1} })
            { int x = b.X + offset[0], z = b.Z + offset[1]; if (Walkable(x,z)) return GridPoint(x,z); }
            return new MilitaryPoint((b.X - 19.5) * 2.2, (b.Z - 13.5) * 2.2);
        }
        private MilitaryPoint RallySlot(int squadId)
        {
            int rx=GridX(Military.RallyX),rz=GridZ(Military.RallyZ);
            for(int radius=0;radius<=3;radius++)
                for(int z=rz-radius;z<=rz+radius;z++)for(int x=rx-radius;x<=rx+radius;x++)
                {
                    if(!Walkable(x,z))continue;var p=GridPoint(x,z);
                    if(Military.Squads.Any(s=>s.Id!=squadId&&!s.Hostile&&(Distance(s.X,s.Z,p.X,p.Z)<1.7||Distance(s.TargetX,s.TargetZ,p.X,p.Z)<1.7)))continue;
                    return p;
                }
            return new MilitaryPoint(Military.RallyX,Military.RallyZ);
        }
        private static double Distance(double ax, double az, double bx, double bz) { double x = ax - bx, z = az - bz; return Math.Sqrt(x*x + z*z); }
        private static MilitaryPoint GridPoint(int x, int z) { return new MilitaryPoint((x-19.5)*2.2, (z-13.5)*2.2); }
        private static int GridX(double x) { return (int)Math.Floor(x/2.2+20); }
        private static int GridZ(double z) { return (int)Math.Floor(z/2.2+14); }
        private static bool ValidPosition(double x, double z) { return Bounded(x,-42.9,42.9) && Bounded(z,-29.7,34.1); }
        private bool Walkable(int x,int z)
        {
            if (x < 0 || x > 39 || z < 0 || z > 29 || TileOccupied(x,z)) return false;
            if (x >= 18 && x <= 21)
                return (State.Crossing.Mode == "bridge" && z == 17) || (State.Crossing.Mode == "ferry" && z == 20);
            return true;
        }
        public bool MilitaryRouteAvailable(double sx, double sz, double tx, double tz) { return MilitaryPath(sx,sz,tx,tz) != null; }
        private List<MilitaryPoint> MilitaryPath(double sx,double sz,double tx,double tz)
        {
            if (!ValidPosition(sx,sz) || !ValidPosition(tx,tz)) return null;
            int startX=GridX(sx),startZ=GridZ(sz),endX=GridX(tx),endZ=GridZ(tz);
            if (!Walkable(endX,endZ)) return null;
            int start=startZ*40+startX,end=endZ*40+endX; int[] previous=new int[1200];
            for(int i=0;i<previous.Length;i++)previous[i]=-1;
            var pending=new Queue<int>();pending.Enqueue(start);previous[start]=start;
            int[] dx={1,0,-1,0},dz={0,1,0,-1};
            while(pending.Count>0&&previous[end]<0)
            {
                int current=pending.Dequeue(),x=current%40,z=current/40;
                for(int i=0;i<4;i++){int nx=x+dx[i],nz=z+dz[i];if(!Walkable(nx,nz))continue;int next=nz*40+nx;if(previous[next]>=0)continue;previous[next]=current;pending.Enqueue(next);}
            }
            if(previous[end]<0)return null;
            var result=new List<MilitaryPoint>();int cursor=end;
            while(cursor!=start){result.Add(GridPoint(cursor%40,cursor/40));cursor=previous[cursor];}
            result.Reverse();result.Insert(0,GridPoint(startX,startZ));result.Add(new MilitaryPoint(tx,tz));return result;
        }
        private static void SetPath(SquadState squad,double x,double z,List<MilitaryPoint> path)
        { squad.TargetX=x;squad.TargetZ=z;squad.Path=path;squad.PathIndex=0; }
        private void Repath(SquadState squad,double x,double z)
        {
            // A stationary destination must not reset progress to the current cell center every second.
            // Preserve the short segment currently being traversed when chasing a moving target.
            bool continuing=squad.PathIndex<squad.Path.Count&&Walkable(GridX(squad.Path[squad.PathIndex].X),GridZ(squad.Path[squad.PathIndex].Z));
            if(continuing&&Distance(squad.TargetX,squad.TargetZ,x,z)<.75)return;
            var origin=continuing?squad.Path[squad.PathIndex]:new MilitaryPoint(squad.X,squad.Z);
            var path=MilitaryPath(origin.X,origin.Z,x,z);
            if(path!=null){if(continuing)path.Insert(0,origin.Copy());SetPath(squad,x,z,path);}
        }
        private void MoveSquad(SquadState squad,double dt,double speed)
        {
            double remaining=dt*speed;
            while(remaining>0&&squad.PathIndex<squad.Path.Count)
            {
                var next=squad.Path[squad.PathIndex];
                if(!Walkable(GridX(next.X),GridZ(next.Z))){squad.Path.Clear();squad.PathIndex=0;if(!squad.Hostile)MilitaryEvent("Troop route blocked by construction or a missing crossing. Issue a new route.");break;}
                double distance=Distance(squad.X,squad.Z,next.X,next.Z);
                if(distance<=remaining){squad.X=next.X;squad.Z=next.Z;remaining-=distance;++squad.PathIndex;}
                else {squad.X+=(next.X-squad.X)*remaining/distance;squad.Z+=(next.Z-squad.Z)*remaining/distance;remaining=0;}
            }
        }
        private bool ClearShot(double ax,double az,double bx,double bz)
        {
            int steps=(int)Math.Ceiling(Distance(ax,az,bx,bz)/.8);
            for(int i=1;i<steps;i++)if(TileOccupied(GridX(ax+(bx-ax)*i/steps),GridZ(az+(bz-az)*i/steps)))return false;
            return true;
        }
        public MilitaryPoint MilitaryCaravanPosition(int id)
        { var cargo=State.Caravans.Find(c=>c.Id==id);return cargo==null?null:CargoPoint(cargo); }
        private MilitaryPoint EscortPoint(CaravanState cargo)
        {
            var p=CargoPoint(cargo);int x=GridX(p.X),z=GridZ(p.Z);if(Walkable(x,z))return p;
            // Caravan loading squares may overlap their source workplace; escorts wait beside it.
            foreach(var offset in new[]{new[]{1,0},new[]{0,1},new[]{-1,0},new[]{0,-1}})
                if(Walkable(x+offset[0],z+offset[1]))return GridPoint(x+offset[0],z+offset[1]);
            return p;
        }
        private MilitaryPoint CargoPoint(CaravanState cargo)
        {
            var anchors=new[]{GridPoint(11,14),GridPoint(31,8),GridPoint(31,22)};var a=anchors[cargo.From];var b=anchors[cargo.To];
            double west=GridPoint(11,14).X,east=GridPoint(30,14).X;var points=new List<MilitaryPoint>{a};
            if(cargo.From!=0&&cargo.To!=0){points.Add(new MilitaryPoint(east,a.Z));points.Add(new MilitaryPoint(east,b.Z));}
            else
            {
                double z=cargo.Route.Contains("ridge")||cargo.Route.Contains("relief")?25.3:cargo.Route=="ferry"?14.3:7.7;
                double ax=cargo.From==0?west:east,bx=cargo.To==0?west:east;
                points.Add(new MilitaryPoint(ax,a.Z));points.Add(new MilitaryPoint(ax,z));points.Add(new MilitaryPoint(bx,z));points.Add(new MilitaryPoint(bx,b.Z));
            }
            points.Add(b);double length=0;for(int i=1;i<points.Count;i++)length+=Distance(points[i-1].X,points[i-1].Z,points[i].X,points[i].Z);
            double remaining=length*Clamp((cargo.TotalDays-cargo.DaysLeft+Military.DayFraction)/cargo.TotalDays,0,1);
            for(int i=1;i<points.Count;i++){double lengthHere=Distance(points[i-1].X,points[i-1].Z,points[i].X,points[i].Z);if(lengthHere<1e-8)continue;if(remaining<=lengthHere)return new MilitaryPoint(points[i-1].X+(points[i].X-points[i-1].X)*remaining/lengthHere,points[i-1].Z+(points[i].Z-points[i-1].Z)*remaining/lengthHere);remaining-=lengthHere;}
            return b.Copy();
        }
        private static string ValidateMilitary(SimulationState data)
        {
            // Missing military and shipment IDs are the supported legacy version-3 shape.
            var cargoIds=new HashSet<int>();int largestCargo=0;
            foreach(var cargo in data.Caravans){if(cargo.Id<0||cargo.Id>10000000||cargo.Id>0&&!cargoIds.Add(cargo.Id))return "The save has invalid caravan identifiers.";largestCargo=Math.Max(largestCargo,cargo.Id);}
            if(!InRange(data.NextCaravanId,0,10000001)||data.NextCaravanId!=0&&data.NextCaravanId<=largestCargo)return "The save reuses a caravan identifier.";
            var m=data.Military;if(m==null)return "";
            if(!InRange(m.BarracksLevel,1,2)||!InRange(m.EquipmentLevel,0,1)||!InRange(m.NextId,1,10000001)||!InRange(m.RaidsSpawned,0,3)||!InRange(m.RaidsDefeated,0,m.RaidsSpawned))return "The save has invalid military levels or raid counts.";
            if(!Bounded(m.Clock,0,1e9)||!Bounded(m.Accumulator,0,.100001)||!Bounded(m.DayFraction,0,1)||!Bounded(m.NextRaidAt,0,1e9)||!Bounded(m.CampHP,0,300)||!Bounded(m.UpkeepGoldSpent,0,1e12)||!Bounded(m.UpkeepFoodSpent,0,1e12))return "The save has invalid military timing or finances.";
            if(!ValidPosition(m.RallyX,m.RallyZ)||!ValidPosition(m.CampX,m.CampZ)||!OneOf(m.Status,"active","won","recovery")||m.LastEvent==null||m.LastEvent.Length>512)return "The save has invalid military campaign data.";
            if(m.Squads==null||m.Queue==null||m.Squads.Count>10||m.Queue.Count>6||m.Squads.Count(s=>s!=null&&!s.Hostile)+m.Queue.Count>MilitarySquadLimit)return "The save exceeds the military unit limit.";
            var ids=new HashSet<int>();int largest=0;
            foreach(var squad in m.Squads)
            {
                if(squad==null||!InRange(squad.Id,1,10000000)||!ids.Add(squad.Id)||squad.Members!=4||!OneOf(squad.Kind,"spearmen","archers","raiders")||squad.Hostile!=(squad.Kind=="raiders"))return "The save has an invalid squad identity.";
                largest=Math.Max(largest,squad.Id);
                if(!ValidPosition(squad.X,squad.Z)||!ValidPosition(squad.TargetX,squad.TargetZ)||!ValidPosition(squad.PatrolX,squad.PatrolZ)||!Bounded(squad.MaxHP,1,300)||!Bounded(squad.HP,0,squad.MaxHP)||!Bounded(squad.Morale,0,100)||!Bounded(squad.Experience,0,1000000)||!Bounded(squad.AttackCooldown,0,2)||!Bounded(squad.RepathAt,0,1e9)||!Bounded(squad.RecoverySeconds,0,30.100001))return "The save has invalid squad health, timing or position.";
                if(!OneOf(squad.Order,"move","hold","attack","patrol","escort","retreat")||!InRange(squad.TargetId,-2,10000000)||squad.Path==null||squad.Path.Count>1201||!InRange(squad.PathIndex,0,squad.Path.Count)||squad.Path.Any(p=>p==null||!ValidPosition(p.X,p.Z)))return "The save has invalid squad orders or route.";
                if(squad.Hostile&&(squad.Recovering||squad.Demobilizing)||squad.IsGuard&&!squad.Hostile)return "The save has inconsistent hostile recovery.";
                if(!squad.Recovering&&squad.HP<=0||squad.Recovering&&squad.HP>0)return "The save has inconsistent wounded squad health.";
                for(int i=1;i<squad.Path.Count;i++)if(Distance(squad.Path[i-1].X,squad.Path[i-1].Z,squad.Path[i].X,squad.Path[i].Z)>3.12)return "The save contains a discontinuous troop route.";
                if(squad.PathIndex<squad.Path.Count&&Distance(squad.X,squad.Z,squad.Path[squad.PathIndex].X,squad.Path[squad.PathIndex].Z)>3.12)return "The save contains a teleporting troop route.";
            }
            foreach(var item in m.Queue)
            {
                if(item==null||!InRange(item.Id,1,10000000)||!ids.Add(item.Id)||!OneOf(item.Kind,"spearmen","archers")||!Bounded(item.TotalSeconds,1,24)||!Bounded(item.SecondsLeft,0,item.TotalSeconds))return "The save has an invalid recruitment queue.";
                largest=Math.Max(largest,item.Id);
            }
            if(m.NextId<=largest)return "The save reuses a military identifier.";
            int reserved=m.Squads.Where(s=>!s.Hostile).Sum(s=>s.Members)+m.Queue.Count*4;
            if(!m.Enabled&&(reserved>0||m.Squads.Count>0||m.Clock>0))return "An inactive military chapter contains active troops.";
            if(data.Buildings.Where(b=>b.Town==0).Sum(b=>b.Workers)+reserved>Math.Floor(data.Towns[0].Population*.6))return "The save assigns military and civilian workers twice.";
            return "";
        }
    }
}
