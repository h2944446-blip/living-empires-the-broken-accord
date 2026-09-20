using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingEmpires
{
    /// <summary>Deterministic military accounting, pathfinding, combat and persistence regressions.
    /// Fixture time advances are explicit and never access the player's save or scene.</summary>
    public static class MilitaryVerification
    {
        public static VerificationReport RunAll()
        {
#if UNITY_5_3_OR_NEWER
            return RunAll(s => UnityEngine.JsonUtility.ToJson(s), s => UnityEngine.JsonUtility.FromJson<SimulationState>(s));
#else
            return RunAll(null, null);
#endif
        }
        public static VerificationReport RunAll(Func<SimulationState,string> serialize, Func<string,SimulationState> deserialize)
        { return new Runner(serialize,deserialize).Run(); }
        private sealed class Runner
        {
            readonly VerificationReport report = new VerificationReport();
            readonly Func<SimulationState,string> serialize;
            readonly Func<string,SimulationState> deserialize;
            public Runner(Func<SimulationState,string> toJson, Func<string,SimulationState> fromJson)
            {serialize=toJson;deserialize=fromJson;report.SerializationTested=toJson!=null&&fromJson!=null;report.SerializationBackend=report.SerializationTested?"Provided JSON serializer":"Snapshot/Restore";}
            void Check(bool value,string message){++report.Checks;if(!value)throw new InvalidOperationException(message);}
            void Near(double a,double b,string message){Check(Math.Abs(a-b)<.00001,message+" ("+a+" / "+b+")");}
            void Okay(string result){Check(result=="","Unexpected rejection: "+result);}
            void Group(string name,Action test){++report.Groups;try{test();report.Scenarios.Add(name+": passed.");}catch(Exception e){report.Failures.Add(name+": "+e.Message);}}
            Simulation War(){var sim=new Simulation();Okay(sim.StartMilitaryChapter());Okay(Simulation.Validate(sim.Snapshot()));return sim;}
            SquadState Train(Simulation sim,string kind){Okay(sim.RecruitSquad(kind));int id=sim.Military.Queue.Last().Id;sim.AdvanceMilitary(25);var s=sim.Military.Squads.Find(x=>x.Id==id);Check(s!=null,"Recruit visibly leaves training");return s;}
            public VerificationReport Run()
            {
                Group("Peaceful chapter remains peaceful",Peaceful);
                Group("Established-town scenario and finite guard",Scenario);
                Group("Recruitment accounting, queue and cancellation",Recruitment);
                Group("Upgrade costs and refit restrictions",Upgrades);
                Group("River and building path obstruction",Routes);
                Group("Demobilization reserves workers until return",Demobilize);
                Group("Daily upkeep and shortage morale",Upkeep);
                Group("Guard combat, wounds and recovery",Recovery);
                Group("Three warned finite raids and victory",Defend);
                Group("Undefended losses have a recovery victory",MissedDefense);
                Group("Stable caravan escort identities",Escort);
                Group("Snapshot and JSON continuation",Save);
                Group("Legacy save migration and invalid military rejection",Invalid);
                Group("Fixed-step frame partition invariance",Determinism);
                Group("Live economy during the military campaign",EconomyCampaign);
                report.Notes.Add("Military squads represent four people; wounds keep all four workers mobilized until recovery or demobilization. These tests verify the model, not rendered animation or mouse input.");
                return report;
            }
            void Peaceful()
            {
                var sim=new Simulation();double gold=sim.State.Towns[0].Gold;int workers=sim.WorkforceTotal();
                sim.AdvanceMilitary(1000);Check(!sim.Military.Enabled,"First Winter starts without military");
                Near(sim.Military.Clock,0,"Peaceful military clock stays still");Near(sim.State.Towns[0].Gold,gold,"No military spending in peaceful chapter");
                Check(sim.WorkforceTotal()==workers,"No hidden workforce reservation");Check(sim.RecruitSquad("spearmen")!="","Recruitment requires military chapter");
            }
            void Scenario()
            {
                var sim=War();Check(sim.State.Day==30&&sim.State.Towns[0].Population==72,"Disclosed day and population");
                Near(sim.State.Towns[0].Gold,560,"Disclosed finite treasury");Check(sim.State.Crossing.Mode=="bridge","Established bridge");
                Check(sim.Military.Squads.Count(s=>!s.Hostile)==0,"No instant player army");
                Check(sim.Military.Squads.Count(s=>s.IsGuard)==1,"One finite camp guard");Check(sim.MilitaryReservedWorkers()==0,"Guard does not consume player workforce");
                Check(sim.StartMilitaryChapter()!="","Cannot duplicate starting resources");
            }
            void Recruitment()
            {
                var sim=War();var town=sim.State.Towns[0];double gold=town.Gold,wood=town.Stock("wood"),iron=town.Stock("iron");int workers=sim.WorkforceTotal();
                Okay(sim.RecruitSquad("spearmen"));Okay(sim.RecruitSquad("archers"));
                Near(town.Gold,gold-95,"Costs charged once");Near(town.Stock("wood"),wood-20,"Wood charged");Near(town.Stock("iron"),iron-6,"Iron charged");
                Check(sim.MilitaryReservedWorkers()==8&&sim.WorkforceTotal()==workers-8,"Queued workers reserved exactly once");
                sim.AdvanceMilitary(5);Near(sim.Military.Queue[0].SecondsLeft,15,"Active queue progresses");Near(sim.Military.Queue[1].SecondsLeft,24,"Waiting queue does not train concurrently");
                Okay(sim.CancelRecruitment(sim.Military.Queue[1].Id));Near(town.Gold,gold-45,"Cancellation refunds only that order");
                Check(sim.MilitaryReservedWorkers()==4,"Cancellation releases its workers");sim.AdvanceMilitary(16);
                Check(sim.Military.Queue.Count==0&&sim.MilitaryReservedWorkers()==4,"Training-to-squad does not double-reserve workers");
                Check(sim.RecruitSquad("invalid")!="","Invalid unit rejected");double before=town.Gold;town.SetStock("iron",0);
                Check(sim.RecruitSquad("spearmen")!="","Insufficient equipment rejected");Near(town.Gold,before,"Rejected order does not charge gold");
                Check(sim.MilitaryReservedWorkers()==4,"Rejected order does not reserve workers");
                var second=War();Train(second,"spearmen");Train(second,"spearmen");
                var positions=second.Military.Squads.Where(s=>!s.Hostile).ToArray();Check(Math.Abs(positions[0].X-positions[1].X)+Math.Abs(positions[0].Z-positions[1].Z)>1,"Recruits use distinct rally slots");
            }
            void Upgrades()
            {
                var sim=War();Okay(sim.UpgradeMilitary("barracks"));Near(sim.State.Towns[0].Gold,460,"Barracks upgrade treasury cost");
                Okay(sim.RecruitSquad("archers"));Near(sim.Military.Queue[0].TotalSeconds,18,"Upgraded training time");sim.AdvanceMilitary(19);
                Okay(sim.UpgradeMilitary("equipment"));var squad=sim.Military.Squads.Find(s=>!s.Hostile);Near(squad.MaxHP,180,"Equipment health applied once");
                double gold=sim.State.Towns[0].Gold;Check(sim.UpgradeMilitary("equipment")!="","Duplicate upgrade rejected");Near(sim.State.Towns[0].Gold,gold,"Duplicate upgrade atomic");
                var other=War();var field=Train(other,"spearmen");Okay(other.OrderSquad(field.Id,"move",14.3,7.7));other.AdvanceMilitary(35);
                double original=other.State.Towns[0].Gold;Check(other.UpgradeMilitary("equipment")!="","Field squad cannot magically refit");Near(other.State.Towns[0].Gold,original,"Rejected refit does not spend");
            }
            void Routes()
            {
                var sim=War();var squad=Train(sim,"spearmen");sim.Military.NextRaidAt=99999;
                sim.State.Crossing.Mode="none";Check(!sim.MilitaryRouteAvailable(squad.X,squad.Z,20.9,7.7),"No crossing means no river route");
                Check(sim.OrderSquad(squad.Id,"move",20.9,7.7)!="","Blocked order rejected");
                sim.State.Crossing.Mode="ferry";Check(sim.MilitaryRouteAvailable(squad.X,squad.Z,20.9,7.7),"Completed ferry permits route");
                Okay(sim.OrderSquad(squad.Id,"move",20.9,7.7));foreach(var p in squad.Path.Where(p=>Math.Abs(p.X)<4))Near(p.Z,14.3,"Ferry route follows its actual landing");
                sim.State.Crossing.Mode="bridge";Okay(sim.OrderSquad(squad.Id,"move",20.9,7.7));
                foreach(var p in squad.Path.Where(p=>Math.Abs(p.X)<4))Near(p.Z,7.7,"Bridge route follows actual deck");
                Check(sim.OrderSquad(squad.Id,"move",-9.9,5.5)!="","Barracks interior rejects movement");
                Check(sim.OrderSquad(squad.Id,"move",double.NaN,0)!="","NaN order rejected");
                sim.AdvanceMilitary(40);Near(squad.X,20.9,"Squad reaches route destination");Near(squad.Z,7.7,"Squad reaches correct bank");
                var blocked=War();blocked.State.Crossing.Mode="none";double gold=blocked.State.Towns[0].Gold;blocked.AdvanceMilitary(300);
                Near(blocked.State.Towns[0].Gold,gold,"Raiders cannot steal remotely across a blocked river");
            }
            void Demobilize()
            {
                var sim=War();var squad=Train(sim,"spearmen");Okay(sim.OrderSquad(squad.Id,"move",14.3,7.7));sim.AdvanceMilitary(30);
                int workers=sim.WorkforceTotal();Okay(sim.DemobilizeSquad(squad.Id));Check(sim.WorkforceTotal()==workers,"Order alone does not free workers");
                sim.AdvanceMilitary(40);Check(!sim.Military.Squads.Contains(squad),"Squad removed at yard");Check(sim.WorkforceTotal()==workers+4,"Workers return after travel");
            }
            void Upkeep()
            {
                var sim=War();var squad=Train(sim,"spearmen");double gold=sim.State.Towns[0].Gold;
                Near(sim.DailyMilitaryGold(),.6,"Four-person daily wage");Near(sim.DailyMilitaryFood(),.32,"Four-person additional rations");
                sim.AdvanceDay();Near(sim.State.Towns[0].Gold,gold-.6,"Daily wage actually deducted");Near(sim.Military.UpkeepFoodSpent,.32,"Daily extra food consumed once");
                sim.State.Towns[0].Gold=0;double morale=squad.Morale;sim.AdvanceDay();Check(squad.Morale<morale,"Unpaid wages reduce morale");
                Check(sim.State.Towns[0].Gold>=0,"Treasury never goes negative");Okay(Simulation.Validate(sim.Snapshot()));
            }
            void Recovery()
            {
                var sim=War();var spear=Train(sim,"spearmen");var guard=sim.Military.Squads.Find(s=>s.IsGuard);
                spear.X=guard.X-2.2;spear.Z=guard.Z;spear.HP=1;spear.Order="hold";spear.Path.Clear();
                sim.AdvanceMilitary(2);Check(spear.Recovering,"Defeated militia become wounded");Check(sim.MilitaryReservedWorkers()==4,"Wounded still reserve workers");
                Check(sim.OrderSquad(spear.Id,"attack",guard.X,guard.Z,guard.Id)!="","Wounded cannot fight immediately");
                sim.AdvanceMilitary(100);Check(!spear.Recovering&&spear.HP>0,"Wounded return and recover");Okay(Simulation.Validate(sim.Snapshot()));
            }
            void Defend()
            {
                var sim=War();var a=Train(sim,"spearmen");var b=Train(sim,"spearmen");var c=Train(sim,"archers");
                Okay(sim.OrderSquad(a.Id,"move",-5.5,7.7));Okay(sim.OrderSquad(b.Id,"move",-5.5,5.5));Okay(sim.OrderSquad(c.Id,"move",-7.7,7.7));sim.AdvanceMilitary(25);
                sim.AdvanceMilitary(60);Check(sim.Military.RaidWarning,"Scouting warning is visible before raid");Check(sim.Military.RaidsSpawned==0,"No party appears during advance warning");
                for(int i=0;i<600&&sim.Military.Status=="active";i++)sim.AdvanceMilitary(1);
                Check(sim.Military.RaidsSpawned==3&&sim.Military.RaidsDefeated==3,"All three finite parties defeated");
                Check(sim.Military.Status=="won","Defense wins chapter");sim.AdvanceMilitary(500);Check(sim.Military.RaidsSpawned==3,"No infinite wave grind after victory");Okay(Simulation.Validate(sim.Snapshot()));
            }
            void MissedDefense()
            {
                var sim=War();double gold=sim.State.Towns[0].Gold;sim.AdvanceMilitary(650);
                Check(sim.Military.Status=="recovery","Three missed parties lead to recovery, not game over");
                Near(sim.State.Towns[0].Gold,gold-105,"Three bounded raid losses");Check(sim.State.Towns[0].Population==72,"Raids do not erase the population");
                var a=Train(sim,"spearmen");var b=Train(sim,"archers");
                Okay(sim.OrderSquad(a.Id,"attack",sim.Military.CampX,sim.Military.CampZ,Simulation.CampTargetId));
                Okay(sim.OrderSquad(b.Id,"attack",sim.Military.CampX,sim.Military.CampZ,Simulation.CampTargetId));
                sim.AdvanceMilitary(180);Check(sim.Military.Status=="won"&&sim.Military.CampHP==0,"Finite remaining supplies support a camp-clearing recovery victory");
                Check(sim.Military.RaidsDefeated==0,"Camp guard is not counted as a raiding party");Okay(Simulation.Validate(sim.Snapshot()));
            }
            void Escort()
            {
                var sim=War();var squad=Train(sim,"spearmen");Okay(sim.DispatchTrade(1,"iron",2,true));Okay(sim.DispatchTrade(2,"wood",2,true));
                int first=sim.State.Caravans[0].Id,second=sim.State.Caravans[1].Id;Check(first>0&&second>first,"Shipments have distinct stable identifiers");
                Okay(sim.OrderSquad(squad.Id,"escort",0,0,second));sim.State.Caravans.RemoveAt(0);sim.AdvanceMilitary(2);
                Check(squad.Order=="escort"&&squad.TargetId==second,"Removing another caravan does not retarget escort");
                sim.State.Caravans.Clear();sim.AdvanceMilitary(2);Check(squad.Order=="hold","Arrived caravan releases escort to hold");
            }
            void Save()
            {
                var sim=War();var squad=Train(sim,"spearmen");Okay(sim.RecruitSquad("archers"));Okay(sim.OrderSquad(squad.Id,"move",20.9,7.7));sim.AdvanceMilitary(2.37);
                var state=sim.Snapshot();if(report.SerializationTested)state=deserialize(serialize(state));
                var restored=new Simulation();Okay(restored.Restore(state));Check(restored.Military!=sim.Military,"Military state deep copied");
                Check(restored.Military.Squads[0]!=sim.Military.Squads[0],"Squad deep copied");
                Check(restored.Military.Queue[0]!=sim.Military.Queue[0],"Training queue deep copied");
                sim.AdvanceMilitary(10.23);restored.AdvanceMilitary(10.23);
                var left=sim.Military.Squads.Find(s=>s.Id==squad.Id);var right=restored.Military.Squads.Find(s=>s.Id==squad.Id);
                Near(left.X,right.X,"Restored movement continues exactly");Near(left.Z,right.Z,"Restored route continues exactly");
                Near(sim.Military.Queue[0].SecondsLeft,restored.Military.Queue[0].SecondsLeft,"Restored training continues exactly");
                Near(sim.Military.Clock,restored.Military.Clock,"Substep accumulator preserved");Okay(Simulation.Validate(restored.Snapshot()));
            }
            void Invalid()
            {
                var sim=new Simulation();var legacy=sim.Snapshot();legacy.Military=null;legacy.NextCaravanId=0;
                Okay(sim.Restore(legacy));Check(sim.Military!=null&&!sim.Military.Enabled,"Legacy version3 migrates to peaceful military defaults");
                var war=War();var bad=war.Snapshot();bad.Military.CampHP=double.NaN;Check(war.Restore(bad)!="","NaN military data rejected");
                bad=war.Snapshot();bad.Military.NextId=1;Check(war.Restore(bad)!="","Reused unit IDs rejected");
                bad=war.Snapshot();bad.Military.Squads[0].Path=null;Check(war.Restore(bad)!="","Null routes rejected");
                bad=war.Snapshot();bad.Military.Queue.Add(new RecruitmentState{Id=50,Kind="spearmen",SecondsLeft=20,TotalSeconds=20});bad.Military.NextId=51;
                bad.Towns[0].Population=45;Check(war.Restore(bad)!="","Double-assigned military/civilian workforce rejected");
                Okay(Simulation.Validate(war.Snapshot()));
            }
            void Determinism()
            {
                var a=War();Okay(a.RecruitSquad("spearmen"));var b=new Simulation();Okay(b.Restore(a.Snapshot()));
                a.AdvanceMilitary(80);for(int i=0;i<800;i++)b.AdvanceMilitary(.1);
                Near(a.Military.Clock,b.Military.Clock,"Large and small frames have same clock");
                var left=a.Military.Squads.Find(s=>!s.Hostile);var right=b.Military.Squads.Find(s=>!s.Hostile);
                Near(left.X,right.X,"Frame partition preserves X");Near(left.Z,right.Z,"Frame partition preserves Z");
                Check(a.Military.Queue.Count==b.Military.Queue.Count,"Frame partition preserves training");
            }
            void EconomyCampaign()
            {
                var sim=War();Okay(sim.RecruitSquad("spearmen"));Okay(sim.RecruitSquad("spearmen"));Okay(sim.RecruitSquad("archers"));
                var commanded=new HashSet<int>();int slot=0;
                for(int second=1;second<=650&&sim.Military.Status=="active";second++)
                {
                    sim.SetMilitaryDayFraction((second%10)/10.0);sim.AdvanceMilitary(1);
                    foreach(var squad in sim.Military.Squads.Where(s=>!s.Hostile))if(commanded.Add(squad.Id))
                    { Okay(sim.OrderSquad(squad.Id,"move",slot==2?-7.7:-5.5,slot==1?5.5:7.7));++slot; }
                    if(second%10==0){sim.AdvanceDay();Okay(Simulation.Validate(sim.Snapshot()));}
                }
                Check(sim.Military.Status=="won","Production, rations, wages and three raids coexist in a winnable campaign");
                Check(sim.State.Towns[0].Gold>0&&sim.State.Towns[0].FoodRatio>.99,"Finite starting economy remains solvent and fed");
                Check(sim.Military.UpkeepGoldSpent>0&&sim.Military.UpkeepFoodSpent>0,"Campaign actually consumes military upkeep");
                Check(sim.State.Towns[0].Population==72,"Supported civilian population remains stable");
            }
        }
    }
}
