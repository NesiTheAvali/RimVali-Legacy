﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AvaliMod
{
    public class NesiStoryTeller : StorytellerComp
    {

        public override void Initialize()
        {
            base.Initialize();
        }

        private Random random;


        protected NesiStorytellerProps Props => (NesiStorytellerProps)props;

        private bool hasPawnsNotAvali;

        private float ratioAvaliToNonAvali => countAvali / countNotAvali;
        private float ratioNonAvaliToAvali => countNotAvali / countAvali;


        private int countNotAvali;
        private int countAvali;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if(random == null)
                random = new Random(Find.World.ConstantRandSeed);


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
                    case StorytellerState.HyperAggressive:
                    default:
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
        bool firstUpdate = false;
        private int daysSinceLastPawnUpdate = 0;
        private void UpdateState()
        {
            if ((GenDate.DaysPassed % 2 == 0 && GenDate.DaysPassed - daysSinceLastPawnUpdate != 0) || !firstUpdate)
            {
                firstUpdate = true;
                daysSinceLastPawnUpdate = GenDate.DaysPassed;
                countAvali = RimValiCore.RimValiUtility.AllPawnsOfFactionSpawned(Faction.OfPlayer)
                    .Where(pawn => pawn.def == AvaliDefs.RimVali).Count();
                countNotAvali = RimValiCore.RimValiUtility.AllPawnsOfFactionSpawned(Faction.OfPlayer)
                    .Where(pawn => pawn.def != AvaliDefs.RimVali).Count();
                hasPawnsNotAvali= RimValiCore.RimValiUtility.AllPawnsOfFactionSpawned(Faction.OfPlayer)
                    .Any(pawn => pawn.RaceProps.Humanlike && pawn.def != AvaliDefs.RimVali);


            }
            //Make sure we're not on the hunting->aggressive route, and we've given the current state time to do it's job.
            if (StorytellerData.state != StorytellerState.Hunting &&
                GenDate.DaysPassed > StorytellerData.dayLastUpdated + 5)
            {
                if (hasPawnsNotAvali && random.Next(0, 2) == 1 &&
                    StorytellerData.state <= 0)
                {
                    StorytellerData.daysPassedSinceLastHunt = GenDate.DaysPassed;
                    StorytellerData.state = StorytellerState.Hunting;
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (GenDate.DaysPassed > StorytellerData.daysPassedSinceLastHunt +
                    random.Next(2, 4) && StorytellerData.state <= 0 &&
                    random.Next(1, 10) == 2)
                {
                    StorytellerData.daysPassedSinceLastHunt = GenDate.DaysPassed;
                    StorytellerData.state = StorytellerState.Hunting;
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (GenDate.DaysPassed > StorytellerData.daysSpentNice +
                    random.Next(0, 4) && StorytellerData.state > 0)
                {
                    StorytellerData.state -= 1;
                    if (StorytellerData.state == 0)
                    {
                        StorytellerData.daysSpentNice = GenDate.DaysPassed;
                    }

                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                if (StorytellerData.state == 0 && random.Next(1, 10) == 2)
                {
                    StorytellerData.state += random.Next(-1, 2);
                    StorytellerData.dayLastUpdated = GenDate.DaysPassed;
                    return;
                }

                // FIXME: Weird use of | instead of ||?
                // Currently checks if state is -1/Hunting
                if (StorytellerData.state == (StorytellerState.Aggressive | StorytellerState.HyperAggressive) &&
                    random.Next(1, 10) == 2)
                {
                    StorytellerData.state += random.Next(0, 1);
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

            parms.faction = hasPawnsNotAvali
                ? Find.FactionManager.FirstFactionOfDef(AvaliDefs.NesiSpecOps)
                : parms.faction;


            parms.points *=
                random.Next(1,
                    (int)StorytellerUtilityPopulation.AdjustedPopulation *
                    random.Next(1, 3)) *
                (hasPawnsNotAvali ? ratioNonAvaliToAvali * 2 : 1);

            if (hasPawnsNotAvali && ratioNonAvaliToAvali > 6)
            {
                var multiplier = 25;

                parms.points *= ratioAvaliToNonAvali * multiplier;
            }
            else if (hasPawnsNotAvali && ratioNonAvaliToAvali > 5)
            {
                var multiplier = 15;
                parms.points *= ratioAvaliToNonAvali * multiplier;
            }

            return new FiringIncident(def, this, parms) { parms = parms };
        }

        public FiringIncident GenHunting(IIncidentTarget targ)
        {
            var defs = new List<IncidentDef>
            {
                IncidentDefOf.Eclipse, IncidentDefOf.SolarFlare, IncidentDefOf.ToxicFallout, IncidentDefOf.ToxicFallout,
                AvaliDefs.VolcanicWinter, AvaliDefs.Alphabeavers, AvaliDefs.HeatWave, AvaliDefs.Flashstorm,
                AvaliDefs.PsychicDrone, AvaliDefs.ShortCircuit,
            };
            defs.AddRange(
                DefDatabase<IncidentDef>.AllDefs.Where(x => x.category == IncidentCategoryDefOf.DiseaseHuman));
            IncidentDef def = defs.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);

            parms.points *= random.Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                random.Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (!def.Worker.CanFireNow(parms2))
            {
                return null;
            }

            if (random.Next(1, 2) == 2)
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
            defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(x => x.category == IncidentCategoryDefOf.Misc));
            IncidentDef def = defs.RandomElement();
            IncidentParms parms = GenerateParms(def.category, targ);
            parms.points *= random.Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                random.Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (def == AvaliDefs.RefugeePodCrash || def == AvaliDefs.WildManWandersIn ||
                random.Next(1, 5) == 2)
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
            parms.points *= random.Next(1,
                (int)StorytellerUtilityPopulation.AdjustedPopulation *
                random.Next(1, 3));
            IncidentParms parms2 = GenerateParms(def.category, targ);
            if (def.category == IncidentCategoryDefOf.OrbitalVisitor ||
                random.Next(1, 5) == 2)
            {
                parms.faction = Find.FactionManager.FirstFactionOfDef(AvaliDefs.AvaliFaction);
                parms.pawnKind = AvaliDefs.RimValiColonist;
            }

            parms.points = ratioAvaliToNonAvali > 2
                ? parms.points * (ratioAvaliToNonAvali * StorytellerUtilityPopulation.AdjustedPopulation)
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
