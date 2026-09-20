using System;
using System.Collections.Generic;

namespace LivingEmpires
{
    // Persistent objects deliberately use public fields and List<T>, supported by
    // Unity JsonUtility. The simulation itself has no dependency on UnityEngine.
    [Serializable]
    public sealed class ResourceAmount
    {
        public string Good;
        public double Amount;
        public ResourceAmount() { }
        public ResourceAmount(string good, double amount) { Good = good; Amount = amount; }
        public ResourceAmount Copy() { return new ResourceAmount(Good, Amount); }
    }

    [Serializable]
    public sealed class TownState
    {
        public string Name;
        public double Gold;
        public int Population;
        public double FoodRatio;
        public List<ResourceAmount> Stocks;
        public List<ResourceAmount> Prices;

        public double Stock(string good) { return Read(Stocks, good); }
        public double Price(string good) { return Read(Prices, good); }
        public void SetStock(string good, double amount) { Write(Stocks, good, amount); }
        public void SetPrice(string good, double amount) { Write(Prices, good, amount); }
        internal static double Read(List<ResourceAmount> values, string good)
        {
            if (values != null) foreach (var value in values)
                if (value != null && value.Good == good) return value.Amount;
            return 0.0;
        }
        private static void Write(List<ResourceAmount> values, string good, double amount)
        {
            if (values != null) foreach (var value in values)
                if (value != null && value.Good == good) { value.Amount = amount; return; }
            throw new ArgumentException("Unknown resource: " + good, "good");
        }
        public TownState Copy()
        {
            return new TownState { Name = Name, Gold = Gold, Population = Population,
                FoodRatio = FoodRatio, Stocks = CopyResources(Stocks), Prices = CopyResources(Prices) };
        }
        internal static List<ResourceAmount> CopyResources(List<ResourceAmount> values)
        {
            if (values == null) return null;
            var result = new List<ResourceAmount>(values.Count);
            foreach (var value in values) result.Add(value == null ? null : value.Copy());
            return result;
        }
    }

    [Serializable]
    public sealed class BuildingState
    {
        public int Id;
        public string Type;
        public int Town;
        public int X;
        public int Z;
        public int Workers;
        public BuildingState Copy() { return (BuildingState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class CaravanState
    {
        public int Id;
        public int From;
        public int To;
        public string Good;
        public double Quantity;
        public int DaysLeft;
        public int TotalDays;
        public string Route;
        public bool PlayerOrder;
        public int ContractId;
        public double Freight;
        public CaravanState Copy() { return (CaravanState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class CrossingState
    {
        public string Mode;
        public string Pending;
        public int DaysLeft;
        public CrossingState Copy() { return (CrossingState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelationState
    {
        public int Town;
        public double Trust;
        public int Fulfilled;
        public int Missed;
        public RelationState Copy() { return (RelationState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class ContractState
    {
        public int Id;
        public int Town;
        public string Good;
        public double Quantity;
        public double UnitPrice;
        public int Deadline;
        public string Status;
        public double Reward;
        public double Escrow;
        public ContractState Copy() { return (ContractState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class ScenarioState
    {
        public string Status;
        public int Delivered;
        public int Missed;
        public bool Winter;
        public bool RecoveryUsed;
        public ScenarioState Copy() { return (ScenarioState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class SimulationState
    {
        public int Version;
        public string Format;
        public int Day;
        public List<TownState> Towns;
        public List<BuildingState> Buildings;
        public List<CaravanState> Caravans;
        public List<string> Events;
        public int CompletedTrades;
        public int NextBuildingId;
        public int NextContractId;
        public int NextCaravanId;
        public int ReserveDays;
        public double FreightPaid;
        public CrossingState Crossing;
        public List<RelationState> Relations;
        public List<ContractState> Contracts;
        public ScenarioState Scenario;
        public List<string> StoryFlags;
        public List<string> PendingStory;
        public MilitaryState Military;

        public SimulationState Copy()
        {
            var copy = (SimulationState)MemberwiseClone();
            copy.Towns = CopyList(Towns, x => x.Copy());
            copy.Buildings = CopyList(Buildings, x => x.Copy());
            copy.Caravans = CopyList(Caravans, x => x.Copy());
            copy.Events = Events == null ? null : new List<string>(Events);
            copy.Crossing = Crossing == null ? null : Crossing.Copy();
            copy.Relations = CopyList(Relations, x => x.Copy());
            copy.Contracts = CopyList(Contracts, x => x.Copy());
            copy.Scenario = Scenario == null ? null : Scenario.Copy();
            copy.StoryFlags = StoryFlags == null ? null : new List<string>(StoryFlags);
            copy.PendingStory = PendingStory == null ? null : new List<string>(PendingStory);
            copy.Military = Military == null ? null : Military.Copy();
            return copy;
        }
        private static List<T> CopyList<T>(List<T> values, Func<T, T> copy) where T : class
        {
            if (values == null) return null;
            var result = new List<T>(values.Count);
            foreach (var value in values) result.Add(value == null ? null : copy(value));
            return result;
        }
    }

    public sealed class BuildingDefinition
    {
        public readonly string Kind;
        public readonly string Name;
        public readonly int Workers;
        public readonly double GoldCost;
        public readonly IReadOnlyList<ResourceAmount> Cost;
        public readonly IReadOnlyList<ResourceAmount> Inputs;
        public readonly IReadOnlyList<ResourceAmount> Outputs;
        public readonly string Description;
        internal BuildingDefinition(string kind, string name, int workers, double goldCost,
            ResourceAmount[] cost, ResourceAmount[] inputs, ResourceAmount[] outputs, string description)
        {
            Kind = kind; Name = name; Workers = workers; GoldCost = goldCost;
            Cost = Array.AsReadOnly(cost); Inputs = Array.AsReadOnly(inputs);
            Outputs = Array.AsReadOnly(outputs); Description = description;
        }
    }

    public sealed class RouteInfoData
    {
        public string Name;
        public double Capacity;
        public double Fee;
        public int Days;
        public bool Ready;
        public string Pending;
        public int DaysLeft;
        public bool HighPassOpen;
        public bool LowerRidgeOpen;
    }

    public sealed class ObjectiveRow
    {
        public string Text;
        public bool Done;
        public ObjectiveRow(string text, bool done) { Text = text; Done = done; }
    }
}
