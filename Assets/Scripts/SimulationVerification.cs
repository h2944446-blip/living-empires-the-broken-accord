using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace LivingEmpires
{
    public sealed class VerificationReport
    {
        public int Checks;
        public int Groups;
        public bool SerializationTested;
        public string SerializationBackend;
        public readonly List<string> Failures = new List<string>();
        public readonly List<string> Scenarios = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public bool Passed { get { return Failures.Count == 0; } }

        public void ThrowIfFailed()
        {
            if (!Passed) throw new InvalidOperationException(ToString());
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1} checks in {2} groups; {3} failures.\n",
                Passed ? "PASS" : "FAIL", Checks, Groups, Failures.Count);
            text.AppendLine("Serialization: " + SerializationBackend);
            foreach (string scenario in Scenarios) text.AppendLine(scenario);
            foreach (string note in Notes) text.AppendLine("NOTE: " + note);
            foreach (string failure in Failures) text.AppendLine("FAIL: " + failure);
            return text.ToString();
        }
    }

    /// <summary>
    /// Pure-model regression tests. No scene, player save, file or network access.
    /// Fresh-game playthroughs never seed resources or force calendar/outcomes.
    /// Controlled fixtures are named separately and isolate accounting rules.
    /// </summary>
    public static class SimulationVerification
    {
        public static VerificationReport RunAll()
        {
#if UNITY_5_3_OR_NEWER
            return RunAll(state => UnityEngine.JsonUtility.ToJson(state),
                json => UnityEngine.JsonUtility.FromJson<SimulationState>(json), "Unity JsonUtility");
#else
            return RunAll(null, null, "Snapshot/Restore only; no JSON serializer supplied");
#endif
        }

        public static VerificationReport RunAll(Func<SimulationState, string> serialize,
            Func<string, SimulationState> deserialize, string serializerName = "Provided JSON serializer")
        {
            if ((serialize == null) != (deserialize == null))
                throw new ArgumentException("Supply both serialization delegates or neither.");
            return new Runner(serialize, deserialize, serializerName).Run();
        }

        private sealed class CaseAbortedException : Exception { }

        private sealed class Runner
        {
            private readonly VerificationReport report = new VerificationReport();
            private readonly Func<SimulationState, string> serialize;
            private readonly Func<string, SimulationState> deserialize;
            private string group;

            public Runner(Func<SimulationState, string> serialize,
                Func<string, SimulationState> deserialize, string name)
            {
                this.serialize = serialize;
                this.deserialize = deserialize;
                report.SerializationBackend = name;
                report.SerializationTested = serialize != null;
            }

            public VerificationReport Run()
            {
                RunCase("Construction and workers", ConstructionAndWorkers);
                RunCase("Market purchase", () => Trade(true));
                RunCase("Market export", () => Trade(false));
                RunCase("Rejected orders", RejectedOrders);
                RunCase("Storage and incoming cargo", Storage);
                RunCase("Crossing upgrade", CrossingUpgrade);
                RunCase("Deadline arrival", () => ContractArrival(false));
                RunCase("Late arrival", () => ContractArrival(true));
                RunCase("Winter relief", Relief);
                RunCase("Save with construction and transit", SaveRoundTrip);
                RunCase("Malformed state rejection", InvalidSaves);
                RunCase("Ferry win", () => Playthrough("ferry", false, 1, false));
                RunCase("Bridge win", () => Playthrough("bridge", false, 1, false));
                RunCase("Missed promise recovery", () => Playthrough("ferry", true, 1, false));
                RunCase("Winter-start recovery", () => Playthrough("ferry", false, Simulation.WinterDay, false));
                RunCase("Bridge interrupted by winter", () => Playthrough("bridge", false, 1, true));
                RunCase("400-day invariants", LongRun);
                if (!report.SerializationTested)
                    report.Notes.Add("Actual JSON parsing was not exercised. Supply serializer delegates or run in Unity for that coverage.");
                report.Notes.Add("Model checks do not verify Unity UI, rendering, audio, disk replacement, or Godot-save migration.");
                return report;
            }

            private void RunCase(string name, Action test)
            {
                group = name;
                report.Groups++;
                try { test(); }
                catch (CaseAbortedException) { }
                catch (Exception exception)
                {
                    report.Checks++;
                    report.Failures.Add(name + ": unexpected " + exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace);
                }
            }

            private void Check(bool condition, string message)
            {
                report.Checks++;
                if (!condition) report.Failures.Add(group + ": " + message);
            }

            private void Require(bool condition, string message)
            {
                Check(condition, message);
                if (!condition) throw new CaseAbortedException();
            }

            private void Success(string error, string message)
            {
                Require(string.IsNullOrEmpty(error), message + (string.IsNullOrEmpty(error) ? "" : " — " + error));
            }

            private void Reject(Simulation sim, Func<string> action, string message)
            {
                string before = Fingerprint(sim.Snapshot());
                Check(!string.IsNullOrEmpty(action()), message + " is rejected");
                Check(Fingerprint(sim.Snapshot()) == before, message + " is atomic");
            }

            private static bool Near(double actual, double expected)
            {
                return Finite(actual) && Finite(expected) && Math.Abs(actual - expected) <= 1e-7 * Math.Max(1.0, Math.Abs(expected));
            }

            private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

            private static BuildingState Building(Simulation sim, string kind)
            {
                return sim.State.Buildings.Find(x => x.Town == 0 && x.Type == kind);
            }

            private static double Amount(IEnumerable<ResourceAmount> values, string good)
            {
                foreach (ResourceAmount value in values) if (value.Good == good) return value.Amount;
                return 0.0;
            }

            private static double Money(Simulation sim)
            {
                double total = 0;
                foreach (TownState town in sim.State.Towns) total += town.Gold;
                foreach (ContractState contract in sim.State.Contracts) total += contract.Escrow;
                return total;
            }

            private static double AccountedMoney(Simulation sim)
            {
                double total = Money(sim) + sim.State.FreightPaid;
                // Free starting buildings add an identical constant. Subsequent
                // NPC construction costs offset their actual treasury spending.
                foreach (BuildingState building in sim.State.Buildings)
                    total += Simulation.Definitions[building.Type].GoldCost;
                return total;
            }

            private static double Goods(Simulation sim, string good)
            {
                double total = 0;
                foreach (TownState town in sim.State.Towns) total += town.Stock(good);
                foreach (CaravanState cargo in sim.State.Caravans) if (cargo.Good == good) total += cargo.Quantity;
                return total;
            }

            private void AdvanceTo(Simulation sim, int target)
            {
                Require(target >= sim.State.Day, "Calendar target does not run backwards");
                int budget = target - sim.State.Day;
                for (int day = 0; day < budget; day++) sim.AdvanceDay();
                Check(sim.State.Day == target, "Calendar advances exactly to day " + target);
            }

            private void ReadyCrossing(Simulation sim, string mode)
            {
                string error = sim.ChooseCrossing(mode);
                for (int day = 0; !string.IsNullOrEmpty(error) && day < 35; day++)
                {
                    sim.AdvanceDay();
                    error = sim.ChooseCrossing(mode);
                }
                Success(error, mode + " can be funded from real starting/produced supplies");
                for (int day = 0; !sim.RouteInfo().Ready && day < 35; day++) sim.AdvanceDay();
                Require(sim.RouteInfo().Ready && sim.State.Crossing.Mode == mode, mode + " completes construction");
            }

            private void ZeroWorkers(Simulation sim)
            {
                foreach (BuildingState building in sim.State.Buildings)
                    if (building.Town == 0) Success(sim.SetWorkers(building.Id, 0), "Fixture releases player workers");
            }

            private void ConstructionAndWorkers()
            {
                var sim = new Simulation();
                Reject(sim, () => sim.PlaceBuilding("unknown", 8, 8), "Unknown building");
                Reject(sim, () => sim.PlaceBuilding("farm", 3, 8), "Out-of-territory placement");
                Reject(sim, () => sim.PlaceBuilding("mine", 10, 8), "Mine away from mineral ridge");
                Reject(sim, () => sim.PlaceBuilding("farm", 10, 13), "Occupied tile");
                TownState before = sim.State.Towns[0].Copy();
                int count = sim.State.Buildings.Count;
                Success(sim.PlaceBuilding("mill", 8, 8), "Funded mill placement");
                Check(sim.State.Buildings.Count == count + 1, "One placement creates one building");
                BuildingDefinition definition = Simulation.Definitions["mill"];
                Check(Near(sim.State.Towns[0].Gold, before.Gold - definition.GoldCost), "Construction charges advertised gold");
                foreach (ResourceAmount cost in definition.Cost)
                    Check(Near(sim.State.Towns[0].Stock(cost.Good), before.Stock(cost.Good) - cost.Amount), "Construction charges advertised " + cost.Good);
                Reject(sim, () => sim.PlaceBuilding("mill", 8, 8), "Duplicate placement");
                ZeroWorkers(sim);
                BuildingState mill = Building(sim, "mill");
                Success(sim.SetWorkers(mill.Id, 1), "One worker assigned to mill");
                double grain = sim.State.Towns[0].Stock("grain"), flour = sim.State.Towns[0].Stock("flour");
                sim.AdvanceDay();
                Check(Near(sim.State.Towns[0].Stock("flour") - flour, Amount(definition.Outputs, "flour") / definition.Workers), "Partial staffing scales output");
                Check(Near(grain - sim.State.Towns[0].Stock("grain"), Amount(definition.Inputs, "grain") / definition.Workers), "Partial staffing scales input equally");
                Reject(sim, () => sim.SetWorkers(mill.Id, -1), "Negative workers");
                Reject(sim, () => sim.SetWorkers(mill.Id, definition.Workers + 1), "Workers above workplace capacity");
                Reject(sim, () => sim.SetWorkers(99999, 1), "Unknown workplace");
                Success(sim.SetWorkers(mill.Id, 0), "Release mill worker");
                flour = sim.State.Towns[0].Stock("flour");
                sim.AdvanceDay();
                Check(Near(sim.State.Towns[0].Stock("flour"), flour), "An unstaffed mill produces nothing");
                sim.State.Towns[0].Population = 6; // isolated labor-limit fixture
                Success(sim.SetWorkers(Building(sim, "farm").Id, 3), "Allocate the three available workers");
                Reject(sim, () => sim.SetWorkers(mill.Id, 1), "Double allocation of available workforce");
            }

            private void Trade(bool buy)
            {
                var sim = new Simulation();
                ReadyCrossing(sim, "ferry");
                int source = buy ? 1 : 0, destination = buy ? 0 : 1;
                sim.State.Towns[source].SetStock("stone", 30.0);
                const double quantity = 4.0;
                double sourceStock = sim.State.Towns[source].Stock("stone"), destinationStock = sim.State.Towns[destination].Stock("stone");
                double payer = sim.State.Towns[destination].Gold, seller = sim.State.Towns[source].Gold;
                double goods = Goods(sim, "stone"), money = Money(sim), paid = sim.State.FreightPaid;
                double quote = sim.TradeQuote(1, "stone", buy), fee = sim.RouteInfo().Fee;
                Success(sim.DispatchTrade(1, "stone", quantity, buy), "Funded trade dispatches");
                CaravanState cargo = sim.State.Caravans[sim.State.Caravans.Count - 1];
                Check(cargo.From == source && cargo.To == destination && Near(cargo.Quantity, quantity), "Cargo follows the correct seller/buyer direction and exact quantity");
                Check(Near(sim.State.Towns[source].Stock("stone"), sourceStock - quantity), "Source reserves cargo immediately");
                Check(Near(sim.State.Towns[destination].Stock("stone"), destinationStock), "Destination receives no instant stock");
                Check(Near(sim.State.Towns[destination].Gold, payer - quantity * quote - (buy ? fee : 0)), "Buyer pays quoted goods and appropriate freight");
                Check(Near(sim.State.Towns[source].Gold, seller + quantity * quote - (buy ? 0 : fee)), "Seller receives real proceeds less any export freight");
                Check(Near(Money(sim) + fee, money) && Near(sim.State.FreightPaid - paid, fee), "Trade cash balances with explicit freight sink");
                Check(Near(Goods(sim, "stone"), goods), "Dispatch conserves town plus transit goods");
                Check(cargo.DaysLeft == sim.RouteInfo().Days && cargo.TotalDays > 0, "Cargo records advertised positive travel time");
                if (buy)
                {
                    int completed = sim.State.CompletedTrades;
                    int days = cargo.TotalDays;
                    for (int i = 0; i < days - 1; i++)
                    {
                        sim.AdvanceDay();
                        Check(sim.State.CompletedTrades == completed && Near(sim.State.Towns[0].Stock("stone"), destinationStock), "Purchase remains in transit until its arrival day");
                    }
                    sim.AdvanceDay();
                    Check(sim.State.CompletedTrades == completed + 1 && Near(sim.State.Towns[0].Stock("stone"), destinationStock + quantity), "Exact cargo arrives once on schedule");
                    sim.AdvanceDay();
                    Check(sim.State.CompletedTrades == completed + 1 && Near(sim.State.Towns[0].Stock("stone"), destinationStock + quantity), "Delivery cannot pay goods twice");
                }
            }

            private void RejectedOrders()
            {
                var sim = new Simulation();
                Reject(sim, () => sim.DispatchTrade(1, "stone", 1, true), "Trade without crossing");
                ReadyCrossing(sim, "ferry");
                foreach (int town in new[] { -1, 0, 99 })
                    Reject(sim, () => sim.DispatchTrade(town, "stone", 1, true), "Invalid/own trade partner " + town);
                foreach (double quantity in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
                    Reject(sim, () => sim.DispatchTrade(1, "stone", quantity, true), "Invalid trade quantity " + quantity);
                Reject(sim, () => sim.DispatchTrade(1, "imaginary", 1, true), "Unknown commodity");
                Reject(sim, () => sim.DispatchTrade(1, "stone", sim.RouteInfo().Capacity + 1, true), "Shipment above route capacity");
                sim.State.Towns[1].SetStock("stone", 2);
                Reject(sim, () => sim.DispatchTrade(1, "stone", 3, true), "Order above available seller stock");
                Success(sim.DispatchTrade(1, "stone", 2, true), "Exact available stock can be bought");
                for (int i = 0; i < 3; i++) Reject(sim, () => sim.DispatchTrade(1, "stone", 1, true), "Previously reserved stock cannot be resold");
                sim.State.Towns[0].Gold = 0;
                Reject(sim, () => sim.DispatchTrade(1, "ore", 1, true), "Unaffordable order");
                sim.State.Towns[0].SetStock("grain", 10);
                sim.State.Towns[0].SetStock("bread", 10);
                Success(sim.SetReserveDays(7), "Food reserve policy");
                Reject(sim, () => sim.DispatchTrade(1, "grain", 1, false), "Export breaking food reserve");
                Reject(sim, () => sim.SetReserveDays(-1), "Negative reserve");
                Reject(sim, () => sim.SetReserveDays(31), "Excessive reserve");
            }

            private void Storage()
            {
                var sim = new Simulation();
                ReadyCrossing(sim, "ferry");
                sim.State.Towns[0].SetStock("stone", sim.StorageCapacity() - 5);
                double initial = sim.State.Towns[0].Stock("stone");
                Success(sim.DispatchTrade(1, "stone", 4, true), "Import fits four of five free slots");
                Check(Near(sim.IncomingQuantity(0, "stone"), 4), "Cargo reserves incoming space");
                Reject(sim, () => sim.DispatchTrade(1, "stone", 2, true), "Second purchase cannot reuse reserved space");
                AdvanceTo(sim, sim.State.Day + sim.RouteInfo().Days);
                Check(Near(sim.State.Towns[0].Stock("stone"), initial + 4) && Near(sim.IncomingQuantity(0, "stone"), 0), "Arrival releases reservation without overflow/loss");

                var production = new Simulation();
                Success(production.PlaceBuilding("mill", 8, 8), "Production fixture mill");
                ZeroWorkers(production);
                Success(production.SetWorkers(Building(production, "mill").Id, 3), "Staff mill");
                production.State.Towns[0].SetStock("flour", production.StorageCapacity());
                double grain = production.State.Towns[0].Stock("grain"), flour = production.State.Towns[0].Stock("flour");
                production.AdvanceDay();
                Check(Near(production.State.Towns[0].Stock("grain"), grain) && Near(production.State.Towns[0].Stock("flour"), flour), "Full output storage consumes no inputs");
                double capacity = production.StorageCapacity();
                Success(production.PlaceBuilding("warehouse", 8, 9), "Expand storage with real materials");
                Check(production.StorageCapacity() > capacity, "Warehouse increases capacity");
                production.AdvanceDay();
                Check(production.State.Towns[0].Stock("flour") > flour, "Production resumes after expansion");
            }

            private void CrossingUpgrade()
            {
                var sim = new Simulation();
                ReadyCrossing(sim, "ferry");
                RouteInfoData ferry = sim.RouteInfo();
                double gold = sim.State.Towns[0].Gold;
                Success(sim.ChooseCrossing("bridge"), "A working ferry can be upgraded");
                Check(sim.State.Towns[0].Gold < gold && sim.State.Crossing.DaysLeft > 0, "Bridge costs real money and construction time");
                Reject(sim, () => sim.ChooseCrossing("bridge"), "Duplicate bridge commission");
                Check(sim.RouteInfo().Ready && sim.State.Crossing.Mode == "ferry", "Ferry remains usable during construction");
                AdvanceTo(sim, sim.State.Day + sim.State.Crossing.DaysLeft - 2);
                Success(sim.DispatchTrade(1, "stone", 1, true), "Ferry cargo can depart during construction");
                CaravanState oldCargo = sim.State.Caravans[sim.State.Caravans.Count - 1];
                sim.AdvanceDay(); sim.AdvanceDay();
                RouteInfoData bridge = sim.RouteInfo();
                Check(sim.State.Crossing.Mode == "bridge" && string.IsNullOrEmpty(sim.State.Crossing.Pending), "Bridge opens after exact construction delay");
                Check(sim.State.Caravans.Contains(oldCargo) && oldCargo.Route == "ferry" && oldCargo.DaysLeft == ferry.Days - 2, "Already-paid cargo retains its ferry schedule");
                Check(bridge.Capacity > ferry.Capacity && bridge.Fee < ferry.Fee && bridge.Days < ferry.Days, "Bridge improves load size, freight cost and travel time");
                Success(sim.DispatchTrade(1, "stone", 1, true), "A new shipment can use the bridge");
                CaravanState newCargo = sim.State.Caravans[sim.State.Caravans.Count - 1];
                Check(newCargo.Route == "bridge" && Near(newCargo.Freight, bridge.Fee), "New shipment uses the bridge tariff");
            }

            private void ContractArrival(bool late)
            {
                var sim = new Simulation();
                ReadyCrossing(sim, "ferry");
                int id = sim.State.Contracts[0].Id;
                int acceptedDay = sim.State.Day;
                double money = Money(sim);
                Success(sim.AcceptContract(id), "Accept funded promise");
                ContractState contract = sim.Contract(id);
                Check(contract.Deadline == acceptedDay + 28 && contract.Escrow > 0, "Acceptance grants expected deadline and escrow");
                Check(Near(Money(sim), money), "Acceptance moves money into escrow without minting");
                Reject(sim, () => sim.AcceptContract(id), "Duplicate acceptance");
                int duration = sim.RouteInfo().Days;
                AdvanceTo(sim, contract.Deadline - duration + (late ? 1 : 0));
                // Only this deadline fixture supplies stock directly. End-to-end
                // scenario runs below source all goods through normal actions.
                sim.State.Towns[0].SetStock(contract.Good, contract.Quantity + 20);
                Success(sim.SetReserveDays(0), "Deadline fixture explicitly releases food restriction");
                double player = sim.State.Towns[0].Gold, goods = Goods(sim, contract.Good);
                double accounted = AccountedMoney(sim), trust = sim.Relation(contract.Town).Trust;
                double payment = contract.Quantity * contract.UnitPrice, reward = contract.Reward, fee = sim.RouteInfo().Fee;
                int delivered = sim.State.Scenario.Delivered, missed = sim.State.Scenario.Missed;
                Success(sim.DispatchContract(id), "Dispatch promised cargo");
                Check(sim.State.Scenario.Delivered == delivered, "Dispatch alone does not fulfill a promise");
                Check(Near(sim.State.Towns[0].Gold, player - fee), "Contract pays freight now and waits for sale proceeds");
                Check(Near(Goods(sim, contract.Good), goods) && Near(AccountedMoney(sim), accounted), "Contract dispatch conserves cargo and money");
                Reject(sim, () => sim.DispatchContract(id), "Duplicate contract shipment");
                for (int i = 0; i < duration - 1; i++)
                {
                    sim.AdvanceDay();
                    Check(sim.State.Scenario.Delivered == delivered, "Promise remains unfulfilled before arrival");
                }
                sim.AdvanceDay();
                Check(Near(contract.Escrow, 0) && Near(sim.State.Towns[0].Gold, player - fee + payment + (late ? 0 : reward)), "Arrival settles exactly the eligible payment and bonus");
                Check(Near(AccountedMoney(sim), accounted), "Settlement preserves existing money including freight and construction sinks");
                if (late)
                {
                    Check(sim.State.Scenario.Missed == missed + 1 && sim.State.Scenario.Delivered == delivered, "Late arrival fails once and earns no scenario credit");
                    Check(Near(sim.Relation(contract.Town).Trust, trust - 12), "Late promise reduces trust by twelve");
                }
                else Check(sim.State.Day == contract.Deadline && sim.State.Scenario.Delivered == delivered + 1 && sim.State.Scenario.Missed == missed, "Arrival exactly on deadline succeeds");
                double finalGold = sim.State.Towns[0].Gold, finalTrust = sim.Relation(contract.Town).Trust;
                int finalMissed = sim.State.Scenario.Missed;
                AdvanceTo(sim, sim.State.Day + 5);
                Check(Near(sim.State.Towns[0].Gold, finalGold) && Near(sim.Relation(contract.Town).Trust, finalTrust) && sim.State.Scenario.Missed == finalMissed, "Reward and penalty cannot repeat on later days");
            }

            private void Relief()
            {
                var sim = new Simulation();
                AdvanceTo(sim, Simulation.WinterDay);
                foreach (ResourceAmount good in sim.State.Towns[0].Stocks) good.Amount = 0;
                sim.State.Towns[0].Gold = 0;
                var totals = new Dictionary<string, double>();
                foreach (ResourceAmount good in sim.State.Towns[0].Stocks) totals[good.Good] = Goods(sim, good.Good);
                double money = Money(sim);
                Success(sim.RequestRelief(), "Winter relief needs no completed crossing");
                Check(sim.State.Scenario.RecoveryUsed && Near(Money(sim), money), "Relief flag persists and cash comes from another treasury");
                foreach (var total in totals) Check(Near(Goods(sim, total.Key), total.Value), "Relief conserves actual " + total.Key);
                double stone = 0;
                int duration = 0;
                foreach (CaravanState cargo in sim.State.Caravans)
                    if (cargo.To == 0)
                    {
                        Check(cargo.Route == "lower_ridge_relief", "Relief follows the lower road");
                        duration = Math.Max(duration, cargo.DaysLeft);
                        if (cargo.Good == "stone") stone += cargo.Quantity;
                    }
                Check(duration > 5 && Near(sim.State.Towns[0].Stock("stone"), 0), "Winter relief has real travel delay");
                Reject(sim, () => sim.RequestRelief(), "Duplicate relief request");
                AdvanceTo(sim, sim.State.Day + duration);
                Check(Near(sim.State.Towns[0].Stock("stone"), stone), "Relief delivers exact reserved stone");
                var empty = new Simulation();
                foreach (ResourceAmount good in empty.State.Towns[2].Stocks) good.Amount = 0;
                empty.State.Towns[2].Gold = 0;
                Reject(empty, () => empty.RequestRelief(), "Relief from an empty counterparty");
            }

            private Simulation SaveFixture()
            {
                var sim = new Simulation();
                ReadyCrossing(sim, "ferry");
                Success(sim.ChooseCrossing("bridge"), "Save fixture starts a bridge upgrade");
                Success(sim.DispatchTrade(1, "stone", 1, true), "Save fixture has ordinary cargo in transit");
                int id = sim.State.Contracts[0].Id;
                Success(sim.AcceptContract(id), "Save fixture has real contract escrow");
                Success(sim.DispatchContract(id), "Save fixture has linked promised cargo");
                sim.AcknowledgeStory("opening");
                return sim;
            }

            private void SaveRoundTrip()
            {
                Simulation sim = SaveFixture();
                SimulationState snapshot = sim.Snapshot();
                Check(snapshot.Crossing.Pending == "bridge" && snapshot.Caravans.Count >= 2, "Fixture contains pending construction and mixed shipments");
                SimulationState payload = snapshot;
                if (serialize != null)
                {
                    string json = serialize(snapshot);
                    Require(!string.IsNullOrEmpty(json), "Serializer produces a document");
                    payload = deserialize(json);
                    Require(payload != null, "Deserializer produces a typed state");
                    report.Notes.Add("Serialized double fields and their ten-day continuation use an absolute 1e-12 tolerance; all discrete state and collection structure are exact. Atomic rejection and deep-copy isolation use exact fingerprints.");
                }
                var restored = new Simulation();
                Success(restored.Restore(payload), "Restore accepts the round-trip state");
                string difference = FirstDifference(snapshot, restored.Snapshot(), "state");
                Check(difference == null, "Every persistent field survives round trip" + (difference == null ? "" : ": " + difference));
                // Isolation is an exact in-memory property of the restored state;
                // it must not reuse the pre-serialization baseline.
                string restoredBaseline = Fingerprint(restored.Snapshot());
                payload.Towns[0].Gold += 100;
                Check(Fingerprint(restored.Snapshot()) == restoredBaseline, "Restore does not retain mutable caller references");
                SimulationState isolated = restored.Snapshot();
                isolated.Caravans.Clear(); isolated.Crossing.Pending = "";
                Check(Fingerprint(restored.Snapshot()) == restoredBaseline, "Snapshot does not share nested mutable state");
                for (int i = 0; i < 10; i++)
                {
                    sim.AdvanceDay(); restored.AdvanceDay();
                    difference = FirstDifference(sim.Snapshot(), restored.Snapshot(), "state");
                    Check(difference == null, "Restored world continues identically through arrivals/construction on step " + (i + 1) + (difference == null ? "" : ": " + difference));
                }
            }

            private void InvalidSaves()
            {
                Simulation sim = SaveFixture();
                RejectSave(sim, null, "Null state");
                RejectSave(sim, new SimulationState(), "Unpopulated state");
                var changes = new List<KeyValuePair<string, Action<SimulationState>>>
                {
                    Mutation("Godot version", s => s.Version = 2),
                    Mutation("Wrong format marker", s => s.Format = "godot"),
                    Mutation("Negative calendar", s => s.Day = -1),
                    Mutation("Missing towns", s => s.Towns = null),
                    Mutation("Negative treasury", s => s.Towns[0].Gold = -1),
                    Mutation("Non-finite stock", s => s.Towns[0].SetStock("grain", double.NaN)),
                    Mutation("Duplicate commodity", s => s.Towns[0].Stocks.Add(s.Towns[0].Stocks[0].Copy())),
                    Mutation("Missing commodity", s => s.Towns[0].Stocks.RemoveAt(0)),
                    Mutation("Non-finite freight", s => s.FreightPaid = double.PositiveInfinity),
                    Mutation("Invalid crossing", s => s.Crossing.Mode = "teleporter"),
                    Mutation("Negative construction time", s => s.Crossing.DaysLeft = -1),
                    Mutation("Overstaffed workplace", s => s.Buildings[0].Workers = 999),
                    Mutation("Duplicate building id", s => s.Buildings[1].Id = s.Buildings[0].Id),
                    Mutation("Storage overflow", s => s.Towns[0].SetStock("stone", sim.StorageCapacity() + 1)),
                    Mutation("Incoming space overflow", s => s.Towns[0].SetStock("stone", sim.StorageCapacity() - 0.5)),
                    Mutation("Invalid scenario", s => s.Scenario.Status = "magic_victory"),
                    Mutation("Negative escrow", s => s.Contracts[0].Escrow = -1),
                    Mutation("Unknown contract state", s => s.Contracts[0].Status = "fabricated"),
                    Mutation("Forged contract completion", s => s.Contracts[0].Status = "fulfilled"),
                    Mutation("Invalid cargo route", s => s.Caravans[0].Route = "teleporter"),
                    Mutation("Impossible cargo time", s => s.Caravans[0].DaysLeft = s.Caravans[0].TotalDays + 1),
                    Mutation("Transit contract without cargo", s => s.Caravans.RemoveAll(x => x.ContractId >= 1))
                };
                foreach (var change in changes)
                {
                    SimulationState broken = sim.Snapshot();
                    change.Value(broken);
                    RejectSave(sim, broken, change.Key);
                }
            }

            private static KeyValuePair<string, Action<SimulationState>> Mutation(string label, Action<SimulationState> change)
            {
                return new KeyValuePair<string, Action<SimulationState>>(label, change);
            }

            private void RejectSave(Simulation sim, SimulationState broken, string label)
            {
                string before = Fingerprint(sim.Snapshot());
                Check(!string.IsNullOrEmpty(sim.Restore(broken)), label + " is rejected");
                Require(Fingerprint(sim.Snapshot()) == before, label + " rejection leaves the full world unchanged");
            }

            private void BuildWhenFunded(Simulation sim, string kind, int z)
            {
                string error = sim.PlaceBuilding(kind, 8, z);
                for (int i = 0; !string.IsNullOrEmpty(error) && i < 45; i++)
                {
                    sim.AdvanceDay(); error = sim.PlaceBuilding(kind, 8, z);
                }
                Success(error, "Build " + kind + " using only starting/produced resources");
            }

            private void Playthrough(string mode, bool missFirst, int startDay, bool winterBetweenOrders)
            {
                var sim = new Simulation();
                if (startDay > 1) AdvanceTo(sim, startDay);
                if (missFirst)
                {
                    int id = sim.State.Contracts[0].Id;
                    Success(sim.AcceptContract(id), "Accept a real promise before intentionally missing it");
                    AdvanceTo(sim, sim.Contract(id).Deadline + 1);
                    Check(sim.State.Scenario.Missed == 1 && Near(sim.Contract(id).Escrow, 0), "Undispatched failure records one miss and refunds escrow");
                    AdvanceTo(sim, sim.State.Day + 3);
                    Check(sim.State.Scenario.Missed == 1 && sim.State.Scenario.Status == "active", "Failure is charged once and leaves town playable");
                }
                BuildWhenFunded(sim, "mill", 8);
                BuildWhenFunded(sim, "bakery", 9);
                BuildWhenFunded(sim, "warehouse", 10);
                ReadyCrossing(sim, mode);
                for (int delivery = 0; delivery < 2; delivery++)
                {
                    if (winterBetweenOrders && delivery == 1)
                    {
                        AdvanceTo(sim, Simulation.WinterDay + 1);
                        Check(sim.State.Scenario.Status == "active" && sim.State.Scenario.Delivered == 1, "One prewinter promise does not falsely finish charter");
                    }
                    ContractState offer = null;
                    for (int attempt = 0; attempt < 15; attempt++)
                    {
                        offer = sim.State.Contracts.Find(x => x.Status == "offered" && x.Deadline >= sim.State.Day);
                        if (offer != null) break;
                        sim.AdvanceDay();
                    }
                    Require(offer != null, "A real funded offer remains available for delivery " + (delivery + 1));
                    int id = offer.Id;
                    Success(sim.AcceptContract(id), "Accept prepared delivery " + (delivery + 1));
                    double missing = Math.Max(0, offer.Quantity - sim.State.Towns[0].Stock(offer.Good));
                    if (missing > 0.0001)
                    {
                        int supplier = sim.State.Towns[1].Stock(offer.Good) >= sim.State.Towns[2].Stock(offer.Good) ? 1 : 2;
                        Success(sim.DispatchTrade(supplier, offer.Good, missing, true), "Buy missing " + offer.Good + " from an actual market");
                        for (int i = 0; i < 15 && sim.State.Towns[0].Stock(offer.Good) + 0.0001 < offer.Quantity; i++) sim.AdvanceDay();
                    }
                    string error = sim.DispatchContract(id);
                    for (int i = 0; !string.IsNullOrEmpty(error) && i < 20; i++)
                    {
                        sim.AdvanceDay(); error = sim.DispatchContract(id);
                    }
                    Success(error, "Dispatch exact promised goods without resource injection");
                    for (int i = 0; i < 15 && sim.State.Scenario.Delivered < delivery + 1; i++) sim.AdvanceDay();
                    Require(sim.State.Scenario.Delivered >= delivery + 1, "Promise " + (delivery + 1) + " physically arrives on time");
                }
                AdvanceTo(sim, Math.Max(sim.State.Day, Simulation.WinterDay + 1));
                bool lateStart = startDay >= Simulation.WinterDay || winterBetweenOrders;
                Check(sim.State.Scenario.Winter, "Scenario crosses winter event");
                Check(sim.State.Scenario.Status == (lateStart ? "recovered" : "won"), "Charter records correct timely/recovered outcome");
                Check(sim.State.Scenario.Missed == (missFirst ? 1 : 0), "Historical missed promises are preserved accurately");
                Check(sim.FoodDays() >= 7 && sim.State.Towns[0].Population >= 6, "Successful settlement retains viable food and people");
                Check(!sim.RouteInfo().HighPassOpen && sim.RouteInfo().LowerRidgeOpen, "High Pass closes while lower road remains open");
                if (lateStart) Check(sim.State.StoryFlags.Contains("recovered"), "Recovered outcome records the correct journal story");
                string issue = StateIssue(sim);
                Check(issue == null, "Final state invariants: " + (issue ?? "valid"));
                report.Scenarios.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}: day {1}, {2}, delivered {3}, missed {4}, food {5:F1} days, people {6}.",
                    group, sim.State.Day, sim.State.Scenario.Status, sim.State.Scenario.Delivered,
                    sim.State.Scenario.Missed, sim.FoodDays(), sim.State.Towns[0].Population));
            }

            private void LongRun()
            {
                var sim = new Simulation();
                int npcs = sim.State.Buildings.FindAll(x => x.Town != 0).Count;
                double money = AccountedMoney(sim);
                for (int i = 0; i < 400; i++)
                {
                    sim.AdvanceDay();
                    string issue = StateIssue(sim);
                    Require(issue == null, "Day " + sim.State.Day + " invariants: " + (issue ?? "valid"));
                    Check(Near(AccountedMoney(sim), money), "Day " + sim.State.Day + " has no unexplained money creation/loss after construction and freight accounting");
                }
                Check(sim.State.Day == 401, "World advances through 400 days");
                int finalNpcs = sim.State.Buildings.FindAll(x => x.Town != 0).Count;
                Check(finalNpcs > npcs, "NPCs fund actual expansion over time");
                report.Scenarios.Add("Extended world: day " + sim.State.Day + ", NPC buildings " + npcs + " -> " + finalNpcs + ".");
            }

            private static string StateIssue(Simulation sim)
            {
                for (int index = 0; index < sim.State.Towns.Count; index++)
                {
                    TownState town = sim.State.Towns[index];
                    if (!Finite(town.Gold) || town.Gold < 0 || town.Population < 1) return "invalid money/population";
                    double capacity = index == 0 ? 80 : 180;
                    int workers = 0;
                    foreach (BuildingState building in sim.State.Buildings)
                        if (building.Town == index)
                        {
                            if (building.Type == "warehouse") capacity += 100;
                            workers += building.Workers;
                            if (building.Workers < 0 || building.Workers > Simulation.Definitions[building.Type].Workers) return "invalid workplace staffing";
                        }
                    if (workers > Math.Floor(town.Population * 0.6)) return "workforce over-allocated";
                    foreach (ResourceAmount stock in town.Stocks)
                    {
                        double price = town.Price(stock.Good);
                        if (!Finite(stock.Amount) || stock.Amount < 0 || !Finite(price) || price <= 0) return "invalid stock/price for " + stock.Good;
                        double reservation = sim.IncomingQuantity(index, stock.Good);
                        foreach (ContractState contract in sim.State.Contracts)
                            if (contract.Town == index && contract.Good == stock.Good && contract.Status == "active") reservation += contract.Quantity;
                        if (stock.Amount + reservation > capacity + 0.0001) return "storage/reservations overflow for " + town.Name + "/" + stock.Good;
                    }
                }
                foreach (CaravanState cargo in sim.State.Caravans)
                    if (!Finite(cargo.Quantity) || cargo.Quantity <= 0 || cargo.DaysLeft < 1 || cargo.DaysLeft > cargo.TotalDays) return "invalid active cargo";
                return null;
            }
        }

        // Unity's JSON codec can normalize a final binary digit: the observed
        // 14.780000000000001 -> 14.78 change is 1.78e-15. Only double fields may
        // differ by 1e-12 absolute; integer state, strings, booleans, types,
        // list length/order, and every public field are still checked exactly.
        private static string FirstDifference(object expected, object actual, string path)
        {
            if (ReferenceEquals(expected, actual)) return null;
            if (expected == null || actual == null) return path + ": null mismatch";
            Type type = expected.GetType();
            if (type != actual.GetType()) return path + ": type mismatch";
            if (expected is double)
            {
                double left = (double)expected, right = (double)actual;
                if (double.IsNaN(left) || double.IsNaN(right) || double.IsInfinity(left) || double.IsInfinity(right))
                    return path + ": non-finite number";
                if (Math.Abs(left - right) <= 1e-12) return null;
            }
            if (type.IsPrimitive || type.IsEnum || expected is string)
            {
                if (expected.Equals(actual)) return null;
                string wanted = expected is double ? ((double)expected).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(expected, CultureInfo.InvariantCulture);
                string got = actual is double ? ((double)actual).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(actual, CultureInfo.InvariantCulture);
                return path + ": expected " + wanted + ", actual " + got;
            }
            var expectedSequence = expected as IEnumerable;
            if (expectedSequence != null)
            {
                IEnumerator left = expectedSequence.GetEnumerator(), right = ((IEnumerable)actual).GetEnumerator();
                int index = 0;
                while (true)
                {
                    bool hasLeft = left.MoveNext(), hasRight = right.MoveNext();
                    if (hasLeft != hasRight) return path + ": list length mismatch at " + index;
                    if (!hasLeft) return null;
                    string difference = FirstDifference(left.Current, right.Current, path + "[" + index + "]");
                    if (difference != null) return difference;
                    index++;
                }
            }
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (FieldInfo field in fields)
            {
                string difference = FirstDifference(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
                if (difference != null) return difference;
            }
            return null;
        }

        // Canonical structural comparison deliberately includes every public
        // persistent field. It is not the game's serializer or save format.
        private static string Fingerprint(object value)
        {
            var text = new StringBuilder();
            AppendValue(text, value);
            return text.ToString();
        }

        private static void AppendValue(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            var stringValue = value as string;
            if (stringValue != null) { text.Append(stringValue.Length).Append(':').Append(stringValue).Append(';'); return; }
            if (value is double) { text.Append(((double)value).ToString("R", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is float) { text.Append(((float)value).ToString("R", CultureInfo.InvariantCulture)).Append(';'); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            var sequence = value as IEnumerable;
            if (sequence != null)
            {
                text.Append('[');
                foreach (object item in sequence) AppendValue(text, item);
                text.Append(']'); return;
            }
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            text.Append(type.FullName).Append('{');
            foreach (FieldInfo field in fields) { text.Append(field.Name).Append('='); AppendValue(text, field.GetValue(value)); }
            text.Append('}');
        }
    }

#if SIMULATION_VERIFICATION_CONSOLE
    // Optional portable host: compile with this symbol and reference
    // System.Runtime.Serialization. Unity builds never include this entry point.
    public static class SimulationVerificationConsole
    {
        public static int Main()
        {
            VerificationReport report = SimulationVerification.RunAll(Serialize, Deserialize, "DataContractJsonSerializer (portable host)");
            Console.WriteLine(report.ToString());
            return report.Passed ? 0 : 1;
        }

        private static string Serialize(SimulationState state)
        {
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(SimulationState));
            using (var stream = new System.IO.MemoryStream())
            {
                serializer.WriteObject(stream, state);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static SimulationState Deserialize(string json)
        {
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(SimulationState));
            using (var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (SimulationState)serializer.ReadObject(stream);
        }
    }
#endif
}
