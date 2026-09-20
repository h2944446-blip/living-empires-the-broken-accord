using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace LivingEmpires
{
    /// <summary>
    /// Deterministic First Winter Delivery economy, independent of rendering/time/UI.
    /// Towns pool household and business stock. Construction and freight are explicit
    /// sinks; trade, escrow and finite relief transfer existing money and cargo.
    /// Each good has its own capacity; route capacity is per shipment, not traffic.
    /// </summary>
    public sealed partial class Simulation
    {
        public const int SaveVersion = 3;
        public const string SaveFormat = "living-empires-unity";
        public const int WinterDay = 120;
        public const int MaxBuildings = 400;
        public const int MaxCaravans = 40;
        private const double Epsilon = 0.0001;
        public static readonly string[] Goods = { "grain", "flour", "bread", "wood", "ore", "iron", "tools", "stone" };
        public static readonly string[] StoryIds = { "opening", "first_production", "crossing_ready", "first_delivery", "missed_contract", "winter", "success", "recovery", "recovered" };
        private static readonly double[] BasePrices = { 2.0, 3.4, 2.8, 2.4, 4.0, 7.5, 12.0, 3.0 };
        private static readonly double[] TargetStocks = { 45.0, 20.0, 25.0, 45.0, 24.0, 18.0, 12.0, 25.0 };
        private static readonly string[] LaborPriority = { "farm", "lumberyard", "mill", "bakery", "mine", "smelter", "toolsmith" };
        private static readonly string[] OfferGoods = { "bread", "grain", "wood", "tools", "stone" };
        public static readonly IReadOnlyDictionary<string, BuildingDefinition> Definitions = CreateDefinitions();
        public static readonly IReadOnlyDictionary<string, BuildingDefinition> CrossingCosts =
            new ReadOnlyDictionary<string, BuildingDefinition>(new Dictionary<string, BuildingDefinition>
            {
                { "ferry", Def("ferry", "Ferry", 0, 45, R("wood", 10, "tools", 2), R(), R(), "Ready in 2 days; 12 cargo; 3 gold freight.") },
                { "bridge", Def("bridge", "Bridge", 0, 120, R("wood", 30, "stone", 12, "tools", 6), R(), R(), "Ready in 8 days; 40 cargo; no freight fee.") }
            });

        public SimulationState State { get; private set; }
        private readonly Dictionary<int, string> productionStatus = new Dictionary<int, string>();

        public Simulation()
        {
            State = new SimulationState
            {
                Version = SaveVersion, Format = SaveFormat, Day = 1, ReserveDays = 7,
                NextBuildingId = 1, NextContractId = 1, NextCaravanId = 1, Military = new MilitaryState(),
                Towns = new List<TownState>
                {
                    MakeTown("Riverhold", 450, 24, new double[] {44, 8, 24, 40, 4, 4, 12, 28}),
                    MakeTown("Ironvale", 670, 30, new double[] {35, 4, 14, 32, 95, 48, 24, 65}),
                    MakeTown("Pinewatch", 620, 28, new double[] {90, 30, 60, 110, 8, 8, 12, 35})
                },
                Buildings = new List<BuildingState>(), Caravans = new List<CaravanState>(),
                Contracts = new List<ContractState>(), Events = new List<string>(),
                Crossing = new CrossingState { Mode = "none", Pending = "", DaysLeft = 0 },
                Relations = new List<RelationState> { new RelationState { Town = 1, Trust = 50 }, new RelationState { Town = 2, Trust = 50 } },
                Scenario = new ScenarioState { Status = "active" },
                StoryFlags = new List<string>(), PendingStory = new List<string>()
            };
            AddBuilding("farm", 0, 10, 13); AddBuilding("lumberyard", 0, 12, 14);
            AddBuilding("mine", 1, 31, 8); AddBuilding("smelter", 1, 32, 8);
            AddBuilding("farm", 1, 30, 10); AddBuilding("toolsmith", 1, 33, 9);
            AddBuilding("lumberyard", 2, 31, 22); AddBuilding("farm", 2, 30, 23);
            AddBuilding("mill", 2, 32, 22); AddBuilding("bakery", 2, 33, 23);
            foreach (var town in State.Towns) foreach (var good in Goods) town.SetPrice(good, TargetPrice(town, good));
            AssignRivalWorkers(1); AssignRivalWorkers(2);
            OfferContract(1, "bread", 12); RefreshContracts(); QueueStory("opening");
            Log("First Winter Delivery: mill, bakery, warehouse, a crossing, two timely contracts and seven food days before day 120.");
        }

        private static IReadOnlyDictionary<string, BuildingDefinition> CreateDefinitions()
        {
            var values = new[]
            {
                Def("farm", "Farm", 4, 45, R("wood",10,"stone",4), R(), R("grain",7), "4 workers harvest 7 grain/day; 55% yield in winter."),
                Def("lumberyard", "Lumberyard", 3, 40, R("wood",8,"stone",3), R(), R("wood",4), "3 workers produce 4 wood/day for construction and industry."),
                Def("mill", "Mill", 3, 65, R("wood",16,"stone",6), R("grain",4), R("flour",3.5), "3 workers turn 4 grain into 3.5 flour/day."),
                Def("bakery", "Bakery", 3, 70, R("wood",14,"stone",8), R("flour",3,"wood",0.5), R("bread",6), "3 workers turn 3 flour and 0.5 wood into 6 bread/day."),
                Def("mine", "Mine", 4, 85, R("wood",18,"tools",3), R(), R("ore",3,"stone",1.5), "4 workers extract 3 ore and 1.5 stone/day on the western ridge."),
                Def("smelter", "Smelter", 3, 90, R("wood",16,"stone",12,"tools",2), R("ore",2,"wood",1), R("iron",1.5), "3 workers smelt 2 ore with 1 wood into 1.5 iron/day."),
                Def("toolsmith", "Toolsmith", 3, 95, R("wood",18,"stone",10,"tools",2), R("iron",1,"wood",1), R("tools",1.2), "3 workers turn 1 iron and 1 wood into 1.2 tools/day."),
                Def("house", "House", 0, 35, R("wood",12,"stone",4), R(), R(), "Welcomes 6 residents. Residents need food; no passive tax income."),
                Def("warehouse", "Warehouse", 0, 55, R("wood",12,"stone",2), R(), R(), "Adds 100 storage capacity for each good. No assigned workers needed."),
                Def("barracks", "Muster Yard", 0, 90, R("wood",24,"stone",12,"tools",4), R(), R(), "Trains four-person squads from available workers in The Toll War chapter.")
            };
            return new ReadOnlyDictionary<string, BuildingDefinition>(values.ToDictionary(x => x.Kind));
        }

        private static BuildingDefinition Def(string kind, string name, int workers, double gold,
            ResourceAmount[] cost, ResourceAmount[] inputs, ResourceAmount[] outputs, string description)
        { return new BuildingDefinition(kind, name, workers, gold, cost, inputs, outputs, description); }

        private static ResourceAmount[] R(params object[] pairs)
        {
            var result = new ResourceAmount[pairs.Length / 2];
            for (int i = 0; i < result.Length; ++i)
                result[i] = new ResourceAmount((string)pairs[i * 2], Convert.ToDouble(pairs[i * 2 + 1], CultureInfo.InvariantCulture));
            return result;
        }

        private static TownState MakeTown(string name, double gold, int population, double[] stocks)
        {
            var town = new TownState { Name = name, Gold = gold, Population = population, FoodRatio = 1,
                Stocks = new List<ResourceAmount>(), Prices = new List<ResourceAmount>() };
            for (int i = 0; i < Goods.Length; ++i)
            {
                town.Stocks.Add(new ResourceAmount(Goods[i], stocks[i]));
                town.Prices.Add(new ResourceAmount(Goods[i], BasePrices[i]));
            }
            return town;
        }

        public void AdvanceDay()
        {
            ++State.Day;
            if (State.Day >= WinterDay)
            {
                State.Scenario.Winter = true; QueueStory("winter");
                if (State.Scenario.Status == "active") QueueStory("recovery");
            }
            AdvanceCrossing(); ArriveCaravans(); ExpireContracts();
            AssignRivalWorkers(1); AssignRivalWorkers(2); Produce(); ConsumeFood(); MilitaryUpkeep();
            foreach (var town in State.Towns) foreach (var good in Goods)
                town.SetPrice(good, Snap(town.Price(good) + (TargetPrice(town, good) - town.Price(good)) * 0.16));
            if (State.Day % 7 == 0) { RivalTrade(); RefreshContracts(); }
            if (State.Day % 9 == 0) RivalExpand(1);
            if (State.Day % 13 == 0) RivalExpand(2);
            RecoveryOffer(); CheckScenario();
        }

        public string ProductionStatus(int buildingId)
        { string value; return productionStatus.TryGetValue(buildingId, out value) ? value : "Ready"; }

        private void Produce()
        {
            productionStatus.Clear();
            foreach (var building in State.Buildings)
            {
                var town = State.Towns[building.Town]; var definition = Definitions[building.Type];
                if (definition.Outputs.Count == 0)
                {
                    productionStatus[building.Id] = building.Type == "warehouse" ? "Storage +100 per good" : building.Type == "barracks" ? "Recruit and equip militia in Army" : "Housing 6 residents";
                    continue;
                }
                double scale = (double)building.Workers / definition.Workers;
                if (scale <= 0) { productionStatus[building.Id] = "No workers assigned"; continue; }
                double yieldScale = scale * (State.Day >= WinterDay && building.Type == "farm" ? 0.55 : 1.0);
                string blocked = "";
                foreach (var input in definition.Inputs)
                    if (town.Stock(input.Good) + Epsilon < input.Amount * scale) { blocked = input.Good; break; }
                if (blocked.Length != 0) { productionStatus[building.Id] = "Waiting for " + blocked; continue; }
                foreach (var output in definition.Outputs)
                {
                    double used = definition.Inputs.Where(x => x.Good == output.Good).Sum(x => x.Amount) * scale;
                    if (town.Stock(output.Good) + IncomingQuantity(building.Town, output.Good) + PromisedSpace(building.Town, output.Good)
                        + output.Amount * yieldScale - used > TownCapacity(building.Town) + Epsilon)
                    { blocked = output.Good; break; }
                }
                if (blocked.Length != 0) { productionStatus[building.Id] = "Storage full: " + blocked; continue; }
                foreach (var input in definition.Inputs) town.SetStock(input.Good, Math.Max(0, town.Stock(input.Good) - input.Amount * scale));
                foreach (var output in definition.Outputs) town.SetStock(output.Good, town.Stock(output.Good) + output.Amount * yieldScale);
                productionStatus[building.Id] = F("Producing at {0:0}%", scale * 100);
                if (building.Town == 0 && building.Type == "bakery") QueueStory("first_production");
            }
        }

        private void ConsumeFood()
        {
            for (int townId = 0; townId < State.Towns.Count; ++townId)
            {
                var town = State.Towns[townId]; double required = town.Population * 0.12;
                double bread = Math.Min(town.Stock("bread"), required);
                town.SetStock("bread", Math.Max(0, town.Stock("bread") - bread));
                double grain = Math.Min(town.Stock("grain"), required - bread);
                town.SetStock("grain", Math.Max(0, town.Stock("grain") - grain));
                double oldRatio = town.FoodRatio;
                town.FoodRatio = (bread + grain) / Math.Max(required, 0.01);
                if (town.FoodRatio < 0.99 && oldRatio >= 0.99) Log(town.Name + " faces a food shortage. Import grain or expand farming.");
                else if (town.FoodRatio >= 0.99 && oldRatio < 0.99) Log("Food supplies have recovered in " + town.Name + ".");
                if (town.FoodRatio < 0.5 && State.Day % 5 == 0 && town.Population > 6
                    && (townId != 0 || Math.Floor((town.Population - 1) * .6) >= MilitaryReservedWorkers()))
                {
                    --town.Population; TrimWorkers(townId);
                    if (townId == 0) Log("A hungry household member left Riverhold. Restore the food supply.");
                }
            }
        }

        private static double TargetPrice(TownState town, string good)
        {
            int index = Array.IndexOf(Goods, good);
            double target = TargetStocks[index] * Math.Max(0.6, town.Population / 24.0);
            double scarcity = Clamp((target - town.Stock(good)) / Math.Max(target, 1), -0.65, 1.5);
            return Snap(BasePrices[index] * (1 + 0.7 * scarcity));
        }

        public string PlaceBuilding(string kind, int x, int z)
        {
            string problem = PlacementProblem(kind, x, z);
            if (problem.Length != 0) return problem;
            var definition = Definitions[kind];
            PayCost(State.Towns[0], definition); AddBuilding(kind, 0, x, z);
            if (kind == "house") State.Towns[0].Population += 6;
            Log("Riverhold built a " + definition.Name.ToLowerInvariant() + "."); CheckScenario(); return "";
        }

        /// <summary>Pure validation for UI placement previews; never spends or assigns anything.</summary>
        public string PlacementProblem(string kind, int x, int z)
        {
            if (kind == null || !Definitions.ContainsKey(kind)) return "Unknown building type.";
            if (x < 4 || x > 17 || z < 5 || z > 23) return "Build inside Riverhold's land: west of the river, within the marked border.";
            if (kind == "mine" && x > 7) return "Mines need rocky ground on the gray western ridge.";
            if (TileOccupied(x, z)) return "This tile already contains a building.";
            if (Military != null && Military.Enabled && Military.Squads.Any(s => GridX(s.X) == x && GridZ(s.Z) == z)) return "Troops occupy this plot. Move them before building.";
            if (State.Buildings.Count >= MaxBuildings) return "The prototype building limit has been reached.";
            return CostProblem(State.Towns[0], Definitions[kind]);
        }

        private static string CostProblem(TownState town, BuildingDefinition definition)
        {
            if (town.Gold + Epsilon < definition.GoldCost) return F("Need {0:0} gold; only {1:0.0} available.", definition.GoldCost, town.Gold);
            foreach (var cost in definition.Cost)
                if (town.Stock(cost.Good) + Epsilon < cost.Amount) return F("Need {0:0} {1}; only {2:0.0} available.", cost.Amount, cost.Good, town.Stock(cost.Good));
            return "";
        }

        private static void PayCost(TownState town, BuildingDefinition definition)
        {
            town.Gold = Math.Max(0, town.Gold - definition.GoldCost);
            foreach (var cost in definition.Cost) town.SetStock(cost.Good, Math.Max(0, town.Stock(cost.Good) - cost.Amount));
        }

        private void AddBuilding(string kind, int townId, int x, int z)
        {
            int available = Math.Max(0, TownWorkforce(townId) - TownWorkersUsed(townId));
            var building = new BuildingState { Id = State.NextBuildingId++, Type = kind, Town = townId, X = x, Z = z,
                Workers = Math.Min(available, Definitions[kind].Workers) };
            State.Buildings.Add(building); productionStatus[building.Id] = "Ready";
        }
        private bool TileOccupied(int x, int z) { return State.Buildings.Any(b => b.X == x && b.Z == z); }
        public int WorkforceTotal() { return TownWorkforce(0); }
        public int WorkersUsed() { return TownWorkersUsed(0); }
        private int TownWorkforce(int town) { return Math.Max(0, (int)Math.Floor(State.Towns[town].Population * 0.6) - (town == 0 ? MilitaryReservedWorkers() : 0)); }
        private int TownWorkersUsed(int town) { return State.Buildings.Where(b => b.Town == town).Sum(b => b.Workers); }

        public string SetWorkers(int buildingId, int count)
        {
            var building = State.Buildings.Find(b => b.Id == buildingId);
            if (building == null) return "Building not found.";
            if (building.Town != 0) return "Only Riverhold's workers can be reassigned.";
            if (count < 0 || count > Definitions[building.Type].Workers) return "Worker count must be between zero and the building's requirement.";
            if (WorkersUsed() - building.Workers + count > WorkforceTotal()) return "Not enough available workers. Reassign workers or build housing.";
            building.Workers = count; productionStatus[buildingId] = "Assignment updated"; return "";
        }

        private void TrimWorkers(int town)
        {
            int excess = TownWorkersUsed(town) - TownWorkforce(town);
            for (int i = State.Buildings.Count - 1; i >= 0 && excess > 0; --i)
            {
                var building = State.Buildings[i]; if (building.Town != town) continue;
                int removed = Math.Min(excess, building.Workers); building.Workers -= removed; excess -= removed;
            }
        }

        private void AssignRivalWorkers(int town)
        {
            int available = TownWorkforce(town);
            foreach (var building in State.Buildings) if (building.Town == town) building.Workers = 0;
            foreach (var kind in LaborPriority) foreach (var building in State.Buildings)
                if (building.Town == town && building.Type == kind)
                { building.Workers = Math.Min(available, Definitions[kind].Workers); available -= building.Workers; }
        }

        public double FoodDays() { return (State.Towns[0].Stock("grain") + State.Towns[0].Stock("bread")) / Math.Max(0.12 * State.Towns[0].Population, 0.12); }
        public double StorageCapacity() { return TownCapacity(0); }
        private double TownCapacity(int town) { return (town == 0 ? 80 : 180) + 100 * State.Buildings.Count(b => b.Town == town && b.Type == "warehouse"); }
        public double IncomingQuantity(int town, string good) { return State.Caravans.Where(c => c.To == town && c.Good == good).Sum(c => c.Quantity); }
        private double PromisedSpace(int town, string good, int excluding = -1)
        { return State.Contracts.Where(c => c.Status == "active" && c.Town == town && c.Good == good && c.Id != excluding).Sum(c => c.Quantity); }

        public string SetReserveDays(int value)
        {
            if (value < 0 || value > 30) return "Food reserve must be between 0 and 30 days.";
            State.ReserveDays = value; return "";
        }
        private string ReserveProblem(string good, double quantity)
        {
            if ((good == "grain" || good == "bread") && FoodDays() - quantity / (State.Towns[0].Population * 0.12) + Epsilon < State.ReserveDays)
                return F("This export would break the {0}-day food reserve. Produce more food or change the reserve.", State.ReserveDays);
            return "";
        }

        public string ChooseCrossing(string mode)
        {
            if (mode == null || !CrossingCosts.ContainsKey(mode)) return "Choose ferry or bridge.";
            if (State.Crossing.Pending.Length != 0) return "A crossing is already under construction.";
            if (State.Crossing.Mode == mode || State.Crossing.Mode == "bridge") return "That crossing is already available.";
            string problem = CostProblem(State.Towns[0], CrossingCosts[mode]); if (problem.Length != 0) return problem;
            PayCost(State.Towns[0], CrossingCosts[mode]); State.Crossing.Pending = mode;
            State.Crossing.DaysLeft = mode == "ferry" ? 2 : 8;
            Log(F("{0} work begins; ready in {1} days.", CrossingCosts[mode].Name, State.Crossing.DaysLeft)); return "";
        }
        private void AdvanceCrossing()
        {
            if (State.Crossing.Pending.Length == 0) return;
            if (--State.Crossing.DaysLeft != 0) return;
            State.Crossing.Mode = State.Crossing.Pending; State.Crossing.Pending = "";
            QueueStory("crossing_ready"); Log("The " + State.Crossing.Mode + " is ready. Lower Ridge Road stays open through winter.");
        }

        public RouteInfoData RouteInfo()
        {
            string mode = State.Crossing.Mode; bool ready = mode != "none";
            return new RouteInfoData
            {
                Name = !ready ? "Crossing required" : (mode == "ferry" ? "Ferry · Lower Ridge Road" : "Shared Bridge · Lower Ridge Road"),
                Capacity = !ready ? 0 : (mode == "ferry" ? 12 : 40), Fee = mode == "ferry" ? 3 : 0,
                Days = (mode == "ferry" ? 6 : 4) + (State.Day >= WinterDay ? 3 : 0), Ready = ready,
                Pending = State.Crossing.Pending, DaysLeft = State.Crossing.DaysLeft,
                HighPassOpen = State.Day < WinterDay, LowerRidgeOpen = true
            };
        }
        public RelationState Relation(int town) { return State.Relations.Find(r => r.Town == town); }
        public ContractState Contract(int id) { return State.Contracts.Find(c => c.Id == id); }
        public double TradeQuote(int town, string good, bool buy)
        {
            if (town < 1 || town > 2 || !KnownGood(good)) return 0;
            double margin = (50 - Relation(town).Trust) * 0.003;
            return Snap(Math.Max(0.01, State.Towns[town].Price(good) * (buy ? 1 + margin : 1 - margin)));
        }

        public string DispatchTrade(int town, string good, double quantity, bool buy)
        {
            if (town < 1 || town > 2) return "Choose Ironvale or Pinewatch as a trading partner.";
            if (!KnownGood(good)) return "Unknown trade good.";
            if (!Finite(quantity) || quantity < 0.01) return "Choose a trade quantity of at least 0.01.";
            var route = RouteInfo(); if (!route.Ready) return "Build a ferry or bridge and wait until it is ready.";
            if (quantity > route.Capacity + Epsilon) return F("This shipment exceeds the crossing capacity of {0:0}.", route.Capacity);
            if (!buy) { string problem = ReserveProblem(good, quantity); if (problem.Length != 0) return problem; }
            return SendCargo(buy ? town : 0, buy ? 0 : town, good, quantity, TradeQuote(town, good, buy), true);
        }

        private string SendCargo(int sellerId, int buyerId, string good, double quantity, double unitPrice, bool playerOrder)
        {
            if (State.Caravans.Count >= MaxCaravans) return "All caravan slots are busy. Wait for a delivery.";
            var seller = State.Towns[sellerId]; var buyer = State.Towns[buyerId]; double available = seller.Stock(good);
            if (available + Epsilon < quantity) return F("{0} has only {1:0.0} {2} available.", seller.Name, available, good);
            if (buyer.Stock(good) + IncomingQuantity(buyerId, good) + PromisedSpace(buyerId, good) + quantity > TownCapacity(buyerId) + Epsilon)
                return buyer.Name + " needs more storage for " + good + ", including incoming cargo.";
            double fee = playerOrder ? RouteInfo().Fee : 0; double total = quantity * unitPrice;
            if (buyer.Gold + Epsilon < total + (buyerId == 0 ? fee : 0)) return buyer.Name + " cannot afford this trade.";
            if (playerOrder && sellerId == 0 && seller.Gold + total + Epsilon < fee) return "Riverhold cannot afford the ferry freight.";
            seller.SetStock(good, Math.Max(0, available - quantity)); buyer.Gold = Math.Max(0, buyer.Gold - total); seller.Gold += total;
            if (playerOrder) { State.Towns[0].Gold = Math.Max(0, State.Towns[0].Gold - fee); State.FreightPaid += fee; }
            int duration = playerOrder ? RouteInfo().Days : 5 + (State.Day >= WinterDay ? 3 : 0);
            AppendCargo(sellerId, buyerId, good, quantity, duration, playerOrder ? State.Crossing.Mode : "lower_ridge_road", playerOrder, -1, fee);
            if (playerOrder) Log(F("{0:0.0} {1}: {2} → {3}. Paid {4:0.0} gold; arrives in {5} days.", quantity, good, seller.Name, buyer.Name, total, duration));
            return "";
        }

        private void AppendCargo(int seller, int buyer, string good, double quantity, int duration, string route, bool playerOrder, int contractId = -1, double freight = 0)
        {
            State.Caravans.Add(new CaravanState { Id = State.NextCaravanId++, From = seller, To = buyer, Good = good, Quantity = quantity,
                DaysLeft = duration, TotalDays = duration, Route = route, PlayerOrder = playerOrder, ContractId = contractId, Freight = freight });
        }

        private void ArriveCaravans()
        {
            for (int i = State.Caravans.Count - 1; i >= 0; --i)
            {
                var cargo = State.Caravans[i]; if (--cargo.DaysLeft > 0) continue;
                var destination = State.Towns[cargo.To];
                if (destination.Stock(cargo.Good) + cargo.Quantity > TownCapacity(cargo.To) + Epsilon) { cargo.DaysLeft = 1; continue; }
                destination.SetStock(cargo.Good, destination.Stock(cargo.Good) + cargo.Quantity);
                if (cargo.ContractId >= 1) CompleteContract(cargo.ContractId);
                if (cargo.From == 0 || cargo.To == 0)
                { ++State.CompletedTrades; Log(F("Caravan delivered {0:0.0} {1} to {2}.", cargo.Quantity, cargo.Good, destination.Name)); }
                State.Caravans.RemoveAt(i);
            }
        }

        private void OfferContract(int town, string good, double quantity)
        {
            double trust = Relation(town).Trust;
            double price = Snap(TradeQuote(town, good, false) * (trust >= 40 ? 1.12 : 1.02));
            double reward = trust >= 40 ? 10 : 5;
            if (State.Towns[town].Gold < quantity * price + reward) return;
            State.Contracts.Add(new ContractState { Id = State.NextContractId++, Town = town, Good = good, Quantity = quantity,
                UnitPrice = price, Deadline = State.Day + 28, Status = "offered", Reward = reward, Escrow = 0 });
        }

        private void RefreshContracts()
        {
            State.Contracts.RemoveAll(c => c.Status == "offered" && State.Day > c.Deadline);
            while (State.Contracts.Count > 120)
            {
                int index = State.Contracts.FindIndex(c => (c.Status == "fulfilled" || c.Status == "failed") && c.Escrow == 0);
                if (index < 0) break; State.Contracts.RemoveAt(index);
            }
            for (int town = 1; town <= 2; ++town)
            {
                if (State.Contracts.Any(c => c.Town == town && Outstanding(c.Status))) continue;
                string bestGood = ""; double bestShortage = 0;
                foreach (string good in OfferGoods)
                {
                    double target = TargetStocks[Array.IndexOf(Goods, good)] * 1.4;
                    double committed = State.Towns[town].Stock(good) + IncomingQuantity(town, good);
                    double shortage = (target - committed) / target;
                    if (shortage > bestShortage && committed + 12 <= TownCapacity(town)) { bestShortage = shortage; bestGood = good; }
                }
                if (bestGood.Length != 0) OfferContract(town, bestGood, 12);
            }
        }

        public string AcceptContract(int id, bool negotiate = false)
        {
            var contract = Contract(id);
            if (contract == null || contract.Status != "offered") return "Choose an available contract offer.";
            if (State.Day > contract.Deadline) return "This offer has expired. New shortage offers arrive every seven days.";
            double trust = Relation(contract.Town).Trust;
            if (negotiate && trust < 40) return "Trust is too low to negotiate. Fulfill a delivery to repair relations.";
            double price = Snap(contract.UnitPrice * (negotiate ? 1.08 : 1));
            double escrow = contract.Quantity * price + contract.Reward; var partner = State.Towns[contract.Town];
            if (partner.Stock(contract.Good) + IncomingQuantity(contract.Town, contract.Good) + PromisedSpace(contract.Town, contract.Good) + contract.Quantity > TownCapacity(contract.Town) + Epsilon)
                return "The partner needs storage space before accepting this promise.";
            if (partner.Gold + Epsilon < escrow) return "The partner cannot currently fund this promise.";
            partner.Gold = Math.Max(0, partner.Gold - escrow); contract.UnitPrice = price; contract.Escrow = escrow;
            contract.Deadline = State.Day + (trust < 40 ? 20 : 28) - (negotiate ? 3 : 0); contract.Status = "active";
            Log(F("Contract {0} accepted: deliver {1:0} {2} to {3} by day {4}. Payment held in escrow.", id, contract.Quantity, contract.Good, partner.Name, contract.Deadline));
            return "";
        }

        public string DispatchContract(int id)
        {
            var contract = Contract(id);
            if (contract == null || contract.Status != "active") return "Choose an accepted contract that has not been dispatched.";
            if (State.Day > contract.Deadline) return "This contract has expired.";
            var route = RouteInfo(); if (!route.Ready) return "A completed ferry or bridge is required for contract cargo.";
            if (contract.Quantity > route.Capacity + Epsilon) return "The crossing cannot carry this contract in one shipment.";
            if (State.Caravans.Count >= MaxCaravans) return "All caravan slots are busy.";
            var player = State.Towns[0]; var partner = State.Towns[contract.Town];
            if (player.Stock(contract.Good) + Epsilon < contract.Quantity) return F("Need {0:0} {1} in Riverhold to dispatch this contract.", contract.Quantity, contract.Good);
            string reserveProblem = ReserveProblem(contract.Good, contract.Quantity); if (reserveProblem.Length != 0) return reserveProblem;
            if (partner.Stock(contract.Good) + IncomingQuantity(contract.Town, contract.Good) + PromisedSpace(contract.Town, contract.Good, id) + contract.Quantity > TownCapacity(contract.Town) + Epsilon)
                return "The partner's storage cannot accept this cargo yet.";
            if (player.Gold + Epsilon < route.Fee) return F("Need {0:0} gold for ferry freight; contract payment arrives with delivery.", route.Fee);
            player.SetStock(contract.Good, Math.Max(0, player.Stock(contract.Good) - contract.Quantity));
            player.Gold = Math.Max(0, player.Gold - route.Fee); State.FreightPaid += route.Fee; contract.Status = "in_transit";
            AppendCargo(0, contract.Town, contract.Good, contract.Quantity, route.Days, State.Crossing.Mode, true, id, route.Fee);
            Log(F("Contract {0} departs; arrival day {1}, deadline {2}.", id, State.Day + route.Days, contract.Deadline)); return "";
        }

        private void FailContract(ContractState contract)
        {
            if (contract.Status != "active" && contract.Status != "in_transit") return;
            double refund = contract.Status == "active" ? contract.Escrow : contract.Reward;
            State.Towns[contract.Town].Gold += refund; contract.Escrow = Math.Max(0, contract.Escrow - refund); contract.Status = "failed";
            var relation = Relation(contract.Town); relation.Trust = Math.Max(0, relation.Trust - 12); ++relation.Missed; ++State.Scenario.Missed;
            QueueStory("missed_contract"); Log(F("Contract {0} missed its deadline. Trust fell; new promises can rebuild it.", contract.Id));
        }
        private void ExpireContracts() { foreach (var contract in State.Contracts) if (State.Day > contract.Deadline) FailContract(contract); }
        private void CompleteContract(int id)
        {
            var contract = Contract(id); if (contract == null) return;
            if (State.Day > contract.Deadline) FailContract(contract);
            State.Towns[0].Gold += contract.Escrow; contract.Escrow = 0;
            if (contract.Status != "in_transit") return;
            contract.Status = "fulfilled"; var relation = Relation(contract.Town);
            relation.Trust = Math.Min(100, relation.Trust + 8); ++relation.Fulfilled; ++State.Scenario.Delivered;
            QueueStory("first_delivery"); Log(F("Contract {0} arrived on time. Payment and reward released; trust increased.", id));
        }

        public string RequestRelief()
        {
            if (State.Scenario.RecoveryUsed) return "Pinewatch's one relief contribution has already been used.";
            var cargo = new List<ResourceAmount>(); var donor = State.Towns[2]; var player = State.Towns[0];
            foreach (var gift in R("grain",20,"wood",16,"stone",4,"tools",2))
            {
                double amount = Math.Min(gift.Amount, donor.Stock(gift.Good));
                amount = Math.Min(amount, Math.Max(0, StorageCapacity() - player.Stock(gift.Good) - IncomingQuantity(0, gift.Good)));
                if (amount >= 0.01) cargo.Add(new ResourceAmount(gift.Good, amount));
            }
            double gold = Math.Min(40, donor.Gold);
            if (cargo.Count == 0 && gold <= 0) return "Pinewatch has no relief available, or Riverhold has no room for it.";
            if (State.Caravans.Count + cargo.Count > MaxCaravans) return "Wait for caravan space before requesting the relief convoy.";
            donor.Gold = Math.Max(0, donor.Gold - gold); player.Gold += gold;
            foreach (var gift in cargo)
            {
                donor.SetStock(gift.Good, Math.Max(0, donor.Stock(gift.Good) - gift.Amount));
                AppendCargo(2, 0, gift.Good, gift.Amount, 5 + (State.Day >= WinterDay ? 3 : 0), "lower_ridge_relief", true);
            }
            State.Scenario.RecoveryUsed = true; QueueStory("recovery");
            Log(F("Pinewatch contributed {0:0} gold and sent finite relief by mule along Lower Ridge Road.", gold)); return "";
        }

        public List<ObjectiveRow> ObjectiveRows()
        {
            var built = new HashSet<string>(State.Buildings.Where(b => b.Town == 0).Select(b => b.Type));
            return new List<ObjectiveRow>
            {
                new ObjectiveRow("Build a mill", built.Contains("mill")), new ObjectiveRow("Build a bakery", built.Contains("bakery")),
                new ObjectiveRow("Build a warehouse", built.Contains("warehouse")), new ObjectiveRow("Complete a ferry or bridge", RouteInfo().Ready),
                new ObjectiveRow(F("Deliver two contracts on time ({0}/2)", State.Scenario.Delivered), State.Scenario.Delivered >= 2),
                new ObjectiveRow(F("Keep seven food days ({0:0.0}/7)", FoodDays()), FoodDays() + Epsilon >= 7)
            };
        }
        private void CheckScenario()
        {
            if (State.Scenario.Status != "active" || ObjectiveRows().Any(o => !o.Done)) return;
            State.Scenario.Status = State.Day < WinterDay ? "won" : "recovered";
            QueueStory(State.Day < WinterDay ? "success" : "recovered");
            Log(State.Day < WinterDay ? "First Winter Delivery complete before winter." : "Riverhold has recovered. The delivery network and food reserve are secure.");
        }
        private void QueueStory(string id)
        {
            if (State.StoryFlags.Contains(id)) return;
            State.StoryFlags.Add(id); State.PendingStory.Add(id);
        }
        public void AcknowledgeStory(string id) { State.PendingStory.Remove(id); }

        private void RecoveryOffer()
        {
            if (State.Scenario.Status != "active" || State.Scenario.Delivered >= 2 || State.Contracts.Any(c => Outstanding(c.Status))) return;
            for (int town = 1; town <= 2; ++town)
            {
                double free = TownCapacity(town) - State.Towns[town].Stock("bread") - IncomingQuantity(town, "bread");
                double quantity = Math.Min(8, Math.Floor(free)); if (quantity < 4) continue;
                int count = State.Contracts.Count; OfferContract(town, "bread", quantity);
                if (State.Contracts.Count <= count) continue;
                Log(F("{0} offers a funded public-reserve order for {1:0} bread.", State.Towns[town].Name, quantity)); return;
            }
        }

        private void RivalTrade()
        {
            for (int buyerId = 1; buyerId <= 2; ++buyerId)
            {
                int sellerId = buyerId == 1 ? 2 : 1; var buyer = State.Towns[buyerId]; var seller = State.Towns[sellerId];
                foreach (string good in new[] { "grain", "wood", "iron", "tools", "stone" })
                {
                    double target = TargetStocks[Array.IndexOf(Goods, good)] * 0.65;
                    double shortage = target - buyer.Stock(good); double surplus = seller.Stock(good) - target;
                    if (shortage < 5 || surplus < 5 || IncomingQuantity(buyerId, good) > 0) continue;
                    string result = SendCargo(sellerId, buyerId, good, Math.Min(16, Math.Min(shortage, surplus)), seller.Price(good), false);
                    if (result.Length != 0) continue;
                    Log(buyer.Name + " secured a " + good + " shipment from " + seller.Name + "."); break;
                }
            }
        }

        private void RivalExpand(int townId)
        {
            var town = State.Towns[townId]; var own = State.Buildings.Where(b => b.Town == townId).ToList();
            if (own.Count >= 16 || State.Buildings.Count >= MaxBuildings) return;
            Func<string, int> count = kind => own.Count(b => b.Type == kind); var priorities = new List<string>();
            if (town.Stock("grain") + town.Stock("bread") < town.Population * 1.5) priorities.Add("farm");
            if (town.Stock("wood") < 55 && count("lumberyard") < 2) priorities.Add("lumberyard");
            if (town.Stock("stone") < 22 && count("mine") == 0) priorities.Add("mine");
            if (count("mill") == 0) priorities.Add("mill");
            if (count("mill") > 0 && count("bakery") == 0) priorities.Add("bakery");
            if (count("mine") == 0) priorities.Add("mine");
            if (count("mine") > 0 && count("smelter") == 0) priorities.Add("smelter");
            if (count("smelter") > 0 && count("toolsmith") == 0) priorities.Add("toolsmith");
            if (count("house") < 2 && town.Stock("grain") + town.Stock("bread") > 90) priorities.Add("house");
            foreach (string kind in priorities)
            {
                var definition = Definitions[kind]; if (CostProblem(town, definition).Length != 0) continue;
                int baseZ = townId == 1 ? 5 : 19;
                for (int offset = 0; offset < 36; ++offset)
                {
                    int x = 28 + offset % 6; int z = baseZ + offset / 6; if (TileOccupied(x, z)) continue;
                    PayCost(town, definition); AddBuilding(kind, townId, x, z); if (kind == "house") town.Population += 6;
                    Log(town.Name + " expanded with a " + definition.Name.ToLowerInvariant() + "."); return;
                }
            }
        }

        private void Log(string message)
        {
            State.Events.Insert(0, F("Day {0} · {1}", State.Day, message));
            while (State.Events.Count > 6) State.Events.RemoveAt(State.Events.Count - 1);
        }

        public SimulationState Snapshot() { return State.Copy(); }

        // Deserialize into a separate SimulationState, then call Restore. Validation
        // never touches live state, and the accepted object is cloned to prevent aliases.
        public string Restore(SimulationState data)
        {
            string problem = Validate(data);
            if (problem.Length != 0) return problem;
            State = data.Copy(); MigrateMilitaryState(); productionStatus.Clear();
            foreach (var building in State.Buildings) productionStatus[building.Id] = "Ready";
            return "";
        }

        // Full structural/cross-reference validation is implemented below.
        public static string Validate(SimulationState data)
        {
            if (data == null) return "The save contains no simulation state.";
            if (data.Version != SaveVersion || data.Format != SaveFormat)
                return "This save uses an unsupported format. Godot saves are separate; your old save is unchanged.";
            if (!InRange(data.Day, 1, 10000000) || !InRange(data.CompletedTrades, 0, 10000000)) return "The save has an invalid day or trade count.";
            if (data.Towns == null || data.Towns.Count != 3) return "The save must contain three towns.";
            if (data.Buildings == null || data.Buildings.Count > MaxBuildings) return "The save has an invalid building list.";
            if (data.Caravans == null || data.Caravans.Count > MaxCaravans) return "The save has an invalid caravan list.";
            if (data.Events == null || data.Events.Count > 6 || data.Events.Any(e => e == null || e.Length > 512)) return "The save has invalid event text.";
            foreach (var town in data.Towns)
            {
                if (town == null || string.IsNullOrEmpty(town.Name) || town.Name.Length > 40) return "The save has an invalid town name.";
                if (!Bounded(town.Gold, 0, 1e12) || !InRange(town.Population, 1, 1000000) || !Bounded(town.FoodRatio, 0, 1.0001))
                    return "The save has invalid town finances, population, or food data.";
                if (!ValidResources(town.Stocks, 0, 1e12) || !ValidResources(town.Prices, 0.01, 1e6)) return "The save has invalid stock or market data.";
            }
            var buildingIds = new HashSet<int>(); var tiles = new HashSet<string>(); var assigned = new int[3]; int largestBuilding = 0;
            foreach (var building in data.Buildings)
            {
                if (building == null || !InRange(building.Id, 1, 10000000) || building.Type == null || !Definitions.ContainsKey(building.Type))
                    return "The save has an invalid building identifier or type.";
                if (!InRange(building.Town, 0, 2) || !InRange(building.X, 0, 39) || !InRange(building.Z, 0, 29)) return "The save has an invalid building location.";
                if (!InRange(building.Workers, 0, Definitions[building.Type].Workers)) return "The save has an invalid worker assignment.";
                if (!buildingIds.Add(building.Id) || !tiles.Add(building.X + "," + building.Z)) return "The save contains duplicate buildings.";
                assigned[building.Town] += building.Workers; largestBuilding = Math.Max(largestBuilding, building.Id);
            }
            for (int town = 0; town < 3; ++town)
                if (assigned[town] > Math.Floor(data.Towns[town].Population * 0.6)) return "The save assigns more workers than the population provides.";
            if (!InRange(data.NextBuildingId, largestBuilding + 1, 10000001)) return "The save has an invalid next building identifier.";
            foreach (var cargo in data.Caravans)
            {
                if (cargo == null || !InRange(cargo.From, 0, 2) || !InRange(cargo.To, 0, 2) || cargo.From == cargo.To) return "The save has an invalid caravan route.";
                if (!KnownGood(cargo.Good) || !Bounded(cargo.Quantity, 0.01, 1e12)) return "The save has invalid caravan cargo.";
                if (!InRange(cargo.TotalDays, 1, 30) || !InRange(cargo.DaysLeft, 1, cargo.TotalDays)) return "The save has an invalid caravan arrival time.";
                if (!OneOf(cargo.Route, "ferry", "bridge", "lower_ridge_road", "lower_ridge_relief")) return "The save has an unknown caravan route.";
                if (!InRange(cargo.ContractId, -1, 10000000) || cargo.ContractId == 0 || !Bounded(cargo.Freight, 0, 3)) return "The save has invalid caravan order data.";
            }
            string problem = ValidateScenario(data); if (problem.Length != 0) return problem;
            problem = ValidateMilitary(data); if (problem.Length != 0) return problem;
            for (int town = 0; town < 3; ++town)
            {
                double capacity = (town == 0 ? 80 : 180) + 100 * data.Buildings.Count(b => b.Town == town && b.Type == "warehouse");
                foreach (string good in Goods)
                {
                    double committed = data.Towns[town].Stock(good) + data.Caravans.Where(c => c.To == town && c.Good == good).Sum(c => c.Quantity)
                        + data.Contracts.Where(c => c.Status == "active" && c.Town == town && c.Good == good).Sum(c => c.Quantity);
                    if (committed > capacity + Epsilon) return "The save exceeds storage capacity, including reserved incoming cargo.";
                }
            }
            return "";
        }

        private static string ValidateScenario(SimulationState data)
        {
            if (!InRange(data.ReserveDays, 0, 30) || !Bounded(data.FreightPaid, 0, 1e12)) return "The save has invalid reserve or freight data.";
            var crossing = data.Crossing;
            if (crossing == null || !OneOf(crossing.Mode, "none", "ferry", "bridge") || !OneOf(crossing.Pending, "", "ferry", "bridge")) return "The save has an invalid crossing mode.";
            if (!InRange(crossing.DaysLeft, 0, 8)) return "The save has an invalid crossing construction time.";
            if ((crossing.Pending.Length == 0 && crossing.DaysLeft != 0) || (crossing.Pending.Length != 0 && crossing.DaysLeft < 1)) return "The save has inconsistent crossing construction data.";
            if (crossing.Pending.Length != 0 && (crossing.Mode == "bridge" || crossing.Pending == crossing.Mode ||
                (crossing.Pending == "ferry" && (crossing.Mode != "none" || crossing.DaysLeft > 2)))) return "The save has an impossible crossing upgrade.";
            if (data.Relations == null || data.Relations.Count != 2 || data.Scenario == null) return "The save has no relations or scenario state.";
            var relationTowns = new HashSet<int>(); int fulfilled = 0; int missed = 0;
            foreach (var relation in data.Relations)
            {
                if (relation == null || !InRange(relation.Town, 1, 2) || !relationTowns.Add(relation.Town) || !Bounded(relation.Trust, 0, 100)
                    || !InRange(relation.Fulfilled, 0, 10000000) || !InRange(relation.Missed, 0, 10000000)) return "The save has invalid town relations.";
                fulfilled += relation.Fulfilled; missed += relation.Missed;
            }
            var scenario = data.Scenario;
            if (!OneOf(scenario.Status, "active", "won", "recovered")) return "The save has an invalid scenario outcome.";
            if (!InRange(scenario.Delivered, 0, 10000000) || !InRange(scenario.Missed, 0, 10000000) || scenario.Delivered != fulfilled || scenario.Missed != missed)
                return "The save's relation and scenario counts disagree.";
            if (scenario.Winter != (data.Day >= WinterDay)) return "The save has inconsistent season data.";
            if (scenario.Status == "recovered" && data.Day < WinterDay) return "The save has a recovery before winter.";
            if (!ValidStoryList(data.StoryFlags) || !ValidStoryList(data.PendingStory) || data.PendingStory.Any(id => !data.StoryFlags.Contains(id)))
                return "The save has invalid story queues or an unearned story beat.";
            if (data.Contracts == null || data.Contracts.Count > 128) return "The save has an invalid contract list.";
            var contracts = new Dictionary<int, ContractState>(); int largest = 0;
            foreach (var contract in data.Contracts)
            {
                if (contract == null || !InRange(contract.Id, 1, 10000000) || !InRange(contract.Town, 1, 2) || !InRange(contract.Deadline, 1, 10000030))
                    return "The save has an invalid contract identifier, town, or deadline.";
                if (!KnownGood(contract.Good) || !OneOf(contract.Status, "offered", "active", "in_transit", "fulfilled", "failed")) return "The save has an invalid contract good or status.";
                if (!Bounded(contract.Quantity, 0.01, 12) || !Bounded(contract.UnitPrice, 0.01, 1e6) || !Bounded(contract.Reward, 0, 1e6) || !Bounded(contract.Escrow, 0, 1e12))
                    return "The save has invalid contract quantities or finances.";
                if (contracts.ContainsKey(contract.Id)) return "The save contains duplicate contract identifiers.";
                contracts.Add(contract.Id, contract); largest = Math.Max(largest, contract.Id);
                double expected = contract.Quantity * contract.UnitPrice + contract.Reward;
                if ((contract.Status == "offered" || contract.Status == "fulfilled") && contract.Escrow != 0) return "An unaccepted or settled contract cannot hold escrow.";
                if ((contract.Status == "active" || contract.Status == "in_transit") && Math.Abs(contract.Escrow - expected) > 0.001) return "The contract's escrow does not match its terms.";
                if (contract.Status == "failed" && contract.Escrow > 0 && Math.Abs(contract.Escrow - (expected - contract.Reward)) > 0.001) return "The failed contract has invalid remaining payment.";
            }
            if (!InRange(data.NextContractId, largest + 1, 10000001)) return "The save has an invalid next contract identifier.";
            var linked = new HashSet<int>();
            foreach (var cargo in data.Caravans)
            {
                if (cargo.ContractId == -1) continue;
                ContractState contract;
                if (!contracts.TryGetValue(cargo.ContractId, out contract) || !linked.Add(cargo.ContractId)) return "The save has an unknown or duplicate contract shipment.";
                if (!OneOf(contract.Status, "in_transit", "failed") || cargo.From != 0 || cargo.To != contract.Town || cargo.Good != contract.Good
                    || Math.Abs(cargo.Quantity - contract.Quantity) > Epsilon || !cargo.PlayerOrder) return "The contract shipment does not match its promise.";
            }
            foreach (var contract in data.Contracts)
                if ((contract.Status == "in_transit" || (contract.Status == "failed" && contract.Escrow > 0)) && !linked.Contains(contract.Id))
                    return "The save has escrowed cargo without its shipment.";
            return "";
        }

        private static bool ValidResources(List<ResourceAmount> resources, double minimum, double maximum)
        {
            if (resources == null || resources.Count != Goods.Length) return false;
            var seen = new HashSet<string>();
            foreach (var resource in resources)
                if (resource == null || !KnownGood(resource.Good) || !seen.Add(resource.Good) || !Bounded(resource.Amount, minimum, maximum)) return false;
            return true;
        }
        private static bool ValidStoryList(List<string> ids)
        { return ids != null && ids.Count <= StoryIds.Length && ids.All(id => id != null && Array.IndexOf(StoryIds, id) >= 0) && ids.Distinct().Count() == ids.Count; }
        private static bool Outstanding(string status) { return status == "offered" || status == "active" || status == "in_transit"; }
        private static bool KnownGood(string good) { return good != null && Array.IndexOf(Goods, good) >= 0; }
        private static bool OneOf(string value, params string[] choices) { return value != null && Array.IndexOf(choices, value) >= 0; }
        private static bool Finite(double number) { return !double.IsNaN(number) && !double.IsInfinity(number); }
        private static bool Bounded(double value, double minimum, double maximum) { return Finite(value) && value >= minimum && value <= maximum; }
        private static bool InRange(int value, int minimum, int maximum) { return value >= minimum && value <= maximum; }
        private static double Clamp(double value, double minimum, double maximum) { return Math.Min(maximum, Math.Max(minimum, value)); }
        // Match Godot's snappedf positive-value tie behavior rather than banker's rounding.
        private static double Snap(double value) { return Math.Floor(value / 0.01 + 0.5) * 0.01; }
        private static string F(string format, params object[] values) { return string.Format(CultureInfo.InvariantCulture, format, values); }
    }
}
