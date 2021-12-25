﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AvaliMod
{
    public class ToddStoryTeller : StorytellerComp
    {
        protected ToddStoryTellerProps Props => (ToddStoryTellerProps)props;

        private bool HasPawnsNotAvali
        {
            get
            {
                return RimValiCore.RimValiUtility.AllPawnsOfFactionSpawned(Faction.OfPlayer)
                    .Any(pawn => pawn.RaceProps.Humanlike && pawn.def != AvaliDefs.RimVali);
            }
        }

        private float RatioAvaliToNonAvali => CountPawnsAvali / (float)CountPawnsNotAvali;
        private float RatioNonAvaliToAvali => CountPawnsNotAvali / (float)CountPawnsAvali;

        private static int CountPawnsAvali
        {
            get
            {
                return RimValiCore.RimValiUtility
                    .AllPawnsOfFactionSpawned(Faction.OfPlayer).Count(pawn => pawn.def == AvaliDefs.RimVali);
            }
        }

        private static int CountPawnsNotAvali
        {
            get
            {
                return RimValiCore.RimValiUtility
                    .AllPawnsOfFactionSpawned(Faction.OfPlayer).Count(pawn => pawn.def != AvaliDefs.RimVali);
            }
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            var num = 1f;
            if (Props.acceptFractionByDaysPassedCurve != null)
            {
                num *= Props.acceptFractionByDaysPassedCurve.Evaluate(GenDate.DaysPassedFloat);
            }

            if (Props.acceptPercentFactorPerThreatPointsCurve != null)
            {
                num *= Props.acceptPercentFactorPerThreatPointsCurve.Evaluate(
                    StorytellerUtility.DefaultThreatPointsNow(target));
            }

            if (Props.acceptPercentFactorPerProgressScoreCurve != null)
            {
                num *= Props.acceptPercentFactorPerProgressScoreCurve.Evaluate(
                    StorytellerUtility.GetProgressScore(target));
            }

            int incCount = IncidentCycleUtility.IncidentCountThisInterval(target,
                Find.Storyteller.storytellerComps.IndexOf(this), Props.minDaysPassed, Props.onDays, Props.offDays,
                Props.minSpacingDays, Props.minIncidents, Props.maxIncidents, num);
            for (var i = 0; i < incCount; i++)
            {
                FiringIncident inc = null;
                switch (StorytellerData.state)
                {
                    case StorytellerState.Aggressive:
                        inc = GenAgressiveIncident(target);
                        break;
                    case StorytellerState.Hunting:
                        inc = GenHunting(target);
                        break;
                    case StorytellerState.Neutral:
                        inc = GenNeutral(target);
                        break;
                    case StorytellerState.Friendly:
                        inc = GenFriendly(target);
                        break;
                    case StorytellerState.Calm:
                        break;
                }

                if (inc == null)
                {
                    continue;
                }

                if (new Random(Find.World.ConstantRandSeed).Next(1, 5) == 3)
                {
                    UpdateState();
                }

                yield return inc;
            }

            //return base.MakeIntervalIncidents(target);
        }

        private void UpdateState()
        {
            //Make sure we're not on the hunting->aggressive route, and we've given the current state time to do it's job.
            if (StorytellerData.state != StorytellerState.Hunting &&
                GenDate.DaysPassed > StorytellerData.dayLastUpdated + 8)
            {
                if (HasPawnsNotAvali && new Random(Find.World.ConstantRandSeed).Next(0, 3) == 1 &&
                    StorytellerData.state <= 0)
                {
                    StorytellerData.daysPassedSinceLastHunt = GenDate.DaysPassed;
                    StorytellerData.state = StorytellerState.Hunting;
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (GenDate.DaysPassed > StorytellerData.daysPassedSinceLastHunt +
                    new Random(Find.World.ConstantRandSeed).Next(2, 4) && StorytellerData.state <= 0 &&
                    new Random(Find.World.ConstantRandSeed).Next(1, 10) == 2)
                {
                    StorytellerData.daysPassedSinceLastHunt = GenDate.DaysPassed;
                    StorytellerData.state = StorytellerState.Hunting;
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (GenDate.DaysPassed > StorytellerData.daysSpentNice +
                    new Random(Find.World.ConstantRandSeed).Next(0, 4) && StorytellerData.state > 0)
                {
                    if (new Random().Next(1, 10) == 5)
                    {
                        StorytellerData.state = StorytellerState.Aggressive;
                        StorytellerData.daysSpentNice = GenDate.DaysPassed;
                    }
                    else
                    {
                        StorytellerData.state -= 1;
                        if (StorytellerData.state == 0)
                        {
                            StorytellerData.daysSpentNice = GenDate.DaysPassed;
                        }
                    }

                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (StorytellerData.state == 0 && new Random(Find.World.ConstantRandSeed).Next(1, 6) == 2)
                {
                    if (new Random().Next(1, 30) == 10)
                    {
                        StorytellerData.state =
                            (StorytellerState)new Random(Find.World.ConstantRandSeed).Next(
                                (int)StorytellerState.Aggressive,
                                (int)StorytellerState.Neutral);
                    }
                    else
                    {
                        StorytellerData.state += new Random(Find.World.ConstantRandSeed).Next(-1, 1);
                    }

                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (StorytellerData.state == (StorytellerState.Aggressive | StorytellerState.HyperAggressive) &&
                    new Random(Find.World.ConstantRandSeed).Next(1, 10) == 2)
                {
                    StorytellerData.state =
                        (StorytellerState)new Random(Find.World.ConstantRandSeed).Next((int)StorytellerState.Aggressive,
                            (int)StorytellerState.Neutral);
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                }
            }
        }


        #region Incident gen

        public FiringIncident GenAgressiveIncident(IIncidentTarget targ)
        {
            var defs = new List<IncidentDef>
            {
                IncidentDefOf.ManhunterPack, IncidentDefOf.MechCluster, IncidentDefOf.RaidEnemy,
                IncidentDefOf.Infestation,
            };
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x => x.category == IncidentCategoryDefOf.ThreatBig));
            IncidentDef def = new List<IncidentDef>
            {
                IncidentDefOf.ManhunterPack, IncidentDefOf.MechCluster, IncidentDefOf.RaidEnemy,
                IncidentDefOf.Infestation,
            }.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (!def.Worker.CanFireNow(parms2))
            {
                return null;
            }

            parms.faction = HasPawnsNotAvali
                ? Find.FactionManager.FirstFactionOfDef(AvaliDefs.NesiSpecOps)
                : parms.faction;
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x =>
                x.category == IncidentCategoryDefOf.ThreatBig || x.category == IncidentCategoryDefOf.ThreatSmall));

            parms.points *=
                new Random(Find.World.ConstantRandSeed).Next(1,
                    (int)StorytellerUtilityPopulation.AdjustedPopulation *
                    new Random(Find.World.ConstantRandSeed).Next(1, 3)) *
                (HasPawnsNotAvali ? RatioNonAvaliToAvali * 2 : 1);

            if (HasPawnsNotAvali && RatioNonAvaliToAvali > 5)
            {
                int multiplier = def.category == IncidentCategoryDefOf.ThreatSmall ? 10 : 5;
                parms.points *= RatioAvaliToNonAvali * multiplier;
            }

            return new FiringIncident(def, this, parms) { parms = parms };
        }

        public FiringIncident GenHunting(IIncidentTarget targ)
        {
            var defs = new List<IncidentDef>
            {
                IncidentDefOf.RaidEnemy, IncidentDefOf.RaidEnemy, IncidentDefOf.SolarFlare, IncidentDefOf.MechCluster,
                IncidentDefOf.ToxicFallout, IncidentDefOf.RaidEnemy, AvaliDefs.VolcanicWinter,
                IncidentDefOf.Infestation, IncidentDefOf.Infestation, IncidentDefOf.ManhunterPack,
                AvaliDefs.PsychicDrone, AvaliDefs.ShortCircuit,
            };
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x => x.category == IncidentCategoryDefOf.ThreatSmall));
            IncidentDef def = defs.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);

            parms.points *= new Random(Find.World.ConstantRandSeed).Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                new Random(Find.World.ConstantRandSeed).Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (!def.Worker.CanFireNow(parms2))
            {
                return null;
            }

            if (new Random(Find.World.ConstantRandSeed).Next(1, 2) == 2)
            {
                StorytellerData.state = StorytellerState.Aggressive;
            }

            return new FiringIncident(def, this, parms) { parms = parms };
        }

        public FiringIncident GenNeutral(IIncidentTarget targ)
        {
            var defs = new List<IncidentDef>
            {
                AvaliDefs.HerdMigration, AvaliDefs.MeteoriteImpact, AvaliDefs.RansomDemand, AvaliDefs.RefugeePodCrash,
                AvaliDefs.ResourcePodCrash, AvaliDefs.SelfTame, AvaliDefs.ThrumboPasses, AvaliDefs.WildManWandersIn,
                AvaliDefs.AmbrosiaSprout,
            };
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x =>
                x.category == IncidentCategoryDefOf.Misc || x.category == IncidentCategoryDefOf.ThreatSmall));
            IncidentDef def = defs.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);
            parms.points *= new Random(Find.World.ConstantRandSeed).Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                new Random(Find.World.ConstantRandSeed).Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (def == AvaliDefs.RefugeePodCrash || def == AvaliDefs.WildManWandersIn ||
                new Random(Find.World.ConstantRandSeed).Next(1, 5) == 2)
            {
                parms.faction = Find.FactionManager.FirstFactionOfDef(AvaliDefs.AvaliFaction);
                parms.pawnKind = AvaliDefs.RimValiColonist;
            }

            if (!def.Worker.CanFireNow(parms2))
            {
                return null;
            }

            return new FiringIncident(def, this, parms) { parms = parms };
        }

        public FiringIncident GenFriendly(IIncidentTarget targ)
        {
            var defs = new List<IncidentDef>
            {
                IncidentDefOf.TraderCaravanArrival, IncidentDefOf.TraderCaravanArrival, IncidentDefOf.TravelerGroup,
                IncidentDefOf.VisitorGroup, IncidentDefOf.WandererJoin,
            };
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x =>
                x.category == IncidentCategoryDefOf.GiveQuest || x.category == IncidentCategoryDefOf.OrbitalVisitor ||
                x.category == IncidentCategoryDefOf.AllyAssistance));
            IncidentDef def = defs.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);
            parms.points *= new Random(Find.World.ConstantRandSeed).Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                new Random(Find.World.ConstantRandSeed).Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (def.category == IncidentCategoryDefOf.OrbitalVisitor ||
                new Random(Find.World.ConstantRandSeed).Next(1, 5) == 2)
            {
                parms.faction = Find.FactionManager.FirstFactionOfDef(AvaliDefs.AvaliFaction);
                parms.pawnKind = AvaliDefs.RimValiColonist;
            }

            parms.points = RatioAvaliToNonAvali > 2
                ? parms.points * (RatioAvaliToNonAvali * StorytellerUtilityPopulation.AdjustedPopulation)
                : parms.points;
            if (!def.Worker.CanFireNow(parms2))
            {
                return null;
            }

            return new FiringIncident(def, this, parms) { parms = parms };
        }

        #endregion
    }
}
